namespace IntranetPrueba.Models.ViewModels;

public static class FarmaciaEstados
{
    public const string Nuevo = "Nuevo";
    public const string Recepcionado = "Recepcionado";
    public const string Facturado = "Facturado";
    public const string Empacado = "Empacado";
    public const string PorDesempacar = "PorDesempacar";
    public const string Despachado = "Despachado";
}

public class FarmaciaIndexViewModel
{
    public string? DocumentoFiltro { get; set; }

    public int TotalPedidos { get; set; }

    public int PedidosNuevos { get; set; }

    public long? UltimoPedidoId { get; set; }

    public int PageSize { get; set; } = 25;

    public FarmaciaSectionPageViewModel Nuevos { get; set; } = new();

    public FarmaciaSectionPageViewModel Recepcionados { get; set; } = new();

    public FarmaciaSectionPageViewModel Facturados { get; set; } = new();

    public FarmaciaSectionPageViewModel Empacados { get; set; } = new();

    public FarmaciaSectionPageViewModel PorDesempacar { get; set; } = new();

    public FarmaciaSectionPageViewModel Despachados { get; set; } = new();

    public bool HasPedidos =>
        Nuevos.TotalItems > 0 || Recepcionados.TotalItems > 0 || Facturados.TotalItems > 0
        || Empacados.TotalItems > 0 || PorDesempacar.TotalItems > 0 || Despachados.TotalItems > 0;
}

public class FarmaciaSectionPageViewModel
{
    public int CurrentPage { get; set; } = 1;

    public int TotalItems { get; set; }

    public int TotalPages { get; set; } = 1;

    public IReadOnlyList<FarmaciaPedidoViewModel> Items { get; set; } = [];
}

public class FarmaciaPedidoViewModel
{
    public long Id { get; set; }

    public string NombrePaciente { get; set; } = string.Empty;

    public string TipoIdentificacion { get; set; } = string.Empty;

    public string NumeroIdentificacion { get; set; } = string.Empty;

    public DateTime FechaEnvioUtc { get; set; }

    public DateTime FechaIngreso { get; set; }

    public TimeSpan HoraIngreso { get; set; }

    public string? EstadoCenso { get; set; }

    public string? AuxiliarAsignado { get; set; }

    public string? MedicamentoPrincipal { get; set; }

    public bool KardexVisto { get; set; }

    public bool RequisicionVisto { get; set; }

    public bool FirmaRegistrada { get; set; }

    public string? NombreRecibe { get; set; }

    public DateTime? FechaHoraRecepcionUtc { get; set; }

    public string FarmaciaEstado { get; set; } = FarmaciaEstados.Nuevo;

    public bool FarmaciaOkKardex { get; set; }

    public bool? FarmaciaEsEntregaParcial { get; set; }

    public int? FarmaciaCantidadEntregas { get; set; }

    public int FarmaciaEntregaActual { get; set; } = 1;

    public bool FarmaciaFacturado { get; set; }

    public DateTime? FarmaciaEmpacadoAtUtc { get; set; }

    public bool TieneAdjuntos { get; set; }

    public TimeSpan? TiempoEnEmpacado => FarmaciaEmpacadoAtUtc.HasValue
        ? DateTime.UtcNow - FarmaciaEmpacadoAtUtc.Value
        : null;

    public bool EmpacadoVencido => TiempoEnEmpacado.HasValue && TiempoEnEmpacado.Value.TotalHours >= 72;

    public double HorasRestantesEmpacado => FarmaciaEmpacadoAtUtc.HasValue
        ? Math.Max(0, 72 - (DateTime.UtcNow - FarmaciaEmpacadoAtUtc.Value).TotalHours)
        : 72;
}

public class FarmaciaDocumentViewModel
{
    public long Id { get; set; }

    public string TipoDocumento { get; set; } = "kardex";

    public string NombrePaciente { get; set; } = string.Empty;

    public string TipoIdentificacion { get; set; } = string.Empty;

