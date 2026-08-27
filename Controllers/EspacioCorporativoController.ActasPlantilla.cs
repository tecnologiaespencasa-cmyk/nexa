using System.Globalization;
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
///
/// Las plantillas vienen de dos origenes -de fabrica y del disenador- pero se resuelven
/// al mismo tipo, asi que de aqui hacia abajo el camino es uno solo.
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
            Plantillas = await ListarPlantillasAsync(soloActivas: true, cancellationToken)
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

        if (!string.IsNullOrWhiteSpace(model.PlantillaFiltro)
            && model.Plantillas.Any(x => string.Equals(x.Codigo, model.PlantillaFiltro, StringComparison.OrdinalIgnoreCase)))
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
    public async Task<IActionResult> ActaNueva(string codigo, CancellationToken cancellationToken)
    {
        var plantilla = await ObtenerPlantillaAsync(codigo, cancellationToken);
        if (plantilla is null)
        {
            TempData[ErrorMessageKey] = "La plantilla solicitada no existe o fue desactivada.";
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
        var plantilla = await ObtenerPlantillaAsync(plantillaCodigo, cancellationToken);
        if (plantilla is null)
        {
            TempData[ErrorMessageKey] = "La plantilla solicitada no existe o fue desactivada.";
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

        return await ConstruirVistaFirmaAsync(plantilla, valores, null, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Emision (firma + envio de la copia)
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> ActaEmitir(
        string plantillaCodigo,
        bool guardarFirmaEmite,
        CancellationToken cancellationToken)
    {
        var plantilla = await ObtenerPlantillaAsync(plantillaCodigo, cancellationToken);
        if (plantilla is null)
        {
            TempData[ErrorMessageKey] = "La plantilla solicitada no existe o fue desactivada.";
            return RedirectToAction(nameof(Actas));
        }

        var valores = LeerValoresDelFormulario(plantilla);
        var errores = ValidarValores(plantilla, valores);

        if (errores.Count > 0)
        {
            return await ConstruirVistaFirmaAsync(plantilla, valores, string.Join(" ", errores), cancellationToken);
        }

        var firmante = await ConstruirFirmanteAsync(cancellationToken);
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var firmasEstampadas = new List<EspacioActaFirmaEmitida>();
        string? trazoEmisorNuevo = null;

        foreach (var definicion in plantilla.FirmasEfectivas)
        {
            var trazo = Request.Form[$"firmas[{definicion.Clave}]"].ToString().Trim();
            var esEmisor = definicion.Origen == EspacioActaFirmaOrigen.Emisor;

            if (esEmisor && !string.IsNullOrWhiteSpace(firmaGuardada?.FirmaDataUrl))
            {
                // La firma del area de TI se reutiliza; solo se pide trazarla la primera vez.
                trazo = firmaGuardada!.FirmaDataUrl;
            }

            if (string.IsNullOrWhiteSpace(trazo))
            {
                if (!definicion.Requerida)
                {
                    continue;
                }

                return await ConstruirVistaFirmaAsync(
                    plantilla,
                    valores,
                    esEmisor
                        ? "Aun no tienes una firma guardada. Trazala para emitir el acta."
                        : $"Falta la firma de '{definicion.Rotulo}'.",
                    cancellationToken);
            }

            if (!EspacioCorporativoCatalogos.EsFirmaValida(trazo))
            {
                return await ConstruirVistaFirmaAsync(
                    plantilla,
                    valores,
                    $"La firma de '{definicion.Rotulo}' no se recibio correctamente. Vuelve a trazarla.",
                    cancellationToken);
            }

            if (esEmisor && string.IsNullOrWhiteSpace(firmaGuardada?.FirmaDataUrl))
            {
                trazoEmisorNuevo = trazo;
            }

            var datos = DescribirFirmante(definicion, plantilla, valores, firmante);

            firmasEstampadas.Add(new EspacioActaFirmaEmitida
            {
                Clave = definicion.Clave,
                Rotulo = definicion.Rotulo,
                Nombre = datos.Nombre,
                Documento = datos.Documento,
                Cargo = datos.Cargo,
                DataUrl = trazo
            });
        }

        if (firmasEstampadas.Count == 0)
        {
            return await ConstruirVistaFirmaAsync(
                plantilla,
                valores,
                "El acta necesita al menos una firma para emitirse.",
                cancellationToken);
        }

        if (trazoEmisorNuevo is not null && guardarFirmaEmite)
        {
            await GuardarFirmaDelUsuarioAsync(trazoEmisorNuevo, null, null, cancellationToken);
        }

        var fecha = ColombiaTime.Convert(DateTime.UtcNow);
        var nombreRecibe = ValorDe(valores, plantilla.CampoNombre);

        var acta = new EspacioActaDocumental
        {
            PlantillaCodigo = plantilla.Codigo,
            PlantillaNombre = plantilla.Nombre,
            TituloActa = plantilla.TituloActa,
            NombreRecibe = Resumir(
                string.IsNullOrWhiteSpace(nombreRecibe)
                    ? firmasEstampadas[^1].Nombre
                    : nombreRecibe,
                160),
            DocumentoRecibe = NormalizarOpcional(ValorDe(valores, plantilla.CampoDocumento)),
            CorreoRecibe = NormalizarOpcional(ValorDe(valores, plantilla.CampoCorreo)),
            UsuarioRecibe = NormalizarOpcional(ValorDe(valores, plantilla.CampoUsuario)),
            ValoresJson = JsonSerializer.Serialize(valores, ActaJsonOptions),
            CuerpoHtml = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha),
            EmitidaPorUserId = GetCurrentUserId(),
            EmitidaPorNombre = firmante.Nombre,
            EmitidaPorCargo = firmante.Cargo,
            EmitidaPorDocumento = firmante.Documento,
            // Las dos columnas de siempre siguen llenas para no romper actas ni consultas
            // anteriores; el detalle completo vive en FirmasJson.
            FirmaEmiteDataUrl = ElegirTrazo(firmasEstampadas, plantilla, EspacioActaFirmaOrigen.Emisor),
            FirmaRecibeDataUrl = ElegirTrazo(firmasEstampadas, plantilla, EspacioActaFirmaOrigen.EnVivo),
            FirmasJson = JsonSerializer.Serialize(firmasEstampadas, ActaJsonOptions),
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

        return View(new EspacioActaEmitidaDocumentoViewModel
        {
            Id = acta.Id,
            TituloActa = acta.TituloActa,
            PlantillaNombre = acta.PlantillaNombre,
            CuerpoHtml = acta.CuerpoHtml,
            NombreRecibe = acta.NombreRecibe,
            DocumentoRecibe = acta.DocumentoRecibe,
            EmitidaPorNombre = acta.EmitidaPorNombre,
            EmitidaPorCargo = acta.EmitidaPorCargo,
            EmitidaPorDocumento = acta.EmitidaPorDocumento,
            FirmaEmiteDataUrl = acta.FirmaEmiteDataUrl,
            FirmaRecibeDataUrl = acta.FirmaRecibeDataUrl,
            Firmas = EspacioActaFirmas.Leer(acta)
                .Select(firma => new EspacioActaFirmaImpresaViewModel
                {
                    Rotulo = firma.Rotulo,
                    Nombre = firma.Nombre,
                    Documento = firma.Documento,
                    Cargo = firma.Cargo,
                    DataUrl = firma.DataUrl
                })
                .ToList(),
            FechaFirma = ColombiaTime.Convert(acta.FirmadaAtUtc)
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Resolucion de plantillas
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Busca primero en las plantillas de fabrica y luego en las del disenador.</summary>
    private async Task<EspacioActaPlantilla?> ObtenerPlantillaAsync(
        string? codigo,
        CancellationToken cancellationToken,
        bool incluirInactivas = false)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        if (EspacioActaPlantillas.Obtener(codigo) is { } deFabrica)
        {
            return deFabrica;
        }

        var entidad = await _context.EspacioActaPlantillas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Codigo == codigo && !x.Eliminada && (incluirInactivas || x.Activa),
                cancellationToken);

        return entidad is null ? null : EspacioActaDisenador.ADominio(entidad);
    }

    private async Task<IReadOnlyList<EspacioActaPlantilla>> ListarPlantillasAsync(
        bool soloActivas,
        CancellationToken cancellationToken)
    {
        var personalizadas = await _context.EspacioActaPlantillas
            .AsNoTracking()
            .Where(x => !x.Eliminada && (!soloActivas || x.Activa))
            .OrderBy(x => x.Nombre)
            .ToListAsync(cancellationToken);

        return
        [
            .. EspacioActaPlantillas.DeFabrica,
            .. personalizadas.Select(EspacioActaDisenador.ADominio)
        ];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Firmas
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record DatosDeFirma(string Nombre, string? Documento, string? Cargo);

    /// <summary>Resuelve a nombre de quien va cada firma segun lo que declaro la plantilla.</summary>
    private static DatosDeFirma DescribirFirmante(
        EspacioActaFirma definicion,
        EspacioActaPlantilla plantilla,
        IReadOnlyDictionary<string, string?> valores,
        EspacioActaRenderer.DatosFirmante emisor)
    {
        if (definicion.Origen == EspacioActaFirmaOrigen.Emisor)
        {
            return new DatosDeFirma(
                string.IsNullOrWhiteSpace(definicion.NombreFijo) ? emisor.Nombre : definicion.NombreFijo!,
                emisor.Documento,
                string.IsNullOrWhiteSpace(definicion.CargoFijo) ? emisor.Cargo : definicion.CargoFijo);
        }

        var nombre = ValorDe(valores, definicion.CampoNombre);
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = definicion.NombreFijo ?? ValorDe(valores, plantilla.CampoNombre) ?? definicion.Rotulo;
        }

        return new DatosDeFirma(
            nombre,
            NormalizarOpcional(ValorDe(valores, definicion.CampoDocumento)),
            NormalizarOpcional(definicion.CargoFijo));
    }

    /// <summary>Trazo que va a las columnas heredadas de firma del acta.</summary>
    private static string ElegirTrazo(
        IReadOnlyList<EspacioActaFirmaEmitida> estampadas,
        EspacioActaPlantilla plantilla,
        EspacioActaFirmaOrigen origen)
    {
        var claves = plantilla.FirmasEfectivas
            .Where(x => x.Origen == origen)
            .Select(x => x.Clave)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var coincidencia = estampadas.FirstOrDefault(x => claves.Contains(x.Clave));

        return coincidencia?.DataUrl
            ?? (origen == EspacioActaFirmaOrigen.Emisor
                ? estampadas[0].DataUrl
                : estampadas[^1].DataUrl);
    }

    /// <summary>
    /// Arma la pantalla de firma. Se usa tanto al previsualizar como al rechazar una
    /// emision, para no mandar al usuario al formulario vacio cuando algo falla.
    /// </summary>
    private async Task<IActionResult> ConstruirVistaFirmaAsync(
        EspacioActaPlantilla plantilla,
        Dictionary<string, string?> valores,
        string? mensajeError,
        CancellationToken cancellationToken)
    {
        var firmante = await ConstruirFirmanteAsync(cancellationToken);
        var firmaGuardada = await GetFirmaGuardadaAsync(cancellationToken);
        var fecha = ColombiaTime.Convert(DateTime.UtcNow);

        var firmas = plantilla.FirmasEfectivas
            .Select(definicion =>
            {
                var datos = DescribirFirmante(definicion, plantilla, valores, firmante);
                var esEmisor = definicion.Origen == EspacioActaFirmaOrigen.Emisor;
                var trazoGuardado = esEmisor ? firmaGuardada?.FirmaDataUrl : null;

                return new EspacioActaFirmaCapturaViewModel
                {
                    Clave = definicion.Clave,
                    Rotulo = definicion.Rotulo,
                    EsEmisor = esEmisor,
                    Requerida = definicion.Requerida,
                    Nombre = datos.Nombre,
                    Documento = datos.Documento,
                    Cargo = datos.Cargo,
                    DataUrl = trazoGuardado,
                    DebeTrazar = string.IsNullOrWhiteSpace(trazoGuardado),
                    OfrecerGuardar = esEmisor && string.IsNullOrWhiteSpace(trazoGuardado)
                };
            })
            .ToList();

        return View("ActaFirmar", new EspacioActaFirmaViewModel
        {
            PlantillaCodigo = plantilla.Codigo,
            PlantillaNombre = plantilla.Nombre,
            TituloActa = plantilla.TituloActa,
            CuerpoHtml = EspacioActaRenderer.Render(plantilla, valores, firmante, fecha),
            Valores = valores,
            NombreRecibe = ValorDe(valores, plantilla.CampoNombre) ?? string.Empty,
            DocumentoRecibe = ValorDe(valores, plantilla.CampoDocumento),
            CorreoRecibe = ValorDe(valores, plantilla.CampoCorreo),
            EmitidaPorNombre = firmante.Nombre,
            EmitidaPorCargo = firmante.Cargo,
            EmitidaPorDocumento = firmante.Documento,
            FirmaEmiteDataUrl = firmaGuardada?.FirmaDataUrl,
            TieneFirmaGuardada = firmaGuardada is not null,
            Firmas = firmas,
            Fecha = fecha,
            MensajeError = mensajeError
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Captura y validacion de valores
    // ─────────────────────────────────────────────────────────────────────────

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

            // Una casilla sin marcar no viaja en el formulario: se guarda como "No".
            if (campo.Tipo == EspacioActaTipoCampo.Casilla)
            {
                valores[campo.Clave] = EspacioActaRenderer.EsAfirmativo(limpio) ? "Si" : "No";
                continue;
            }

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

            if (campo.Tipo == EspacioActaTipoCampo.Casilla)
            {
                if (campo.Requerido && !EspacioActaRenderer.EsAfirmativo(valor))
                {
                    errores.Add($"Debes marcar '{campo.Etiqueta}'.");
                }

                continue;
            }

            if (campo.Requerido && string.IsNullOrWhiteSpace(valor))
            {
                errores.Add($"El campo '{campo.Etiqueta}' es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(valor))
            {
                continue;
            }

            switch (campo.Tipo)
            {
                case EspacioActaTipoCampo.Seleccion
                    when campo.Opciones.Count > 0
                         && !campo.Opciones.Any(o => string.Equals(o.Valor, valor, StringComparison.OrdinalIgnoreCase)):
                    errores.Add($"Selecciona un valor valido para '{campo.Etiqueta}'.");
                    break;

                case EspacioActaTipoCampo.Correo
                    when !valor.Contains('@', StringComparison.Ordinal)
                         || valor.Contains(' ', StringComparison.Ordinal):
                    errores.Add($"El campo '{campo.Etiqueta}' debe ser un correo valido.");
                    break;

                case EspacioActaTipoCampo.Numero
                    when !long.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out _):
                    errores.Add($"El campo '{campo.Etiqueta}' debe ser un numero entero.");
                    break;

                case EspacioActaTipoCampo.Decimal or EspacioActaTipoCampo.Moneda
                    when !decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out _):
                    errores.Add($"El campo '{campo.Etiqueta}' debe ser una cifra.");
                    break;

                case EspacioActaTipoCampo.Fecha
                    when !DateTime.TryParseExact(
                        valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                    errores.Add($"El campo '{campo.Etiqueta}' debe ser una fecha valida.");
                    break;

                case EspacioActaTipoCampo.Hora
                    when !DateTime.TryParseExact(
                        valor, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                    errores.Add($"El campo '{campo.Etiqueta}' debe ser una hora valida.");
                    break;
            }
        }

        return errores;
    }

    /// <summary>Lectura tolerante: la clave puede no estar declarada por la plantilla.</summary>
    private static string? ValorDe(IReadOnlyDictionary<string, string?> valores, string? clave) =>
        string.IsNullOrWhiteSpace(clave) ? null : valores.GetValueOrDefault(clave);

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
