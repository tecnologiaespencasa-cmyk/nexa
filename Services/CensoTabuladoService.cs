using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;

namespace Nexa.Services;

/// <summary>
/// Arma el tabulado unificado del censo.
///
/// Vive en un servicio y no en el controlador del censo porque lo consume la pantalla de reportes,
/// que es donde se consulta el historial. La pantalla del paciente ya no lo carga: abrir un paciente
/// no tiene por que traer miles de filas de todos los programas.
///
/// Con "todos los programas" entrega el juego de columnas nucleo, comun a los cinco censos. Al
/// filtrar por uno solo agrega sus filas completas para desplegar todas sus columnas: la union
/// literal de los cinco pasaria de seiscientas y seria ilegible.
/// </summary>
public class CensoTabuladoService : ICensoTabuladoService
{
    private const int LimiteFilas = 100;
    private const int LimiteFilasConRango = 50;

    private readonly ApplicationDbContext _context;

    public CensoTabuladoService(ApplicationDbContext context)
    {
        _context = context;
    }

    private static string NormalizarDocumento(string? valor) =>
        (valor ?? string.Empty).Trim().ToUpperInvariant();

    public async Task ConstruirAsync(CensoUnificadoViewModel model, CancellationToken ct)
    {
        var doc = NormalizarDocumento(model.CedulaFiltro);
        var desde = model.FechaIngresoFiltroDesde?.Date;
        var hasta = model.FechaIngresoFiltroHasta?.Date;
        var filtro = CensoProgramas.EsValido(model.ProgramaFiltro) ? model.ProgramaFiltro : null;
        model.ProgramaFiltro = filtro;

        var filas = await ConstruirFilasResumenAsync(doc, desde, hasta, ct);

        // Los contadores de los chips se calculan sobre el total, antes de aplicar el filtro: si no,
        // al mirar un programa los demas apareceran siempre en cero y no se sabria donde mas buscar.
        model.ConteoPorPrograma = filas
            .GroupBy(x => x.Programa)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        if (filtro is not null)
        {
            filas.RemoveAll(x => !string.Equals(x.Programa, filtro, StringComparison.Ordinal));
        }

        model.TotalSinRecorte = filas.Count;
        model.LimiteFilas = model.TieneFiltroFechaIngreso ? LimiteFilasConRango : LimiteFilas;

        var ordenadas = filas
            .OrderByDescending(x => x.FechaIngreso ?? DateTime.MinValue)
            .ThenBy(x => CensoProgramas.Jerarquia(x.Programa))
            .ThenByDescending(x => x.RegistroId);

        // El tope es de la pantalla, no de los datos. Al filtrar por documento se traen todas:
        // un paciente no llega al tope y recortarlo escondería atenciones suyas.
        model.Filas = string.IsNullOrWhiteSpace(doc)
            ? ordenadas.Take(model.LimiteFilas).ToList()
            : ordenadas.ToList();

        model.IngresosHoyCount = await ContarIngresosHoyAsync(ct);

        if (filtro is not null)
        {
            await CargarDetallePorProgramaAsync(model, filtro, doc, desde, hasta, ct);
        }
    }

    /// <summary>
    /// Arma las filas núcleo de los cinco programas (sin el recorte de la pantalla), con Abierto y
    /// TieneAdjuntos ya resueltos. La usa <see cref="ConstruirAsync"/> para el tabulado en pantalla
    /// y el exportable "Todos los programas" del censo, que sí necesita el total sin recortar.
    /// </summary>
    public async Task<List<CensoUnificadoTablaRowViewModel>> ConstruirFilasResumenAsync(
        string? cedulaPaciente,
        DateTime? desde,
        DateTime? hasta,
        CancellationToken ct)
    {
        var doc = NormalizarDocumento(cedulaPaciente);
        var filas = new List<CensoUnificadoTablaRowViewModel>();

        // --- Agudos. Excluye las copias internas de despacho a farmacia con el mismo criterio que
        // usaba la tabla del censo de agudos, para no duplicar al paciente.
        {
            var q = _context.Censos.AsNoTracking().Where(CensoVisibility.EditableRecord(_context));
            if (!string.IsNullOrWhiteSpace(doc)) q = q.Where(x => x.NumeroIdentificacion == doc);
            if (desde.HasValue) q = q.Where(x => x.FechaIngreso >= desde.Value);
            if (hasta.HasValue) q = q.Where(x => x.FechaIngreso <= hasta.Value);

            filas.AddRange(await q
                .OrderByDescending(x => x.FechaIngreso).ThenByDescending(x => x.Id)
                .Select(x => new CensoUnificadoTablaRowViewModel
                {
                    Programa = CensoProgramas.Agudos,
                    RegistroId = x.Id,
                    PacienteId = x.CensoPacienteId,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    FechaIngreso = x.FechaIngreso,
                    Estado = x.Estado,
                    Asegurador = x.Asegurador,
                    ClasificacionZonaSura = x.ClasificacionZonaSura,
                    DiagnosticoDescriptivo = x.DiagnosticoDescriptivo,
                    EstadoFarmacia = x.FarmaciaEnviadoAtUtc == null ? null : x.FarmaciaEstado,
                    TieneProrroga = x.EsProrroga
                })
                .ToListAsync(ct));
        }

        {
            var q = _context.CensoCronicos.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(doc)) q = q.Where(x => x.NumeroIdentificacion == doc);
            if (desde.HasValue) q = q.Where(x => x.FechaIngreso >= desde.Value);
            if (hasta.HasValue) q = q.Where(x => x.FechaIngreso <= hasta.Value);

            filas.AddRange(await q
                .OrderByDescending(x => x.FechaIngreso).ThenByDescending(x => x.Id)
                .Select(x => new CensoUnificadoTablaRowViewModel
                {
                    Programa = CensoProgramas.Cronicos,
                    RegistroId = x.Id,
                    PacienteId = x.CensoPacienteId,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    FechaIngreso = x.FechaIngreso,
                    Estado = x.EstadoPaciente,
                    ClasificacionZonaSura = x.ClasificacionZonaSura,
                    DiagnosticoDescriptivo = x.GrupoPatologiaCronica
                })
                .ToListAsync(ct));
        }

