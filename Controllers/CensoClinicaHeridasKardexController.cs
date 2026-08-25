using System.Security.Claims;
using System.Text.Json;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Requisiciones de insumos de clínica de heridas. Cada tipo de atención marcada con "Sí" en la
/// sección 3 (manejo de la herida, VAC, NPT, PICC) abre su propio kardex, editable hasta que
/// farmacia le da el OK.
/// </summary>
public partial class CensoController
{
    private const long MaxAdjuntoKardexBytes = 10L * 1024 * 1024;

    private static readonly JsonSerializerOptions ClinicaHeridasKardexJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly string[] AdjuntoKardexExtensionesPermitidas =
        [".pdf", ".xls", ".xlsx", ".xlsm", ".csv", ".jpg", ".jpeg", ".png"];

    /// <summary>Tipos de kardex habilitados según los "Sí" guardados en la sección 3.</summary>
    public static IReadOnlyList<string> TiposKardexHabilitados(CensoClinicaHeridasRecord record)
    {
        var habilitados = new List<string>();
        if (EsSi(record.ManejoHerida)) habilitados.Add(ClinicaHeridasKardexTipos.ManejoHerida);
        if (EsSi(record.Vac)) habilitados.Add(ClinicaHeridasKardexTipos.Vac);
        if (EsSi(record.Npt)) habilitados.Add(ClinicaHeridasKardexTipos.Npt);
        if (EsSi(record.Picc)) habilitados.Add(ClinicaHeridasKardexTipos.Picc);
        return habilitados;
    }

    private static bool EsSi(string? valor) =>
        string.Equals(valor?.Trim(), "Si", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ApositosDe(params string?[] valores) =>
    [
        .. valores.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())
    ];

    private string PerfilQueAbreKardex() =>
        User.FindFirstValue("full_name")
            ?? User.Identity?.Name
            ?? string.Empty;

    /// <summary>
    /// Devuelve el plan vigente del paciente, creando el primero si todavia no existe. Solo puede
    /// haber uno abierto: el resto quedan cerrados y de consulta.
    /// </summary>
    private async Task<CensoClinicaHeridasPlan> ObtenerOCrearPlanVigenteAsync(
        CensoClinicaHeridasRecord record,
        CancellationToken cancellationToken)
    {
        var vigente = await _context.CensoClinicaHeridasPlanes
            .Where(x => x.CensoClinicaHeridasRecordId == record.Id && x.CerradoAtUtc == null)
            .OrderByDescending(x => x.Numero)
            .FirstOrDefaultAsync(cancellationToken);

        if (vigente is not null)
        {
            return vigente;
        }

        var ultimoNumero = await _context.CensoClinicaHeridasPlanes
            .Where(x => x.CensoClinicaHeridasRecordId == record.Id)
            .MaxAsync(x => (int?)x.Numero, cancellationToken) ?? 0;

        var plan = new CensoClinicaHeridasPlan
        {
            CensoClinicaHeridasRecordId = record.Id,
            Numero = ultimoNumero + 1,
            CreadoPor = PerfilQueAbreKardex(),
            CreadoAtUtc = DateTime.UtcNow
        };

        SincronizarPlanConCenso(plan, record);
        await _context.CensoClinicaHeridasPlanes.AddAsync(plan, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return plan;
    }

    /// <summary>
    /// Copia al plan los apositos y el tratamiento vigentes del censo. Se llama mientras el plan
    /// esta abierto; al cerrarse, esa copia queda congelada.
    /// </summary>
    private static void SincronizarPlanConCenso(
        CensoClinicaHeridasPlan plan,
        CensoClinicaHeridasRecord record)
    {
        plan.ApositoMedicamento1 = record.ApositoMedicamento1;
        plan.ApositoMedicamento2 = record.ApositoMedicamento2;
        plan.ApositoMedicamento3 = record.ApositoMedicamento3;
        plan.ApositoMedicamento4 = record.ApositoMedicamento4;
        plan.DuracionTratamientoDias = record.DuracionTratamientoDias;
        plan.FrecuenciaVisita = record.FrecuenciaVisita;
    }

    /// <summary>
    /// Mantiene el plan vigente al dia con lo que se acaba de guardar en la seccion 3. Lo llama el
    /// guardado de la seccion para que el plan abierto y el censo no se desincronicen.
    /// </summary>
    private async Task SincronizarPlanVigenteAsync(long recordId, CancellationToken cancellationToken)
    {
        var record = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return;
        }

        var vigente = await _context.CensoClinicaHeridasPlanes
            .Where(x => x.CensoClinicaHeridasRecordId == recordId && x.CerradoAtUtc == null)
            .OrderByDescending(x => x.Numero)
            .FirstOrDefaultAsync(cancellationToken);

        if (vigente is null)
        {
            return;
        }

        SincronizarPlanConCenso(vigente, record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Abre un plan nuevo: cierra el vigente con sus datos congelados, crea el siguiente y limpia
    /// los apositos del censo para que se capturen los del plan nuevo.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AbrirPlanClinicaHeridas(long recordId, CancellationToken cancellationToken)
    {
        var record = await _context.CensoClinicaHeridas
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "No se encontró el registro del paciente." });
        }

