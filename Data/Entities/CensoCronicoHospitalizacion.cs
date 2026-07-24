using System.ComponentModel.DataAnnotations;

namespace IntranetPrueba.Data.Entities;

/// <summary>
/// Hospitalización y seguimiento del censo de Programa Crónicos. Cada paciente puede tener
/// N hospitalizaciones (mismo patrón que las agudizaciones); los campos del episodio se
/// almacenan como JSON en <see cref="HospitalizacionJson"/>.
/// </summary>
public class CensoCronicoHospitalizacion
{
    [Key]
    public long Id { get; set; }

    public long CensoCronicoRecordId { get; set; }

    public int Numero { get; set; }

    [Required]
    public string HospitalizacionJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public CensoCronicoRecord CensoCronicoRecord { get; set; } = null!;
}