        {
            var q = _context.CensoClinicaHeridas.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(doc)) q = q.Where(x => x.NumeroIdentificacion == doc);
            if (desde.HasValue) q = q.Where(x => x.FechaIngresoPrograma >= desde.Value);
            if (hasta.HasValue) q = q.Where(x => x.FechaIngresoPrograma <= hasta.Value);

            filas.AddRange(await q
                .OrderByDescending(x => x.FechaIngresoPrograma).ThenByDescending(x => x.Id)
                .Select(x => new CensoUnificadoTablaRowViewModel
                {
                    Programa = CensoProgramas.ClinicaHeridas,
                    RegistroId = x.Id,
                    PacienteId = x.CensoPacienteId,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    FechaIngreso = x.FechaIngresoPrograma,
                    Estado = x.Estado,
                    Asegurador = x.Asegurador,
                    ClasificacionZonaSura = x.ClasificacionZonaSura,
                    DiagnosticoDescriptivo = x.DiagnosticoDescriptivo
                })
                .ToListAsync(ct));
        }

        {
            var q = _context.CensoNpt.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(doc)) q = q.Where(x => x.NumeroIdentificacion == doc);
            if (desde.HasValue) q = q.Where(x => x.FechaIngresoPrograma >= desde.Value);
            if (hasta.HasValue) q = q.Where(x => x.FechaIngresoPrograma <= hasta.Value);

            filas.AddRange(await q
                .OrderByDescending(x => x.FechaIngresoPrograma).ThenByDescending(x => x.Id)
                .Select(x => new CensoUnificadoTablaRowViewModel
                {
                    Programa = CensoProgramas.Npt,
                    RegistroId = x.Id,
                    PacienteId = x.CensoPacienteId,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    FechaIngreso = x.FechaIngresoPrograma,
                    Estado = x.Estado,
                    Asegurador = x.Asegurador,
                    ClasificacionZonaSura = x.ClasificacionZonaSura,
                    DiagnosticoDescriptivo = x.DiagnosticoDescriptivo
                })
                .ToListAsync(ct));
        }

        {
            var q = _context.CensoTerapiasAmbulatorias.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(doc)) q = q.Where(x => x.NumeroIdentificacion == doc);
            if (desde.HasValue) q = q.Where(x => x.FechaIngreso >= desde.Value);
            if (hasta.HasValue) q = q.Where(x => x.FechaIngreso <= hasta.Value);

            filas.AddRange(await q
                .OrderByDescending(x => x.FechaIngreso).ThenByDescending(x => x.Id)
                .Select(x => new CensoUnificadoTablaRowViewModel
                {
                    Programa = CensoProgramas.TerapiaAmbulatoria,
                    RegistroId = x.Id,
                    PacienteId = x.CensoPacienteId,
                    NombrePaciente = x.NombrePaciente,
                    TipoIdentificacion = x.TipoIdentificacion,
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    FechaIngreso = x.FechaIngreso,
                    Estado = x.EstadoPaciente,
                    ClasificacionZonaSura = x.ClasificacionZonaSura,
                    DiagnosticoDescriptivo = x.DiagnosticoDescriptivo
                })
                .ToListAsync(ct));
        }

        // El estado abierto/cerrado sale de los episodios, que es donde vive esa noción unificada.
        var claves = filas.Select(x => x.RegistroId).Distinct().ToList();
        var episodios = await _context.CensoPacienteProgramas
            .AsNoTracking()
            .Where(x => x.RegistroId != null && claves.Contains(x.RegistroId!.Value))
            .Select(x => new { x.Programa, x.RegistroId, x.CerradoAtUtc })
            .ToListAsync(ct);
        var abiertoPorClave = episodios
            .GroupBy(x => (x.Programa, x.RegistroId))
            .ToDictionary(g => g.Key, g => g.Any(x => x.CerradoAtUtc == null));
        foreach (var fila in filas)
        {
            fila.Abierto = abiertoPorClave.TryGetValue((fila.Programa, fila.RegistroId), out var abierto) && abierto;
        }

        // Adjuntos: solo agudos tiene tabla de adjuntos por registro.
        var idsAgudos = filas.Where(x => x.Programa == CensoProgramas.Agudos).Select(x => x.RegistroId).ToList();
        if (idsAgudos.Count > 0)
        {
            var conAdjuntos = await _context.CensoAdjuntos
                .AsNoTracking()
                .Where(x => idsAgudos.Contains(x.CensoRecordId))
                .Select(x => x.CensoRecordId)
                .Distinct()
                .ToListAsync(ct);
            var set = conAdjuntos.ToHashSet();
            foreach (var fila in filas.Where(x => x.Programa == CensoProgramas.Agudos))
            {
                fila.TieneAdjuntos = set.Contains(fila.RegistroId);
            }
        }

        return filas;
    }

    /// <summary>
    /// Carga las filas completas del programa filtrado para desplegar su juego total de columnas.
    /// </summary>
    private async Task CargarDetallePorProgramaAsync(
        CensoUnificadoViewModel model,
        string programa,
        string? doc,
        DateTime? desde,
        DateTime? hasta,
        CancellationToken ct)
    {
        var ids = model.Filas
            .Where(x => x.Programa == programa)
            .Select(x => x.RegistroId)
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        switch (programa)
        {
            case CensoProgramas.Agudos:
                model.TipoDetalle = typeof(CensoRecord);
                model.RegistrosDetalle = (await _context.Censos.AsNoTracking()
                    .Where(x => ids.Contains(x.Id)).ToListAsync(ct)).Cast<object>().ToList();
                break;
            case CensoProgramas.Cronicos:
                model.TipoDetalle = typeof(CensoCronicoRecord);
                model.RegistrosDetalle = (await _context.CensoCronicos.AsNoTracking()
                    .Where(x => ids.Contains(x.Id)).ToListAsync(ct)).Cast<object>().ToList();
                break;
            case CensoProgramas.ClinicaHeridas:
                model.TipoDetalle = typeof(CensoClinicaHeridasRecord);
                model.RegistrosDetalle = (await _context.CensoClinicaHeridas.AsNoTracking()
                    .Where(x => ids.Contains(x.Id)).ToListAsync(ct)).Cast<object>().ToList();
                break;
            case CensoProgramas.Npt:
                model.TipoDetalle = typeof(CensoNptRecord);
                model.RegistrosDetalle = (await _context.CensoNpt.AsNoTracking()
                    .Where(x => ids.Contains(x.Id)).ToListAsync(ct)).Cast<object>().ToList();
                break;
            case CensoProgramas.TerapiaAmbulatoria:
                model.TipoDetalle = typeof(CensoTerapiaAmbulatoriaRecord);
                model.RegistrosDetalle = (await _context.CensoTerapiasAmbulatorias.AsNoTracking()
                    .Where(x => ids.Contains(x.Id)).ToListAsync(ct)).Cast<object>().ToList();
                break;
        }
    }

    private async Task<int> ContarIngresosHoyAsync(CancellationToken ct)
    {
        var hoy = ColombiaTime.Convert(DateTime.UtcNow).Date;

        // Solo agudos, no los cinco programas: sumarlos todos bajo una sola cifra "Ingresos hoy"
        // generaba confusión al compararla contra el gráfico "Censo Agudos" de Reportes, que solo
        // mide agudos (reporte real 2026-09-15, "los números no cuadran"). Mismo criterio que ese
        // gráfico (ReportesController.ExcludeCancelledAndRejected): un ingreso cancelado o
        // rechazado el mismo día no es un ingreso real.
        return await _context.Censos.AsNoTracking()
            .Where(CensoVisibility.EditableRecord(_context))
            .Where(x => x.Estado == null
                || (!EF.Functions.ILike(x.Estado, "%cancelado%")
                    && !EF.Functions.ILike(x.Estado, "%rechazado%")))
            .CountAsync(x => x.FechaIngreso == hoy, ct);
    }
}
