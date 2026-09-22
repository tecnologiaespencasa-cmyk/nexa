using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Panel de agudos.
///
/// Universo: la tabla <c>censo</c> sin las copias internas de despacho a farmacia
/// (<see cref="CensoVisibility.EditableRecord"/>). Un ingreso del periodo es una fila con
/// <c>FechaIngreso</c> dentro del rango que no esté cancelada ni rechazada: así se validó contra el
/// exportable del censo el 2026-08-11 (1005 filas − 35 canceladas/rechazadas = 970).
/// </summary>
public partial class ReportesController
{
    private static readonly IReadOnlyDictionary<string, string> AliasNoParametrizados =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ALTAVISTA"] = "Altavista",
            ["PALMITAS"] = "Palmitas",
            ["SANANTONIODEPRADO"] = "San Antonio de Prado",
            ["SANCRISTOBAL"] = "San Cristóbal",
            ["SANTAELENA"] = "Santa Elena"
        };

    private async Task<ReportesAgudosViewModel> ConstruirAgudosAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel f,
        Periodo p,
        DateTime hoy,
        CancellationToken ct)
    {
        var visibles = contexto.Censos.AsNoTracking().Where(CensoVisibility.EditableRecord(contexto));
        if (f.Municipio is not null)
        {
            visibles = visibles.Where(x => x.MunicipioResidencia == f.Municipio);
        }

        var delPeriodo = FiltrarGestionAgudos(
            visibles.Where(x => x.FechaIngreso >= p.Desde && x.FechaIngreso < p.HastaExclusivo), f);

        // Los cancelados y rechazados nunca se prestaron y no cuentan como ingreso. Se informa cuántos
        // quedaron fuera para que la cifra cuadre con el tabulado, que sí los lista. Con un estado
        // filtrado no aplica: el filtro ya deja solo ese estado.
        var canceladosRechazados = f.EstadoCenso is null
            ? await delPeriodo.CountAsync(x => x.Estado != null
                && (EF.Functions.ILike(x.Estado, "%cancelado%") || EF.Functions.ILike(x.Estado, "%rechazado%")), ct)
            : 0;

        var filas = await FiltrarEstadoAgudos(ExcludeCancelledAndRejected(delPeriodo), f)
            .Select(x => new FilaAgudos
            {
                Id = x.Id,
                FechaIngreso = x.FechaIngreso,
                NombrePaciente = x.NombrePaciente,
                TipoIdentificacion = x.TipoIdentificacion,
                NumeroIdentificacion = x.NumeroIdentificacion,
                NombreRecepcionaCaso = x.NombreRecepcionaCaso,
                AuxiliarAsignado = x.AuxiliarAsignado,
                MunicipioResidencia = x.MunicipioResidencia,
                Barrio = x.Barrio,
                Direccion = x.Direccion,
                Estado = x.Estado,
                AutorizacionEvento = x.AutorizacionEvento,
                GestionCompletaPendiente = x.GestionCompletaPendiente,
                Asegurador = x.Asegurador,
                ClasificacionRiesgo = x.ClasificacionRiesgo
            })
            .ToListAsync(ct);

        var anterior = p.Anterior;
        var ingresosAnterior = await FiltrarEstadoAgudos(ExcludeCancelledAndRejected(FiltrarGestionAgudos(
                visibles.Where(x => x.FechaIngreso >= anterior.Desde && x.FechaIngreso < anterior.HastaExclusivo), f)), f)
            .CountAsync(ct);

        var activos = await SoloAgudosActivos(visibles)
            .Select(x => new { x.AuxiliarAsignado })
            .ToListAsync(ct);

        var total = filas.Count;
        var pendientes = filas.Count(EsGestionPendiente);
        var completas = filas.Count(x => EsIgual(x.GestionCompletaPendiente, "Completa"));
        var sinAutorizacion = filas.Count(EsSinAutorizacion);
        var sinAuxiliar = activos.Count(x => string.IsNullOrWhiteSpace(x.AuxiliarAsignado));

        var conAlerta = filas.Where(x => EsSinAutorizacion(x) || EsGestionPendiente(x)).ToList();

        var modelo = new ReportesAgudosViewModel
        {
            Nombre = "Agudos",
            ActivosHoy = activos.Count,
            IngresosPeriodo = total,
            IngresosPeriodoAnterior = ingresosAnterior,
            PromedioIngresosDia = Promedio(total, p.Dias),
            Ingresos = ConstruirSerie(filas.Select(x => x.FechaIngreso), p),
            FiltrosPropios = f.EstadoGestion is not null || f.EstadoCenso is not null,
            CanceladosRechazadosPeriodo = canceladosRechazados,
            Calendario = ConstruirCalendario(filas.Select(x => x.FechaIngreso), p),
            GestionPendiente = pendientes,
            GestionCompleta = completas,
            SinAutorizacion = sinAutorizacion,
            Criticos = filas.Count(x => EsGestionPendiente(x) && EsSinAutorizacion(x)),
            Gestion = ConstruirSegmentos(
                ("Pendiente", pendientes, "alerta"),
                ("Completa", completas, "ok"),
                ("Sin estado", total - pendientes - completas, "neutro")),
            Autorizacion = ConstruirSegmentos(
                ("Sin autorización", sinAutorizacion, "alerta"),
                ("Con autorización", total - sinAutorizacion, "ok")),
            EstadoActual = ConstruirCategorias(
                Contar(filas.Select(x => x.Estado), EtiquetaEstadoAgudos, "Sin estado"),
                total,
                esSecundaria: x => x == "Sin estado"),
            Aseguradora = ConstruirSegmentos(
                ("EPS Sura", filas.Count(x => AseguradoraAgudos(x.Asegurador) == "EPS Sura"), "n1"),
                ("Pan-American Life", filas.Count(x => AseguradoraAgudos(x.Asegurador) == "Pan-American Life"), "n2"),
                ("Particular", filas.Count(x => AseguradoraAgudos(x.Asegurador) == "Particular"), "n3"),
                ("Otra o sin dato", filas.Count(x => AseguradoraAgudos(x.Asegurador) == SinDato), "neutro")),
            Riesgo = ConstruirSegmentos(
                ("Bajo", filas.Count(x => EsIgual(x.ClasificacionRiesgo, "Bajo")), "n3"),
                ("Medio", filas.Count(x => EsIgual(x.ClasificacionRiesgo, "Medio")), "n2"),
                ("Alto", filas.Count(x => EsIgual(x.ClasificacionRiesgo, "Alto")), "n1"),
                ("Sin dato", filas.Count(x => !EsIgual(x.ClasificacionRiesgo, "Bajo")
                    && !EsIgual(x.ClasificacionRiesgo, "Medio")
                    && !EsIgual(x.ClasificacionRiesgo, "Alto")), "neutro")),
            Municipios = ConstruirMunicipiosAgudos(filas),
            // Hasta cuatro personas y el resto en "Otros": se dibuja como dona.
            SinAutorizacionPorRecepcion = ConstruirCategorias(
                Contar(filas.Where(EsSinAutorizacion).Select(x => x.NombreRecepcionaCaso), NombrePropio),
                sinAutorizacion,
                maximoFilas: 4),
            ActivosPorAuxiliar = ConstruirCategorias(
                Contar(activos.Select(x => x.AuxiliarAsignado), vacio: "Sin auxiliar asignado"),
                activos.Count,
                maximoFilas: int.MaxValue,
                esSecundaria: x => x == "Sin auxiliar asignado"),
            ActivosSinAuxiliar = sinAuxiliar,
            AuxiliaresConPacientes = activos
                .Where(x => !string.IsNullOrWhiteSpace(x.AuxiliarAsignado))
                .Select(x => x.AuxiliarAsignado!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            // Lo mismo sobre los ingresos del periodo: sigue las fechas. El auxiliar es el que el
            // registro tiene asignado hoy (el censo no guarda el historial de asignaciones).
            IngresosPorAuxiliar = ConstruirCategorias(
                Contar(filas.Select(x => x.AuxiliarAsignado), vacio: "Sin auxiliar asignado"),
                total,
                maximoFilas: int.MaxValue,
                esSecundaria: x => x == "Sin auxiliar asignado"),
            IngresosSinAuxiliar = filas.Count(x => string.IsNullOrWhiteSpace(x.AuxiliarAsignado)),
            AuxiliaresConIngresos = filas
                .Where(x => !string.IsNullOrWhiteSpace(x.AuxiliarAsignado))
                .Select(x => x.AuxiliarAsignado!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            TotalConAlerta = conAlerta.Count,
            RegistrosPrioritarios = conAlerta
                .OrderByDescending(x => (EsSinAutorizacion(x) ? 2 : 0) + (EsGestionPendiente(x) ? 1 : 0))
                .ThenBy(x => x.FechaIngreso)
                .ThenBy(x => x.Id)
                .Take(15)
                .Select(x => new ReportesRegistroRevisarViewModel
                {
                    Id = x.Id,
                    Paciente = string.IsNullOrWhiteSpace(x.NombrePaciente) ? "Sin nombre" : x.NombrePaciente.Trim(),
                    Documento = Documento(x.TipoIdentificacion, x.NumeroIdentificacion),
                    NumeroIdentificacion = x.NumeroIdentificacion,
                    Municipio = string.IsNullOrWhiteSpace(x.MunicipioResidencia) ? SinDato : EtiquetaMunicipio(x.MunicipioResidencia),
                    // Solo el auxiliar asignado del censo (directorio de Neon). Antes, si faltaba, se
                    // mostraba quien hizo el kardex, que sale de la lista administrativa de auxiliares:
                    // la columna mezclaba dos listas de personas distintas.
                    AuxiliarAsignado = string.IsNullOrWhiteSpace(x.AuxiliarAsignado) ? null : x.AuxiliarAsignado.Trim(),
                    Estado = string.IsNullOrWhiteSpace(x.Estado) ? "Sin estado" : EtiquetaEstadoAgudos(x.Estado),
                    SinAutorizacion = EsSinAutorizacion(x),
                    GestionPendiente = EsGestionPendiente(x),
                    Alerta = (EsSinAutorizacion(x), EsGestionPendiente(x)) switch
                    {
                        (true, true) => "Sin autorización y gestión pendiente",
                        (true, false) => "Sin autorización",
                        _ => "Gestión pendiente"
                    },
                    FechaIngreso = x.FechaIngreso
                })
                .ToList()
        };

        return modelo;
    }

    /// <summary>
    /// Atención de agudos en curso: la misma lista de estados que usa el informe de pacientes activos
    /// (CensoController.ExportarPacientesActivos) y la verificación de paciente en programa.
    /// </summary>
    private static IQueryable<CensoRecord> SoloAgudosActivos(IQueryable<CensoRecord> query) =>
        query.Where(x => x.Estado != null
            && (EF.Functions.ILike(x.Estado, "Aceptado activo")
                || EF.Functions.ILike(x.Estado, "Aceptado cronico")
                || EF.Functions.ILike(x.Estado, "Aceptado crónico")
                || EF.Functions.ILike(x.Estado, "Activo Estancia prolongada")
                || EF.Functions.ILike(x.Estado, "Aceptado estancia prolongada")));

    private static IQueryable<CensoRecord> ExcludeCancelledAndRejected(IQueryable<CensoRecord> query) =>
        query.Where(x => x.Estado == null
            || (!EF.Functions.ILike(x.Estado, "%cancelado%")
                && !EF.Functions.ILike(x.Estado, "%rechazado%")));

    private static IQueryable<CensoRecord> FiltrarGestionAgudos(IQueryable<CensoRecord> query, ReportesFilterViewModel f) =>
        f.EstadoGestion is null ? query : query.Where(x => x.GestionCompletaPendiente == f.EstadoGestion);

    private static IQueryable<CensoRecord> FiltrarEstadoAgudos(IQueryable<CensoRecord> query, ReportesFilterViewModel f) =>
        f.EstadoCenso is null ? query : query.Where(x => x.Estado == f.EstadoCenso);

    private static bool EsSinAutorizacion(FilaAgudos fila) => string.IsNullOrWhiteSpace(fila.AutorizacionEvento);

    private static bool EsGestionPendiente(FilaAgudos fila) => EsIgual(fila.GestionCompletaPendiente, "Pendiente");

    /// <summary>
    /// La misma aseguradora está escrita de varias formas ("EPS SURA" en el catálogo actual, "Sura EPS"
    /// en registros antiguos): se agrupan por la palabra, igual que el informe de activos.
    /// </summary>
    private static string AseguradoraAgudos(string? asegurador)
    {
        if (string.IsNullOrWhiteSpace(asegurador))
        {
            return SinDato;
        }

        if (asegurador.Contains("sura", StringComparison.OrdinalIgnoreCase))
        {
            return "EPS Sura";
        }

        if (asegurador.Contains("pan", StringComparison.OrdinalIgnoreCase)
            && asegurador.Contains("american", StringComparison.OrdinalIgnoreCase))
        {
            return "Pan-American Life";
        }

        return EsIgual(asegurador, "PARTICULAR") ? "Particular" : SinDato;
    }

    private static List<ReportesMunicipioFilaViewModel> ConstruirMunicipiosAgudos(IReadOnlyList<FilaAgudos> filas)
    {
        var grupos = filas
            .GroupBy(x => EtiquetaMunicipioAgudos(x), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Municipio = g.Key,
                NoParametrizado = IsNoParametrizado(g.First().MunicipioResidencia),
                Ingresos = g.Count(),
                Pendientes = g.Count(EsGestionPendiente),
                SinAutorizacion = g.Count(EsSinAutorizacion)
            })
            .OrderByDescending(x => x.Ingresos)
            .ThenBy(x => x.Municipio, StringComparer.Create(Cultura, ignoreCase: true))
            .ToList();

        var mayor = grupos.Count == 0 ? 0 : grupos.Max(x => x.Ingresos);
        return grupos
            .Select(x => new ReportesMunicipioFilaViewModel
            {
                Municipio = x.Municipio,
                NoParametrizado = x.NoParametrizado,
                Ingresos = x.Ingresos,
                GestionPendiente = x.Pendientes,
                SinAutorizacion = x.SinAutorizacion,
                Barra = mayor == 0 ? 0 : Math.Round(x.Ingresos * 100d / mayor, 2)
            })
            .ToList();
    }

    /// <summary>
    /// Los corregimientos de Medellín se registran como "NO PARAMETRIZADO": se reconocen por el barrio o
    /// la dirección para no mezclarlos en una sola fila.
    /// </summary>
    private static string EtiquetaMunicipioAgudos(FilaAgudos fila)
    {
        if (!IsNoParametrizado(fila.MunicipioResidencia))
        {
            return EtiquetaMunicipio(fila.MunicipioResidencia);
        }

        var lugar = AliasNoParametrizado(fila.Barrio) ?? AliasNoParametrizado(fila.Direccion);
        return lugar is null ? "No parametrizado" : $"{lugar} (no parametrizado)";
    }

    private static string? AliasNoParametrizado(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var clave = NormalizeMunicipalityKey(valor);
        foreach (var alias in AliasNoParametrizados)
        {
            if (clave.Contains(alias.Key, StringComparison.Ordinal))
            {
                return alias.Value;
            }
        }

        return null;
    }

    private static bool IsNoParametrizado(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(NormalizeMunicipalityKey(value), NormalizeMunicipalityKey(MunicipioNoParametrizado), StringComparison.Ordinal);

    /// <summary>Los estados de agudos se guardan sin tildes; la pantalla los muestra con ellas.</summary>
    private static string EtiquetaEstadoAgudos(string estado) => estado.Trim() switch
    {
        var e when e.Equals("Aceptado cronico", StringComparison.OrdinalIgnoreCase) => "Aceptado crónico",
        var e when e.Equals("Cronico alta", StringComparison.OrdinalIgnoreCase) => "Crónico alta",
        var e when e.Equals("Cronico activo agudizado", StringComparison.OrdinalIgnoreCase) => "Crónico activo agudizado",
        var e when e.Equals("Cancelado EPS Respuesta extemporanea", StringComparison.OrdinalIgnoreCase) => "Cancelado EPS Respuesta extemporánea",
        var e => e
    };

    /// <summary>"VALERIA FUEREZ" → "Valeria Fuerez": los nombres en mayúscula sostenida cuestan leer en lista.</summary>
    private static string NombrePropio(string nombre)
    {
        var limpio = nombre.Trim();
        return limpio.Any(char.IsLower) ? limpio : Cultura.TextInfo.ToTitleCase(limpio.ToLower(Cultura));
    }

    private sealed class FilaAgudos
    {
        public long Id { get; init; }
        public DateTime FechaIngreso { get; init; }
        public string NombrePaciente { get; init; } = string.Empty;
        public string TipoIdentificacion { get; init; } = string.Empty;
        public string NumeroIdentificacion { get; init; } = string.Empty;
        public string NombreRecepcionaCaso { get; init; } = string.Empty;
        public string? AuxiliarAsignado { get; init; }
        public string MunicipioResidencia { get; init; } = string.Empty;
        public string Barrio { get; init; } = string.Empty;
        public string Direccion { get; init; } = string.Empty;
        public string? Estado { get; init; }
        public string? AutorizacionEvento { get; init; }
        public string GestionCompletaPendiente { get; init; } = string.Empty;
        public string Asegurador { get; init; } = string.Empty;
        public string ClasificacionRiesgo { get; init; } = string.Empty;
    }
}
