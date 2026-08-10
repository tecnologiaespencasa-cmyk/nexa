using System.Text.Json;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.EspacioCorporativo;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Modulo de Actas por plantilla (solo administrador).
///
/// Flujo: elegir plantilla -> llenar variables -> previsualizar el documento -> firmar.
/// Al firmar se guarda el acta con el cuerpo ya renderizado y se envia la copia al correo
/// capturado (ese correo no se imprime en el acta).
/// </summary>
public partial class EspacioCorporativoController
{
    private static readonly JsonSerializerOptions ActaJsonOptions = new() { WriteIndented = false };

    // ─────────────────────────────────────────────────────────────────────────
    // Listado y busqueda
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> Actas(
        string? busqueda,
        string? plantilla,
        CancellationToken cancellationToken)
    {
        var model = new EspacioActasIndexViewModel
        {
            Busqueda = busqueda?.Trim(),
            PlantillaFiltro = plantilla?.Trim(),
            Plantillas = EspacioActaPlantillas.Todas
        };

        var query = _context.EspacioActasDocumentales.AsNoTracking();

        // Un solo cuadro de busqueda que cubre documento, nombre y usuario.
        if (!string.IsNullOrWhiteSpace(model.Busqueda))
        {
            var termino = $"%{model.Busqueda}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.NombreRecibe, termino)
                || (x.DocumentoRecibe != null && EF.Functions.ILike(x.DocumentoRecibe, termino))
                || (x.UsuarioRecibe != null && EF.Functions.ILike(x.UsuarioRecibe, termino))
                || (x.CorreoRecibe != null && EF.Functions.ILike(x.CorreoRecibe, termino)));
        }

        if (EspacioActaPlantillas.Obtener(model.PlantillaFiltro) is not null)
        {
            query = query.Where(x => x.PlantillaCodigo == model.PlantillaFiltro);
        }

        var actas = await query
            .OrderByDescending(x => x.FirmadaAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(300)
            .Select(x => new
            {
                x.Id,
                x.PlantillaNombre,
                x.TituloActa,
                x.NombreRecibe,
                x.DocumentoRecibe,
                x.CorreoRecibe,
                x.UsuarioRecibe,
                x.EmitidaPorNombre,
                x.CorreoEnviado,
                x.CorreoError,
                x.FirmadaAtUtc
            })
            .ToListAsync(cancellationToken);

        model.Actas = actas
            .Select(x => new EspacioActaEmitidaViewModel
            {
                Id = x.Id,
                PlantillaNombre = x.PlantillaNombre,
                TituloActa = x.TituloActa,
                NombreRecibe = x.NombreRecibe,
                DocumentoRecibe = x.DocumentoRecibe,
                CorreoRecibe = x.CorreoRecibe,
                UsuarioRecibe = x.UsuarioRecibe,
                EmitidaPorNombre = x.EmitidaPorNombre,
                CorreoEnviado = x.CorreoEnviado,
                CorreoError = x.CorreoError,
                FechaFirma = ColombiaTime.Convert(x.FirmadaAtUtc)
            })
            .ToList();

        model.TotalActas = await _context.EspacioActasDocumentales.CountAsync(cancellationToken);
        model.TotalCorreosPendientes = await _context.EspacioActasDocumentales
            .CountAsync(x => !x.CorreoEnviado, cancellationToken);
        model.TieneFirmaGuardada = await GetFirmaGuardadaAsync(cancellationToken) is not null;

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Captura de variables
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public IActionResult ActaNueva(string codigo)
    {
        var plantilla = EspacioActaPlantillas.Obtener(codigo);
        if (plantilla is null)
        {
            TempData[ErrorMessageKey] = "La plantilla solicitada no existe.";
            return RedirectToAction(nameof(Actas));
        }

        return View(new EspacioActaCapturaViewModel
        {
            PlantillaCodigo = plantilla.Codigo,
            Plantilla = plantilla
        });
    }

    /// <summary>Valida las variables y muestra el acta renderizada lista para firmar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPrevisualizar(
        string plantillaCodigo,
        CancellationToken cancellationToken)
    {
        var plantilla = EspacioActaPlantillas.Obtener(plantillaCodigo);
        if (plantilla is null)
        {
            TempData[ErrorMessageKey] = "La plantilla solicitada no existe.";
            return RedirectToAction(nameof(Actas));
        }

        var valores = LeerValoresDelFormulario(plantilla);
        var errores = ValidarValores(plantilla, valores);

        if (errores.Count > 0)
        {
            return View("ActaNueva", new EspacioActaCapturaViewModel
            {
                PlantillaCodigo = plantilla.Codigo,
                Plantilla = plantilla,
                Valores = valores,
                MensajeError = string.Join(" ", errores)
            });
        }

        var firmante = await ConstruirFirmanteAsync(cancellationToken);
        var fecha = ColombiaTime.Convert(DateTime.UtcNow);
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);

