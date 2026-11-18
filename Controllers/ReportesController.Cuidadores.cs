using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Services;
using Nexa.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

/// <summary>
/// Panel de cuidadores (tabla <c>censo_cuidadores</c>).
///
/// Activo hoy: sin fecha de egreso real y con estado "Activo", igual que el informe de pacientes
/// activos. «Suspendido» y «Pendiente» no cierran la atención, pero el paciente no está recibiendo el
/// servicio, así que no cuenta como activo.
///
/// La atención tiene dos fechas de entrada. El ingreso al programa es el inicio de la atención; el
/// reporte de la EPS es la solicitud, y la diferencia entre las dos es la oportunidad de inicio.
///
/// El censo no guarda municipio ni aseguradora: los filtros se resuelven con el maestro del paciente,
/// como en crónicos y terapia.
/// </summary>
public partial class ReportesController
{
    private static readonly (string Dia, string Corto)[] DiasDeLaSemana =
    [
        ("Lunes", "Lu"), ("Martes", "Ma"), ("Miércoles", "Mi"), ("Jueves", "Ju"),
        ("Viernes", "Vi"), ("Sábado", "Sá"), ("Domingo", "Do")
    ];

    /// <summary>Un certificado que vence dentro de estos días se pide renovar desde ya.</summary>
    private const int DiasAvisoCertificado = 30;

