using IntranetPrueba.Data.Entities;
using IntranetPrueba.Helpers;
using IntranetPrueba.Models.EspacioCorporativo;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

public partial class EspacioCorporativoController
{
    // ─────────────────────────────────────────────────────────────────────────
    // Administracion de documentacion
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> Documentacion(
        string? busqueda,
        string? categoria,
        string? tipo,
        CancellationToken cancellationToken)
    {
        var model = new EspacioDocumentacionAdminViewModel
        {
            Busqueda = busqueda?.Trim(),
            CategoriaFiltro = categoria?.Trim(),
            TipoFiltro = tipo?.Trim(),
            Categorias = EspacioCorporativoCatalogos.CategoriasDocumento,
            TiposDocumento = EspacioCorporativoCatalogos.TiposDocumento,
            TiposContenido = EspacioCorporativoCatalogos.TiposContenido,
            ExtensionesPermitidas = EspacioCorporativoCatalogos.ExtensionesPermitidas.Keys.ToList()
        };

        var query = _context.EspacioDocumentos
            .AsNoTracking()
            .Where(x => !x.Eliminado);

        if (!string.IsNullOrWhiteSpace(model.Busqueda))
        {
            var termino = $"%{model.Busqueda}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Titulo, termino)
                || (x.Descripcion != null && EF.Functions.ILike(x.Descripcion, termino))
                || (x.Etiquetas != null && EF.Functions.ILike(x.Etiquetas, termino))
                || (x.CodigoDocumento != null && EF.Functions.ILike(x.CodigoDocumento, termino)));
        }

        if (EspacioCorporativoCatalogos.EsCategoriaDocumentoValida(model.CategoriaFiltro))
        {
            query = query.Where(x => x.Categoria == model.CategoriaFiltro);
        }

        if (EspacioCorporativoCatalogos.EsTipoDocumentoValido(model.TipoFiltro))
        {
            query = query.Where(x => x.TipoDocumento == model.TipoFiltro);
        }

