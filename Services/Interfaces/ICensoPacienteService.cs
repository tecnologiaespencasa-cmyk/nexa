using Nexa.Data.Entities;
using Nexa.Models.ViewModels;
using Nexa.Services.Models;

namespace Nexa.Services.Interfaces;

/// <summary>
/// Maestro de paciente del censo: la capa que unifica recepción y datos básicos y los replica hacia
/// los censos de programa.
///
/// Contrato de continuidad operativa: este servicio nunca borra filas de los censos y nunca escribe
/// en columnas que sean propias de un programa. Solo escribe los campos compartidos y solo sobre
/// episodios abiertos, para que las atenciones cerradas conserven los datos con los que se
/// prestaron y facturaron.
/// </summary>
public interface ICensoPacienteService
{
    Task<CensoPaciente?> BuscarPorDocumentoAsync(string? documento, CancellationToken cancellationToken);

    Task<CensoPaciente?> ObtenerAsync(long pacienteId, CancellationToken cancellationToken);

    /// <summary>Episodios del paciente, abiertos y cerrados, ordenados por programa y fecha.</summary>
    Task<IReadOnlyList<CensoPacientePrograma>> ObtenerProgramasAsync(long pacienteId, CancellationToken cancellationToken);

    /// <summary>
    /// Crea o actualiza el maestro y replica los campos compartidos a los programas abiertos.
    /// Si el documento ya existe se actualiza ese maestro, aunque el formulario no traiga su id.
    /// </summary>
    Task<ServiceResult<CensoPaciente>> GuardarAsync(
        CensoPacienteFormViewModel formulario,
        string usuario,
        CancellationToken cancellationToken);

    /// <summary>
    /// Agrega un programa al paciente. Rechaza agudos si ya tiene crónicos abierto y viceversa, y
    /// rechaza duplicar un programa que ya está abierto.
    /// </summary>
    Task<ServiceResult<CensoPacientePrograma>> AgregarProgramaAsync(
        long pacienteId,
        string programa,
        string usuario,
        CancellationToken cancellationToken);

    /// <summary>
    /// Quita un programa recién agregado. Solo procede si todavía no tiene registro guardado: un
    /// programa con datos no se elimina, se cierra desde su propia sección de alta o egreso.
    /// </summary>
    Task<ServiceResult> QuitarProgramaAsync(long episodioId, string usuario, CancellationToken cancellationToken);

    /// <summary>
    /// Vuelca los campos compartidos del maestro sobre las filas de los programas abiertos. No toca
    /// episodios cerrados ni las copias internas de despacho a farmacia.
    /// </summary>
    Task ReplicarAProgramasAbiertosAsync(CensoPaciente paciente, CancellationToken cancellationToken);

    /// <summary>
    /// Pone al día los episodios del paciente contra lo que realmente hay en las tablas de los
    /// censos: enlaza las filas que todavía no tienen episodio, rellena el vínculo con el maestro y
    /// abre o cierra el episodio según el estado de cada registro.
    ///
    /// Existe porque las pantallas de cada programa siguen guardando por su cuenta, sin saber del
    /// maestro. En vez de intervenir sus rutas de guardado —que son las que mueven kardex,
    /// requisiciones y farmacia— la pantalla unificada reconcilia al abrirse. Solo escribe en las
    /// tablas del maestro y en la columna CensoPacienteId; nunca en datos clínicos.
    /// </summary>
    Task ReconciliarEpisodiosAsync(long pacienteId, CancellationToken cancellationToken);

    /// <summary>
    /// Crea el maestro de un documento que ya tiene filas en algún censo pero todavía no está
    /// unificado, aplicando la misma regla del backfill. Devuelve null si ese documento no aparece
    /// en ningún censo.
    /// </summary>
    Task<CensoPaciente?> AsegurarMaestroAsync(string? documento, CancellationToken cancellationToken);
}
