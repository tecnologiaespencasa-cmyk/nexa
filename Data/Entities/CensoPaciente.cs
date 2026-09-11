using System.ComponentModel.DataAnnotations;

namespace Nexa.Data.Entities;

/// <summary>
/// Maestro de paciente del censo. Guarda una sola vez los datos básicos que antes se repetían en
/// cada censo, para que un paciente que está en varios programas se capture una vez.
///
/// La recepción NO está aquí, aunque lo estuvo: es del ingreso, no del paciente. Cada ingreso nace
/// de un correo distinto, así que un paciente que reingresa trae una recepción nueva y la anterior
/// tiene que quedarse con su ingreso. Vive en <see cref="CensoPacientePrograma"/>.
///
/// Continuidad operativa: esta tabla NO reemplaza a las tablas de programa. Cada programa sigue
/// guardando su propia copia de los campos compartidos (censo, censo_cronicos, censo_clinica_heridas,
/// censo_npt, censo_terapias_ambulatorias), que es de donde leen los kardex, las requisiciones, la
/// bandeja de farmacia, las prórrogas, el puente de Supabase y todos los exportables. El maestro se
/// replica hacia ellas; nunca al revés y nunca borrando lo que ya existe.
///
/// Casi todo es nullable a propósito: el backfill de los pacientes que ya están en producción tiene
/// que poder crear el maestro aunque al registro de origen le falten campos. La obligatoriedad se
/// valida en el formulario, no en el esquema.
/// </summary>
public class CensoPaciente
{
    [Key]
    public long Id { get; set; }

    // ----- Identificación -----
    [Required]
    [StringLength(3)]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string NombrePaciente { get; set; } = string.Empty;

    public DateTime FechaNacimiento { get; set; }

    public int Edad { get; set; }

    // Subió al maestro: lo capturaban crónicos, clínica de heridas y NPT por separado.
    [StringLength(20)]
    public string? Genero { get; set; }

    [StringLength(150)]
    public string? CorreoElectronico { get; set; }

    // ----- Información clínica -----
    [StringLength(4)]
    public string? CodigoCie10 { get; set; }

    [StringLength(300)]
    public string? DiagnosticoDescriptivo { get; set; }

    // Subió al maestro: en agudos vivía en la sección 3 (plan de manejo) y en clínica de heridas y
    // NPT en datos básicos. Se sigue replicando a la sección 3 de agudos para no alterar el kardex.
    [StringLength(120)]
    public string? Asegurador { get; set; }

    // ----- Dirección y ubicación -----
    [StringLength(300)]
    public string? Direccion { get; set; }

    public bool DireccionValidada { get; set; }

    public bool AsumirDireccionErrada { get; set; }

    [StringLength(200)]
    public string? DetalleDireccion { get; set; }

    [StringLength(30)]
    public string? ClasificacionZonaSura { get; set; }

    [StringLength(120)]
    public string? MunicipioResidencia { get; set; }

    [StringLength(120)]
    public string? Barrio { get; set; }

    [StringLength(50)]
    public string? ZonaDireccionSegunMunicipio { get; set; }

    [StringLength(10)]
    public string? Area { get; set; }

    // ----- Remisión y contacto -----
    [StringLength(200)]
    public string? IpsQueRemite { get; set; }

    [StringLength(2)]
    public string? VistoBuenoRangoFueraAnexo { get; set; }

    // Telefono1/2/3 en agudos = TelefonoPrincipal/Adicional1/Adicional2 en los demás censos.
    [StringLength(10)]
    public string? Telefono1 { get; set; }

    [StringLength(10)]
    public string? Telefono2 { get; set; }

    [StringLength(10)]
    public string? Telefono3 { get; set; }

    // ----- Auditoría -----
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    [StringLength(200)]
    public string? CreadoPor { get; set; }

    [StringLength(200)]
    public string? ActualizadoPor { get; set; }

    public ICollection<CensoPacientePrograma> Programas { get; set; } = [];
}