    private async Task<ReportesCuidadoresViewModel> ConstruirCuidadoresAsync(
        ApplicationDbContext contexto,
        ReportesFilterViewModel f,
        Periodo p,
        DateTime hoy,
        CancellationToken ct)
    {
        var query = contexto.CensoCuidadores.AsNoTracking();
        if (f.Municipio is not null)
        {
            var delMunicipio = contexto.CensoPacientes.Where(x => x.MunicipioResidencia == f.Municipio).Select(x => x.Id);
            query = query.Where(x => x.CensoPacienteId != null && delMunicipio.Contains(x.CensoPacienteId.Value));
        }

        if (f.Asegurador is not null)
        {
            var pacientes = PacientesDelAsegurador(contexto, f.Asegurador);
            query = query.Where(x => x.CensoPacienteId == null
                ? f.Asegurador == FiltroAseguradorSura
                : pacientes.Contains(x.CensoPacienteId.Value));
        }

        var filas = await query
            .Select(x => new
            {
                x.Id,
                x.TipoIdentificacion,
                x.NumeroIdentificacion,
                x.NombrePaciente,
                x.FechaReporteEps,
                x.FechaInicioAtencion,
                x.Estado,
                x.FechaEgreso,
                x.MotivoEgreso,
                x.TiempoCuidadorHorasDia,
                x.DiasAtencion,
                x.Horario,
                x.EscalaBarthel,
                x.FechaVencimientoCertificado,
                x.ParentescoResponsable,
                x.AuxiliarAsignado,
                x.ServicioAutorizado,
                Municipio = contexto.CensoPacientes
                    .Where(m => m.Id == x.CensoPacienteId)
                    .Select(m => m.MunicipioResidencia)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        // "También en crónicos" con la misma regla de persona que las tarjetas de arriba: el documento.
        var documentosEnCronicos = (await contexto.CensoCronicos.AsNoTracking()
                .Select(x => new { x.NumeroIdentificacion, x.FechaEgreso, x.EstadoPaciente })
                .ToListAsync(ct))
            .Where(x => EsCronicoActivo(x.FechaEgreso, x.EstadoPaciente))
            .Select(x => NormalizarDocumento(x.NumeroIdentificacion))
            .ToHashSet(StringComparer.Ordinal);

        var activos = filas.Where(x => EsActivoSinEgreso(x.FechaEgreso, x.Estado)).ToList();
        var ingresos = filas
            .Where(x => x.FechaInicioAtencion.HasValue && p.Contiene(x.FechaInicioAtencion.Value))
            .ToList();
        var reportes = filas.Where(x => p.Contiene(x.FechaReporteEps)).ToList();
        var egresos = filas
            .Where(x => CensoVisibility.HayEgreso(x.FechaEgreso) && p.Contiene(x.FechaEgreso!.Value))
            .ToList();

        // Oportunidad de los ingresos del periodo. El formulario no deja un inicio anterior al reporte;
        // si alguna carga antigua lo trajera, cuenta como el mismo día y no resta al promedio.
        var oportunidades = ingresos
            .Select(x => Math.Max(0, (x.FechaInicioAtencion!.Value.Date - x.FechaReporteEps.Date).Days))
            .Order()
            .ToList();

        // La semana del cuidado: por cada día, los activos que lo tienen entre sus días de atención y la
        // suma de sus horas al día.
        var diasPorActivo = activos
            .Select(x => (Horas: x.TiempoCuidadorHorasDia ?? 0, Dias: DiasDeAtencion(x.DiasAtencion)))
            .ToList();
        var semana = DiasDeLaSemana
            .Select((d, i) => (
                d.Dia,
                d.Corto,
                FinDeSemana: i >= 5,
                Pacientes: diasPorActivo.Count(x => x.Dias.Contains(i)),
                Horas: diasPorActivo.Where(x => x.Dias.Contains(i)).Sum(x => x.Horas)))
            .ToList();
        var mayorHoras = semana.Max(x => x.Horas);
        var mayorPacientes = semana.Max(x => x.Pacientes);

        var certificados = activos
            .Select(x => (Fila: x, Dias: x.FechaVencimientoCertificado.HasValue
                ? (int?)(x.FechaVencimientoCertificado.Value.Date - hoy).Days
                : null))
            .ToList();
        var vencidos = certificados.Count(x => x.Dias < 0);
        var porVencer = certificados.Count(x => x.Dias is >= 0 and <= DiasAvisoCertificado);
        var sinVencimiento = certificados.Count(x => x.Dias is null);

        var sinAuxiliar = activos.Count(x => string.IsNullOrWhiteSpace(x.AuxiliarAsignado));

        // Servicio que autorizó la EPS (2026-11-18). Todas las atenciones anteriores se rellenaron con
        // «Cuidador» y el campo es obligatorio desde entonces, así que «sin servicio» solo saldría si
        // alguien carga una atención por fuera del formulario.
        const string SinServicio = "Sin servicio registrado";
        var servicios = activos
            .Select(x => (Fila: x, Servicio: ServiciosAsistencia.Normalizar(x.ServicioAutorizado) ?? SinServicio))
            .ToList();

        var modelo = new ReportesCuidadoresViewModel
        {
            Nombre = "Cuidadores",
            ActivosHoy = activos.Count,
            IngresosPeriodo = ingresos.Count,
            IngresosPeriodoAnterior = filas.Count(x => x.FechaInicioAtencion.HasValue && p.Anterior.Contiene(x.FechaInicioAtencion.Value)),
            PromedioIngresosDia = Promedio(ingresos.Count, p.Dias),
            Ingresos = ConstruirSerie(ingresos.Select(x => x.FechaInicioAtencion!.Value), p),
            ReportesEpsPeriodo = reportes.Count,
            SinIniciar = filas.Count(x => x.FechaInicioAtencion is null
                && !CensoPacienteService.EsCuidadorCerrado(x.Estado, x.FechaEgreso)),
            EgresosPeriodo = egresos.Count,
            ActivosEnCronicos = activos.Count(x => documentosEnCronicos.Contains(NormalizarDocumento(x.NumeroIdentificacion))),
            OportunidadPromedio = oportunidades.Count == 0 ? null : Math.Round(oportunidades.Average(), 1),
            // La mitad: el menor número de días tal que al menos la mitad inició en ese plazo o antes.
            OportunidadMitad = oportunidades.Count == 0 ? null : oportunidades[(oportunidades.Count + 1) / 2 - 1],
            Demanda = ReportesLineaViewModel.DeDosSeries(
                "cuidadores-demanda",
                ConstruirSerie(reportes.Select(x => x.FechaReporteEps), p),
                "Reporta la EPS",
                ConstruirSerie(ingresos.Select(x => x.FechaInicioAtencion!.Value), p),
                "Inicia la atención"),
            Oportunidad = ConstruirTramos(
                ["El mismo día", "1 a 3 días", "4 a 7 días", "8 a 15 días", "Más de 15 días"],
                oportunidades.Select(d => d switch
                {
                    0 => "El mismo día",
                    <= 3 => "1 a 3 días",
                    <= 7 => "4 a 7 días",
                    <= 15 => "8 a 15 días",
                    _ => "Más de 15 días"
                })),
            Semana = semana
                .Select(x => new ReportesSemanaDiaViewModel
                {
                    Dia = x.Dia,
                    Corto = x.Corto,
                    FinDeSemana = x.FinDeSemana,
                    Pacientes = x.Pacientes,
                    Horas = x.Horas,
                    // Sin horas registradas en ningún activo, la columna sigue a los pacientes.
                    Altura = mayorHoras > 0
                        ? Math.Round(x.Horas * 100d / mayorHoras, 2)
                        : mayorPacientes == 0 ? 0 : Math.Round(x.Pacientes * 100d / mayorPacientes, 2)
                })
                .ToList(),
            HorasAlDia = activos.Sum(x => x.TiempoCuidadorHorasDia ?? 0),
            HorasALaSemana = semana.Sum(x => x.Horas),
            ActivosSinHoras = activos.Count(x => x.TiempoCuidadorHorasDia is null),
            ActivosSinDias = diasPorActivo.Count(x => x.Dias.Count == 0),
            Intensidad = ConstruirTramos(
                ["Hasta 6 h", "7 a 8 h", "9 a 12 h", "Más de 12 h"],
                activos.Select(x => x.TiempoCuidadorHorasDia switch
                {
                    null => SinDato,
                    <= 6 => "Hasta 6 h",
                    <= 8 => "7 a 8 h",
                    <= 12 => "9 a 12 h",
                    _ => "Más de 12 h"
                })),
            Horario = ConstruirCategorias(
                Contar(activos.Select(x => x.Horario), FraseEnMinuscula),
                activos.Count),
            // Los mismos rangos de Barthel que la hoja clínica del paciente.
            Dependencia = ConstruirTramos(
                ["Total (0 a 20)", "Grave (21 a 60)", "Moderada (61 a 90)", "Leve (91 a 99)", "Independiente (100)"],
                activos.Select(x => x.EscalaBarthel switch
                {
                    null => "Sin escala registrada",
                    <= 20 => "Total (0 a 20)",
                    <= 60 => "Grave (21 a 60)",
                    <= 90 => "Moderada (61 a 90)",
                    < 100 => "Leve (91 a 99)",
                    _ => "Independiente (100)"
                }),
                sinDato: "Sin escala registrada"),
            Certificados = ConstruirSegmentos(
                ("Vencido", vencidos, "critico"),
                ($"Vence en {DiasAvisoCertificado} días o menos", porVencer, "alerta"),
                ("Vigente", certificados.Count(x => x.Dias > DiasAvisoCertificado), "ok"),
                ("Sin fecha de vencimiento", sinVencimiento, "neutro")),
            CertificadosVencidos = vencidos,
            CertificadosPorVencer = porVencer,
            CertificadosParaGestionar = certificados
                .Where(x => x.Dias <= DiasAvisoCertificado)
                .OrderBy(x => x.Dias)
                .ThenBy(x => x.Fila.NombrePaciente, StringComparer.Create(Cultura, ignoreCase: true))
                .Select(x => new ReportesCertificadoFilaViewModel
                {
                    Paciente = NombrePropio(x.Fila.NombrePaciente),
                    Documento = Documento(x.Fila.TipoIdentificacion, x.Fila.NumeroIdentificacion),
                    NumeroIdentificacion = x.Fila.NumeroIdentificacion,
                    Vence = x.Fila.FechaVencimientoCertificado!.Value.Date,
                    Dias = x.Dias!.Value,
                    Auxiliar = string.IsNullOrWhiteSpace(x.Fila.AuxiliarAsignado) ? null : x.Fila.AuxiliarAsignado.Trim()
                })
                .ToList(),
            ServicioAutorizado = ConstruirTramos(ServiciosAsistencia.Todos, servicios.Select(x => x.Servicio), sinDato: SinServicio),
            HorasCuidador = servicios.Where(x => x.Servicio == ServiciosAsistencia.Cuidador).Sum(x => x.Fila.TiempoCuidadorHorasDia ?? 0),
            HorasAuxiliar = servicios.Where(x => x.Servicio == ServiciosAsistencia.AuxiliarEnfermeria).Sum(x => x.Fila.TiempoCuidadorHorasDia ?? 0),
            Parentesco = ConstruirCategorias(
                Contar(activos.Select(x => ParentescoUnificado(x.ParentescoResponsable)), FraseEnMinuscula, "Sin responsable registrado"),
                activos.Count,
                maximoFilas: 7,
                esSecundaria: x => x == "Sin responsable registrado"),
            MunicipiosActivos = ConstruirCategorias(
                Contar(activos.Select(x => x.Municipio), EtiquetaMunicipio, "Sin municipio"),
                activos.Count,
                maximoFilas: 7,
                esSecundaria: x => x == "Sin municipio"),
            ActivosPorAuxiliar = ConstruirCategorias(
                Contar(activos.Select(x => x.AuxiliarAsignado), vacio: "Sin auxiliar asignado"),
                activos.Count,
                maximoFilas: 7,
                esSecundaria: x => x == "Sin auxiliar asignado"),
            ActivosSinAuxiliar = sinAuxiliar,
            MotivosEgreso = ConstruirCategorias(
                Contar(egresos.Select(x => x.MotivoEgreso), FraseEnMinuscula, "Sin motivo registrado"),
                egresos.Count,
                maximoFilas: 6,
                esSecundaria: x => x == "Sin motivo registrado")
        };

        return modelo;
    }

    /// <summary>
    /// Índices (lunes = 0) de los días de atención guardados como "Lunes, Martes, …". Se comparan sin
    /// tildes ni mayúsculas para no perder un día escrito en una carga antigua.
    /// </summary>
    private static HashSet<int> DiasDeAtencion(string? dias)
    {
        var resultado = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(dias))
        {
            return resultado;
        }

        foreach (var parte in dias.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var indice = Array.FindIndex(DiasDeLaSemana, d =>
                Cultura.CompareInfo.Compare(d.Dia, parte, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) == 0);
            if (indice >= 0)
            {
                resultado.Add(indice);
            }
        }

        return resultado;
    }

    /// <summary>"MAMA" y "MADRE" son la misma persona en el formulario; igual "PAPA" y "PADRE".</summary>
    private static string? ParentescoUnificado(string? parentesco) => parentesco?.Trim().ToUpperInvariant() switch
    {
        "MAMA" or "MAMÁ" => "MADRE",
        "PAPA" or "PAPÁ" => "PADRE",
        _ => parentesco
    };

    /// <summary>
    /// Tramos en un orden fijo, conservando los que quedan en cero para que la forma no engañe. Lo que
    /// no cae en ningún tramo (el dato vacío) va al final como fila secundaria, solo si hay.
    /// </summary>
    private static List<ReportesCategoriaViewModel> ConstruirTramos(
        IReadOnlyList<string> tramos,
        IEnumerable<string> valores,
        string sinDato = SinDato)
    {
        var lista = valores.ToList();
        var conteo = tramos.Select(t => (Etiqueta: t, Valor: lista.Count(v => v == t), Secundaria: false)).ToList();
        var vacios = lista.Count(v => v == sinDato);
        if (vacios > 0)
        {
            conteo.Add((sinDato, vacios, true));
        }

        var total = lista.Count;
        var mayor = conteo.Count == 0 ? 0 : conteo.Max(x => x.Valor);
        return conteo
            .Select(x => new ReportesCategoriaViewModel
            {
                Etiqueta = x.Etiqueta,
                Valor = x.Valor,
                Porcentaje = total == 0 ? 0 : Math.Round(x.Valor * 100d / total, 1),
                Barra = mayor == 0 ? 0 : Math.Round(x.Valor * 100d / mayor, 2),
                Secundaria = x.Secundaria
            })
            .ToList();
    }
}