        if (TiposKardexHabilitados(record).Count == 0)
        {
            return BadRequest(new
            {
                message = "Marca al menos una atención en Sí antes de abrir un plan de requisiciones."
            });
        }

        var perfil = PerfilQueAbreKardex();
        var nowUtc = DateTime.UtcNow;

        var vigente = await _context.CensoClinicaHeridasPlanes
            .Where(x => x.CensoClinicaHeridasRecordId == recordId && x.CerradoAtUtc == null)
            .OrderByDescending(x => x.Numero)
            .FirstOrDefaultAsync(cancellationToken);

        if (vigente is not null)
        {
            // El plan que se cierra conserva los apósitos con los que realmente se trabajó.
            SincronizarPlanConCenso(vigente, record);
            vigente.CerradoAtUtc = nowUtc;
            vigente.CerradoPor = perfil;
        }

        var ultimoNumero = await _context.CensoClinicaHeridasPlanes
            .Where(x => x.CensoClinicaHeridasRecordId == recordId)
            .MaxAsync(x => (int?)x.Numero, cancellationToken) ?? 0;

        // El plan nuevo arranca sin apósitos: se capturan otra vez en la sección 3.
        record.ApositoMedicamento1 = null;
        record.ApositoMedicamento2 = null;
        record.ApositoMedicamento3 = null;
        record.ApositoMedicamento4 = null;
        record.UpdatedAtUtc = nowUtc;

        var plan = new CensoClinicaHeridasPlan
        {
            CensoClinicaHeridasRecordId = recordId,
            Numero = ultimoNumero + 1,
            CreadoPor = perfil,
            CreadoAtUtc = nowUtc,
            DuracionTratamientoDias = record.DuracionTratamientoDias,
            FrecuenciaVisita = record.FrecuenciaVisita
        };

        await _context.CensoClinicaHeridasPlanes.AddAsync(plan, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid)
            ? (Guid?)parsedUid
            : null;

