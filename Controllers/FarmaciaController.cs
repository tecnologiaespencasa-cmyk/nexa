using System.Globalization;
using System.Text;
using System.Text.Json;
using IntranetPrueba.Data;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.Farmacia)]
public class FarmaciaController : Controller
{
    private const string DocumentoKardex = "kardex";
    private const string DocumentoRequisicion = "requisicion";
    private const string ValorNoAplicaMedicamentoAdicional = "No";
    private const int PageSize = 25;
    private static readonly TimeSpan TiempoLimiteEmpacado = TimeSpan.FromHours(72);
    private readonly ApplicationDbContext _context;
    private readonly IFarmaciaDispatchNotificationService _notificationService;

    public FarmaciaController(ApplicationDbContext context, IFarmaciaDispatchNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? documento,
        int nuevosPagina = 1,
        int recepcionadosPagina = 1,
        int facturadosPagina = 1,
        int empacadosPagina = 1,
        int porDesempacarPagina = 1,
        int despachadosPagina = 1,
        CancellationToken cancellationToken = default)
    {
        await ApplyEmpacadoTimeoutAsync(cancellationToken);

        var filtro = documento?.Trim();
        var query = _context.Censos
            .AsNoTracking()
            .Where(x => x.FarmaciaEnviadoAtUtc != null);

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            query = query.Where(x => x.NumeroIdentificacion.Contains(filtro));
        }

