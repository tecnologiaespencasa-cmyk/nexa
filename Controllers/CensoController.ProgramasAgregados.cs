using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Services;

namespace Nexa.Controllers;

/// <summary>
/// "Programas agregados" de un paciente: los que el carril de su ficha muestra abiertos hoy.
///
/// Se decide leyendo los registros de los cinco censos con las mismas reglas de cierre que usa la
/// ficha (<see cref="CensoPacienteService.EsAgudoCerrado"/> y compañía), no con el
/// <c>CerradoAtUtc</c> del episodio: ese campo es una copia que solo se refresca al abrir la ficha y
/// puede estar atrasada. Un programa asignado cuyo formulario nadie ha diligenciado (episodio abierto
/// sin registro) también cuenta, marcado "(sin diligenciar)".
///
/// El paciente se reconoce por documento y, si la fila está vinculada al maestro, también por su
/// <c>CensoPacienteId</c>: igual que la reconciliación de la ficha.
/// </summary>
public partial class CensoController
{
    internal static string EtiquetaProgramaAgregado(string programa) => programa switch
    {
        CensoProgramas.Agudos => "Agudos",
        CensoProgramas.Cronicos => "Crónicos",
        CensoProgramas.ClinicaHeridas => "Clínica de heridas",
        CensoProgramas.Npt => "NPT",
        CensoProgramas.TerapiaAmbulatoria => "Terapia ambulatoria",
        _ => programa
    };

    internal const string SinProgramasAgregados = "Ninguno abierto";