        await _auditService.LogAsync(
            "CENSO_CLINICA_HERIDAS_PLAN_ABIERTO",
            "CensoClinicaHeridasPlan",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}, Plan: {plan.Numero}",
            auditUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return Json(new
        {
            success = true,
            message = $"Plan {plan.Numero} abierto. Los apósitos quedaron en blanco para capturarlos de nuevo.",
            planId = plan.Id,
            numero = plan.Numero
        });
    }

    /// <summary>
    /// Tarjetas de kardex que se pintan al pie de la seccion 3: una por cada atencion en "Si", con
    /// su estado frente a farmacia y el tamano de la requisicion que se generaria.
    /// </summary>
    private async Task PopulateClinicaHeridasKardexAsync(
        CensoClinicaHeridasViewModel model,
        CancellationToken cancellationToken)
    {
        model.KardexDisponibles = [];

        if (!model.EditingRecordId.HasValue)
        {
            return;
        }

        var record = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);

        if (record is null)
        {
            return;
        }

        // La barra de planes se pinta aunque todavia no haya ninguna atencion en Si: asi el usuario
        // ve el historial de lo que ya se envio a farmacia.
        var planes = await _context.CensoClinicaHeridasPlanes
            .AsNoTracking()
            .Where(x => x.CensoClinicaHeridasRecordId == record.Id)
            .OrderByDescending(x => x.Numero)
            .Select(x => new
            {
                x.Id,
                x.Numero,
                x.CerradoAtUtc,
                x.CerradoPor,
                x.CreadoPor,
                x.CreadoAtUtc,
                x.ApositoMedicamento1,
                x.ApositoMedicamento2,
                x.ApositoMedicamento3,
                x.ApositoMedicamento4,
                Requisiciones = x.Kardex.Count
            })
            .ToListAsync(cancellationToken);

        model.Planes = planes
            .Select(x => new CensoClinicaHeridasPlanResumenViewModel
            {
                Id = x.Id,
                Numero = x.Numero,
                Vigente = x.CerradoAtUtc is null,
                CreadoPor = x.CreadoPor,
                CreadoEn = ToColombiaTime(x.CreadoAtUtc),
                CerradoEn = x.CerradoAtUtc.HasValue ? ToColombiaTime(x.CerradoAtUtc.Value) : null,
                CerradoPor = x.CerradoPor,
                Requisiciones = x.Requisiciones,
                // El plan vigente muestra los apósitos que hoy tiene el censo; los cerrados, su copia.
                Apositos = x.CerradoAtUtc is null
                    ? ApositosDe(record.ApositoMedicamento1, record.ApositoMedicamento2, record.ApositoMedicamento3, record.ApositoMedicamento4)
                    : ApositosDe(x.ApositoMedicamento1, x.ApositoMedicamento2, x.ApositoMedicamento3, x.ApositoMedicamento4)
            })
            .ToList();

        var habilitados = TiposKardexHabilitados(record);
        if (habilitados.Count == 0)
        {
            return;
        }

        var planVigenteId = planes.FirstOrDefault(x => x.CerradoAtUtc is null)?.Id;

        var existentes = planVigenteId is null
            ? []
            : await _context.CensoClinicaHeridasKardex
                .AsNoTracking()
                .Where(x => x.CensoClinicaHeridasPlanId == planVigenteId.Value)
                .Select(x => new
                {
                    x.Tipo,
                    x.FarmaciaEnviadoAtUtc,
                    x.KardexCerradoAtUtc,
                    Adjuntos = x.Adjuntos.Count
                })
                .ToListAsync(cancellationToken);

        var apositos = new[]
            {
                record.ApositoMedicamento1,
                record.ApositoMedicamento2,
                record.ApositoMedicamento3,
                record.ApositoMedicamento4
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        var aplicaciones = ClinicaHeridasKardexBuilder.CalcularAplicaciones(
            record.DuracionTratamientoDias,
            record.FrecuenciaVisita);

        model.KardexDisponibles = habilitados
            .Select(tipo =>
            {
                var guardado = existentes.FirstOrDefault(x => x.Tipo == tipo);
                var insumos = ClinicaHeridasKardexBuilder.Insumos(
                    tipo,
                    ClinicaHeridasKardexBuilder.UsaApositosSeleccionados(tipo) ? apositos : []);

                return new CensoClinicaHeridasKardexResumenViewModel
                {
                    Tipo = tipo,
                    Nombre = ClinicaHeridasKardexTipos.Nombre(tipo),
                    Enviado = guardado?.FarmaciaEnviadoAtUtc is not null,
                    Cerrado = guardado?.KardexCerradoAtUtc is not null,
                    EnviadoAtUtc = guardado?.FarmaciaEnviadoAtUtc,
                    Adjuntos = guardado?.Adjuntos ?? 0,
                    Aplicaciones = aplicaciones,
                    Insumos = insumos.Count
                };
            })
            .ToList();
    }

    [HttpGet]
    public async Task<IActionResult> KardexClinicaHeridas(
        long recordId,
        string tipo,
        long? planId,
        CancellationToken cancellationToken)
    {
        if (!ClinicaHeridasKardexTipos.EsValido(tipo))
        {
            return BadRequest(new { message = "Tipo de kardex no válido." });
        }

        var record = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "No se encontró el registro del paciente." });
        }

        var plan = await ResolverPlanAsync(recordId, planId, cancellationToken);
        if (plan is null)
        {
            return NotFound(new { message = "No se encontró el plan de requisiciones." });
        }

        // En el plan vigente manda lo que hoy está marcado en Sí. En un plan cerrado se muestra lo
        // que tuvo ese plan, aunque después se hayan cambiado las atenciones del paciente.
        if (plan.EstaVigente && !TiposKardexHabilitados(record).Contains(tipo))
        {
            return BadRequest(new
            {
                message = $"El paciente no tiene {ClinicaHeridasKardexTipos.Nombre(tipo)} en Sí."
            });
        }

        var kardex = await _context.CensoClinicaHeridasKardex
            .Include(x => x.Adjuntos)
            .FirstOrDefaultAsync(x => x.CensoClinicaHeridasPlanId == plan.Id && x.Tipo == tipo, cancellationToken);

        var perfil = PerfilQueAbreKardex();
        var documento = ResolverDocumentoKardex(record, plan, tipo, kardex, perfil);
        var planCerrado = !plan.EstaVigente;

        return Json(new
        {
            documento,
            estado = new
            {
                existe = kardex is not null,
                // Un kardex deja de editarse por dos vías: el OK de farmacia o el cierre de su plan.
                cerrado = kardex?.KardexCerradoAtUtc is not null || planCerrado,
                cerradoPorFarmacia = kardex?.KardexCerradoAtUtc is not null,
                planCerrado,
                cerradoAtUtc = kardex?.KardexCerradoAtUtc,
                enviadoAtUtc = kardex?.FarmaciaEnviadoAtUtc,
                farmaciaEstado = kardex?.FarmaciaEstado,
                okFarmacia = kardex?.FarmaciaOkKardex ?? false
            },
            plan = new
            {
                id = plan.Id,
                numero = plan.Numero,
                vigente = plan.EstaVigente,
                creadoPor = plan.CreadoPor,
                creadoAtUtc = plan.CreadoAtUtc
            },
            adjuntos = (kardex?.Adjuntos ?? [])
                .OrderByDescending(x => x.UploadedAtUtc)
                .Select(x => new { id = x.Id, nombre = x.FileName, subidoAtUtc = x.UploadedAtUtc })
        });
    }

    /// <summary>Plan pedido, o el vigente cuando no se especifica ninguno.</summary>
    private async Task<CensoClinicaHeridasPlan?> ResolverPlanAsync(
        long recordId,
        long? planId,
        CancellationToken cancellationToken)
    {
        if (planId.HasValue)
        {
            return await _context.CensoClinicaHeridasPlanes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == planId.Value && x.CensoClinicaHeridasRecordId == recordId,
                    cancellationToken);
        }

        return await _context.CensoClinicaHeridasPlanes
            .AsNoTracking()
            .Where(x => x.CensoClinicaHeridasRecordId == recordId && x.CerradoAtUtc == null)
            .OrderByDescending(x => x.Numero)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Estado completo de un plan para pintar la navegación: sus requisiciones y los apósitos con
    /// los que se armó.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> PlanClinicaHeridas(long recordId, long planId, CancellationToken cancellationToken)
    {
        var record = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "No se encontró el registro del paciente." });
        }

        var plan = await _context.CensoClinicaHeridasPlanes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == planId && x.CensoClinicaHeridasRecordId == recordId, cancellationToken);

        if (plan is null)
        {
            return NotFound(new { message = "No se encontró el plan de requisiciones." });
        }

        var kardexDelPlan = await _context.CensoClinicaHeridasKardex
            .AsNoTracking()
            .Where(x => x.CensoClinicaHeridasPlanId == plan.Id)
            .Select(x => new
            {
                x.Tipo,
                x.FarmaciaEnviadoAtUtc,
                x.KardexCerradoAtUtc,
                Adjuntos = x.Adjuntos.Count
            })
            .ToListAsync(cancellationToken);

        // En el plan vigente se ofrecen las atenciones marcadas hoy; en uno cerrado, las que
        // realmente se generaron entonces.
        var tipos = plan.EstaVigente
            ? TiposKardexHabilitados(record)
            : [.. ClinicaHeridasKardexTipos.Todos.Where(t => kardexDelPlan.Any(k => k.Tipo == t))];

        var apositosPlan = plan.Apositos;
        var aplicaciones = ClinicaHeridasKardexBuilder.CalcularAplicaciones(
            plan.EstaVigente ? record.DuracionTratamientoDias : plan.DuracionTratamientoDias,
            plan.EstaVigente ? record.FrecuenciaVisita : plan.FrecuenciaVisita);

        return Json(new
        {
            plan = new
            {
                id = plan.Id,
                numero = plan.Numero,
                vigente = plan.EstaVigente,
                creadoPor = plan.CreadoPor,
                creadoAtUtc = plan.CreadoAtUtc,
                cerradoAtUtc = plan.CerradoAtUtc,
                cerradoPor = plan.CerradoPor,
                apositos = apositosPlan,
                duracionDias = plan.EstaVigente ? record.DuracionTratamientoDias : plan.DuracionTratamientoDias,
                frecuencia = plan.EstaVigente ? record.FrecuenciaVisita : plan.FrecuenciaVisita
            },
            kardex = tipos.Select(tipo =>
            {
                var guardado = kardexDelPlan.FirstOrDefault(x => x.Tipo == tipo);
                var insumos = ClinicaHeridasKardexBuilder.Insumos(
                    tipo,
                    ClinicaHeridasKardexBuilder.UsaApositosSeleccionados(tipo) ? apositosPlan : []);

                return new
                {
                    tipo,
                    nombre = ClinicaHeridasKardexTipos.Nombre(tipo),
                    enviado = guardado?.FarmaciaEnviadoAtUtc is not null,
                    cerrado = guardado?.KardexCerradoAtUtc is not null,
                    adjuntos = guardado?.Adjuntos ?? 0,
                    aplicaciones,
                    insumos = insumos.Count
                };
            })
        });
    }

    /// <summary>
    /// Devuelve la versión editada si existe; si no, genera el documento desde el censo. Aunque haya
    /// versión guardada se refresca "elaborado por" con el perfil que abre, salvo que ya esté cerrado.
    /// </summary>
    private static ClinicaHeridasKardexDocumento ResolverDocumentoKardex(
        CensoClinicaHeridasRecord record,
        CensoClinicaHeridasPlan plan,
        string tipo,
        CensoClinicaHeridasKardex? kardex,
        string perfil)
    {
        // Un plan cerrado se genera con su propia foto de apósitos y tratamiento, no con lo que el
        // censo tenga hoy.
        var origen = plan.EstaVigente ? record : ClonarRecordConDatosDelPlan(record, plan);
        var generado = ClinicaHeridasKardexBuilder.Generar(origen, tipo, perfil, GetColombiaNow());

        if (kardex is null || string.IsNullOrWhiteSpace(kardex.KardexJson))
        {
            return generado;
        }

        try
        {
            var guardado = JsonSerializer.Deserialize<ClinicaHeridasKardexDocumento>(
                kardex.KardexJson,
                ClinicaHeridasKardexJsonOptions);

            if (guardado is null)
            {
                return generado;
            }

            guardado.Tipo = tipo;
            guardado.TipoNombre = ClinicaHeridasKardexTipos.Nombre(tipo);
            guardado.Titulo = ClinicaHeridasKardexBuilder.Titulo;
            guardado.Encabezados = ClinicaHeridasKardexBuilder.NormalizarEncabezados(
                guardado.Encabezados,
                guardado.Aplicaciones);

            if (kardex.KardexCerradoAtUtc is null && string.IsNullOrWhiteSpace(guardado.ElaboradoPor))
            {
                guardado.ElaboradoPor = perfil;
            }

            return guardado;
        }
        catch (JsonException)
        {
            // Un JSON corrupto no debe dejar sin kardex al paciente: se vuelve al generado.
            return generado;
        }
    }

    /// <summary>
    /// Copia del registro con los apósitos y el tratamiento congelados del plan. Solo se usa para
    /// generar el documento de un plan cerrado; no se guarda nunca.
    /// </summary>
    private static CensoClinicaHeridasRecord ClonarRecordConDatosDelPlan(
        CensoClinicaHeridasRecord record,
        CensoClinicaHeridasPlan plan)
    {
        return new CensoClinicaHeridasRecord
        {
            Id = record.Id,
            NombrePaciente = record.NombrePaciente,
            TipoIdentificacion = record.TipoIdentificacion,
            NumeroIdentificacion = record.NumeroIdentificacion,
            Asegurador = record.Asegurador,
            Edad = record.Edad,
            Direccion = record.Direccion,
            DetalleDireccion = record.DetalleDireccion,
            TelefonoPrincipal = record.TelefonoPrincipal,
            TelefonoAdicional1 = record.TelefonoAdicional1,
            TelefonoAdicional2 = record.TelefonoAdicional2,
            CodigoCie10 = record.CodigoCie10,
            DiagnosticoDescriptivo = record.DiagnosticoDescriptivo,
            AuxiliarEnfermeriaAsignado = record.AuxiliarEnfermeriaAsignado,
            ApositoMedicamento1 = plan.ApositoMedicamento1,
            ApositoMedicamento2 = plan.ApositoMedicamento2,
            ApositoMedicamento3 = plan.ApositoMedicamento3,
            ApositoMedicamento4 = plan.ApositoMedicamento4,
            DuracionTratamientoDias = plan.DuracionTratamientoDias,
            FrecuenciaVisita = plan.FrecuenciaVisita
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarKardexClinicaHeridas(
        long recordId,
        string tipo,
        string? kardexJson,
        CancellationToken cancellationToken)
    {
        var resultado = await ResolverKardexEditableAsync(recordId, tipo, cancellationToken);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        var kardex = resultado.Kardex!;
        kardex.KardexJson = string.IsNullOrWhiteSpace(kardexJson) ? null : kardexJson.Trim();
        kardex.ElaboradoPor = PerfilQueAbreKardex();
        kardex.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaKardexAsync(
            "CENSO_CLINICA_HERIDAS_KARDEX_GUARDADO",
            resultado.Record!,
            tipo,
            cancellationToken);

        return Json(new { success = true, message = $"Kardex de {ClinicaHeridasKardexTipos.Nombre(tipo)} guardado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarKardexClinicaHeridasAFarmacia(
        long recordId,
        string tipo,
        string? kardexJson,
        CancellationToken cancellationToken)
    {
        var resultado = await ResolverKardexEditableAsync(recordId, tipo, cancellationToken);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        var kardex = resultado.Kardex!;
        var nowUtc = DateTime.UtcNow;

        kardex.KardexJson = string.IsNullOrWhiteSpace(kardexJson) ? kardex.KardexJson : kardexJson.Trim();
        kardex.ElaboradoPor = PerfilQueAbreKardex();
        kardex.FarmaciaEnviadoAtUtc = nowUtc;
        kardex.FarmaciaEstado = FarmaciaEstados.Nuevo;
        kardex.FarmaciaKardexVistoAtUtc = null;
        kardex.UpdatedAtUtc = nowUtc;

        await _context.SaveChangesAsync(cancellationToken);
        await RegistrarAuditoriaKardexAsync(
            "CENSO_CLINICA_HERIDAS_KARDEX_ENVIADO_FARMACIA",
            resultado.Record!,
            tipo,
            cancellationToken);

        // El correo al auxiliar no debe bloquear el envío: si falla, queda en el log y el kardex ya
        // está en farmacia.
        var avisos = await _farmaciaDispatchNotificationService
            .NotifyClinicaHeridasRequisicionEnviadaAsync(kardex, cancellationToken);

        foreach (var aviso in avisos)
        {
            _logger.LogWarning("Notificación de requisición {KardexId}: {Aviso}", kardex.Id, aviso);
        }

        return Json(new
        {
            success = true,
            avisos,
            message = $"Kardex de {ClinicaHeridasKardexTipos.Nombre(tipo)} enviado a farmacia."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxAdjuntoKardexBytes + 1024 * 1024)]
    public async Task<IActionResult> SubirAdjuntoKardexClinicaHeridas(
        long recordId,
        string tipo,
        IFormFile? archivo,
        CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new { message = "Selecciona un archivo." });
        }

        if (archivo.Length > MaxAdjuntoKardexBytes)
        {
            return BadRequest(new { message = "El archivo debe pesar máximo 10 MB." });
        }

        var extension = Path.GetExtension(archivo.FileName);
        if (!AdjuntoKardexExtensionesPermitidas.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Formato no permitido. Adjunta PDF, Excel, CSV o imagen."
            });
        }

        var resultado = await ResolverKardexEditableAsync(recordId, tipo, cancellationToken);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        using var memoria = new MemoryStream();
        await archivo.CopyToAsync(memoria, cancellationToken);

        var adjunto = new CensoClinicaHeridasKardexAdjunto
        {
            CensoClinicaHeridasKardexId = resultado.Kardex!.Id,
            FileName = Path.GetFileName(archivo.FileName),
            FileData = memoria.ToArray(),
            UploadedAtUtc = DateTime.UtcNow
        };

        await _context.CensoClinicaHeridasKardexAdjuntos.AddAsync(adjunto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            success = true,
            adjunto = new { id = adjunto.Id, nombre = adjunto.FileName, subidoAtUtc = adjunto.UploadedAtUtc }
        });
    }

    [HttpGet]
    public async Task<IActionResult> DescargarAdjuntoKardexClinicaHeridas(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoClinicaHeridasKardexAdjuntos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound();
        }

        return File(adjunto.FileData, "application/octet-stream", adjunto.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarAdjuntoKardexClinicaHeridas(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoClinicaHeridasKardexAdjuntos
            .Include(x => x.Kardex)
            .FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound(new { message = "El adjunto no existe." });
        }

        if (adjunto.Kardex.KardexCerradoAtUtc is not null)
        {
            return BadRequest(new { message = "Kardex cerrado por farmacia: ya no admite cambios." });
        }

        _context.CensoClinicaHeridasKardexAdjuntos.Remove(adjunto);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { success = true });
    }

    private sealed class KardexEditableResultado
    {
        public CensoClinicaHeridasRecord? Record { get; init; }
        public CensoClinicaHeridasKardex? Kardex { get; init; }
        public IActionResult? Error { get; init; }
    }

    /// <summary>
    /// Localiza (o crea) el kardex del tipo pedido y comprueba que se pueda editar: el tipo tiene que
    /// estar en Sí en la sección 3 y farmacia no puede haberlo cerrado todavía.
    /// </summary>
    private async Task<KardexEditableResultado> ResolverKardexEditableAsync(
        long recordId,
        string tipo,
        CancellationToken cancellationToken)
    {
        if (!ClinicaHeridasKardexTipos.EsValido(tipo))
        {
            return new KardexEditableResultado { Error = BadRequest(new { message = "Tipo de kardex no válido." }) };
        }

        var record = await _context.CensoClinicaHeridas
            .FirstOrDefaultAsync(x => x.Id == recordId, cancellationToken);

        if (record is null)
        {
            return new KardexEditableResultado
            {
                Error = NotFound(new { message = "No se encontró el registro del paciente." })
            };
        }

        if (!TiposKardexHabilitados(record).Contains(tipo))
        {
            return new KardexEditableResultado
            {
                Error = BadRequest(new
                {
                    message = $"El paciente no tiene {ClinicaHeridasKardexTipos.Nombre(tipo)} en Sí."
                })
            };
        }

        // Solo se edita dentro del plan vigente: los planes anteriores quedan de consulta.
        var plan = await ObtenerOCrearPlanVigenteAsync(record, cancellationToken);

        var kardex = await _context.CensoClinicaHeridasKardex
            .FirstOrDefaultAsync(x => x.CensoClinicaHeridasPlanId == plan.Id && x.Tipo == tipo, cancellationToken);

        if (kardex is null)
        {
            kardex = new CensoClinicaHeridasKardex
            {
                CensoClinicaHeridasRecordId = recordId,
                CensoClinicaHeridasPlanId = plan.Id,
                Tipo = tipo,
                ElaboradoPor = PerfilQueAbreKardex(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.CensoClinicaHeridasKardex.AddAsync(kardex, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (kardex.KardexCerradoAtUtc is not null)
        {
            return new KardexEditableResultado
            {
                Error = BadRequest(new
                {
                    message = "Kardex cerrado. Farmacia ya lo aprobó y queda solo para consulta."
                })
            };
        }

        return new KardexEditableResultado { Record = record, Kardex = kardex };
    }

    private Task RegistrarAuditoriaKardexAsync(
        string accion,
        CensoClinicaHeridasRecord record,
        string tipo,
        CancellationToken cancellationToken)
    {
        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid)
            ? (Guid?)parsedUid
            : null;

        return _auditService.LogAsync(
            accion,
            "CensoClinicaHeridasKardex",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}, Kardex: {ClinicaHeridasKardexTipos.Nombre(tipo)}",
            auditUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
    }
}