    public string NumeroIdentificacion { get; set; } = string.Empty;

    public string Asegurador { get; set; } = string.Empty;

    public string DiagnosticoDescriptivo { get; set; } = string.Empty;

    public string CodigoCie10 { get; set; } = string.Empty;

    public int Edad { get; set; }

    public string Direccion { get; set; } = string.Empty;

    public string? DetalleDireccion { get; set; }

    public string Telefonos { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    public string? ResponsableLlamadaBienvenida { get; set; }

    public string? AuxiliarAsignado { get; set; }

    public string? NombreRealizaKardex { get; set; }

    public string? Autorizacion { get; set; }

    public string? FechaSolicitudRequisicion { get; set; }

    public string? PesoKardex { get; set; }

    public string CambioEquipoKardex { get; set; } = "CADA 3 DIAS";

    public string? MedicoTratanteKardex { get; set; }

    public bool EsProrrogaActiva { get; set; }

    public bool EsEntregaParcial { get; set; }

    public int? CantidadEntregas { get; set; }

    public int EntregaActual { get; set; } = 1;

    public FarmaciaSignatureViewModel Firma { get; set; } = new();

    public IReadOnlyList<FarmaciaKardexMedicationViewModel> Medicamentos { get; set; } = [];

    public IReadOnlyList<FarmaciaRequisicionItemViewModel> RequisicionItems { get; set; } = [];

    public IReadOnlyList<FarmaciaAdjuntoDto> Adjuntos { get; set; } = [];
}

public class FarmaciaAdjuntoDto
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class FarmaciaKardexMedicationViewModel
{
    public int Row { get; set; }

    public string Presentacion { get; set; } = string.Empty;

    public string DosisFrecuencia { get; set; } = string.Empty;

    public string? VehiculoReconstitucion { get; set; }

    public string? VolumenDilucion { get; set; }

    public string? TiempoInfusion { get; set; }

    public string? Fotosensible { get; set; }

    public string? CadenaFrio { get; set; }

    public string? Aislamiento { get; set; }

    public string? Estabilidad { get; set; }

    public string? BombaInfusion { get; set; }

    public string FechaInicio { get; set; } = string.Empty;

    public string FechaFin { get; set; } = string.Empty;
}

public class FarmaciaRequisicionItemViewModel
{
    public int Item { get; set; }

    public string Descripcion { get; set; } = string.Empty;

    public string? Detalle { get; set; }

    public string Cantidad { get; set; } = string.Empty;

    public string FechaInicio { get; set; } = string.Empty;

    public string FechaFin { get; set; } = string.Empty;
}

public class FarmaciaSignatureViewModel
{
    public long PedidoId { get; set; }

    public string? NombreRecibe { get; set; }

    public string? FirmaEntregaDataUrl { get; set; }

    public string? FirmaRecibeDataUrl { get; set; }

    public DateTime? FechaHoraRecepcionUtc { get; set; }

    public DateTime? ActualizadaAtUtc { get; set; }

    public bool EstaCompleta =>
        !string.IsNullOrWhiteSpace(NombreRecibe)
        && !string.IsNullOrWhiteSpace(FirmaEntregaDataUrl)
        && !string.IsNullOrWhiteSpace(FirmaRecibeDataUrl)
        && FechaHoraRecepcionUtc.HasValue;

    public string FechaHoraRecepcionTexto => FechaHoraRecepcionUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
}

public class FarmaciaSignatureInputModel
{
    public long Id { get; set; }

    public string NombreRecibe { get; set; } = string.Empty;

    public string FirmaEntregaDataUrl { get; set; } = string.Empty;

    public string FirmaRecibeDataUrl { get; set; } = string.Empty;

    public DateTime FechaHoraRecepcion { get; set; }
}

public class FarmaciaEntregaParcialInputModel
{
    public long Id { get; set; }

    public bool EsEntregaParcial { get; set; }

    public int? CantidadEntregas { get; set; }
}
