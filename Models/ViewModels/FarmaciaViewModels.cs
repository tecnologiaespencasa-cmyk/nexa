namespace IntranetPrueba.Models.ViewModels;

public class FarmaciaIndexViewModel
{
    public string? DocumentoFiltro { get; set; }

    public int TotalPedidos { get; set; }

    public int PedidosNuevos { get; set; }

    public long? UltimoPedidoId { get; set; }

    public int PageSize { get; set; } = 25;

    public FarmaciaSectionPageViewModel Nuevos { get; set; } = new();

    public FarmaciaSectionPageViewModel EnRevision { get; set; } = new();

    public FarmaciaSectionPageViewModel Vistos { get; set; } = new();

    public bool HasPedidos => Nuevos.TotalItems > 0 || EnRevision.TotalItems > 0 || Vistos.TotalItems > 0;
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

    public bool EsNuevo => !KardexVisto && !RequisicionVisto && !FirmaRegistrada;

    public bool EstaEnRevision => !EsNuevo && (!KardexVisto || !RequisicionVisto || !FirmaRegistrada);
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

    public string Telefonos { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    public string? ResponsableLlamadaBienvenida { get; set; }

    public string? AuxiliarAsignado { get; set; }

    public string? NombreRealizaKardex { get; set; }

    public string? Autorizacion { get; set; }

    public string? FechaSolicitudRequisicion { get; set; }

    public FarmaciaSignatureViewModel Firma { get; set; } = new();

    public IReadOnlyList<FarmaciaKardexMedicationViewModel> Medicamentos { get; set; } = [];

    public IReadOnlyList<FarmaciaRequisicionItemViewModel> RequisicionItems { get; set; } = [];
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
