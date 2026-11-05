using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Nexa.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Programa Cuidadores: un programa adicional que se abre solo o junto a crónicos.
///
/// Cada sección tiene su propio formulario y su propia acción, que solo escribe sus campos. Los datos
/// básicos no pasan por aquí: son los del maestro. La atención nace al guardar la recepción, que es
/// distinta a la de los demás programas (no viene de un correo sino del reporte de la EPS) y por eso
/// vive en la fila del programa y no en el episodio.
/// </summary>
public partial class CensoController
{
    private const string SeccionCuidadoresRecepcion = "tab-cuidadores-recepcion";
    private const string SeccionCuidadoresResponsable = "tab-cuidadores-responsable";
    private const string SeccionCuidadoresPlan = "tab-cuidadores-plan";
    private const string SeccionCuidadoresEstado = "tab-cuidadores-estado";

    private static readonly CultureInfo CuidadorTextCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] CuidadorHorarioValues = ["Diurno", "Nocturno"];
    private static readonly string[] CuidadorDiasValues =
        ["Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo"];
    private static readonly Regex CuidadorNombrePattern = new(@"^[\p{L}\s]+$", RegexOptions.Compiled);
    private static readonly Regex CuidadorTelefonoPattern = new(@"^\d{1,10}$", RegexOptions.Compiled);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCuidadoresRecepcion(
        CensoCuidadorViewModel model,
        CancellationToken cancellationToken)
    {
        var documento = NormalizeCedulaFilter(model.CedulaFiltro);
        var paciente = await _context.CensoPacientes
            .FirstOrDefaultAsync(x => x.NumeroIdentificacion.ToUpper() == documento, cancellationToken);

        var record = model.EditingRecordId.HasValue
            ? await _context.CensoCuidadores.FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
            : null;

        if (paciente is null)
        {
            ModelState.AddModelError(string.Empty, "Guarda primero los datos básicos del paciente.");
        }
        else if (record is null)
        {
            // Un segundo envío del mismo formulario no debe abrir otra atención: la conciliación
            // enlaza el registro con el episodio al volver a pintar la ficha.
            record = await _context.CensoCuidadores
                .Where(x => x.CensoPacienteId == paciente.Id
                    && x.FechaEgreso == null
                    && x.Estado != CuidadorEstados.Cancelado)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var tieneElPrograma = await _context.CensoPacienteProgramas.AnyAsync(
                x => x.CensoPacienteId == paciente.Id
                    && x.Programa == CensoProgramas.Cuidadores
                    && x.CerradoAtUtc == null,
                cancellationToken);
            if (!tieneElPrograma)
            {
                ModelState.AddModelError(string.Empty, "Agrega primero el programa Cuidadores al paciente.");
            }
        }

        if (!model.FechaReporteEps.HasValue)
        {
            ModelState.AddModelError(nameof(model.FechaReporteEps), "Indica la fecha de reporte de la EPS.");
        }
        else if (model.FechaReporteEps.Value.Date > GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaReporteEps), "La fecha de reporte no puede ser futura.");
        }

        if (model.FechaReporteEps.HasValue
            && model.FechaInicioAtencion.HasValue
            && model.FechaInicioAtencion.Value.Date < model.FechaReporteEps.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaInicioAtencion),
                "La fecha de inicio no puede ser anterior a la fecha de reporte.");
        }

        if (!ModelState.IsValid)
        {
            return await VolverACuidadoresConErroresAsync(model, record, SeccionCuidadoresRecepcion, null, cancellationToken);
        }

        var nuevo = record is null;
        record ??= new CensoCuidadorRecord
        {
            CensoPacienteId = paciente!.Id,
            TipoIdentificacion = paciente.TipoIdentificacion,
            NumeroIdentificacion = paciente.NumeroIdentificacion,
            NombrePaciente = paciente.NombrePaciente,
            Estado = CuidadorEstados.Activo,
            CreatedAtUtc = DateTime.UtcNow
        };

        record.FechaReporteEps = model.FechaReporteEps!.Value.Date;
        record.FechaInicioAtencion = model.FechaInicioAtencion?.Date;
        record.UpdatedAtUtc = DateTime.UtcNow;

        if (nuevo)
        {
            await _context.CensoCuidadores.AddAsync(record, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await AuditarCuidadorAsync(nuevo ? "CENSO_CUIDADORES_CREADO" : "CENSO_CUIDADORES_RECEPCION_ACTUALIZADA", record, cancellationToken);

        TempData["SuccessMessage"] = "Recepción del ingreso guardada correctamente.";
        return VolverACuidadores(record, SeccionCuidadoresRecepcion);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarCuidadoresResponsable(
        CensoCuidadorViewModel model,
        CancellationToken cancellationToken)
    {
        return GuardarCuidadorSeccionAsync(
            model,
            SeccionCuidadoresResponsable,
            validar: ValidarCuidadorResponsable,
            restaurar: (vista, enviado) =>
            {
                vista.DireccionMismaDelPaciente = enviado.DireccionMismaDelPaciente;
                vista.DireccionResponsable = enviado.DireccionResponsable;
            },
            aplicar: AplicarCuidadorResponsableAsync,
            "CENSO_CUIDADORES_RESPONSABLE_ACTUALIZADO",
            "Responsable guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCuidadoresPlan(
        CensoCuidadorViewModel model,
        CancellationToken cancellationToken)
    {
        var auxiliares = await GetOpsAssistantOptionsAsync(cancellationToken);
        var guardado = model.EditingRecordId.HasValue
            ? await _context.CensoCuidadores.AsNoTracking()
                .Where(x => x.Id == model.EditingRecordId.Value)
                .Select(x => x.AuxiliarAsignado)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return await GuardarCuidadorSeccionAsync(
            model,
            SeccionCuidadoresPlan,
            validar: enviado => ValidarCuidadorPlan(enviado, auxiliares, guardado),
            restaurar: (vista, enviado) => vista.DiasAtencion = enviado.DiasAtencion,
            aplicar: (record, enviado, _) =>
            {
                AplicarCuidadorPlan(record, enviado);
                return Task.CompletedTask;
            },
            "CENSO_CUIDADORES_PLAN_ACTUALIZADO",
            "Plan de manejo guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarCuidadoresEstado(
        CensoCuidadorViewModel model,
        CancellationToken cancellationToken)
    {
        var fechaReporte = model.EditingRecordId.HasValue
            ? await _context.CensoCuidadores.AsNoTracking()
                .Where(x => x.Id == model.EditingRecordId.Value)
                .Select(x => (DateTime?)x.FechaReporteEps)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var estadoElegido = model.Estado?.Trim();
        var estadoFinal = CensoPacienteService.EstadoCuidadorAlRegistrarEgreso(estadoElegido, model.FechaEgreso);
        var estadoCambio = !string.Equals(estadoFinal, estadoElegido, StringComparison.OrdinalIgnoreCase);

        return await GuardarCuidadorSeccionAsync(
            model,
            SeccionCuidadoresEstado,
            validar: enviado =>
            {
                if (!CuidadorEstados.Todos.Contains(enviado.Estado?.Trim(), StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(enviado.Estado), "Selecciona el estado en el programa.");
                }

                if (enviado.FechaEgreso.HasValue
                    && fechaReporte.HasValue
                    && enviado.FechaEgreso.Value.Date < fechaReporte.Value.Date)
                {
                    ModelState.AddModelError(nameof(enviado.FechaEgreso),
                        "La fecha de egreso no puede ser anterior a la fecha de reporte.");
                }
            },
            restaurar: null,
            aplicar: (record, enviado, _) =>
            {
                record.Estado = CuidadorEstados.Todos.First(x =>
                    string.Equals(x, estadoFinal, StringComparison.OrdinalIgnoreCase));
                record.FechaEgreso = enviado.FechaEgreso?.Date;
                record.MotivoEgreso = TextoCuidador(enviado.MotivoEgreso);
                return Task.CompletedTask;
            },
            "CENSO_CUIDADORES_ESTADO_ACTUALIZADO",
            estadoCambio
                ? $"Estado guardado correctamente. Pasó a «{CuidadorEstados.Cancelado}» porque se registró la fecha de egreso."
                : "Estado guardado correctamente.",
            cancellationToken);
    }

    private async Task<IActionResult> GuardarCuidadorSeccionAsync(
        CensoCuidadorViewModel model,
        string seccion,
        Action<CensoCuidadorViewModel> validar,
        Action<CensoCuidadorViewModel, CensoCuidadorViewModel>? restaurar,
        Func<CensoCuidadorRecord, CensoCuidadorViewModel, CancellationToken, Task> aplicar,
        string accionAuditoria,
        string mensaje,
        CancellationToken cancellationToken)
    {
        var record = model.EditingRecordId.HasValue
            ? await _context.CensoCuidadores.FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
            : null;

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Guarda primero la recepción del ingreso.");
        }
        else
        {
            validar(model);
        }

        if (!ModelState.IsValid)
        {
            return await VolverACuidadoresConErroresAsync(model, record, seccion, restaurar, cancellationToken);
        }

        await aplicar(record!, model, cancellationToken);
        record!.UpdatedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        await AuditarCuidadorAsync(accionAuditoria, record, cancellationToken);

        TempData["SuccessMessage"] = mensaje;
        return VolverACuidadores(record, seccion);
    }

    private RedirectToActionResult VolverACuidadores(CensoCuidadorRecord record, string seccion) =>
        RedirectToAction(nameof(Index), new
        {
            cedulaPaciente = record.NumeroIdentificacion,
            programa = CensoProgramas.Cuidadores,
            seccion
        });

    /// <summary>
    /// Vuelve a la misma sección conservando lo escrito. Las demás secciones no viajaron en el
    /// envío, así que se repintan desde el registro; los campos enviados los recupera ModelState.
    /// </summary>
    private async Task<IActionResult> VolverACuidadoresConErroresAsync(
        CensoCuidadorViewModel enviado,
        CensoCuidadorRecord? record,
        string seccion,
        Action<CensoCuidadorViewModel, CensoCuidadorViewModel>? restaurar,
        CancellationToken cancellationToken)
    {
        var documento = NormalizeCedulaFilter(enviado.CedulaFiltro);
        var paciente = await _context.CensoPacientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.NumeroIdentificacion.ToUpper() == documento, cancellationToken);

        var vista = new CensoCuidadorViewModel { CedulaFiltro = documento };
        if (record is not null)
        {
            ApplyCuidadorRecordToModel(vista, record);
        }

        await PopulateCuidadorModelAsync(vista, paciente, cancellationToken);

        if (seccion == SeccionCuidadoresRecepcion)
        {
            vista.FechaReporteEps = enviado.FechaReporteEps;
            vista.FechaInicioAtencion = enviado.FechaInicioAtencion;
        }

        restaurar?.Invoke(vista, enviado);

        ViewData["SeccionInicial"] = seccion;
        return await VistaUnificadaConProgramaAsync(CensoProgramas.Cuidadores, vista, documento, cancellationToken);
    }

    private void ValidarCuidadorResponsable(CensoCuidadorViewModel model)
    {
        var nombre = model.NombreResponsable?.Trim();
        if (!string.IsNullOrEmpty(nombre) && !CuidadorNombrePattern.IsMatch(nombre))
        {
            ModelState.AddModelError(nameof(model.NombreResponsable),
                "El nombre solo admite letras y espacios: sin números ni caracteres especiales.");
        }

        var telefono = model.TelefonoResponsable?.Trim();
        if (!string.IsNullOrEmpty(telefono) && !CuidadorTelefonoPattern.IsMatch(telefono))
        {
            ModelState.AddModelError(nameof(model.TelefonoResponsable),
                "El teléfono solo admite números, máximo 10 dígitos.");
        }
    }

    private async Task AplicarCuidadorResponsableAsync(
        CensoCuidadorRecord record,
        CensoCuidadorViewModel model,
        CancellationToken cancellationToken)
    {
        record.NombreResponsable = TextoCuidador(model.NombreResponsable);
        record.ParentescoResponsable = TextoCuidador(model.ParentescoResponsable);
        record.TelefonoResponsable = string.IsNullOrWhiteSpace(model.TelefonoResponsable) ? null : model.TelefonoResponsable.Trim();
        record.DireccionMismaDelPaciente = model.DireccionMismaDelPaciente;

        if (model.DireccionMismaDelPaciente)
        {
            // Sale del maestro, no de lo que llegó en el formulario.
            record.DireccionResponsable = await _context.CensoPacientes.AsNoTracking()
                .Where(x => x.Id == record.CensoPacienteId)
                .Select(x => x.Direccion)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            record.DireccionResponsable = TextoCuidador(model.DireccionResponsable);
        }
    }

    private void ValidarCuidadorPlan(
        CensoCuidadorViewModel model,
        IReadOnlyList<SelectListItem> auxiliares,
        string? auxiliarGuardado)
    {
        if (model.TiempoCuidadorHorasDia is < 1 or > 24)
        {
            ModelState.AddModelError(nameof(model.TiempoCuidadorHorasDia), "Indica un número de horas entre 1 y 24.");
        }

        if (model.EscalaBarthel is < 0 or > 100)
        {
            ModelState.AddModelError(nameof(model.EscalaBarthel), "La escala de Barthel va de 0 a 100.");
        }

        if (model.TiempoOrdenamientoMeses is < 0)
        {
            ModelState.AddModelError(nameof(model.TiempoOrdenamientoMeses), "El tiempo de ordenamiento no puede ser negativo.");
        }

        if (!string.IsNullOrWhiteSpace(model.Horario)
            && !CuidadorHorarioValues.Contains(model.Horario.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Horario), "Selecciona Diurno o Nocturno.");
        }

        var auxiliar = model.AuxiliarAsignado?.Trim();
        if (!string.IsNullOrEmpty(auxiliar)
            && !string.Equals(auxiliar, auxiliarGuardado, StringComparison.OrdinalIgnoreCase)
            && !auxiliares.Any(x => string.Equals(x.Value, auxiliar, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.AuxiliarAsignado), "Selecciona un auxiliar de la lista.");
        }
        else if (!string.IsNullOrEmpty(auxiliar))
        {
            // Queda con la forma del directorio aunque se haya escrito en otra caja.
            model.AuxiliarAsignado = auxiliares
                .FirstOrDefault(x => string.Equals(x.Value, auxiliar, StringComparison.OrdinalIgnoreCase))?.Value
                ?? auxiliarGuardado;
        }
    }

    private static void AplicarCuidadorPlan(CensoCuidadorRecord record, CensoCuidadorViewModel model)
    {
        var dias = CuidadorDiasValues
            .Where(dia => (model.DiasAtencion ?? []).Contains(dia, StringComparer.OrdinalIgnoreCase))
            .ToList();

        record.TiempoCuidadorHorasDia = model.TiempoCuidadorHorasDia;
        record.DiasAtencion = dias.Count == 0 ? null : string.Join(", ", dias);
        record.Horario = CuidadorHorarioValues.FirstOrDefault(x =>
            string.Equals(x, model.Horario?.Trim(), StringComparison.OrdinalIgnoreCase));
        record.EscalaBarthel = model.EscalaBarthel;
        record.FechaCertificacionDependencia = model.FechaCertificacionDependencia?.Date;
        record.TiempoOrdenamientoMeses = model.TiempoOrdenamientoMeses;
        record.FechaVencimientoCertificado = model.FechaVencimientoCertificado?.Date;
        record.Observacion = TextoCuidador(model.Observacion);
        record.VerificacionDerechos = TextoCuidador(model.VerificacionDerechos);
        record.FechaVerificacionDerechos = model.FechaVerificacionDerechos?.Date;
        record.AuxiliarAsignado = string.IsNullOrWhiteSpace(model.AuxiliarAsignado) ? null : model.AuxiliarAsignado.Trim();
    }

    private static string? TextoCuidador(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpper(CuidadorTextCulture);

    private async Task AuditarCuidadorAsync(string accion, CensoCuidadorRecord record, CancellationToken cancellationToken)
    {
        var usuario = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? (Guid?)uid : null;
        await _auditService.LogAsync(accion, "CensoCuidador",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}",
            usuario, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
    }

    private static void ApplyCuidadorRecordToModel(CensoCuidadorViewModel model, CensoCuidadorRecord record)
    {
        model.EditingRecordId = record.Id;
        model.CedulaFiltro = record.NumeroIdentificacion;
        model.FechaReporteEps = record.FechaReporteEps;
        model.FechaInicioAtencion = record.FechaInicioAtencion;
        model.NombreResponsable = record.NombreResponsable;
        model.ParentescoResponsable = record.ParentescoResponsable;
        model.TelefonoResponsable = record.TelefonoResponsable;
        model.DireccionResponsable = record.DireccionResponsable;
        model.DireccionMismaDelPaciente = record.DireccionMismaDelPaciente;
        model.TiempoCuidadorHorasDia = record.TiempoCuidadorHorasDia;
        model.DiasAtencion = string.IsNullOrWhiteSpace(record.DiasAtencion)
            ? []
            : record.DiasAtencion.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        model.Horario = record.Horario;
        model.EscalaBarthel = record.EscalaBarthel;
        model.FechaCertificacionDependencia = record.FechaCertificacionDependencia;
        model.TiempoOrdenamientoMeses = record.TiempoOrdenamientoMeses;
        model.FechaVencimientoCertificado = record.FechaVencimientoCertificado;
        model.Observacion = record.Observacion;
        model.VerificacionDerechos = record.VerificacionDerechos;
        model.FechaVerificacionDerechos = record.FechaVerificacionDerechos;
        model.AuxiliarAsignado = record.AuxiliarAsignado;
        model.AuxiliarGuardado = record.AuxiliarAsignado;
        model.Estado = record.Estado;
        model.FechaEgreso = record.FechaEgreso;
        model.MotivoEgreso = record.MotivoEgreso;
    }

    /// <summary>Catálogos y lo que el formulario lee del maestro y de Programa crónicos.</summary>
    private async Task PopulateCuidadorModelAsync(
        CensoCuidadorViewModel model,
        CensoPaciente? paciente,
        CancellationToken cancellationToken)
    {
        model.DiasSemanaOptions = CuidadorDiasValues;
        model.HorarioOptions = BuildOptions(CuidadorHorarioValues);
        model.EstadoOptions = BuildOptions(CuidadorEstados.Todos);

        var auxiliares = await GetOpsAssistantOptionsAsync(cancellationToken);
        var guardado = model.AuxiliarGuardado;
        model.AuxiliarEnfermeriaOptions = !string.IsNullOrWhiteSpace(guardado)
            && !auxiliares.Any(x => string.Equals(x.Value, guardado, StringComparison.OrdinalIgnoreCase))
                ? [.. auxiliares, new SelectListItem(guardado, guardado)]
                : auxiliares;

        if (paciente is null)
        {
            return;
        }

        model.DireccionPaciente = paciente.Direccion;

        var permanencia = await ProgramaPermanentesAsync([(paciente.NumeroIdentificacion, paciente.Id)], cancellationToken);
        var (enPrograma, fechaIngreso) = permanencia(paciente.NumeroIdentificacion, paciente.Id);
        model.EnProgramaPermanentes = enPrograma;
        model.FechaIngresoPermanentes = fechaIngreso;
    }

    /// <summary>
    /// «¿Está en programa permanentes?»: si el paciente tiene crónicos agregado, con la misma regla
    /// del carril (un registro abierto, o el programa recién agregado y todavía sin diligenciar), y
    /// la fecha en que ingresó. Devuelve una función para que el exportable haga una sola consulta.
    /// </summary>
    private async Task<Func<string?, long?, (bool EnPrograma, DateTime? FechaIngreso)>> ProgramaPermanentesAsync(
        IReadOnlyCollection<(string? Documento, long? PacienteId)> pacientes,
        CancellationToken cancellationToken)
    {
        static string Doc(string? valor) => (valor ?? string.Empty).Trim().ToUpperInvariant();

        var docs = pacientes.Select(x => Doc(x.Documento)).Where(x => x.Length > 0).Distinct().ToList();
        var ids = pacientes.Where(x => x.PacienteId.HasValue).Select(x => x.PacienteId!.Value).Distinct().ToList();

        var cronicos = await _context.CensoCronicos.AsNoTracking()
            .Where(x => docs.Contains(x.NumeroIdentificacion.Trim().ToUpper())
                || (x.CensoPacienteId != null && ids.Contains(x.CensoPacienteId.Value)))
            .Select(x => new { x.Id, x.NumeroIdentificacion, x.CensoPacienteId, x.FechaIngreso, x.EstadoPaciente, x.FechaEgreso })
            .ToListAsync(cancellationToken);
        var abiertos = cronicos
            .Where(x => !CensoPacienteService.EsCronicoCerrado(x.EstadoPaciente, x.FechaEgreso))
            .OrderByDescending(x => x.Id)
            .ToList();

        var sinDiligenciar = await _context.CensoPacienteProgramas.AsNoTracking()
            .Where(x => x.Programa == CensoProgramas.Cronicos
                && x.RegistroId == null
                && x.CerradoAtUtc == null
                && ids.Contains(x.CensoPacienteId))
            .Select(x => x.CensoPacienteId)
            .ToListAsync(cancellationToken);

        return (documento, pacienteId) =>
        {
            var doc = Doc(documento);
            var abierto = abiertos.FirstOrDefault(x =>
                Doc(x.NumeroIdentificacion) == doc
                || (pacienteId.HasValue && x.CensoPacienteId == pacienteId));
            if (abierto is not null)
            {
                return (true, abierto.FechaIngreso);
            }

            return (pacienteId.HasValue && sinDiligenciar.Contains(pacienteId.Value), null);
        };
    }

    /// <summary>
    /// Exportable del programa, con los campos en el orden del archivo con que la operación llevaba
    /// este censo. Los datos básicos salen del maestro, que es donde viven.
    /// </summary>
    private async Task<IActionResult> ExportarCuidadoresExcel(string? cedulaPaciente, CancellationToken cancellationToken)
    {
        var documento = NormalizeCedulaFilter(cedulaPaciente);
        var registros = await _context.CensoCuidadores.AsNoTracking()
            .Where(x => string.IsNullOrEmpty(documento) || x.NumeroIdentificacion == documento)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var idsPaciente = registros.Where(x => x.CensoPacienteId.HasValue).Select(x => x.CensoPacienteId!.Value).Distinct().ToList();
        var maestros = await _context.CensoPacientes.AsNoTracking()
            .Where(x => idsPaciente.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var permanencia = await ProgramaPermanentesAsync(
            registros.Select(x => ((string?)x.NumeroIdentificacion, x.CensoPacienteId)).ToList(),
            cancellationToken);

        var columnas = new (string Titulo, Func<CensoCuidadorRecord, CensoPaciente?, string?> Valor, bool EsFecha, bool EsNumero)[]
        {
            ("Fecha de reporte por parte de la EPS", (r, _) => Fecha(r.FechaReporteEps), true, false),
            ("Fecha de inicio de atención", (r, _) => Fecha(r.FechaInicioAtencion), true, false),
            ("Oportunidad de inicio (días)", (r, _) => r.OportunidadInicioDias?.ToString(CultureInfo.InvariantCulture), false, true),
            ("Estado en programa", (r, _) => r.Estado, false, false),
            ("Fecha de egreso", (r, _) => Fecha(r.FechaEgreso), true, false),
            ("Motivo de egreso o cancelación", (r, _) => r.MotivoEgreso, false, false),
            ("Tipo de identificación", (r, _) => r.TipoIdentificacion, false, false),
            ("Número de identificación", (r, _) => r.NumeroIdentificacion, false, false),
            ("Nombre del paciente", (r, m) => m?.NombrePaciente ?? r.NombrePaciente, false, false),
            ("Fecha de nacimiento", (_, m) => Fecha(m?.FechaNacimiento), true, false),
            ("Edad", (_, m) => m?.Edad.ToString(CultureInfo.InvariantCulture), false, true),
            ("Género", (_, m) => m?.Genero, false, false),
            ("Asegurador", (_, m) => m?.Asegurador, false, false),
            ("CIE10", (_, m) => m?.CodigoCie10, false, false),
            ("Diagnóstico", (_, m) => m?.DiagnosticoDescriptivo, false, false),
            ("Nombre del responsable", (r, _) => r.NombreResponsable, false, false),
            ("Parentesco", (r, _) => r.ParentescoResponsable, false, false),
            ("Teléfono del responsable", (r, _) => r.TelefonoResponsable, false, false),
            ("Dirección del responsable", (r, _) => r.DireccionResponsable, false, false),
            ("Dirección del paciente", (_, m) => m?.Direccion, false, false),
            ("Barrio", (_, m) => m?.Barrio, false, false),
            ("Municipio", (_, m) => m?.MunicipioResidencia, false, false),
            ("Zona dirección según municipio", (_, m) => m?.ZonaDireccionSegunMunicipio, false, false),
            ("Clasificación zona Sura", (_, m) => m?.ClasificacionZonaSura, false, false),
            ("Tiempo de cuidador (horas al día)", (r, _) => r.TiempoCuidadorHorasDia?.ToString(CultureInfo.InvariantCulture), false, true),
            ("Días de atención", (r, _) => r.DiasAtencion, false, false),
            ("Horario", (r, _) => r.Horario, false, false),
            ("¿Está en programa permanentes?", (r, _) => permanencia(r.NumeroIdentificacion, r.CensoPacienteId).EnPrograma ? "Si" : "No", false, false),
            ("Fecha de ingreso a permanente", (r, _) => Fecha(permanencia(r.NumeroIdentificacion, r.CensoPacienteId).FechaIngreso), true, false),
            ("Escala de Barthel", (r, _) => r.EscalaBarthel?.ToString(CultureInfo.InvariantCulture), false, true),
            ("Fecha de certificación de dependencia funcional", (r, _) => Fecha(r.FechaCertificacionDependencia), true, false),
            ("Tiempo de ordenamiento (meses)", (r, _) => r.TiempoOrdenamientoMeses?.ToString(CultureInfo.InvariantCulture), false, true),
            ("Fecha de vencimiento del certificado de dependencia", (r, _) => Fecha(r.FechaVencimientoCertificado), true, false),
            ("Observación", (r, _) => r.Observacion, false, false),
            ("Verificación de derechos", (r, _) => r.VerificacionDerechos, false, false),
            ("Fecha de verificación de derechos", (r, _) => Fecha(r.FechaVerificacionDerechos), true, false),
            ("Auxiliar asignado", (r, _) => r.AuxiliarAsignado, false, false),
            ("Fecha de creación", (r, _) => FechaExportable.FechaHora(ColombiaTime.Convert(r.CreatedAtUtc)), false, false),
            ("Última actualización", (r, _) => r.UpdatedAtUtc.HasValue ? FechaExportable.FechaHora(ColombiaTime.Convert(r.UpdatedAtUtc.Value)) : null, false, false)
        };

        var filas = registros
            .Select(r =>
            {
                var maestro = r.CensoPacienteId is long id && maestros.TryGetValue(id, out var m) ? m : null;
                return (IReadOnlyList<string?>)columnas.Select(c => c.Valor(r, maestro)).ToList();
            })
            .ToList();

        var libro = ExcelWorkbookWriter.BuildTableWorkbook(
            "Cuidadores",
            columnas.Select(x => x.Titulo).ToList(),
            filas,
            DateTime.UtcNow,
            dateColumns: columnas.Index().Where(x => x.Item.EsFecha).Select(x => x.Index).ToList(),
            numberColumns: columnas.Index().Where(x => x.Item.EsNumero).Select(x => x.Index).ToList());

        return File(
            libro,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"censo_cuidadores_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        static string? Fecha(DateTime? valor) => valor.HasValue ? FechaExportable.Fecha(valor.Value) : null;
    }
}