        var totalPedidos = await query.CountAsync(cancellationToken);
        var pedidosNuevos = await query.CountAsync(x => x.FarmaciaEstado == FarmaciaEstados.Nuevo, cancellationToken);
        var ultimoPedidoId = await query
            .OrderByDescending(x => x.FarmaciaEnviadoAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var model = new FarmaciaIndexViewModel
        {
            DocumentoFiltro = filtro,
            TotalPedidos = totalPedidos,
            PedidosNuevos = pedidosNuevos,
            UltimoPedidoId = ultimoPedidoId,
            PageSize = PageSize,
            Nuevos = await BuildSectionPageAsync(query.Where(x => x.FarmaciaEstado == FarmaciaEstados.Nuevo), nuevosPagina, cancellationToken),
            Recepcionados = await BuildSectionPageAsync(query.Where(x => x.FarmaciaEstado == FarmaciaEstados.Recepcionado), recepcionadosPagina, cancellationToken),
            Facturados = await BuildSectionPageAsync(query.Where(x => x.FarmaciaEstado == FarmaciaEstados.Facturado), facturadosPagina, cancellationToken),
            Empacados = await BuildSectionPageAsync(query.Where(x => x.FarmaciaEstado == FarmaciaEstados.Empacado), empacadosPagina, cancellationToken),
            PorDesempacar = await BuildSectionPageAsync(query.Where(x => x.FarmaciaEstado == FarmaciaEstados.PorDesempacar), porDesempacarPagina, cancellationToken),
            Despachados = await BuildSectionPageAsync(query.Where(x => x.FarmaciaEstado == FarmaciaEstados.Despachado), despachadosPagina, cancellationToken),
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Documento(long id, string tipo = DocumentoKardex, CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeDocumentType(tipo);
        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null,
            cancellationToken);

        if (record is null)
        {
            return NotFound();
        }

        var now = DateTime.UtcNow;
        if (normalizedType == DocumentoRequisicion)
        {
            record.FarmaciaRequisicionVistoAtUtc ??= now;
        }
        else
        {
            record.FarmaciaKardexVistoAtUtc ??= now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var medicamentos = await _context.Medicamentos
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        return View(BuildDocumentModel(record, medicamentos, normalizedType));
    }

    [HttpGet]
    public async Task<IActionResult> PendientesSnapshot(CancellationToken cancellationToken)
    {
        await ApplyEmpacadoTimeoutAsync(cancellationToken);

        var query = _context.Censos
            .AsNoTracking()
            .Where(x => x.FarmaciaEnviadoAtUtc != null);

        var newCount = await query.CountAsync(x => x.FarmaciaEstado == FarmaciaEstados.Nuevo, cancellationToken);
        var lastId = await query
            .OrderByDescending(x => x.FarmaciaEnviadoAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return Json(new { newCount, lastId });
    }

    [HttpGet]
    public async Task<IActionResult> Firma(long id, CancellationToken cancellationToken)
    {
        var record = await _context.Censos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.FarmaciaEnviadoAtUtc != null, cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "No se encontro el despacho de farmacia." });
        }

        if (record.FarmaciaEstado != FarmaciaEstados.Empacado)
        {
            return BadRequest(new { message = "La firma solo esta disponible en estado Empacado." });
        }

        var firma = BuildSignatureModel(record);
        return Json(new
        {
            id = firma.PedidoId,
            nombreRecibe = firma.NombreRecibe,
            firmaEntregaDataUrl = firma.FirmaEntregaDataUrl,
            firmaRecibeDataUrl = firma.FirmaRecibeDataUrl,
            fechaHoraRecepcion = firma.FechaHoraRecepcionUtc?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            fechaHoraRecepcionTexto = firma.FechaHoraRecepcionTexto,
            estaCompleta = firma.EstaCompleta
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarFirma(FarmaciaSignatureInputModel model, CancellationToken cancellationToken)
    {
        if (model.Id <= 0)
        {
            return BadRequest(new { message = "No se encontro el despacho para guardar la firma." });
        }

        var nombreRecibe = model.NombreRecibe?.Trim();
        if (string.IsNullOrWhiteSpace(nombreRecibe))
        {
            return BadRequest(new { message = "Ingresa el nombre de quien recibe." });
        }

        if (!IsValidSignatureDataUrl(model.FirmaEntregaDataUrl))
        {
            return BadRequest(new { message = "La firma de quien entrega es obligatoria." });
        }

        if (!IsValidSignatureDataUrl(model.FirmaRecibeDataUrl))
        {
            return BadRequest(new { message = "La firma de quien recibe es obligatoria." });
        }

        if (model.FechaHoraRecepcion == default)
        {
            return BadRequest(new { message = "Ingresa la fecha y hora de recepcion." });
        }

        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == model.Id && x.FarmaciaEnviadoAtUtc != null,
            cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "No se encontro el despacho de farmacia." });
        }

        if (record.FarmaciaEstado != FarmaciaEstados.Empacado)
        {
            return BadRequest(new { message = "La firma solo esta disponible en estado Empacado." });
        }

        record.FarmaciaNombreRecibe = nombreRecibe;
        record.FarmaciaFirmaEntregaDataUrl = model.FirmaEntregaDataUrl.Trim();
        record.FarmaciaFirmaRecibeDataUrl = model.FirmaRecibeDataUrl.Trim();
        record.FarmaciaFechaHoraRecepcionUtc = DateTime.SpecifyKind(model.FechaHoraRecepcion, DateTimeKind.Local).ToUniversalTime();
        record.FarmaciaFirmaActualizadaAtUtc = DateTime.UtcNow;
        record.FarmaciaEstado = FarmaciaEstados.Despachado;

        await _context.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            message = "Firmas guardadas. Paciente pasado a Despachado.",
            estaCompleta = true,
            nombreRecibe = record.FarmaciaNombreRecibe,
            fechaHoraRecepcionTexto = record.FarmaciaFechaHoraRecepcionUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOkKardex(long id, CancellationToken cancellationToken)
    {
        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Nuevo,
            cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Nuevo." });
        }

        record.FarmaciaOkKardex = true;
        record.FarmaciaEstado = FarmaciaEstados.Recepcionado;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { message = "Kardex aprobado. Paciente en estado Recepcionado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEntregaParcial(
        [FromBody] FarmaciaEntregaParcialInputModel model,
        CancellationToken cancellationToken)
    {
        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == model.Id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Recepcionado,
            cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Recepcionado." });
        }

        if (model.EsEntregaParcial && (model.CantidadEntregas is null or < 2))
        {
            return BadRequest(new { message = "La cantidad de entregas debe ser al menos 2." });
        }

        record.FarmaciaEsEntregaParcial = model.EsEntregaParcial;
        record.FarmaciaCantidadEntregas = model.EsEntregaParcial ? model.CantidadEntregas : null;
        record.FarmaciaEntregaActual = 1;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { message = "Configuracion de entrega guardada." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AvanzarEntrega(long id, CancellationToken cancellationToken)
    {
        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null
                && (x.FarmaciaEstado == FarmaciaEstados.Recepcionado
                    || x.FarmaciaEstado == FarmaciaEstados.Facturado
                    || x.FarmaciaEstado == FarmaciaEstados.Empacado
                    || (x.FarmaciaEstado == FarmaciaEstados.Despachado && x.FarmaciaEsEntregaParcial == true)),
            cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no tiene entrega parcial activa." });
        }

        if (record.FarmaciaEsEntregaParcial != true || !record.FarmaciaCantidadEntregas.HasValue)
        {
            return BadRequest(new { message = "El pedido no tiene entrega parcial configurada." });
        }

        if (record.FarmaciaEntregaActual >= record.FarmaciaCantidadEntregas.Value)
        {
            return BadRequest(new { message = "Ya se alcanzo la ultima entrega." });
        }

        record.FarmaciaEntregaActual++;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new
        {
            message = $"Avanzado a entrega {record.FarmaciaEntregaActual} de {record.FarmaciaCantidadEntregas}.",
            entregaActual = record.FarmaciaEntregaActual,
            cantidadEntregas = record.FarmaciaCantidadEntregas
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetFacturado(long id, CancellationToken cancellationToken)
    {
        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Recepcionado,
            cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Recepcionado." });
        }

        record.FarmaciaFacturado = true;
        record.FarmaciaEstado = FarmaciaEstados.Facturado;
        await _context.SaveChangesAsync(cancellationToken);

        _ = Task.Run(() => _notificationService.NotifyDespachadoAsync(record, CancellationToken.None), CancellationToken.None);

        return Json(new { message = "Pedido marcado como Facturado." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEmpacado(long id, CancellationToken cancellationToken)
    {
        var record = await _context.Censos.FirstOrDefaultAsync(
            x => x.Id == id && x.FarmaciaEnviadoAtUtc != null && x.FarmaciaEstado == FarmaciaEstados.Facturado,
            cancellationToken);

        if (record is null)
        {
            return NotFound(new { message = "Pedido no encontrado o no esta en estado Facturado." });
        }

        record.FarmaciaEstado = FarmaciaEstados.Empacado;
        record.FarmaciaEmpacadoAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { message = "Pedido en estado Empacado. Tiene 72 horas para firmar." });
    }

    private async Task ApplyEmpacadoTimeoutAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - TiempoLimiteEmpacado;
        var vencidos = await _context.Censos
            .Where(x => x.FarmaciaEstado == FarmaciaEstados.Empacado
                && x.FarmaciaEmpacadoAtUtc != null
                && x.FarmaciaEmpacadoAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        if (vencidos.Count > 0)
        {
            foreach (var r in vencidos)
            {
                r.FarmaciaEstado = FarmaciaEstados.PorDesempacar;
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private static FarmaciaPedidoViewModel MapPedido(CensoRecord record)
    {
        return new FarmaciaPedidoViewModel
        {
            Id = record.Id,
            NombrePaciente = record.NombrePaciente,
            TipoIdentificacion = record.TipoIdentificacion,
            NumeroIdentificacion = record.NumeroIdentificacion,
            FechaEnvioUtc = record.FarmaciaEnviadoAtUtc ?? record.CreatedAtUtc,
            FechaIngreso = record.FechaIngreso,
            HoraIngreso = record.HoraIngreso,
            EstadoCenso = record.Estado,
            AuxiliarAsignado = record.AuxiliarAsignado,
            MedicamentoPrincipal = record.NombreMedicamentoPrincipalTratante,
            KardexVisto = record.FarmaciaKardexVistoAtUtc.HasValue,
            RequisicionVisto = record.FarmaciaRequisicionVistoAtUtc.HasValue,
            FirmaRegistrada = BuildSignatureModel(record).EstaCompleta,
            NombreRecibe = record.FarmaciaNombreRecibe,
            FechaHoraRecepcionUtc = record.FarmaciaFechaHoraRecepcionUtc,
            FarmaciaEstado = record.FarmaciaEstado,
            FarmaciaOkKardex = record.FarmaciaOkKardex,
            FarmaciaEsEntregaParcial = record.FarmaciaEsEntregaParcial,
            FarmaciaCantidadEntregas = record.FarmaciaCantidadEntregas,
            FarmaciaEntregaActual = record.FarmaciaEntregaActual,
            FarmaciaFacturado = record.FarmaciaFacturado,
            FarmaciaEmpacadoAtUtc = record.FarmaciaEmpacadoAtUtc,
        };
    }

    private static async Task<FarmaciaSectionPageViewModel> BuildSectionPageAsync(
        IQueryable<CensoRecord> query,
        int requestedPage,
        CancellationToken cancellationToken)
    {
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        var currentPage = Math.Clamp(requestedPage, 1, totalPages);
        var records = await query
            .OrderBy(x => x.FarmaciaEnviadoAtUtc)
            .ThenBy(x => x.Id)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return new FarmaciaSectionPageViewModel
        {
            CurrentPage = currentPage,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Items = records.Select(MapPedido).ToList()
        };
    }

    private static FarmaciaDocumentViewModel BuildDocumentModel(
        CensoRecord record,
        IReadOnlyList<Medicamento> medicamentos,
        string tipoDocumento)
    {
        var medicationCatalog = medicamentos
            .GroupBy(x => NormalizeCatalogKey(x.Nombre), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var fechaInicio = FormatNullableDate(record.FechaInicioTratamiento);
        var fechaFin = FormatNullableDate(record.FechaFinTratamiento);
        var isolationValue = string.Equals(record.Aislamiento, "Si", StringComparison.OrdinalIgnoreCase)
            ? record.TipoAislamiento ?? "Si"
            : "No";

        var rows = new[]
        {
            BuildMedicationRow(1, record.NombreMedicamentoPrincipalTratante, record.DosisMedicamentoPrincipal, record.MedidaMedicamentoPrincipal, record.ViaAdministracionMedicamentoPrincipal, record.FrecuenciaAdministracionMxPrincipal, record.DiasMedicamentoPrincipal, fechaInicio, fechaFin, isolationValue, medicationCatalog),
            BuildMedicationRow(2, record.NombreMedicamentoNumero2, record.DosisMedicamento2, record.MedidaMedicamento2, record.ViaAdministracionMedicamento2, record.FrecuenciaAdministracionMedicamento2, record.DiasMedicamento2, fechaInicio, fechaFin, isolationValue, medicationCatalog),
            BuildMedicationRow(3, record.NombreMedicamentoNumero3, record.DosisMedicamento3, record.MedidaMedicamento3, record.ViaAdministracionMedicamento3, record.FrecuenciaAdministracionMedicamento3, record.DiasMedicamento3, fechaInicio, fechaFin, isolationValue, medicationCatalog)
        };
        var requisicionRows = BuildRequisitionRows(record, medicationCatalog, fechaInicio, fechaFin);

        var model = new FarmaciaDocumentViewModel
        {
            Id = record.Id,
            TipoDocumento = tipoDocumento,
            NombrePaciente = record.NombrePaciente,
            TipoIdentificacion = record.TipoIdentificacion,
            NumeroIdentificacion = record.NumeroIdentificacion,
            Asegurador = record.Asegurador,
            DiagnosticoDescriptivo = record.DiagnosticoDescriptivo,
            CodigoCie10 = record.CodigoCie10,
            Edad = record.Edad,
            Direccion = record.Direccion,
            DetalleDireccion = record.DetalleDireccion,
            Telefonos = string.Join(" / ", new[] { record.Telefono1, record.Telefono2, record.Telefono3 }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Observaciones = record.ObservacionesPlanManejo,
            ResponsableLlamadaBienvenida = record.ResponsableLlamadaBienvenida,
            AuxiliarAsignado = record.AuxiliarAsignado,
            NombreRealizaKardex = record.NombreRealizaKardex,
            Autorizacion = record.AutorizacionEvento,
            EsEntregaParcial = record.FarmaciaEsEntregaParcial == true,
            CantidadEntregas = record.FarmaciaCantidadEntregas,
            EntregaActual = record.FarmaciaEntregaActual,
            Firma = BuildSignatureModel(record),
            Medicamentos = rows,
            RequisicionItems = requisicionRows
        };

        if (tipoDocumento == DocumentoRequisicion)
        {
            ApplyStoredRequisition(model, record.RequisicionFarmaciaJson);
            if (model.EsEntregaParcial && model.CantidadEntregas is > 1)
            {
                ApplyEntregaParcialDivision(model);
            }
        }
        else
        {
            ApplyStoredKardex(model, record.KardexEdicionJson);
        }

        return model;
    }

    private static void ApplyEntregaParcialDivision(FarmaciaDocumentViewModel model)
    {
        var divisor = model.CantidadEntregas!.Value;
        model.RequisicionItems = model.RequisicionItems
            .Select(item =>
            {
                if (!decimal.TryParse(item.Cantidad, NumberStyles.Number, CultureInfo.InvariantCulture, out var qty) || qty <= 0)
                {
                    return item;
                }

                var divided = Math.Ceiling(qty / divisor);
                return new FarmaciaRequisicionItemViewModel
                {
                    Item = item.Item,
                    Descripcion = item.Descripcion,
                    Detalle = item.Detalle,
                    Cantidad = divided.ToString("0.##", CultureInfo.InvariantCulture),
                    FechaInicio = item.FechaInicio,
                    FechaFin = item.FechaFin
                };
            })
            .ToList();
    }

    private static FarmaciaKardexMedicationViewModel BuildMedicationRow(
        int row,
        string? name,
        decimal? dose,
        string? measure,
        string? route,
        string? frequency,
        int? days,
        string fechaInicio,
        string fechaFin,
        string isolationValue,
        IReadOnlyDictionary<string, Medicamento> medicationCatalog)
    {
        var hasMedication = HasMedication(name);
        medicationCatalog.TryGetValue(NormalizeCatalogKey(name), out var metadata);
        var presentacion = hasMedication
            ? string.Join("\n", new[] { name, metadata?.PresentacionRequisicion }.Where(x => !string.IsNullOrWhiteSpace(x)))
            : $"Medicamento {row}";

        return new FarmaciaKardexMedicationViewModel
        {
            Row = row,
            Presentacion = presentacion,
            DosisFrecuencia = BuildDoseText(dose, measure, route, frequency, days),
            VehiculoReconstitucion = metadata?.VehiculoReconstitucion,
            VolumenDilucion = metadata?.DilucionRecomendada,
            TiempoInfusion = FormatInfusionTime(metadata),
            Fotosensible = metadata?.EquipoFotosensible,
            CadenaFrio = metadata?.CadenaFrio,
            Aislamiento = hasMedication ? isolationValue : string.Empty,
            Estabilidad = metadata?.TiempoEstabilidad,
            BombaInfusion = metadata?.BombaInfusion,
            FechaInicio = hasMedication ? fechaInicio : string.Empty,
            FechaFin = hasMedication ? fechaFin : string.Empty
        };
    }

    private static string NormalizeDocumentType(string? tipo)
    {
        return string.Equals(tipo, DocumentoRequisicion, StringComparison.OrdinalIgnoreCase)
            ? DocumentoRequisicion
            : DocumentoKardex;
    }

    private static bool HasMedication(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, ValorNoAplicaMedicamentoAdicional, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDoseText(decimal? dose, string? measure, string? route, string? frequency, int? days)
    {
        var doseText = dose.HasValue
            ? dose.Value.ToString("0.##", CultureInfo.InvariantCulture)
            : string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(doseText) || !string.IsNullOrWhiteSpace(measure))
        {
            parts.Add($"{doseText} {measure}".Trim());
        }

        if (!string.IsNullOrWhiteSpace(route))
        {
            parts.Add(route);
        }

        if (!string.IsNullOrWhiteSpace(frequency))
        {
            parts.Add(frequency);
        }

        if (days.HasValue)
        {
            parts.Add($"{days.Value.ToString(CultureInfo.InvariantCulture)} dias");
        }

        return string.Join(" / ", parts);
    }

    private static IReadOnlyList<FarmaciaRequisicionItemViewModel> BuildRequisitionRows(
        CensoRecord record,
        IReadOnlyDictionary<string, Medicamento> medicationCatalog,
        string fechaInicio,
        string fechaFin)
    {
        var medications = new[]
        {
            new RequisitionMedication(record.NombreMedicamentoPrincipalTratante, record.DosisMedicamentoPrincipal, record.FrecuenciaAdministracionMxPrincipal, record.NumeroDosisDiaMedicamentoPrincipal, record.DiasMedicamentoPrincipal),
            new RequisitionMedication(record.NombreMedicamentoNumero2, record.DosisMedicamento2, record.FrecuenciaAdministracionMedicamento2, record.NumeroDosisMedicamento2, record.DiasMedicamento2),
            new RequisitionMedication(record.NombreMedicamentoNumero3, record.DosisMedicamento3, record.FrecuenciaAdministracionMedicamento3, record.NumeroDosisMedicamento3, record.DiasMedicamento3)
        }
            .Where(x => HasMedication(x.Name))
            .ToList();

        var rows = new List<FarmaciaRequisicionItemViewModel>();
        foreach (var medication in medications)
        {
            rows.Add(new FarmaciaRequisicionItemViewModel
            {
                Descripcion = medication.Name!.Trim(),
                Cantidad = FormatQuantity(CalculateMedicationQuantity(medication)),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            });
        }

        foreach (var detail in medications
            .Select(x =>
            {
                medicationCatalog.TryGetValue(NormalizeCatalogKey(x.Name), out var metadata);
                return metadata?.SolucionParaDilucion;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new FarmaciaRequisicionItemViewModel
            {
                Descripcion = "SOLUCION PARA DILUCION",
                Detalle = detail,
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            });
        }

        var totalApplications = ParseDecimal(record.AplicacionesTotales);
        foreach (var detail in medications
            .Select(x =>
            {
                medicationCatalog.TryGetValue(NormalizeCatalogKey(x.Name), out var metadata);
                return metadata?.Jeringa;
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new FarmaciaRequisicionItemViewModel
            {
                Descripcion = "JERINGA",
                Detalle = detail,
                Cantidad = FormatQuantity(totalApplications),
                FechaInicio = fechaInicio,
                FechaFin = fechaFin
            });
        }

        var treatmentDays = ParseDecimal(record.DiasTratamientoIv);
        rows.Add(new FarmaciaRequisicionItemViewModel { Descripcion = "JELCO #22", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays, true)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new FarmaciaRequisicionItemViewModel { Descripcion = "ATI", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new FarmaciaRequisicionItemViewModel { Descripcion = "MACROGOTERO", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new FarmaciaRequisicionItemViewModel { Descripcion = "BURETRA", Cantidad = FormatQuantity(CalculateEveryThreeDaysQuantity(treatmentDays)), FechaInicio = fechaInicio, FechaFin = fechaFin });
        rows.Add(new FarmaciaRequisicionItemViewModel { Descripcion = "GUANTES (PAR)", Cantidad = FormatQuantity(totalApplications), FechaInicio = fechaInicio, FechaFin = fechaFin });

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Item = i + 1;
        }

        return rows;
    }

    private static FarmaciaSignatureViewModel BuildSignatureModel(CensoRecord record)
    {
        return new FarmaciaSignatureViewModel
        {
            PedidoId = record.Id,
            NombreRecibe = record.FarmaciaNombreRecibe,
            FirmaEntregaDataUrl = record.FarmaciaFirmaEntregaDataUrl,
            FirmaRecibeDataUrl = record.FarmaciaFirmaRecibeDataUrl,
            FechaHoraRecepcionUtc = record.FarmaciaFechaHoraRecepcionUtc,
            ActualizadaAtUtc = record.FarmaciaFirmaActualizadaAtUtc
        };
    }

    private static bool IsValidSignatureDataUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 1_000_000
            && trimmed.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyStoredRequisition(FarmaciaDocumentViewModel model, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        RequisitionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RequisitionPayload>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return;
        }

        if (payload is null)
        {
            return;
        }

        if (payload.Fields.TryGetValue("auxiliarEncargado", out var auxiliarEncargado))
        {
            model.AuxiliarAsignado = auxiliarEncargado;
        }

        if (payload.Fields.TryGetValue("realizadoPor", out var realizadoPor))
        {
            model.NombreRealizaKardex = realizadoPor;
        }

        if (payload.Fields.TryGetValue("paciente", out var paciente) && !string.IsNullOrWhiteSpace(paciente))
        {
            model.NombrePaciente = paciente;
        }

        if (payload.Fields.TryGetValue("autorizacion", out var autorizacion))
        {
            model.Autorizacion = autorizacion;
        }

        if (payload.Fields.TryGetValue("documento", out var documento) && !string.IsNullOrWhiteSpace(documento))
        {
            model.NumeroIdentificacion = documento;
        }

        if (payload.Fields.TryGetValue("fechaSolicitud", out var fechaSolicitud))
        {
            model.FechaSolicitudRequisicion = fechaSolicitud;
        }

        if (payload.Fields.TryGetValue("direccion", out var direccion))
        {
            model.Direccion = direccion ?? string.Empty;
        }

        if (payload.Fields.TryGetValue("telefonos", out var telefonos))
        {
            model.Telefonos = telefonos ?? string.Empty;
        }

        if (payload.Fields.TryGetValue("diagnostico", out var diagnostico))
        {
            model.CodigoCie10 = string.Empty;
            model.DiagnosticoDescriptivo = diagnostico ?? string.Empty;
        }

        var rows = payload.Rows
            .Select((row, index) => new FarmaciaRequisicionItemViewModel
            {
                Item = int.TryParse(row.Item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var item) && item > 0 ? item : index + 1,
                Descripcion = row.Descripcion ?? string.Empty,
                Detalle = row.Detalle,
                Cantidad = row.Cantidad ?? string.Empty,
                FechaInicio = row.FechaInicio ?? string.Empty,
                FechaFin = row.FechaFin ?? string.Empty
            })
            .Where(row =>
                !string.IsNullOrWhiteSpace(row.Descripcion)
                || !string.IsNullOrWhiteSpace(row.Detalle)
                || !string.IsNullOrWhiteSpace(row.Cantidad))
            .ToList();

        if (rows.Count > 0)
        {
            model.RequisicionItems = rows;
        }
    }

    private static decimal CalculateMedicationQuantity(RequisitionMedication medication)
    {
        var dose = medication.Dose.GetValueOrDefault(1);
        if (dose <= 0)
        {
            dose = 1;
        }

        var dailyDoses = ParseDecimal(medication.DailyDoses);
        if (dailyDoses <= 0)
        {
            dailyDoses = ParseFrequencyPerDay(medication.Frequency);
        }

        var days = medication.Days.GetValueOrDefault();
        if (days <= 0)
        {
            return 0;
        }

        return dose * dailyDoses * days;
    }

    private static decimal CalculateEveryThreeDaysQuantity(decimal days, bool minimumTwoWhenLessThanOne = false)
    {
        if (days <= 0)
        {
            return minimumTwoWhenLessThanOne ? 2 : 0;
        }

        var raw = days / 3;
        if (minimumTwoWhenLessThanOne && raw < 1)
        {
            return 2;
        }

        return Math.Ceiling(raw);
    }

    private static decimal ParseFrequencyPerDay(string? frequency)
    {
        if (string.IsNullOrWhiteSpace(frequency))
        {
            return 0;
        }

        var normalized = frequency.Trim().ToUpperInvariant();
        var numbers = normalized.Split(new[] { ' ', '/', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => decimal.TryParse(part.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0)
            .Where(value => value > 0)
            .ToList();

        if (normalized.Contains("24") && normalized.Contains("HORA")) return 1;
        if (normalized.Contains("12") && normalized.Contains("HORA")) return 2;
        if (normalized.Contains("8") && normalized.Contains("HORA")) return 3;
        if (normalized.Contains("6") && normalized.Contains("HORA")) return 4;
        if (normalized.Contains("4") && normalized.Contains("HORA")) return 6;

        if (numbers.Count > 0 && normalized.Contains("HORA"))
        {
            return 24 / numbers[0];
        }

        if (normalized.Contains("DIA") || normalized.Contains("DIARIA"))
        {
            return numbers.Count > 0 ? numbers[0] : 1;
        }

        return numbers.Count > 0 ? numbers[0] : 0;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var numericText = new string(value.Where(ch => char.IsDigit(ch) || ch is ',' or '.').ToArray()).Replace(',', '.');
        return decimal.TryParse(numericText, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static string FormatQuantity(decimal value)
    {
        return value <= 0 ? string.Empty : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private sealed record RequisitionMedication(
        string? Name,
        decimal? Dose,
        string? Frequency,
        string? DailyDoses,
        int? Days);

    private static void ApplyStoredKardex(FarmaciaDocumentViewModel model, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        KardexPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<KardexPayload>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException) { return; }

        if (payload is null) return;

        var f = payload.Fields;
        if (f.TryGetValue("paciente", out var p) && !string.IsNullOrWhiteSpace(p)) model.NombrePaciente = p;
        if (f.TryGetValue("asegurador", out var a) && !string.IsNullOrWhiteSpace(a)) model.Asegurador = a;
        if (f.TryGetValue("documento", out var doc) && !string.IsNullOrWhiteSpace(doc)) model.NumeroIdentificacion = doc;
        if (f.TryGetValue("direccion", out var dir)) model.Direccion = dir ?? string.Empty;
        if (f.TryGetValue("telefonos", out var tel)) model.Telefonos = tel ?? string.Empty;
        if (f.TryGetValue("diagnostico", out var diag)) { model.CodigoCie10 = string.Empty; model.DiagnosticoDescriptivo = diag ?? string.Empty; }
        if (f.TryGetValue("observaciones", out var obs)) model.Observaciones = obs;
        if (f.TryGetValue("auxiliarAsignado", out var aux)) model.AuxiliarAsignado = aux;
        if (f.TryGetValue("programador", out var prog)) model.ResponsableLlamadaBienvenida = prog;
        if (f.TryGetValue("peso", out var peso)) model.PesoKardex = peso;
        if (f.TryGetValue("cambioEquipo", out var cambio) && !string.IsNullOrWhiteSpace(cambio)) model.CambioEquipoKardex = cambio;
        if (f.TryGetValue("medicoTratante", out var medico)) model.MedicoTratanteKardex = medico;

        if (payload.MedRows is not { Count: > 0 }) return;

        var medList = model.Medicamentos.ToList();
        foreach (var sr in payload.MedRows)
        {
            if (!int.TryParse(sr.Row, out var rowNum)) continue;
            var existing = medList.FirstOrDefault(m => m.Row == rowNum);
            if (existing is null) continue;

            if (sr.Presentacion is not null) existing.Presentacion = sr.Presentacion;
            if (sr.DosisFrecuencia is not null) existing.DosisFrecuencia = sr.DosisFrecuencia;
            existing.VehiculoReconstitucion = sr.VehiculoReconstitucion ?? existing.VehiculoReconstitucion;
            existing.VolumenDilucion = sr.VolumenDilucion ?? existing.VolumenDilucion;
            existing.TiempoInfusion = sr.TiempoInfusion ?? existing.TiempoInfusion;
            existing.Fotosensible = sr.Fotosensible ?? existing.Fotosensible;
            existing.CadenaFrio = sr.CadenaFrio ?? existing.CadenaFrio;
            existing.Aislamiento = sr.Aislamiento ?? existing.Aislamiento;
            existing.Estabilidad = sr.Estabilidad ?? existing.Estabilidad;
            existing.BombaInfusion = sr.BombaInfusion ?? existing.BombaInfusion;
            if (sr.FechaInicio is not null) existing.FechaInicio = sr.FechaInicio;
            if (sr.FechaFin is not null) existing.FechaFin = sr.FechaFin;
        }
        model.Medicamentos = medList;
    }

    private sealed class RequisitionPayload
    {
        public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<RequisitionPayloadRow> Rows { get; set; } = [];
    }

    private sealed class RequisitionPayloadRow
    {
        public string? Item { get; set; }

        public string? Descripcion { get; set; }

        public string? Detalle { get; set; }

        public string? Cantidad { get; set; }

        public string? FechaInicio { get; set; }

        public string? FechaFin { get; set; }
    }

    private sealed class KardexPayload
    {
        public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public List<KardexPayloadMedRow> MedRows { get; set; } = [];
    }

    private sealed class KardexPayloadMedRow
    {
        public string? Row { get; set; }
        public string? Presentacion { get; set; }
        public string? DosisFrecuencia { get; set; }
        public string? VehiculoReconstitucion { get; set; }
        public string? VolumenDilucion { get; set; }
        public string? TiempoInfusion { get; set; }
        public string? Fotosensible { get; set; }
        public string? CadenaFrio { get; set; }
        public string? Aislamiento { get; set; }
        public string? Estabilidad { get; set; }
        public string? BombaInfusion { get; set; }
        public string? FechaInicio { get; set; }
        public string? FechaFin { get; set; }
    }

    private static string? FormatInfusionTime(Medicamento? medicamento)
    {
        if (medicamento is null || string.IsNullOrWhiteSpace(medicamento.TiempoInfusionMinutos))
        {
            return null;
        }

        return $"{medicamento.TiempoInfusionMinutos} min";
    }

    private static string FormatNullableDate(DateTime? value)
    {
        return value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string NormalizeCatalogKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : ' ');
            }
        }

        return string.Join(
            ' ',
            builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