        var documentos = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Titulo,
                x.Descripcion,
                x.Categoria,
                x.TipoDocumento,
                x.TipoContenido,
                x.Version,
                x.CodigoDocumento,
                x.Etiquetas,
                x.ArchivoNombre,
                x.ArchivoTamanoBytes,
                x.EnlaceUrl,
                x.ContenidoTexto,
                x.Publicado,
                x.Destacado,
                x.FechaVigencia,
                x.Descargas,
                x.CreadoPorNombre,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                Favoritos = x.Favoritos.Count
            })
            .ToListAsync(cancellationToken);

        model.Documentos = documentos
            .Select(documento => new EspacioDocumentoAdminItemViewModel
            {
                Id = documento.Id,
                Titulo = documento.Titulo,
                Descripcion = documento.Descripcion,
                Categoria = documento.Categoria,
                TipoDocumento = documento.TipoDocumento,
                TipoContenido = documento.TipoContenido,
                Version = documento.Version,
                CodigoDocumento = documento.CodigoDocumento,
                Etiquetas = documento.Etiquetas,
                ArchivoNombre = documento.ArchivoNombre,
                TamanoLegible = FormatearTamano(documento.ArchivoTamanoBytes),
                EnlaceUrl = documento.EnlaceUrl,
                ContenidoTexto = documento.ContenidoTexto,
                Publicado = documento.Publicado,
                Destacado = documento.Destacado,
                FechaVigencia = documento.FechaVigencia,
                Descargas = documento.Descargas,
                Favoritos = documento.Favoritos,
                CreadoPorNombre = documento.CreadoPorNombre,
                FechaCreacion = ColombiaTime.Convert(documento.CreatedAtUtc),
                FechaActualizacion = ColombiaTime.Convert(documento.UpdatedAtUtc)
            })
            .ToList();

        model.TotalPublicados = await _context.EspacioDocumentos
            .CountAsync(x => !x.Eliminado && x.Publicado, cancellationToken);
        model.TotalBorradores = await _context.EspacioDocumentos
            .CountAsync(x => !x.Eliminado && !x.Publicado, cancellationToken);
        model.TotalDescargas = await _context.EspacioDocumentos
            .Where(x => !x.Eliminado)
            .SumAsync(x => x.Descargas, cancellationToken);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> GuardarDocumento(
        EspacioDocumentoFormViewModel model,
        CancellationToken cancellationToken)
    {
        NormalizarDocumento(model);

        var esNuevo = !model.Id.HasValue;
        EspacioDocumento? documento = null;

        if (!esNuevo)
        {
            documento = await _context.EspacioDocumentos
                .FirstOrDefaultAsync(x => x.Id == model.Id!.Value && !x.Eliminado, cancellationToken);

            if (documento is null)
            {
                TempData[ErrorMessageKey] = "El documento no existe o fue eliminado.";
                return RedirectToAction(nameof(Documentacion));
            }
        }

        var archivoValido = await ValidarDocumentoAsync(model, documento, cancellationToken);

        if (!ModelState.IsValid)
        {
            TempData[ErrorMessageKey] = JoinModelStateErrors();
            return RedirectToAction(nameof(Documentacion));
        }

        if (esNuevo)
        {
            documento = new EspacioDocumento
            {
                CreatedAtUtc = DateTime.UtcNow,
                CreadoPorUserId = GetCurrentUserId(),
                CreadoPorNombre = GetCurrentUserFullName()
            };
            await _context.EspacioDocumentos.AddAsync(documento, cancellationToken);
        }
        else
        {
            documento!.UpdatedAtUtc = DateTime.UtcNow;
            documento.ActualizadoPorNombre = GetCurrentUserFullName();
        }

        documento!.Titulo = model.Titulo;
        documento.Descripcion = model.Descripcion;
        documento.Categoria = model.Categoria;
        documento.TipoDocumento = model.TipoDocumento;
        documento.TipoContenido = model.TipoContenido;
        documento.Version = model.Version;
        documento.CodigoDocumento = model.CodigoDocumento;
        documento.Etiquetas = model.Etiquetas;
        documento.Publicado = model.Publicado;
        documento.Destacado = model.Destacado;
        documento.FechaVigencia = model.FechaVigencia;

        switch (model.TipoContenido)
        {
            case EspacioCorporativoCatalogos.TipoContenidoArchivo:
                if (archivoValido is not null)
                {
                    documento.ArchivoNombre = archivoValido.Nombre;
                    documento.ArchivoContentType = archivoValido.ContentType;
                    documento.ArchivoTamanoBytes = archivoValido.Contenido.LongLength;
                    documento.ArchivoContenido = archivoValido.Contenido;
                }

                documento.EnlaceUrl = null;
                documento.ContenidoTexto = null;
                break;

            case EspacioCorporativoCatalogos.TipoContenidoEnlace:
                documento.EnlaceUrl = model.EnlaceUrl;
                documento.ArchivoNombre = null;
                documento.ArchivoContentType = null;
                documento.ArchivoTamanoBytes = null;
                documento.ArchivoContenido = null;
                documento.ContenidoTexto = null;
                break;

            default:
                documento.ContenidoTexto = model.ContenidoTexto;
                documento.EnlaceUrl = null;
                documento.ArchivoNombre = null;
                documento.ArchivoContentType = null;
                documento.ArchivoTamanoBytes = null;
                documento.ArchivoContenido = null;
                break;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            esNuevo ? "ESPACIO_DOCUMENTO_CREADO" : "ESPACIO_DOCUMENTO_ACTUALIZADO",
            $"Documento #{documento.Id} '{documento.Titulo}' ({documento.Categoria} / {documento.TipoDocumento})",
            cancellationToken);

        TempData[SuccessMessageKey] = esNuevo
            ? "Documento publicado correctamente."
            : "Documento actualizado correctamente.";

        return RedirectToAction(nameof(Documentacion));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> EliminarDocumento(long id, CancellationToken cancellationToken)
    {
        var documento = await _context.EspacioDocumentos
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);

        if (documento is null)
        {
            TempData[ErrorMessageKey] = "El documento no existe o ya fue eliminado.";
            return RedirectToAction(nameof(Documentacion));
        }

        documento.Eliminado = true;
        documento.Publicado = false;
        documento.UpdatedAtUtc = DateTime.UtcNow;
        documento.ActualizadoPorNombre = GetCurrentUserFullName();

        await _context.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "ESPACIO_DOCUMENTO_ELIMINADO",
            $"Documento #{documento.Id} '{documento.Titulo}'",
            cancellationToken);

        TempData[SuccessMessageKey] = "Documento eliminado.";
        return RedirectToAction(nameof(Documentacion));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.EspacioCorporativoAdmin)]
    public async Task<IActionResult> AlternarPublicacion(long id, CancellationToken cancellationToken)
    {
        var documento = await _context.EspacioDocumentos
            .FirstOrDefaultAsync(x => x.Id == id && !x.Eliminado, cancellationToken);

        if (documento is null)
        {
            TempData[ErrorMessageKey] = "El documento no existe.";
            return RedirectToAction(nameof(Documentacion));
        }

        documento.Publicado = !documento.Publicado;
        documento.UpdatedAtUtc = DateTime.UtcNow;
        documento.ActualizadoPorNombre = GetCurrentUserFullName();

        await _context.SaveChangesAsync(cancellationToken);

        TempData[SuccessMessageKey] = documento.Publicado
            ? "Documento publicado."
            : "Documento pasado a borrador.";

        return RedirectToAction(nameof(Documentacion));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers de documentacion
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record ArchivoCargado(string Nombre, string ContentType, byte[] Contenido);

    private static void NormalizarDocumento(EspacioDocumentoFormViewModel model)
    {
        model.Titulo = model.Titulo?.Trim() ?? string.Empty;
        model.Descripcion = NormalizarOpcional(model.Descripcion);
        model.Categoria = model.Categoria?.Trim() ?? string.Empty;
        model.TipoDocumento = model.TipoDocumento?.Trim() ?? string.Empty;
        model.TipoContenido = model.TipoContenido?.Trim() ?? string.Empty;
        model.Version = NormalizarOpcional(model.Version);
        model.CodigoDocumento = NormalizarOpcional(model.CodigoDocumento);
        model.Etiquetas = NormalizarOpcional(model.Etiquetas);
        model.EnlaceUrl = NormalizarOpcional(model.EnlaceUrl);
        model.ContenidoTexto = NormalizarOpcional(model.ContenidoTexto);
    }

    private async Task<ArchivoCargado?> ValidarDocumentoAsync(
        EspacioDocumentoFormViewModel model,
        EspacioDocumento? documentoExistente,
        CancellationToken cancellationToken)
    {
        if (!EspacioCorporativoCatalogos.EsCategoriaDocumentoValida(model.Categoria))
        {
            ModelState.AddModelError(nameof(model.Categoria), "Selecciona una categoría válida.");
        }

        if (!EspacioCorporativoCatalogos.EsTipoDocumentoValido(model.TipoDocumento))
        {
            ModelState.AddModelError(nameof(model.TipoDocumento), "Selecciona un tipo de documento válido.");
        }

        if (!EspacioCorporativoCatalogos.EsTipoContenidoValido(model.TipoContenido))
        {
            ModelState.AddModelError(nameof(model.TipoContenido), "Selecciona cómo se cargará el documento.");
            return null;
        }

        switch (model.TipoContenido)
        {
            case EspacioCorporativoCatalogos.TipoContenidoEnlace:
                if (string.IsNullOrWhiteSpace(model.EnlaceUrl))
                {
                    ModelState.AddModelError(nameof(model.EnlaceUrl), "Indica el enlace del documento.");
                }
                else if (!Uri.TryCreate(model.EnlaceUrl, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    ModelState.AddModelError(nameof(model.EnlaceUrl), "El enlace debe iniciar con http:// o https://");
                }

                return null;

            case EspacioCorporativoCatalogos.TipoContenidoTexto:
                if (string.IsNullOrWhiteSpace(model.ContenidoTexto))
                {
                    ModelState.AddModelError(nameof(model.ContenidoTexto), "Escribe el contenido del documento.");
                }

                return null;

            default:
                return await LeerArchivoAsync(model, documentoExistente, cancellationToken);
        }
    }

    private async Task<ArchivoCargado?> LeerArchivoAsync(
        EspacioDocumentoFormViewModel model,
        EspacioDocumento? documentoExistente,
        CancellationToken cancellationToken)
    {
        var tieneArchivoPrevio = documentoExistente?.ArchivoContenido is { Length: > 0 };

        if (model.Archivo is null || model.Archivo.Length == 0)
        {
            if (!tieneArchivoPrevio)
            {
                ModelState.AddModelError(nameof(model.Archivo), "Adjunta el archivo del documento.");
            }

            return null;
        }

        if (model.Archivo.Length > EspacioCorporativoCatalogos.TamanoMaximoArchivoBytes)
        {
            ModelState.AddModelError(
                nameof(model.Archivo),
                $"El archivo supera el máximo permitido ({FormatearTamano(EspacioCorporativoCatalogos.TamanoMaximoArchivoBytes)}).");
            return null;
        }

        var extension = Path.GetExtension(model.Archivo.FileName);
        if (string.IsNullOrWhiteSpace(extension)
            || !EspacioCorporativoCatalogos.ExtensionesPermitidas.TryGetValue(extension, out var contentType))
        {
            ModelState.AddModelError(
                nameof(model.Archivo),
                $"Formato no permitido. Formatos válidos: {string.Join(", ", EspacioCorporativoCatalogos.ExtensionesPermitidas.Keys)}.");
            return null;
        }

        using var stream = new MemoryStream();
        await model.Archivo.CopyToAsync(stream, cancellationToken);

        var nombre = SanitizarNombreArchivo(Path.GetFileName(model.Archivo.FileName));
        return new ArchivoCargado(nombre, contentType, stream.ToArray());
    }
}
