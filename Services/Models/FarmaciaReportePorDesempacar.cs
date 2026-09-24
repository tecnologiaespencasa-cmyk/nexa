namespace Nexa.Services.Models;

/// <summary>
/// Un despacho que venció en la ventana del reporte (72 h empacado sin firma) y al corte sigue
/// por desempacar: sin firmar y sin la bolsa desempacada.
/// </summary>
public sealed record FarmaciaDespachoPorDesempacar(
    string Programa,
    string? Detalle,
    long Pedido,
    string Paciente,
    string TipoDocumento,
    string Documento,
    string? Auxiliar,
    DateTime EmpacadoUtc,
    DateTime VencioUtc);

/// <summary>
/// Reporte de un corte: la ventana de 24 horas que termina a las 11:00 p. m. de <see cref="Dia"/>.
/// </summary>
public sealed record FarmaciaReportePorDesempacar(
    DateTime Dia,
    DateTime DesdeUtc,
    DateTime HastaUtc,
    DateTime GeneradoUtc,
    IReadOnlyList<FarmaciaDespachoPorDesempacar> Despachos);

/// <summary>El corte de un día: a qué día pertenece y qué ventana cubre.</summary>
public readonly record struct FarmaciaCorteReporte(DateTime Dia, DateTime DesdeUtc, DateTime HastaUtc);