    /// <summary>
    /// Devuelve una función que, para un documento y un paciente del maestro, entrega el texto de la
    /// columna: los programas abiertos en el orden del carril (NPT, crónicos, heridas, agudos, terapia).
    /// </summary>
    internal static async Task<Func<string?, long?, string>> ProgramasAgregadosAsync(
        ApplicationDbContext contexto,
        IReadOnlyCollection<(string? Documento, long? PacienteId)> pacientes,
        CancellationToken ct)
    {
        static string Doc(string? valor) => (valor ?? string.Empty).Trim().ToUpperInvariant();

        var docs = pacientes.Select(x => Doc(x.Documento)).Where(x => x.Length > 0).Distinct().ToList();
        var ids = pacientes.Where(x => x.PacienteId.HasValue).Select(x => x.PacienteId!.Value).Distinct().ToList();

        // (documento, paciente del maestro, programa, con registro)
        var abiertos = new List<(string Doc, long? PacienteId, string Programa, bool ConRegistro)>();

        var agudos = await contexto.Censos.AsNoTracking()
            .Where(CensoVisibility.EditableRecord(contexto))
            .Where(x => docs.Contains(x.NumeroIdentificacion.Trim().ToUpper())
                || (x.CensoPacienteId != null && ids.Contains(x.CensoPacienteId.Value)))
            .Select(x => new { x.NumeroIdentificacion, x.CensoPacienteId, x.Estado })
            .ToListAsync(ct);
        abiertos.AddRange(agudos
            .Where(x => !CensoPacienteService.EsAgudoCerrado(x.Estado))
            .Select(x => (Doc(x.NumeroIdentificacion), x.CensoPacienteId, CensoProgramas.Agudos, true)));

        var cronicos = await contexto.CensoCronicos.AsNoTracking()
            .Where(x => docs.Contains(x.NumeroIdentificacion.Trim().ToUpper())
                || (x.CensoPacienteId != null && ids.Contains(x.CensoPacienteId.Value)))
            .Select(x => new { x.NumeroIdentificacion, x.CensoPacienteId, x.EstadoPaciente, x.FechaEgreso })
            .ToListAsync(ct);
        abiertos.AddRange(cronicos
            .Where(x => !CensoPacienteService.EsCronicoCerrado(x.EstadoPaciente, x.FechaEgreso))
            .Select(x => (Doc(x.NumeroIdentificacion), x.CensoPacienteId, CensoProgramas.Cronicos, true)));

        var heridas = await contexto.CensoClinicaHeridas.AsNoTracking()
            .Where(x => docs.Contains(x.NumeroIdentificacion.Trim().ToUpper())
                || (x.CensoPacienteId != null && ids.Contains(x.CensoPacienteId.Value)))
            .Select(x => new { x.NumeroIdentificacion, x.CensoPacienteId, x.Estado, x.FechaEgreso })
            .ToListAsync(ct);
        abiertos.AddRange(heridas
            .Where(x => !CensoPacienteService.EsProgramaCerrado(x.Estado, x.FechaEgreso))
            .Select(x => (Doc(x.NumeroIdentificacion), x.CensoPacienteId, CensoProgramas.ClinicaHeridas, true)));

        var npt = await contexto.CensoNpt.AsNoTracking()
            .Where(x => docs.Contains(x.NumeroIdentificacion.Trim().ToUpper())
                || (x.CensoPacienteId != null && ids.Contains(x.CensoPacienteId.Value)))
            .Select(x => new { x.NumeroIdentificacion, x.CensoPacienteId, x.Estado, x.FechaEgreso })
            .ToListAsync(ct);
        abiertos.AddRange(npt
            .Where(x => !CensoPacienteService.EsProgramaCerrado(x.Estado, x.FechaEgreso))
            .Select(x => (Doc(x.NumeroIdentificacion), x.CensoPacienteId, CensoProgramas.Npt, true)));

        var terapia = await contexto.CensoTerapiasAmbulatorias.AsNoTracking()
            .Where(x => docs.Contains(x.NumeroIdentificacion.Trim().ToUpper())
                || (x.CensoPacienteId != null && ids.Contains(x.CensoPacienteId.Value)))
            .Select(x => new { x.NumeroIdentificacion, x.CensoPacienteId, x.EstadoPaciente, x.EstadoAlta })
            .ToListAsync(ct);
        abiertos.AddRange(terapia
            .Where(x => !CensoPacienteService.EsTerapiaCerrada(x.EstadoPaciente, x.EstadoAlta))
            .Select(x => (Doc(x.NumeroIdentificacion), x.CensoPacienteId, CensoProgramas.TerapiaAmbulatoria, true)));

        // Programa asignado en el carril que todavía no tiene formulario guardado.
        var pendientes = await contexto.CensoPacienteProgramas.AsNoTracking()
            .Where(e => e.RegistroId == null && e.CerradoAtUtc == null)
            .Where(e => ids.Contains(e.CensoPacienteId)
                || docs.Contains(e.CensoPaciente.NumeroIdentificacion.Trim().ToUpper()))
            .Select(e => new { e.Programa, e.CensoPacienteId, e.CensoPaciente.NumeroIdentificacion })
            .ToListAsync(ct);
        abiertos.AddRange(pendientes
            .Where(e => CensoProgramas.EsValido(e.Programa))
            .Select(e => (Doc(e.NumeroIdentificacion), (long?)e.CensoPacienteId, e.Programa, false)));

        var porDocumento = abiertos
            .GroupBy(x => x.Doc, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var porPaciente = abiertos
            .Where(x => x.PacienteId.HasValue)
            .GroupBy(x => x.PacienteId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return (documento, pacienteId) =>
        {
            var encontrados = new List<(string Doc, long? PacienteId, string Programa, bool ConRegistro)>();
            if (porDocumento.TryGetValue(Doc(documento), out var delDocumento))
            {
                encontrados.AddRange(delDocumento);
            }

            if (pacienteId.HasValue && porPaciente.TryGetValue(pacienteId.Value, out var delPaciente))
            {
                encontrados.AddRange(delPaciente);
            }

            var programas = encontrados
                .GroupBy(x => x.Programa, StringComparer.Ordinal)
                .OrderBy(g => CensoProgramas.Jerarquia(g.Key))
                // Si el programa ya tiene un registro abierto, el episodio vacío no agrega nada.
                .Select(g => g.Any(x => x.ConRegistro)
                    ? EtiquetaProgramaAgregado(g.Key)
                    : $"{EtiquetaProgramaAgregado(g.Key)} (sin diligenciar)")
                .ToList();

            return programas.Count == 0 ? SinProgramasAgregados : string.Join(", ", programas);
        };
    }
}
