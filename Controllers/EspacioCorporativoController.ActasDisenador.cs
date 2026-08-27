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
/// Disenador de plantillas de acta (solo administrador).
///
/// Un administrador redacta el pliego por bloques, declara los campos que cambian
/// en cada acta y decide cuantas firmas lleva el documento. Lo que guarda queda
/// disponible de inmediato en la pantalla de Actas, junto a las plantillas de fabrica.
///
/// Nada de lo que se escribe aqui llega crudo al acta: el texto se codifica al
/// renderizar y la definicion completa pasa por EspacioActaDisenador.Normalizar.
/// </summary>
public partial class EspacioCorporativoController
{
    // ─────────────────────────────────────────────────────────────────────────
    // Administracion de plantillas
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillas(CancellationToken cancellationToken)
    {
        var entidades = await _context.EspacioActaPlantillas
            .AsNoTracking()
            .Where(x => !x.Eliminada)
            .OrderByDescending(x => x.ActualizadaAtUtc ?? x.CreadaAtUtc)
            .ToListAsync(cancellationToken);

        var codigos = entidades.Select(x => x.Codigo).ToList();

        var emitidasPorCodigo = await _context.EspacioActasDocumentales
            .AsNoTracking()
            .Where(x => codigos.Contains(x.PlantillaCodigo))
            .GroupBy(x => x.PlantillaCodigo)
            .Select(g => new { Codigo = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Codigo, x => x.Total, cancellationToken);

        var model = new EspacioActaPlantillasIndexViewModel
        {
            DeFabrica = EspacioActaPlantillas.DeFabrica,
            TotalActivas = entidades.Count(x => x.Activa),
            TotalInactivas = entidades.Count(x => !x.Activa),
            Plantillas = entidades
                .Select(entidad =>
                {
                    var plantilla = EspacioActaDisenador.ADominio(entidad);

                    return new EspacioActaPlantillaResumenViewModel
                    {
                        Id = entidad.Id,
                        Codigo = entidad.Codigo,
                        Nombre = entidad.Nombre,
                        Descripcion = entidad.Descripcion,
                        Icono = entidad.Icono,
                        TituloActa = entidad.TituloActa,
                        TotalCampos = plantilla.Campos.Count,
                        TotalFirmas = plantilla.FirmasEfectivas.Count,
                        Activa = entidad.Activa,
                        Version = entidad.Version,
                        CreadaPorNombre = entidad.CreadaPorNombre,
                        ActualizadaAt = ColombiaTime.Convert(entidad.ActualizadaAtUtc ?? entidad.CreadaAtUtc),
                        ActasEmitidas = emitidasPorCodigo.GetValueOrDefault(entidad.Codigo)
                    };
                })
                .ToList()
        };

        return View(model);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Disenador
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public IActionResult ActaPlantillaNueva(string? modelo)
    {
        var elegido = EspacioActaModelos.Obtener(modelo);

        var vista = ConstruirDisenador(
            null,
            "Nueva acta",
            elegido?.Definicion ?? new EspacioActaDefinicionDto(),
            0);

        // Sin modelo elegido, el editor abre preguntando por donde empezar en vez de
        // soltar una hoja en blanco.
        vista.ElegirModelo = elegido is null;

        return View("ActaPlantillaDisenador", vista);
    }

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillaEditar(long id, CancellationToken cancellationToken)
    {
        var entidad = await _context.EspacioActaPlantillas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, cancellationToken);

        if (entidad is null)
        {
            TempData[ErrorMessageKey] = "La plantilla no existe o fue eliminada.";
            return RedirectToAction(nameof(ActaPlantillas));
        }

        var emitidas = await _context.EspacioActasDocumentales
            .CountAsync(x => x.PlantillaCodigo == entidad.Codigo, cancellationToken);

        var definicion = EspacioActaDisenador.ADto(EspacioActaDisenador.ADominio(entidad));

        var vista = ConstruirDisenador(entidad.Id, entidad.Nombre, definicion, emitidas);
        vista.Publicada = entidad.Activa;

        return View("ActaPlantillaDisenador", vista);
    }

    /// <summary>
    /// Guarda la plantilla. Devuelve JSON porque el disenador vive en el navegador.
    ///
    /// Con publicar=false se guarda como borrador aunque este a medias: quien la arma
    /// puede parar y volver despues. Solo al publicarla se exige que este completa.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillaGuardar(
        [FromBody] EspacioActaDefinicionDto definicion,
        bool publicar,
        CancellationToken cancellationToken)
    {
        EspacioActaPlantillaPersonalizada? entidad = null;

        if (definicion?.Id is > 0)
        {
            entidad = await _context.EspacioActaPlantillas
                .FirstOrDefaultAsync(x => x.Id == definicion.Id && !x.Eliminada, cancellationToken);

            if (entidad is null)
            {
                return BadRequest(new { ok = false, errores = new[] { "La plantilla ya no existe." } });
            }
        }

        var resultado = EspacioActaDisenador.Normalizar(
            definicion,
            entidad?.Codigo,
            publicar ? EspacioActaDisenador.ModoDefinicion.Publicacion : EspacioActaDisenador.ModoDefinicion.Borrador);

        if (!resultado.EsValida)
        {
            return BadRequest(new { ok = false, errores = resultado.Errores });
        }

        var plantilla = resultado.Plantilla!;
        var ahora = DateTime.UtcNow;
        var esNueva = entidad is null;

        if (entidad is null)
        {
            entidad = new EspacioActaPlantillaPersonalizada
            {
                Codigo = await GenerarCodigoLibreAsync(plantilla.Codigo, cancellationToken),
                CreadaPorUserId = GetCurrentUserId(),
                CreadaPorNombre = Resumir(GetCurrentUserFullName(), 160),
                CreadaAtUtc = ahora,
                Version = 1,
                // Nace como borrador; se publica cuando quien la arma lo decide.
                Activa = false
            };

            await _context.EspacioActaPlantillas.AddAsync(entidad, cancellationToken);
        }
        else
        {
            entidad.Version++;
        }

        EspacioActaDisenador.Volcar(plantilla, entidad);
        entidad.ActualizadaPorNombre = Resumir(GetCurrentUserFullName(), 160);
        entidad.ActualizadaAtUtc = ahora;

        if (publicar)
        {
            entidad.Activa = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            esNueva ? "ESPACIO_ACTA_PLANTILLA_CREADA" : "ESPACIO_ACTA_PLANTILLA_EDITADA",
            $"Acta '{entidad.Nombre}' ({entidad.Codigo}) v{entidad.Version}, {(entidad.Activa ? "publicada" : "borrador")}",
            cancellationToken);

        return Json(new
        {
            ok = true,
            id = entidad.Id,
            codigo = entidad.Codigo,
            version = entidad.Version,
            publicada = entidad.Activa,
            mensaje = publicar
                ? "Listo. El acta ya aparece en la lista para empezar a usarla."
                : "Guardado. Puedes cerrar y seguir después; nadie la ve hasta que la publiques."
        });
    }