        return View("ActaFirmar", new EspacioActaFirmaViewModel
        {
            PlantillaCodigo = plantilla.Codigo,
            PlantillaNombre = plantilla.Nombre,
            TituloActa = plantilla.TituloActa,
            RotuloRecibe = plantilla.RotuloRecibe,
            CuerpoHtml = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha),
            Valores = valores,
            NombreRecibe = valores.GetValueOrDefault(plantilla.CampoNombre) ?? string.Empty,
            DocumentoRecibe = valores.GetValueOrDefault(plantilla.CampoDocumento),
            CorreoRecibe = valores.GetValueOrDefault(plantilla.CampoCorreo),
            EmitidaPorNombre = firmante.Nombre,
            EmitidaPorCargo = firmante.Cargo,
            EmitidaPorDocumento = firmante.Documento,
            FirmaEmiteDataUrl = firmaGuardada?.FirmaDataUrl,
            TieneFirmaGuardada = firmaGuardada is not null,
            Fecha = fecha
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Emision (firma + envio de la copia)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> ActaEmitir(
        string plantillaCodigo,
        string? firmaRecibeDataUrl,
        string? firmaEmiteDataUrl,
        bool guardarFirmaEmite,
        CancellationToken cancellationToken)
    {
        var plantilla = EspacioActaPlantillas.Obtener(plantillaCodigo);
        if (plantilla is null)
        {
            TempData[ErrorMessageKey] = "La plantilla solicitada no existe.";
            return RedirectToAction(nameof(Actas));
        }

        var valores = LeerValoresDelFormulario(plantilla);
        var errores = ValidarValores(plantilla, valores);

        if (errores.Count > 0)
        {
            return await VolverAFirmarAsync(plantilla, valores, string.Join(" ", errores), cancellationToken);
        }

        if (!EspacioCorporativoCatalogos.EsFirmaValida(firmaRecibeDataUrl))
        {
            return await VolverAFirmarAsync(plantilla, valores, "Falta la firma de quien recibe.", cancellationToken);
        }

        // La firma del area de TI se reutiliza; solo se pide trazarla la primera vez.
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var firmaEmite = firmaGuardada?.FirmaDataUrl;

        if (string.IsNullOrWhiteSpace(firmaEmite))
        {
            if (!EspacioCorporativoCatalogos.EsFirmaValida(firmaEmiteDataUrl))
            {
                return await VolverAFirmarAsync(
                    plantilla,
                    valores,
                    "Aun no tienes una firma guardada. Trazala para emitir el acta.",
                    cancellationToken);
            }

            firmaEmite = firmaEmiteDataUrl!.Trim();

            if (guardarFirmaEmite)
            {
                await GuardarFirmaDelUsuarioAsync(firmaEmite, null, null, cancellationToken);
            }
        }

        var firmante = await ConstruirFirmanteAsync(cancellationToken);
        var fecha = ColombiaTime.Convert(DateTime.UtcNow);

        var acta = new EspacioActaDocumental
        {
            PlantillaCodigo = plantilla.Codigo,
            PlantillaNombre = plantilla.Nombre,
            TituloActa = plantilla.TituloActa,
            NombreRecibe = (valores.GetValueOrDefault(plantilla.CampoNombre) ?? string.Empty).Trim(),
            DocumentoRecibe = NormalizarOpcional(valores.GetValueOrDefault(plantilla.CampoDocumento)),
            CorreoRecibe = NormalizarOpcional(valores.GetValueOrDefault(plantilla.CampoCorreo)),
            UsuarioRecibe = plantilla.CampoUsuario is null
                ? null
                : NormalizarOpcional(valores.GetValueOrDefault(plantilla.CampoUsuario)),
            ValoresJson = JsonSerializer.Serialize(valores, ActaJsonOptions),
            CuerpoHtml = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha),
            EmitidaPorUserId = GetCurrentUserId(),
            EmitidaPorNombre = firmante.Nombre,
            EmitidaPorCargo = firmante.Cargo,
            EmitidaPorDocumento = firmante.Documento,
            FirmaEmiteDataUrl = firmaEmite,
            FirmaRecibeDataUrl = firmaRecibeDataUrl!.Trim(),
            FirmadaAtUtc = DateTime.UtcNow
        };

        await _context.EspacioActasDocumentales.AddAsync(acta, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // El envio del correo no debe impedir que el acta quede firmada y guardada.
        if (!string.IsNullOrWhiteSpace(acta.CorreoRecibe))
        {
            var envio = await _notificationService.EnviarCopiaActaAsync(acta, cancellationToken);
            acta.CorreoEnviado = envio.Succeeded;
            acta.CorreoEnviadoAtUtc = envio.Succeeded ? DateTime.UtcNow : null;
            acta.CorreoError = envio.Succeeded ? null : Resumir(envio.ErrorMessage ?? "Error desconocido", 500);
            await _context.SaveChangesAsync(cancellationToken);

            TempData[envio.Succeeded ? SuccessMessageKey : WarningMessageKey] = envio.Succeeded
                ? $"Acta N° {acta.Id} firmada. Se envio la copia a {acta.CorreoRecibe}."
                : $"Acta N° {acta.Id} firmada, pero no se pudo enviar el correo. Puedes reenviarlo desde el listado.";
        }
        else
        {
            TempData[SuccessMessageKey] = $"Acta N° {acta.Id} firmada y guardada.";
        }

        await LogAuditAsync(
            "ESPACIO_ACTA_PLANTILLA_EMITIDA",
            $"Acta #{acta.Id} ({acta.PlantillaNombre}) a nombre de {acta.NombreRecibe}",
            cancellationToken);

        return RedirectToAction(nameof(ActaVer), new { id = acta.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaReenviarCorreo(long id, CancellationToken cancellationToken)
    {
        var acta = await _context.EspacioActasDocumentales
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (acta is null)
        {
            TempData[ErrorMessageKey] = "El acta no existe.";
            return RedirectToAction(nameof(Actas));
        }

        if (string.IsNullOrWhiteSpace(acta.CorreoRecibe))
        {
            TempData[ErrorMessageKey] = "El acta no tiene un correo de destino registrado.";
            return RedirectToAction(nameof(Actas));
        }

        var envio = await _notificationService.EnviarCopiaActaAsync(acta, cancellationToken);
        acta.CorreoEnviado = envio.Succeeded;
        acta.CorreoEnviadoAtUtc = envio.Succeeded ? DateTime.UtcNow : acta.CorreoEnviadoAtUtc;
        acta.CorreoError = envio.Succeeded ? null : Resumir(envio.ErrorMessage ?? "Error desconocido", 500);
        await _context.SaveChangesAsync(cancellationToken);

        TempData[envio.Succeeded ? SuccessMessageKey : ErrorMessageKey] = envio.Succeeded
            ? $"Copia reenviada a {acta.CorreoRecibe}."
            : $"No se pudo enviar: {acta.CorreoError}";

        return RedirectToAction(nameof(Actas));
    }

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaVer(long id, CancellationToken cancellationToken)
    {
        var acta = await _context.EspacioActasDocumentales
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (acta is null)
        {
            return NotFound();
        }

        var plantilla = EspacioActaPlantillas.Obtener(acta.PlantillaCodigo);

        return View(new EspacioActaEmitidaDocumentoViewModel
        {
            Id = acta.Id,
            TituloActa = acta.TituloActa,
            PlantillaNombre = acta.PlantillaNombre,
            RotuloRecibe = plantilla?.RotuloRecibe ?? "Recibe",
            CuerpoHtml = acta.CuerpoHtml,
            NombreRecibe = acta.NombreRecibe,
            DocumentoRecibe = acta.DocumentoRecibe,
            EmitidaPorNombre = acta.EmitidaPorNombre,
            EmitidaPorCargo = acta.EmitidaPorCargo,
            EmitidaPorDocumento = acta.EmitidaPorDocumento,
            FirmaEmiteDataUrl = acta.FirmaEmiteDataUrl,
            FirmaRecibeDataUrl = acta.FirmaRecibeDataUrl,
            FechaFirma = ColombiaTime.Convert(acta.FirmadaAtUtc)
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Vuelve a la pantalla de firma conservando lo capturado, en vez de mandar al
    /// usuario al formulario vacio cuando el servidor rechaza algo.
    /// </summary>
    private async Task<IActionResult> VolverAFirmarAsync(
        EspacioActaPlantilla plantilla,
        Dictionary<string, string?> valores,
        string mensajeError,
        CancellationToken cancellationToken)
    {
        var firmante = await ConstruirFirmanteAsync(cancellationToken);
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var fecha = ColombiaTime.Convert(DateTime.UtcNow);

        return View("ActaFirmar", new EspacioActaFirmaViewModel
        {
            PlantillaCodigo = plantilla.Codigo,
            PlantillaNombre = plantilla.Nombre,
            TituloActa = plantilla.TituloActa,
            RotuloRecibe = plantilla.RotuloRecibe,
            CuerpoHtml = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha),
            Valores = valores,
            NombreRecibe = valores.GetValueOrDefault(plantilla.CampoNombre) ?? string.Empty,
            DocumentoRecibe = valores.GetValueOrDefault(plantilla.CampoDocumento),
            CorreoRecibe = valores.GetValueOrDefault(plantilla.CampoCorreo),
            EmitidaPorNombre = firmante.Nombre,
            EmitidaPorCargo = firmante.Cargo,
            EmitidaPorDocumento = firmante.Documento,
            FirmaEmiteDataUrl = firmaGuardada?.FirmaDataUrl,
            TieneFirmaGuardada = firmaGuardada is not null,
            Fecha = fecha,
            MensajeError = mensajeError
        });
    }

    /// <summary>
    /// Lee del formulario solo las claves declaradas por la plantilla; cualquier otro
    /// campo enviado se ignora.
    /// </summary>
    private Dictionary<string, string?> LeerValoresDelFormulario(EspacioActaPlantilla plantilla)
    {
        var valores = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var campo in plantilla.Campos)
        {
            var bruto = Request.Form[$"valores[{campo.Clave}]"].ToString();
            var limpio = bruto.Trim();

            if (limpio.Length > campo.MaxLength)
            {
                limpio = limpio[..campo.MaxLength];
            }

            valores[campo.Clave] = limpio;
        }

        return valores;
    }

    private static List<string> ValidarValores(
        EspacioActaPlantilla plantilla,
        IReadOnlyDictionary<string, string?> valores)
    {
        var errores = new List<string>();

        foreach (var campo in plantilla.Campos)
        {
            var valor = valores.GetValueOrDefault(campo.Clave);

            if (campo.Requerido && string.IsNullOrWhiteSpace(valor))
            {
                errores.Add($"El campo '{campo.Etiqueta}' es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(valor))
            {
                continue;
            }

            if (campo.Tipo == EspacioActaTipoCampo.Seleccion
                && campo.Opciones.Count > 0
                && !campo.Opciones.Any(o => string.Equals(o.Valor, valor, StringComparison.OrdinalIgnoreCase)))
            {
                errores.Add($"Selecciona un valor valido para '{campo.Etiqueta}'.");
            }

            if (campo.Tipo == EspacioActaTipoCampo.Correo
                && (!valor.Contains('@', StringComparison.Ordinal) || valor.Contains(' ', StringComparison.Ordinal)))
            {
                errores.Add($"El campo '{campo.Etiqueta}' debe ser un correo valido.");
            }
        }

        return errores;
    }

    /// <summary>Datos de quien firma por la compania: salen de la firma guardada y del usuario.</summary>
    private async Task<EspacioActaRenderer.DatosFirmante> ConstruirFirmanteAsync(CancellationToken cancellationToken)
    {
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var userId = GetCurrentUserId();

        var documento = userId.HasValue
            ? await _context.Users
                .AsNoTracking()
                .Where(x => x.Id == userId.Value)
                .Select(x => x.NationalId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new EspacioActaRenderer.DatosFirmante(
            string.IsNullOrWhiteSpace(firmaGuardada?.NombreFirmante)
                ? GetCurrentUserFullName()
                : firmaGuardada!.NombreFirmante!,
            documento ?? "No registra",
            string.IsNullOrWhiteSpace(firmaGuardada?.Cargo) ? "Lider de Tecnologia" : firmaGuardada!.Cargo!);
    }
}