    /// <summary>Renderiza la definicion con valores de muestra, sin guardar nada.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillaPrevisualizar(
        [FromBody] EspacioActaDefinicionDto definicion,
        CancellationToken cancellationToken)
    {
        var resultado = EspacioActaDisenador.Normalizar(definicion, "PREVIA");

        if (!resultado.EsValida)
        {
            return BadRequest(new { ok = false, errores = resultado.Errores });
        }

        var plantilla = resultado.Plantilla!;
        var firmante = await ConstruirFirmanteAsync(cancellationToken);
        var fecha = ColombiaTime.Convert(DateTime.UtcNow);
        var muestra = EspacioActaRenderer.ValoresDeMuestra(plantilla.Campos);

        return Json(new
        {
            ok = true,
            tituloActa = plantilla.TituloActa,
            cuerpoHtml = EspacioActaRenderer.Render(plantilla, muestra, firmante, fecha, resaltarVariables: true),
            fecha = fecha.ToString("dd/MM/yyyy"),
            firmas = plantilla.FirmasEfectivas.Select(definicionFirma =>
            {
                var datos = DescribirFirmante(definicionFirma, plantilla, muestra, firmante);
                return new
                {
                    rotulo = definicionFirma.Rotulo,
                    nombre = datos.Nombre,
                    documento = datos.Documento,
                    cargo = datos.Cargo,
                    esEmisor = definicionFirma.Origen == EspacioActaFirmaOrigen.Emisor
                };
            })
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ciclo de vida
    // ─────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillaEstado(
        long id,
        bool activa,
        CancellationToken cancellationToken)
    {
        var entidad = await _context.EspacioActaPlantillas
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, cancellationToken);

        if (entidad is null)
        {
            TempData[ErrorMessageKey] = "La plantilla no existe o fue eliminada.";
            return RedirectToAction(nameof(ActaPlantillas));
        }

        // Publicar un borrador incompleto dejaria emitir actas con huecos: se revisa
        // igual que si se publicara desde el editor.
        if (activa)
        {
            var revision = EspacioActaDisenador.Normalizar(
                EspacioActaDisenador.ADto(EspacioActaDisenador.ADominio(entidad)),
                entidad.Codigo);

            if (!revision.EsValida)
            {
                TempData[ErrorMessageKey] =
                    $"'{entidad.Nombre}' todavia esta incompleta: {revision.Errores[0]}";
                return RedirectToAction(nameof(ActaPlantillaEditar), new { id = entidad.Id });
            }
        }

        entidad.Activa = activa;
        entidad.ActualizadaPorNombre = Resumir(GetCurrentUserFullName(), 160);
        entidad.ActualizadaAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "ESPACIO_ACTA_PLANTILLA_ESTADO",
            $"Acta '{entidad.Nombre}' {(activa ? "publicada" : "retirada")}",
            cancellationToken);

        TempData[SuccessMessageKey] = activa
            ? $"'{entidad.Nombre}' ya esta disponible para emitir actas."
            : $"'{entidad.Nombre}' se retiro; no aparece al emitir actas nuevas.";

        return RedirectToAction(nameof(ActaPlantillas));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillaDuplicar(long id, CancellationToken cancellationToken)
    {
        var origen = await _context.EspacioActaPlantillas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, cancellationToken);

        if (origen is null)
        {
            TempData[ErrorMessageKey] = "La plantilla no existe o fue eliminada.";
            return RedirectToAction(nameof(ActaPlantillas));
        }

        var nombre = Resumir($"{origen.Nombre} (copia)", 200);

        var copia = new EspacioActaPlantillaPersonalizada
        {
            Codigo = await GenerarCodigoLibreAsync(EspacioActaDisenador.GenerarCodigo(nombre), cancellationToken),
            Nombre = nombre,
            Descripcion = origen.Descripcion,
            Icono = origen.Icono,
            TituloActa = origen.TituloActa,
            CamposJson = origen.CamposJson,
            BloquesJson = origen.BloquesJson,
            FirmasJson = origen.FirmasJson,
            NumerarTitulos = origen.NumerarTitulos,
            CampoNombre = origen.CampoNombre,
            CampoDocumento = origen.CampoDocumento,
            CampoCorreo = origen.CampoCorreo,
            CampoUsuario = origen.CampoUsuario,
            Activa = false,
            CreadaPorUserId = GetCurrentUserId(),
            CreadaPorNombre = Resumir(GetCurrentUserFullName(), 160),
            CreadaAtUtc = DateTime.UtcNow,
            Version = 1
        };

        await _context.EspacioActaPlantillas.AddAsync(copia, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "ESPACIO_ACTA_PLANTILLA_DUPLICADA",
            $"Copia de '{origen.Nombre}' creada como '{copia.Nombre}'",
            cancellationToken);

        TempData[SuccessMessageKey] = "Copia creada. Ajústala y actívala cuando esté lista.";
        return RedirectToAction(nameof(ActaPlantillaEditar), new { id = copia.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> ActaPlantillaEliminar(long id, CancellationToken cancellationToken)
    {
        var entidad = await _context.EspacioActaPlantillas
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminada, cancellationToken);

        if (entidad is null)
        {
            TempData[ErrorMessageKey] = "La plantilla no existe o ya fue eliminada.";
            return RedirectToAction(nameof(ActaPlantillas));
        }

        // Borrado logico: las actas emitidas siguen apuntando a este codigo.
        entidad.Eliminada = true;
        entidad.Activa = false;
        entidad.ActualizadaPorNombre = Resumir(GetCurrentUserFullName(), 160);
        entidad.ActualizadaAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "ESPACIO_ACTA_PLANTILLA_ELIMINADA",
            $"Plantilla '{entidad.Nombre}' ({entidad.Codigo}) eliminada",
            cancellationToken);

        TempData[SuccessMessageKey] = $"'{entidad.Nombre}' se eliminó. Las actas ya emitidas se conservan.";
        return RedirectToAction(nameof(ActaPlantillas));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static EspacioActaDisenadorViewModel ConstruirDisenador(
        long? id,
        string titulo,
        EspacioActaDefinicionDto definicion,
        int actasEmitidas) =>
        new()
        {
            Id = id,
            Titulo = titulo,
            DefinicionJson = JsonSerializer.Serialize(definicion, EspacioActaDisenador.JsonOptions),
            TiposDeCampoJson = JsonSerializer.Serialize(
                EspacioActaPlantillas.TiposDeCampo,
                EspacioActaDisenador.JsonOptions),
            ModelosJson = JsonSerializer.Serialize(
                EspacioActaModelos.Todos.Select(x => new
                {
                    clave = x.Clave,
                    nombre = x.Nombre,
                    descripcion = x.Descripcion,
                    icono = x.Icono,
                    definicion = x.Definicion
                }),
                EspacioActaDisenador.JsonOptions),
            MarcadoresDelSistema = EspacioActaPlantillas.MarcadoresDelSistema,
            Iconos = EspacioActaPlantillas.Iconos,
            ActasEmitidas = actasEmitidas
        };

    /// <summary>El codigo lleva marca de tiempo, pero se verifica igual antes de insertar.</summary>
    private async Task<string> GenerarCodigoLibreAsync(string propuesto, CancellationToken cancellationToken)
    {
        var candidato = propuesto;
        var intento = 2;

        while (await _context.EspacioActaPlantillas.AnyAsync(x => x.Codigo == candidato, cancellationToken))
        {
            var sufijo = $"_{intento}";
            candidato = propuesto.Length + sufijo.Length <= 60
                ? propuesto + sufijo
                : propuesto[..(60 - sufijo.Length)] + sufijo;
            intento++;
        }

        return candidato;
    }

}
