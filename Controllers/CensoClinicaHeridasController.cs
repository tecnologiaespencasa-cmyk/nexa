using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Models;
using Nexa.Models.ViewModels;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Nexa.Controllers;

public partial class CensoController
{
    private static readonly CultureInfo ClinicaHeridasTextCulture = CultureInfo.GetCultureInfo("es-CO");
    private static readonly string[] ClinicaHeridasAseguradorValues =
    [
        "EPS SURA",
        "PANAMERICAN LIFE",
        "PARTICULAR"
    ];
    private static readonly string[] ClinicaHeridasGeneroValues =
    [
        "Masculino",
        "Femenino"
    ];
    private static readonly string[] ClinicaHeridasLlamadaBienvenidaValues =
    [
        "Efectivo",
        "No efectivo"
    ];
    private static readonly string[] ClinicaHeridasProgramaValues =
    [
        "Agudo",
        "Cronico",
        "NPT"
    ];
    private static readonly string[] ClinicaHeridasSiNoValues = ["Si", "No"];

    // Catalogo de insumos de clinica de heridas (seccion 3). Para agregar o retirar un aposito basta
    // con editar esta lista: alimenta el autocompletado de los cuatro campos y su validacion.
    private static readonly string[] ClinicaHeridasApositoMedicamentoValues =
    [
        "ACTICOAT FLEX3 10X10CM",
        "ACTICOAT FLEX3 10X20CM",
        "ALGISITE M 15X20CM",
        "ALLEVYN ADH 10X10CM",
        "ALLEVYN ADH 12.5X12.5",
        "ALLEVYN CLASSIC AG 17.5X17.5CM",
        "ALLEVYN SACRUM SMALL 17X17CM",
        "APOSITO TRANSPARENTE TEGADERM 10 CM X 12 CM",
        "APÓSITO DUODERM CGF",
        "APÓSITO DUODERM EXTRA LARGO 15X15CMS",
        "AQUACEL AG+EXTRA CON PLATA 10X10",
        "AQUACEL AG+EXTRA CON PLATA 15X15",
        "AQUACEL AG+EXTRA CON PLATA 20X30",
        "BACTIGRAS 10X10CM",
        "BACTIGRAS 15X20CM",
        "BACTIGRAS 5X5CM",
        "CANISTER 1100ML - GENADYNE",
        "CANISTER 300ML - SMITH",
        "CANISTER 600ML GENADYNE",
        "CANISTER 800ML - SMITH",
        "CLORURO DE SODIO 0.9% 1000ML",
        "CLORURO DE SODIO 0.9% 100ML",
        "CLORURO DE SODIO 0.9% 250ML",
        "CLORURO DE SODIO 0.9% 500ML",
        "CLORURO DE SODIO 0.9% 50ML",
        "CONECTOR EN Y - GENADYNE",
        "CONECTOR Y - SMITH",
        "DURAFIBER 15X15CM",
        "DURAFIBER AG10CMX10CM",
        "ELECT HYDRO 20X20",
        "ELECT HYDRO HYDROC 10X10",
        "ELECT HYDRO HYDROC 15X15",
        "FITOSTIMOLINE CREMA X 32 GR",
        "FLEXIDRESS BOTA DE UNNA",
        "GASA ADHESIVA (ELECTOFIX) 10X10",
        "INTRASITE GEL AP25",
        "IODOSORB DRESSING 5G X5",
        "KIT APOSITO DE PLATA DE ESPUMA Y POLIURETANO TALLA L - SMITH",
        "KIT APOSITO DE PLATA DE ESPUMA Y POLIURETANO TALLA M - SMITH",
        "KIT APOSITO DE PLATA DE ESPUMA Y POLIURETANO TALLA S - SMITH",
        "KIT APOSITO DE POLIURETANO VERDE L GENADYNE",
        "KIT APOSITO DE POLIURETANO VERDE M GENADYNE",
        "KIT APOSITO DE POLIURETANO VERDE S GENADYNE",
        "OPSITE INCISE 30X28CM",
        "PASTA STOMAHESIVE TUBO X 56 GR",
        "PINZA COLOSTOMIA",
        "POLVO STOMAHESIVE FCOX29GR",
        "RENASYS SOFT PORTRENASYS"
    ];

    // Catalogo CIE10 propio del programa: clinica de heridas solo admite estos diagnosticos, no el
    // catalogo general del censo de agudos. Para agregar o retirar uno basta con editar esta lista.
    private static readonly IReadOnlyDictionary<string, string> ClinicaHeridasCie10Values =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["E105"] = "DIABETES MELLITUS, NO ESPECIFICADA CON COMPLICACIONES CIRCULATORIAS PERIFÉRICAS",
        ["I771"] = "ESTRECHEZ ARTERIAL",
        ["I830"] = "VENAS VARICOSAS DE LOS MIEMBROS INFERIORES CON ÚLCERA",
        ["K604"] = "FÍSTULA RECTAL",
        ["K632"] = "FÍSTULA DEL INTESTINO",
        ["L020"] = "ABSCESO EN CARA",
        ["L023"] = "ABSCESO DEL GLUTEO",
        ["L024"] = "ABSCESO REGION AXILIAR",
        ["L039"] = "DERMATITIS, NO ESPECIFICADA",
        ["L89X"] = "ÚLCERA DE DECÚBITO",
        ["L97X"] = "ÚLCERA DEL MIEMBRO INFERIOR NO CLASIFICADA EN OTRA PARTE",
        ["L984"] = "ÚLCERA CRÓNICA DE LA PIEL NO CLASIFICADA EN OTRA PARTE",
        ["M868"] = "OTRAS OSTEOMIELITIS",
        ["N322"] = "FÍSTULA DE LA VEJIGA",
        ["S311"] = "HERIDA DE LA PARED ABDOMINAL",
        ["T141"] = "HERIDA DE REGIÓN NO ESPECIFICADA DEL CUERPO",
        ["T813"] = "DESGARRO DE HERIDA OPERATORIA, NO CLASIFICADO EN OTRA PARTE",
        ["T958"] = "SECUELAS DE OTRAS QUEMADURAS, CORROSIONES Y CONGELAMIENTOS ESPECIFICADOS",
        ["Z430"] = "ATENCION DE TRAQUEOSTOMIA",
        ["Z431"] = "ATENCION DE GASTROSTOMIA",
        ["Z432"] = "ATENCION DE ILEOSTOMIA",
        ["Z433"] = "ATENCION DE COLOSTOMIA",
        ["Z452"] = "CONTACTO PARA AJUSTE Y MANTENIMIENTO DE DISPOSITIVO DE ACCESO VASCULAR"
    };

    private static readonly string[] ClinicaHeridasFrecuenciaVisitaValues =
    [
        "Cada 24 horas",
        "Cada 48 horas",
        "Cada 72 horas",
        "Una vez a la semana"
    ];

    private const int ClinicaHeridasMaxApositos = 4;
    private static readonly Regex ClinicaHeridasNombrePattern = new(@"^[\p{L}\s]+$", RegexOptions.Compiled);

    // Etiquetas de las cuatro fotos que la aplicación de clínica de heridas exige por seguimiento.
    // Las claves son los valores del enum TipoFotoHerida en la base de datos del portal.
    private static readonly IReadOnlyList<KeyValuePair<string, string>> ClinicaHeridasTiposFoto =
    [
        new("PLANO_GENERAL", "Plano general"),
        new("MEDIDA_VERTICAL", "Medida vertical"),
        new("MEDIDA_HORIZONTAL", "Medida horizontal"),
        new("LATERAL", "Lateral")
    ];
    private static readonly string[] ClinicaHeridasMotivoHospitalizacionValues =
    [
        "Dolor",
        "No mejoria clinica",
        "Fallas en la atención domiciliaria",
        "Infección",
        "Hospitalización programada"
    ];
    private static readonly string[] ClinicaHeridasFuenteIngresoValues =
    [
        "Asegurador",
        "Ordenamiento interno"
    ];
    private static readonly string[] ClinicaHeridasRemitidoPorValues =
    [
        "Familiar",
        "Medico IPS",
        "EMI/CEM/OTROS"
    ];
    private static readonly string[] ClinicaHeridasMotivoEgresoValues =
    [
        "CURACION",
        "FALLECE",
        "NO CUMPLE CRITERIOS",
        "AMBITO DE ATENCION NO CORRESPONDE",
        "CAMBIO DE PRESTADOR CAMBIO DE ASEGURADOR",
        "ALTA VOLUNTARIA",
        "NO APLICA",
        "ALTA MEDICA AMBULATORIA",
        "REINGRESO HOSPITALARIO",
        "ALTA MEDICA",
        "SIN COBERTURA",
        "DESMONTE",
        "CANCELAN TRAMITE DOMICILIARIO",
        "NO PERTINENCIA/LESION PALIATIVA/EDUCACION"
    ];
    private static readonly string[] ClinicaHeridasEstadoProgramaValues = ["Activo", "Inactivo"];

    [HttpGet]
    public async Task<IActionResult> ClinicaHeridas(
        string? cedulaPaciente,
        long? recordId,
        CancellationToken cancellationToken)
    {
        var model = BuildDefaultClinicaHeridasModel();
        model.CedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);

        if (recordId.HasValue)
        {
            var record = await _context.CensoClinicaHeridas
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == recordId.Value, cancellationToken);
            if (record is not null)
            {
                ApplyClinicaHeridasRecordToModel(model, record);
                model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                    ? record.NumeroIdentificacion
                    : model.CedulaFiltro;
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            var record = await _context.CensoClinicaHeridas
                .AsNoTracking()
                .Where(x => x.NumeroIdentificacion == model.CedulaFiltro)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (record is not null)
            {
                ApplyClinicaHeridasRecordToModel(model, record);
                model.CedulaFiltro = record.NumeroIdentificacion;
            }
        }

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        return View("ClinicaHeridas", model);
    }

    // Precarga de datos básicos: si el paciente ya existe en el censo de agudos o en el de crónicos
    // se reutiliza su información demográfica, de residencia y de contacto para no volver a digitarla.
    // Se combinan las dos fuentes (agudos aporta asegurador y teléfonos, crónicos aporta género) dando
    // prioridad a la del ingreso más reciente. Los valores se traducen a los catálogos de clínica de
    // heridas y los que no existen allí se devuelven vacíos para no romper la validación al guardar.
    [HttpGet]
    public async Task<IActionResult> BuscarPacienteOtrosCensos(string? numeroDocumento, CancellationToken cancellationToken)
    {
        var documento = NormalizeCedulaFilter(numeroDocumento);
        if (documento.Length < 4)
        {
            return Json(new { found = false });
        }

        var agudo = await _context.Censos
            .AsNoTracking()
            .Where(IsEditableCensoRecordExpression())
            .Where(x => x.NumeroIdentificacion == documento)
            .OrderByDescending(x => x.FechaIngreso)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var cronico = await _context.CensoCronicos
            .AsNoTracking()
            .Where(x => x.NumeroIdentificacion == documento)
            .OrderByDescending(x => x.FechaIngreso)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (agudo is null && cronico is null)
        {
            return Json(new { found = false });
        }

        var datosAgudo = agudo is null ? null : BuildClinicaHeridasPrefill(agudo);
        var datosCronico = cronico is null ? null : BuildClinicaHeridasPrefill(cronico);

        var cronicoEsMasReciente = agudo is null
            || (cronico is not null && cronico.FechaIngreso.Date >= agudo.FechaIngreso.Date);

        var datos = MergeClinicaHeridasPrefill(
            cronicoEsMasReciente ? datosCronico! : datosAgudo!,
            cronicoEsMasReciente ? datosAgudo : datosCronico);

        var origen = agudo is not null && cronico is not null
            ? "los censos de programa agudos y programa crónicos"
            : agudo is not null
                ? "el censo de programa agudos"
                : "el censo de programa crónicos";

        return Json(new
        {
            found = true,
            documento,
            origen,
            datos
        });
    }

    private sealed class ClinicaHeridasPrefillData
    {
        public string Asegurador { get; set; } = string.Empty;

        public string TipoIdentificacion { get; set; } = string.Empty;

        public string NombrePaciente { get; set; } = string.Empty;

        public string FechaNacimiento { get; set; } = string.Empty;

        public int Edad { get; set; }

        public string Genero { get; set; } = string.Empty;

        public string Direccion { get; set; } = string.Empty;

        public bool DireccionValidada { get; set; }

        public bool AsumirDireccionErrada { get; set; }

        public string DetalleDireccion { get; set; } = string.Empty;

        public string ClasificacionZonaSura { get; set; } = string.Empty;

        public string MunicipioResidencia { get; set; } = string.Empty;

        public string Barrio { get; set; } = string.Empty;

        public string ZonaDireccionSegunMunicipio { get; set; } = string.Empty;

        public string TelefonoPrincipal { get; set; } = string.Empty;

        public string TelefonoAdicional1 { get; set; } = string.Empty;

        public string TelefonoAdicional2 { get; set; } = string.Empty;

        public string LlamadaBienvenida { get; set; } = string.Empty;

        public string TelefonoContacto { get; set; } = string.Empty;
    }

    private ClinicaHeridasPrefillData BuildClinicaHeridasPrefill(CensoRecord record) => new()
    {
        Asegurador = MapToClinicaHeridasCatalogValue(record.Asegurador, ClinicaHeridasAseguradorValues),
        TipoIdentificacion = MapToClinicaHeridasCatalogValue(record.TipoIdentificacion, TiposIdentificacion),
        NombrePaciente = record.NombrePaciente?.Trim() ?? string.Empty,
        FechaNacimiento = FormatClinicaHeridasPrefillDate(record.FechaNacimiento),
        Edad = record.Edad,
        Direccion = record.Direccion?.Trim() ?? string.Empty,
        DireccionValidada = record.DireccionValidada,
        AsumirDireccionErrada = record.AsumirDireccionErrada,
        DetalleDireccion = record.DetalleDireccion?.Trim() ?? string.Empty,
        ClasificacionZonaSura = MapToClinicaHeridasCatalogValue(record.ClasificacionZonaSura, ClasificacionZonaSuraValues),
        MunicipioResidencia = MapToClinicaHeridasCatalogValue(ToCanonicalMunicipality(record.MunicipioResidencia), MunicipiosResidenciaValues),
        Barrio = record.Barrio?.Trim() ?? string.Empty,
        ZonaDireccionSegunMunicipio = MapToClinicaHeridasCatalogValue(record.ZonaDireccionSegunMunicipio, ZonaDireccionValues),
        TelefonoPrincipal = NormalizePhone(record.Telefono1),
        TelefonoAdicional1 = NormalizePhone(record.Telefono2),
        TelefonoAdicional2 = NormalizePhone(record.Telefono3),
        LlamadaBienvenida = MapToClinicaHeridasCatalogValue(record.EstadoLlamadaBienvenida, ClinicaHeridasLlamadaBienvenidaValues),
        TelefonoContacto = NormalizePhone(record.NumeroTelefonoLlamadaBienvenida)
    };

    private ClinicaHeridasPrefillData BuildClinicaHeridasPrefill(CensoCronicoRecord record) => new()
    {
        TipoIdentificacion = MapToClinicaHeridasCatalogValue(record.TipoIdentificacion, TiposIdentificacion),
        NombrePaciente = record.NombrePaciente?.Trim() ?? string.Empty,
        FechaNacimiento = FormatClinicaHeridasPrefillDate(record.FechaNacimiento),
        Edad = record.Edad,
        Genero = MapToClinicaHeridasCatalogValue(record.Genero, ClinicaHeridasGeneroValues),
        Direccion = record.Direccion?.Trim() ?? string.Empty,
        DireccionValidada = record.DireccionValidada,
        AsumirDireccionErrada = record.AsumirDireccionErrada,
        DetalleDireccion = record.DetalleDireccion?.Trim() ?? string.Empty,
        ClasificacionZonaSura = MapToClinicaHeridasCatalogValue(record.ClasificacionZonaSura, ClasificacionZonaSuraValues),
        MunicipioResidencia = MapToClinicaHeridasCatalogValue(ToCanonicalMunicipality(record.MunicipioResidencia), MunicipiosResidenciaValues),
        Barrio = record.Barrio?.Trim() ?? string.Empty,
        ZonaDireccionSegunMunicipio = MapToClinicaHeridasCatalogValue(record.ZonaDireccionSegunMunicipio, ZonaDireccionValues)
    };

    private static ClinicaHeridasPrefillData MergeClinicaHeridasPrefill(
        ClinicaHeridasPrefillData principal,
        ClinicaHeridasPrefillData? secundario)
    {
        if (secundario is null)
        {
            return principal;
        }

        static string Coalesce(string principalValue, string secundarioValue) =>
            string.IsNullOrWhiteSpace(principalValue) ? secundarioValue : principalValue;

        // La dirección y sus banderas de validación se toman como un bloque: mezclarlas dejaría una
        // dirección marcada como validada con los datos de validación de la otra fuente.
        var fuenteDireccion = string.IsNullOrWhiteSpace(principal.Direccion) ? secundario : principal;

        return new ClinicaHeridasPrefillData
        {
            Asegurador = Coalesce(principal.Asegurador, secundario.Asegurador),
            TipoIdentificacion = Coalesce(principal.TipoIdentificacion, secundario.TipoIdentificacion),
            NombrePaciente = Coalesce(principal.NombrePaciente, secundario.NombrePaciente),
            FechaNacimiento = Coalesce(principal.FechaNacimiento, secundario.FechaNacimiento),
            Edad = principal.Edad > 0 ? principal.Edad : secundario.Edad,
            Genero = Coalesce(principal.Genero, secundario.Genero),
            Direccion = fuenteDireccion.Direccion,
            DireccionValidada = fuenteDireccion.DireccionValidada,
            AsumirDireccionErrada = fuenteDireccion.AsumirDireccionErrada,
            DetalleDireccion = Coalesce(principal.DetalleDireccion, secundario.DetalleDireccion),
            ClasificacionZonaSura = Coalesce(principal.ClasificacionZonaSura, secundario.ClasificacionZonaSura),
            MunicipioResidencia = Coalesce(principal.MunicipioResidencia, secundario.MunicipioResidencia),
            Barrio = Coalesce(principal.Barrio, secundario.Barrio),
            ZonaDireccionSegunMunicipio = Coalesce(principal.ZonaDireccionSegunMunicipio, secundario.ZonaDireccionSegunMunicipio),
            TelefonoPrincipal = Coalesce(principal.TelefonoPrincipal, secundario.TelefonoPrincipal),
            TelefonoAdicional1 = Coalesce(principal.TelefonoAdicional1, secundario.TelefonoAdicional1),
            TelefonoAdicional2 = Coalesce(principal.TelefonoAdicional2, secundario.TelefonoAdicional2),
            LlamadaBienvenida = Coalesce(principal.LlamadaBienvenida, secundario.LlamadaBienvenida),
            TelefonoContacto = Coalesce(principal.TelefonoContacto, secundario.TelefonoContacto)
        };
    }

    private static string FormatClinicaHeridasPrefillDate(DateTime value)
    {
        if (value.Date <= DateTime.MinValue.Date || value.Date >= DateTime.Today)
        {
            return string.Empty;
        }

        return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    // Los censos comparten la mayoría de catálogos, pero no todos usan la misma redacción
    // ("PAN-AMERICAN LIFE DE COLOMBIA" vs "PANAMERICAN LIFE") ni las mismas opciones
    // ("Indeterminado" solo existe en crónicos). Se devuelve el valor canónico de clínica de
    // heridas o vacío cuando la opción no existe en este censo.
    private static string MapToClinicaHeridasCatalogValue(string? value, IReadOnlyList<string> catalogo)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var exacto = catalogo.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exacto is not null)
        {
            return exacto;
        }

        var clave = NormalizeClinicaHeridasCatalogKey(trimmed);
        if (clave.Length == 0)
        {
            return string.Empty;
        }

        return catalogo.FirstOrDefault(x =>
        {
            var claveCatalogo = NormalizeClinicaHeridasCatalogKey(x);
            return claveCatalogo.Length > 0
                && (claveCatalogo.StartsWith(clave, StringComparison.Ordinal)
                    || clave.StartsWith(claveCatalogo, StringComparison.Ordinal));
        }) ?? string.Empty;
    }

    private static string NormalizeClinicaHeridasCatalogKey(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    [HttpPost]
    public async Task<IActionResult> ClinicaHeridas(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        NormalizeClinicaHeridasModel(model);
        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        ValidateClinicaHeridasModel(model);

        var direccionParaGuardar = model.Direccion ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(model.Direccion))
        {
            var direccionValidation = await _addressValidationService.ValidateAddressAsync(direccionParaGuardar, cancellationToken);
            ApplyClinicaHeridasAddressValidationResult(model, direccionValidation, ref direccionParaGuardar);
        }
        else
        {
            ClearClinicaHeridasAddressModelState();
            model.DireccionEsValida = false;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = null;
            model.DireccionMensajeValidacion = null;
            direccionParaGuardar = model.Direccion ?? string.Empty;
        }

        if (!ModelState.IsValid)
        {
            await PopulateClinicaHeridasLatestRecordsAsync(model, cancellationToken);
            return View("ClinicaHeridas", model);
        }

        CensoClinicaHeridasRecord record;
        var auditAction = "CENSO_CLINICA_HERIDAS_CREADO";
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken)
                ?? new CensoClinicaHeridasRecord();
            ApplyClinicaHeridasModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: record.Id != 0);
            auditAction = record.Id == 0
                ? "CENSO_CLINICA_HERIDAS_CREADO"
                : "CENSO_CLINICA_HERIDAS_ACTUALIZADO";
            if (record.Id == 0)
            {
                await _context.CensoClinicaHeridas.AddAsync(record, cancellationToken);
            }
        }
        else
        {
            record = new CensoClinicaHeridasRecord();
            ApplyClinicaHeridasModelToRecord(model, record, direccionParaGuardar, preserveCreatedAt: false);
            await _context.CensoClinicaHeridas.AddAsync(record, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Envio inmediato al puente de Supabase. Solo encola: el guardado no
        // espera a Supabase ni falla si el puente no responde.
        _bridgeSyncQueue.Enqueue(new BridgePatient(record.NumeroIdentificacion, record.NombrePaciente));

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoClinicaHeridas",
            $"Paciente: {record.NombrePaciente}, Doc: {record.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = model.EditingRecordId.HasValue
            ? "Registro de clínica de heridas actualizado correctamente."
            : "Registro de clínica de heridas guardado correctamente.";
        return RedirectToAction(nameof(ClinicaHeridas), new { cedulaPaciente = record.NumeroIdentificacion });
    }

    // Proxy de las fotos de la herida. El navegador del usuario no tiene acceso a SharePoint, así
    // que la intranet descarga el archivo con sus credenciales de aplicación y lo reenvía. Solo
    // sirve archivos registrados como foto de seguimiento en Neon: cualquier otro identificador
    // devuelve 404 aunque exista en la biblioteca.
    [HttpGet]
    public async Task<IActionResult> FotoSeguimientoClinicaHeridas(
        string? driveItemId,
        bool miniatura,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(driveItemId))
        {
            return NotFound();
        }

        ClinicaHeridasFotoRow? foto;
        try
        {
            foto = await _neonClinicaHeridasRepository.GetFotoPorDriveItemIdAsync(driveItemId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible validar la foto de clínica de heridas en Neon.");
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        if (foto is null)
        {
            return NotFound();
        }

        var result = await _sharePointDocumentService.GetClinicaHeridasPhotoAsync(
            driveItemId,
            miniatura,
            cancellationToken);

        if (!result.Succeeded || result.Value is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        // El contenido es inmutable: cada foto vive en su propio driveItemId y el portal nunca la
        // reemplaza, así que se puede cachear en el navegador.
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(result.Value.Content, result.Value.ContentType);
    }

    // Seccion 3 "Manejo de la herida": PICC, VAC, hasta cuatro apositos/medicamentos, la duracion del
    // tratamiento y la frecuencia de visita. Se guarda con su propio boton, como las demas secciones.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasManejoHerida(
        CensoClinicaHeridasViewModel model,
        CancellationToken cancellationToken)
    {
        NormalizeClinicaHeridasManejoHeridaModel(model);
        ValidateClinicaHeridasManejoHeridaModel(model);

        var posted = (model.Picc, model.Vac, model.Npt, model.ManejoHerida,
            model.ApositoMedicamento1, model.ApositoMedicamento2,
            model.ApositoMedicamento3, model.ApositoMedicamento4, model.DuracionTratamientoDias,
            model.FrecuenciaVisita);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m =>
            {
                (m.Picc, m.Vac, m.Npt, m.ManejoHerida,
                    m.ApositoMedicamento1, m.ApositoMedicamento2,
                    m.ApositoMedicamento3, m.ApositoMedicamento4, m.DuracionTratamientoDias,
                    m.FrecuenciaVisita) = posted;
            },
            applySectionToRecord: (record, m) =>
            {
                record.Picc = m.Picc;
                record.Vac = m.Vac;
                record.Npt = m.Npt;
                record.ManejoHerida = m.ManejoHerida;
                record.ApositoMedicamento1 = m.ApositoMedicamento1;
                record.ApositoMedicamento2 = m.ApositoMedicamento2;
                record.ApositoMedicamento3 = m.ApositoMedicamento3;
                record.ApositoMedicamento4 = m.ApositoMedicamento4;
                record.DuracionTratamientoDias = m.DuracionTratamientoDias;
                record.FrecuenciaVisita = m.FrecuenciaVisita;

                // VAC manda sobre el activo fijo: al pasarlo a No esa seccion se bloquea y sus datos
                // dejan de tener sentido, asi que se limpian aqui, que es donde vive el campo.
                if (!string.Equals(m.Vac, "Si", StringComparison.OrdinalIgnoreCase))
                {
                    record.EquipoComodato = null;
                    record.NumeroPlacaEquipos = null;
                    record.FechaEntregaEquipo = null;
                    record.FechaDevolucionEquipo = null;
                }
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el manejo de la herida.",
            auditAction: "CENSO_CLINICA_HERIDAS_MANEJO_HERIDA_ACTUALIZADO",
            successMessage: "Manejo de la herida guardado correctamente.",
            cancellationToken,
            // El plan abierto refleja siempre los apósitos y el tratamiento vigentes; al cerrarse,
            // esa copia queda congelada.
            afterSaveAsync: (recordId, ct) => SincronizarPlanVigenteAsync(recordId, ct));
    }

    private static void NormalizeClinicaHeridasManejoHeridaModel(CensoClinicaHeridasViewModel model)
    {
        model.Picc = string.IsNullOrWhiteSpace(model.Picc) ? null : model.Picc.Trim();
        model.Vac = string.IsNullOrWhiteSpace(model.Vac) ? null : model.Vac.Trim();
        model.Npt = string.IsNullOrWhiteSpace(model.Npt) ? null : model.Npt.Trim();
        model.ManejoHerida = string.IsNullOrWhiteSpace(model.ManejoHerida) ? null : model.ManejoHerida.Trim();

        // Los apositos solo aplican a manejo de la herida y VAC; si ninguno esta en Si, el campo ni
        // siquiera se muestra y lo que hubiera quedado guardado se descarta.
        if (!EsSi(model.ManejoHerida) && !EsSi(model.Vac))
        {
            model.ApositoMedicamento1 = null;
            model.ApositoMedicamento2 = null;
            model.ApositoMedicamento3 = null;
            model.ApositoMedicamento4 = null;
        }

        // Los cuatro campos se compactan: si el usuario retira el segundo, el tercero pasa a ocupar
        // su lugar y no quedan huecos guardados en la base.
        var apositos = new[]
            {
                model.ApositoMedicamento1,
                model.ApositoMedicamento2,
                model.ApositoMedicamento3,
                model.ApositoMedicamento4
            }
            .Select(NormalizeOptionalClinicaHeridasText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => CanonicalClinicaHeridasAposito(value!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(ClinicaHeridasMaxApositos)
            .ToList();

        model.ApositoMedicamento1 = apositos.ElementAtOrDefault(0);
        model.ApositoMedicamento2 = apositos.ElementAtOrDefault(1);
        model.ApositoMedicamento3 = apositos.ElementAtOrDefault(2);
        model.ApositoMedicamento4 = apositos.ElementAtOrDefault(3);

        model.FrecuenciaVisita = CanonicalClinicaHeridasFrecuenciaVisita(model.FrecuenciaVisita);
    }

    // Devuelve el valor tal como esta escrito en el catalogo para no guardar variantes con distinta
    // capitalizacion. Si no existe alli se devuelve igual, y la validacion lo rechaza.
    private static string CanonicalClinicaHeridasAposito(string value)
    {
        return ClinicaHeridasApositoMedicamentoValues
            .FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase))
            ?? value;
    }

    private void ValidateClinicaHeridasManejoHeridaModel(CensoClinicaHeridasViewModel model)
    {
        if (!ClinicaHeridasSiNoValues.Contains(model.Picc ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Picc), "Selecciona una opción válida para PICC.");
        }

        if (!ClinicaHeridasSiNoValues.Contains(model.Vac ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Vac), "Selecciona una opción válida para VAC.");
        }

        if (!ClinicaHeridasSiNoValues.Contains(model.Npt ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Npt), "Selecciona una opción válida para NPT.");
        }

        if (!ClinicaHeridasSiNoValues.Contains(model.ManejoHerida ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ManejoHerida), "Selecciona una opción válida para manejo de la herida.");
        }

        var apositos = new[]
        {
            (Nombre: nameof(model.ApositoMedicamento1), Valor: model.ApositoMedicamento1),
            (Nombre: nameof(model.ApositoMedicamento2), Valor: model.ApositoMedicamento2),
            (Nombre: nameof(model.ApositoMedicamento3), Valor: model.ApositoMedicamento3),
            (Nombre: nameof(model.ApositoMedicamento4), Valor: model.ApositoMedicamento4)
        };

        foreach (var aposito in apositos)
        {
            if (!string.IsNullOrWhiteSpace(aposito.Valor)
                && !ClinicaHeridasApositoMedicamentoValues.Contains(aposito.Valor, StringComparer.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(aposito.Nombre, "Selecciona un apósito o medicamento del listado.");
            }
        }

        if (model.DuracionTratamientoDias.HasValue
            && (model.DuracionTratamientoDias.Value < 1 || model.DuracionTratamientoDias.Value > 999))
        {
            ModelState.AddModelError(
                nameof(model.DuracionTratamientoDias),
                "La duración del tratamiento debe estar entre 1 y 999 días.");
        }

        if (!string.IsNullOrWhiteSpace(model.FrecuenciaVisita)
            && !ClinicaHeridasFrecuenciaVisitaValues.Contains(model.FrecuenciaVisita, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.FrecuenciaVisita), "Selecciona una frecuencia de visita válida.");
        }
    }

    // Devuelve la frecuencia tal como esta escrita en el catalogo. Los registros anteriores al cambio
    // a seleccion unica pueden traer varias separadas por coma: se conserva la primera reconocible.
    private static string? CanonicalClinicaHeridasFrecuenciaVisita(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => ClinicaHeridasFrecuenciaVisitaValues
                .FirstOrDefault(option => string.Equals(option, item, StringComparison.OrdinalIgnoreCase))
                ?? item)
            .FirstOrDefault();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarClinicaHeridasActivoFijo(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.EquipoComodato = model.EquipoComodato?.Trim();
        model.NumeroPlacaEquipos = NormalizeOptionalClinicaHeridasText(model.NumeroPlacaEquipos);

        var postedEquipoComodato = model.EquipoComodato;
        var postedNumeroPlaca = model.NumeroPlacaEquipos;
        var postedFechaEntrega = model.FechaEntregaEquipo;
        var postedFechaDevolucion = model.FechaDevolucionEquipo;

        CensoClinicaHeridasRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, "Primero guarda los datos básicos del paciente para registrar el activo fijo.");
        }
        else
        {
            if (!string.Equals(record.Vac, "Si", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "La sección Activo fijo solo se habilita cuando el campo VAC de los datos básicos está guardado en Si.");
            }

            ApplyClinicaHeridasRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        ValidateClinicaHeridasActivoFijoModel(postedEquipoComodato, postedNumeroPlaca, postedFechaEntrega, postedFechaDevolucion);

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        model.EquipoComodato = postedEquipoComodato;
        model.NumeroPlacaEquipos = postedNumeroPlaca;
        model.FechaEntregaEquipo = postedFechaEntrega;
        model.FechaDevolucionEquipo = postedFechaDevolucion;

        if (!ModelState.IsValid)
        {
            return View("ClinicaHeridas", model);
        }

        var heridasRecord = record!;
        heridasRecord.EquipoComodato = model.EquipoComodato;
        heridasRecord.NumeroPlacaEquipos = model.NumeroPlacaEquipos;
        heridasRecord.FechaEntregaEquipo = model.FechaEntregaEquipo?.Date;
        heridasRecord.FechaDevolucionEquipo = model.FechaDevolucionEquipo?.Date;
        heridasRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync("CENSO_CLINICA_HERIDAS_ACTIVO_FIJO_ACTUALIZADO", "CensoClinicaHeridas",
            $"Paciente: {heridasRecord.NombrePaciente}, Doc: {heridasRecord.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = "Activo fijo guardado correctamente.";
        return RedirectToAction(nameof(ClinicaHeridas), new { recordId = heridasRecord.Id, cedulaPaciente = heridasRecord.NumeroIdentificacion });
    }

    private void ValidateClinicaHeridasActivoFijoModel(
        string? equipoComodato,
        string? numeroPlaca,
        DateTime? fechaEntrega,
        DateTime? fechaDevolucion)
    {
        if (!ClinicaHeridasSiNoValues.Contains(equipoComodato ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.EquipoComodato), "Selecciona si el equipo está en comodato.");
        }

        if (string.IsNullOrWhiteSpace(numeroPlaca))
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.NumeroPlacaEquipos), "Ingresa el número de placa de los equipos asignados.");
        }

        if (!fechaEntrega.HasValue)
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.FechaEntregaEquipo), "Selecciona la fecha de entrega del equipo.");
        }

        if (fechaDevolucion.HasValue
            && fechaEntrega.HasValue
            && fechaDevolucion.Value.Date < fechaEntrega.Value.Date)
        {
            ModelState.AddModelError(nameof(CensoClinicaHeridasViewModel.FechaDevolucionEquipo), "La fecha de devolución no puede ser anterior a la fecha de entrega.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasSeguimientoHospitalizado(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoHospitalizacion = string.IsNullOrWhiteSpace(model.MotivoHospitalizacion) ? null : model.MotivoHospitalizacion.Trim();
        model.RemitidoPorHospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion) ? null : model.RemitidoPorHospitalizacion.Trim();
        model.IpsIntramural = NormalizeOptionalClinicaHeridasText(model.IpsIntramural);

        if (!string.IsNullOrWhiteSpace(model.MotivoHospitalizacion)
            && !ClinicaHeridasMotivoHospitalizacionValues.Contains(model.MotivoHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoHospitalizacion), "Selecciona un motivo de hospitalización válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion)
            && !ClinicaHeridasRemitidoPorValues.Contains(model.RemitidoPorHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.RemitidoPorHospitalizacion), "Selecciona un valor válido para remitido por.");
        }

        if (!string.IsNullOrWhiteSpace(model.IpsIntramural)
            && !IpsQueRemiteValues.Contains(model.IpsIntramural, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.IpsIntramural), "Selecciona una IPS intramural válida.");
        }

        var posted = (model.FechaHospitalizacion, model.MotivoHospitalizacion, model.RemitidoPorHospitalizacion, model.IpsIntramural,
            model.FechaPrimerSeguimiento24Horas, model.FechaSegundoSeguimiento48Horas, model.FechaTercerSeguimiento72Horas,
            model.FechaCuartoSeguimientoSemana1, model.FechaQuintoSeguimientoSemana2, model.FechaSextoSeguimientoSemana3,
            model.FechaSeptimoSeguimientoSemana4);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m =>
            {
                (m.FechaHospitalizacion, m.MotivoHospitalizacion, m.RemitidoPorHospitalizacion, m.IpsIntramural,
                    m.FechaPrimerSeguimiento24Horas, m.FechaSegundoSeguimiento48Horas, m.FechaTercerSeguimiento72Horas,
                    m.FechaCuartoSeguimientoSemana1, m.FechaQuintoSeguimientoSemana2, m.FechaSextoSeguimientoSemana3,
                    m.FechaSeptimoSeguimientoSemana4) = posted;
            },
            applySectionToRecord: (record, m) =>
            {
                record.FechaHospitalizacion = m.FechaHospitalizacion?.Date;
                record.MotivoHospitalizacion = m.MotivoHospitalizacion;
                record.RemitidoPorHospitalizacion = m.RemitidoPorHospitalizacion;
                record.IpsIntramural = m.IpsIntramural;
                record.FechaPrimerSeguimiento24Horas = m.FechaPrimerSeguimiento24Horas?.Date;
                record.FechaSegundoSeguimiento48Horas = m.FechaSegundoSeguimiento48Horas?.Date;
                record.FechaTercerSeguimiento72Horas = m.FechaTercerSeguimiento72Horas?.Date;
                record.FechaCuartoSeguimientoSemana1 = m.FechaCuartoSeguimientoSemana1?.Date;
                record.FechaQuintoSeguimientoSemana2 = m.FechaQuintoSeguimientoSemana2?.Date;
                record.FechaSextoSeguimientoSemana3 = m.FechaSextoSeguimientoSemana3?.Date;
                record.FechaSeptimoSeguimientoSemana4 = m.FechaSeptimoSeguimientoSemana4?.Date;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el seguimiento hospitalizado.",
            auditAction: "CENSO_CLINICA_HERIDAS_SEGUIMIENTO_HOSPITALIZADO_ACTUALIZADO",
            successMessage: "Seguimiento hospitalizado guardado correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasDevolucionProductos(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos.Trim();
        model.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos.Trim();
        model.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico.Trim();

        if (!string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos)
            && !MotivoNovedadDevolucionProductosValues.Contains(model.MotivoNovedadDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoNovedadDevolucionProductos), "Selecciona un motivo de la novedad válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos)
            && !ClinicaHeridasSiNoValues.Contains(model.NotificacionAuxiliarDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.NotificacionAuxiliarDevolucionProductos), "Selecciona una opción válida para notificación al auxiliar.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico)
            && !EstadoDevolucionServicioFarmaceuticoValues.Contains(model.EstadoDevolucionServicioFarmaceutico, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoDevolucionServicioFarmaceutico), "Selecciona un estado de la devolución válido.");
        }

        var posted = (model.FechaNovedadDevolucionProductos, model.MotivoNovedadDevolucionProductos,
            model.NotificacionAuxiliarDevolucionProductos, model.FechaMaximaDevolucionProductos,
            model.EstadoDevolucionServicioFarmaceutico);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m =>
            {
                (m.FechaNovedadDevolucionProductos, m.MotivoNovedadDevolucionProductos,
                    m.NotificacionAuxiliarDevolucionProductos, m.FechaMaximaDevolucionProductos,
                    m.EstadoDevolucionServicioFarmaceutico) = posted;
            },
            applySectionToRecord: (record, m) =>
            {
                record.FechaNovedadDevolucionProductos = m.FechaNovedadDevolucionProductos?.Date;
                record.MotivoNovedadDevolucionProductos = m.MotivoNovedadDevolucionProductos;
                record.NotificacionAuxiliarDevolucionProductos = m.NotificacionAuxiliarDevolucionProductos;
                record.FechaMaximaDevolucionProductos = m.FechaMaximaDevolucionProductos?.Date;
                record.EstadoDevolucionServicioFarmaceutico = m.EstadoDevolucionServicioFarmaceutico;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar la devolución de productos.",
            auditAction: "CENSO_CLINICA_HERIDAS_DEVOLUCION_PRODUCTOS_ACTUALIZADA",
            successMessage: "Devolución de productos guardada correctamente.",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GuardarClinicaHeridasAltaPrograma(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.MotivoEgreso = string.IsNullOrWhiteSpace(model.MotivoEgreso) ? null : model.MotivoEgreso.Trim();
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !ClinicaHeridasMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo del egreso válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !ClinicaHeridasEstadoProgramaValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        var posted = (model.MotivoEgreso, model.FechaEgreso, model.Estado);

        return GuardarClinicaHeridasSeccionAsync(
            model,
            restorePostedFields: m => (m.MotivoEgreso, m.FechaEgreso, m.Estado) = posted,
            applySectionToRecord: (record, m) =>
            {
                record.MotivoEgreso = m.MotivoEgreso;
                record.FechaEgreso = m.FechaEgreso?.Date;
                record.Estado = string.IsNullOrWhiteSpace(m.Estado) ? record.Estado : m.Estado;
            },
            missingRecordMessage: "Primero guarda los datos básicos del paciente para registrar el alta del programa.",
            auditAction: "CENSO_CLINICA_HERIDAS_ALTA_PROGRAMA_ACTUALIZADA",
            successMessage: "Alta del programa guardada correctamente.",
            cancellationToken);
    }

    private async Task<IActionResult> GuardarClinicaHeridasSeccionAsync(
        CensoClinicaHeridasViewModel model,
        Action<CensoClinicaHeridasViewModel> restorePostedFields,
        Action<CensoClinicaHeridasRecord, CensoClinicaHeridasViewModel> applySectionToRecord,
        string missingRecordMessage,
        string auditAction,
        string successMessage,
        CancellationToken cancellationToken,
        Func<long, CancellationToken, Task>? afterSaveAsync = null)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        CensoClinicaHeridasRecord? record = null;
        if (model.EditingRecordId.HasValue)
        {
            record = await _context.CensoClinicaHeridas
                .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);
        }

        if (record is null)
        {
            ModelState.AddModelError(string.Empty, missingRecordMessage);
        }
        else
        {
            ApplyClinicaHeridasRecordToModel(model, record);
            model.CedulaFiltro = string.IsNullOrWhiteSpace(model.CedulaFiltro)
                ? record.NumeroIdentificacion
                : model.CedulaFiltro;
        }

        await PopulateClinicaHeridasDropdownsAsync(model, cancellationToken);
        restorePostedFields(model);

        if (!ModelState.IsValid)
        {
            return View("ClinicaHeridas", model);
        }

        var heridasRecord = record!;
        applySectionToRecord(heridasRecord, model);
        heridasRecord.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        if (afterSaveAsync is not null)
        {
            await afterSaveAsync(heridasRecord.Id, cancellationToken);
        }

        var auditUserId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUid) ? (Guid?)parsedUid : null;
        var auditIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditService.LogAsync(auditAction, "CensoClinicaHeridas",
            $"Paciente: {heridasRecord.NombrePaciente}, Doc: {heridasRecord.NumeroIdentificacion}",
            auditUserId, auditIp, cancellationToken);

        TempData["SuccessMessage"] = successMessage;
        return RedirectToAction(nameof(ClinicaHeridas), new { recordId = heridasRecord.Id, cedulaPaciente = heridasRecord.NumeroIdentificacion });
    }

    // Sección 2 "Características de la herida": historial de solo lectura.
    //
    // El documento del paciente no existe en la base de datos del portal: allí cada paciente se
    // identifica con un seudónimo aleatorio (pacienteRef) que la intranet no puede calcular. El
    // enlace disponible es la carpeta de SharePoint que el portal crea por paciente y nombra
    // "NOMBRE - DOCUMENTO": se busca por documento, y con su driveItemId se leen en Neon el
    // paciente, sus seguimientos y las fotos de cada uno.
    private async Task PopulateClinicaHeridasHistorialAsync(
        CensoClinicaHeridasViewModel model,
        CancellationToken cancellationToken)
    {
        model.Historial = new CensoClinicaHeridasHistorialViewModel();

        if (!model.EditingRecordId.HasValue)
        {
            return;
        }

        var record = await _context.CensoClinicaHeridas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == model.EditingRecordId.Value, cancellationToken);

        if (record is null || string.IsNullOrWhiteSpace(record.NumeroIdentificacion))
        {
            return;
        }

        var carpeta = await _sharePointDocumentService.FindClinicaHeridasPatientFolderAsync(
            record.NumeroIdentificacion,
            cancellationToken);

        if (!carpeta.Succeeded)
        {
            model.Historial.Error = carpeta.ErrorMessage;
            return;
        }

        if (carpeta.Value is null || string.IsNullOrWhiteSpace(carpeta.Value.Id))
        {
            return;
        }

        model.Historial.CarpetaWebUrl = carpeta.Value.WebUrl;
        model.Historial.CarpetaNombre = carpeta.Value.Name;

        IReadOnlyList<ClinicaHeridasSeguimientoRow> seguimientos;
        try
        {
            seguimientos = await _neonClinicaHeridasRepository.GetSeguimientosPorCarpetaAsync(
                carpeta.Value.Id,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible leer los seguimientos de clínica de heridas desde Neon.");
            model.Historial.Error = "No fue posible consultar los seguimientos registrados en la aplicación de clínica de heridas.";
            return;
        }

        model.Historial.Seguimientos = BuildClinicaHeridasSeguimientos(seguimientos);
    }

    private static IReadOnlyList<CensoClinicaHeridasSeguimientoViewModel> BuildClinicaHeridasSeguimientos(
        IReadOnlyList<ClinicaHeridasSeguimientoRow> filas)
    {
        var seguimientos = filas
            .Select(fila => new CensoClinicaHeridasSeguimientoViewModel
            {
                Id = fila.Id,
                Numero = fila.Numero,
                RegistradoEn = ToColombiaTime(fila.CreatedAtUtc),
                Origen = fila.Origen,
                Ubicacion = fila.Ubicacion,
                DiametroVerticalCm = fila.DiametroVerticalCm,
                DiametroHorizontalCm = fila.DiametroHorizontalCm,
                ProfundidadCm = fila.ProfundidadCm,
                Fondo = fila.Fondo,
                Lecho = fila.Lecho,
                Tejido = fila.Tejido,
                CavitacionTunelizacion = fila.CavitacionTunelizacion,
                PielPerilesional = fila.PielPerilesional,
                ExudadoCantidad = fila.ExudadoCantidad,
                ExudadoCaracteristicas = fila.ExudadoCaracteristicas,
                AuxiliarNombre = fila.AuxiliarNombre,
                AuxiliarProfesion = fila.AuxiliarProfesion,
                AuxiliarCedula = fila.AuxiliarCedula,
                AuxiliarEmail = fila.AuxiliarEmail,
                Fotos = BuildClinicaHeridasFotos(fila.Fotos)
            })
            .ToList();

        // Las filas llegan del más reciente al más antiguo, así que el anterior de cada seguimiento
        // es el siguiente de la lista.
        for (var index = 0; index < seguimientos.Count - 1; index++)
        {
            var anterior = seguimientos[index + 1];
            if (anterior.AreaCm2 > 0)
            {
                seguimientos[index].VariacionAreaPorcentaje =
                    (seguimientos[index].AreaCm2 - anterior.AreaCm2) / anterior.AreaCm2 * 100d;
            }
        }

        return seguimientos;
    }

    // Siempre se muestran las cuatro posiciones de foto, aunque alguna falte, para que se note
    // cuando un seguimiento quedó incompleto.
    private static IReadOnlyList<CensoClinicaHeridasFotoViewModel> BuildClinicaHeridasFotos(
        IReadOnlyList<ClinicaHeridasFotoRow> fotos)
    {
        return ClinicaHeridasTiposFoto
            .Select(tipo =>
            {
                var foto = fotos.FirstOrDefault(x =>
                    string.Equals(x.Tipo, tipo.Key, StringComparison.OrdinalIgnoreCase));

                return new CensoClinicaHeridasFotoViewModel
                {
                    Tipo = tipo.Key,
                    TipoDescripcion = tipo.Value,
                    DriveItemId = foto?.DriveItemId ?? string.Empty,
                    Nombre = foto?.Nombre ?? string.Empty
                };
            })
            .ToList();
    }

    private static DateTime ToColombiaTime(DateTime utcValue)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcValue, DateTimeKind.Utc),
            ColombiaTimeZone);
    }

    private CensoClinicaHeridasViewModel BuildDefaultClinicaHeridasModel()
    {
        var today = GetColombiaNow().Date;
        return new CensoClinicaHeridasViewModel
        {
            FechaIngresoPrograma = today,
            FechaNacimiento = today,
            FechaValoracion = today,
            DireccionEsValida = false
        };
    }

    private async Task PopulateClinicaHeridasDropdownsAsync(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.AseguradorOptions = BuildOptions(ClinicaHeridasAseguradorValues);
        model.FuenteIngresoOptions = BuildOptions(ClinicaHeridasFuenteIngresoValues);
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.GeneroOptions = BuildOptions(ClinicaHeridasGeneroValues);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.LlamadaBienvenidaOptions = BuildOptions(ClinicaHeridasLlamadaBienvenidaValues);
        model.ProgramaPerteneceOptions = BuildOptions(ClinicaHeridasProgramaValues);
        model.AuxiliarEnfermeriaOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.SiNoOptions = BuildOptions(ClinicaHeridasSiNoValues);
        model.ApositoMedicamentoOptions = ClinicaHeridasApositoMedicamentoValues;
        model.Cie10Options = BuildClinicaHeridasCie10Options(model.CodigoCie10);
        model.FrecuenciaVisitaOptions = ClinicaHeridasFrecuenciaVisitaValues;
        model.MotivoHospitalizacionOptions = BuildOptions(ClinicaHeridasMotivoHospitalizacionValues);
        model.RemitidoPorHospitalizacionOptions = BuildOptions(ClinicaHeridasRemitidoPorValues);
        model.IpsIntramuralOptions = BuildOptions(IpsQueRemiteValues);
        model.MotivoNovedadDevolucionOptions = BuildOptions(MotivoNovedadDevolucionProductosValues);
        model.EstadoDevolucionOptions = BuildOptions(EstadoDevolucionServicioFarmaceuticoValues);
        model.MotivoEgresoOptions = BuildOptions(ClinicaHeridasMotivoEgresoValues);
        model.EstadoProgramaOptions = BuildOptions(ClinicaHeridasEstadoProgramaValues);

        model.MunicipioResidencia = ToCanonicalMunicipality(model.MunicipioResidencia);

        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            && string.IsNullOrWhiteSpace(model.ClasificacionZonaSura))
        {
            model.ClasificacionZonaSura = InferClasificacionZonaSura(model.MunicipioResidencia);
        }

        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            && string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio))
        {
            model.ZonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(model.MunicipioResidencia, model.Barrio, direccion: model.Direccion);
        }

        if (!string.IsNullOrWhiteSpace(model.CodigoCie10))
        {
            model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
            if (string.IsNullOrWhiteSpace(model.DiagnosticoDescriptivo)
                && ClinicaHeridasCie10Values.TryGetValue(model.CodigoCie10, out var diagnostico))
            {
                model.DiagnosticoDescriptivo = diagnostico;
            }
        }

        IReadOnlyList<string> barrioOptions = string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            ? []
            : await _addressValidationService.SearchNeighborhoodsAsync(
                model.MunicipioResidencia,
                string.IsNullOrWhiteSpace(model.Barrio) ? "a" : model.Barrio,
                cancellationToken);

        if (barrioOptions.Count == 0)
        {
            barrioOptions = ["NO PARAMETRIZADO"];
        }

        if (!string.IsNullOrWhiteSpace(model.Barrio)
            && !barrioOptions.Contains(model.Barrio, StringComparer.OrdinalIgnoreCase))
        {
            barrioOptions = barrioOptions
                .Concat([model.Barrio])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
        }

        model.BarrioOptions = barrioOptions;
        await PopulateClinicaHeridasLatestRecordsAsync(model, cancellationToken);
        await PopulateClinicaHeridasHistorialAsync(model, cancellationToken);
        await PopulateClinicaHeridasKardexAsync(model, cancellationToken);
    }

    private async Task PopulateClinicaHeridasLatestRecordsAsync(CensoClinicaHeridasViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);

        var query = _context.CensoClinicaHeridas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            query = query.Where(x => x.NumeroIdentificacion == model.CedulaFiltro);
        }

        model.UltimosRegistros = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    private void NormalizeClinicaHeridasModel(CensoClinicaHeridasViewModel model)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.Asegurador = model.Asegurador?.Trim() ?? string.Empty;
        model.FuenteIngreso = string.IsNullOrWhiteSpace(model.FuenteIngreso) ? null : model.FuenteIngreso.Trim();
        model.TipoIdentificacion = NormalizeClinicaHeridasText(model.TipoIdentificacion);
        model.NumeroIdentificacion = NormalizeIdentificationNumber(model.TipoIdentificacion, model.NumeroIdentificacion);
        model.NombrePaciente = NormalizeClinicaHeridasText(model.NombrePaciente);
        model.Genero = model.Genero?.Trim() ?? string.Empty;
        model.Direccion = NormalizeClinicaHeridasText(model.Direccion);
        model.DetalleDireccion = NormalizeOptionalClinicaHeridasText(model.DetalleDireccion);
        model.ClasificacionZonaSura = model.ClasificacionZonaSura?.Trim() ?? string.Empty;
        model.MunicipioResidencia = model.MunicipioResidencia?.Trim() ?? string.Empty;
        model.Barrio = NormalizeClinicaHeridasText(model.Barrio);
        model.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio?.Trim() ?? string.Empty;
        model.TelefonoPrincipal = NormalizePhone(model.TelefonoPrincipal);
        model.TelefonoAdicional1 = NormalizePhone(model.TelefonoAdicional1);
        model.TelefonoAdicional2 = string.IsNullOrWhiteSpace(model.TelefonoAdicional2) ? null : NormalizePhone(model.TelefonoAdicional2);
        model.LlamadaBienvenida = model.LlamadaBienvenida?.Trim();
        model.TelefonoContacto = string.IsNullOrWhiteSpace(model.TelefonoContacto) ? null : NormalizePhone(model.TelefonoContacto);
        model.Observacion = NormalizeOptionalClinicaHeridasText(model.Observacion);
        model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
        model.DiagnosticoDescriptivo = NormalizeOptionalClinicaHeridasText(model.DiagnosticoDescriptivo);
        model.ProgramaPertenece = model.ProgramaPertenece?.Trim() ?? string.Empty;
        model.AuxiliarEnfermeriaAsignado = NormalizeOptionalClinicaHeridasText(model.AuxiliarEnfermeriaAsignado);
        NormalizeClinicaHeridasManejoHeridaModel(model);
        model.EquipoComodato = string.IsNullOrWhiteSpace(model.EquipoComodato) ? null : model.EquipoComodato.Trim();
        model.NumeroPlacaEquipos = NormalizeOptionalClinicaHeridasText(model.NumeroPlacaEquipos);
        model.MotivoHospitalizacion = string.IsNullOrWhiteSpace(model.MotivoHospitalizacion) ? null : model.MotivoHospitalizacion.Trim();
        model.RemitidoPorHospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion) ? null : model.RemitidoPorHospitalizacion.Trim();
        model.IpsIntramural = NormalizeOptionalClinicaHeridasText(model.IpsIntramural);
        model.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos.Trim();
        model.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos.Trim();
        model.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico.Trim();
        model.MotivoEgreso = string.IsNullOrWhiteSpace(model.MotivoEgreso) ? null : model.MotivoEgreso.Trim();
        model.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado.Trim();

        model.Edad = CalculateAge(model.FechaNacimiento.Date, GetColombiaNow().Date);
        ModelState.Remove(nameof(model.Edad));
    }

    /// <summary>
    /// Opciones del desplegable. Si el registro trae un código que ya no está en el catálogo (por
    /// ejemplo, uno anterior al recorte), se agrega al final para no perderlo de vista.
    /// </summary>
    private static IReadOnlyList<SelectListItem> BuildClinicaHeridasCie10Options(string? codigoActual)
    {
        var opciones = ClinicaHeridasCie10Values
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new SelectListItem($"{x.Key} - {x.Value}", x.Key))
            .ToList();

        var codigo = NormalizeCie10(codigoActual ?? string.Empty);
        if (codigo.Length > 0 && !ClinicaHeridasCie10Values.ContainsKey(codigo))
        {
            opciones.Add(new SelectListItem($"{codigo} - (fuera del listado actual)", codigo));
        }

        return opciones;
    }

    /// <summary>
    /// True cuando el código enviado es exactamente el que ya tenía el registro guardado. Permite
    /// conservar diagnósticos históricos sin abrir la puerta a códigos nuevos fuera del catálogo.
    /// </summary>
    private bool EsCie10HeredadoDelRegistro(CensoClinicaHeridasViewModel model)
    {
        if (!model.EditingRecordId.HasValue || string.IsNullOrWhiteSpace(model.CodigoCie10))
        {
            return false;
        }

        var guardado = _context.CensoClinicaHeridas
            .AsNoTracking()
            .Where(x => x.Id == model.EditingRecordId.Value)
            .Select(x => x.CodigoCie10)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(guardado)
            && string.Equals(guardado, model.CodigoCie10, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeClinicaHeridasText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpper(ClinicaHeridasTextCulture);
    }

    private static string? NormalizeOptionalClinicaHeridasText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpper(ClinicaHeridasTextCulture);
    }

    private void ValidateClinicaHeridasModel(CensoClinicaHeridasViewModel model)
    {
        if (!ClinicaHeridasAseguradorValues.Contains(model.Asegurador, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Asegurador), "Selecciona un asegurador válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.FuenteIngreso)
            && !ClinicaHeridasFuenteIngresoValues.Contains(model.FuenteIngreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.FuenteIngreso), "Selecciona una fuente de ingreso válida.");
        }

        if (!TiposIdentificacion.Contains(model.TipoIdentificacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.TipoIdentificacion), "Selecciona un tipo de identificación válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.NumeroIdentificacion))
        {
            if (AllowsAlphaNumericIdentification(model.TipoIdentificacion))
            {
                if (!AlphaNumericIdentificationPattern.IsMatch(model.NumeroIdentificacion))
                {
                    ModelState.AddModelError(nameof(model.NumeroIdentificacion), "El número de documento solo permite letras y dígitos para PA o CE.");
                }
            }
            else if (!NumericIdentificationPattern.IsMatch(model.NumeroIdentificacion))
            {
                ModelState.AddModelError(nameof(model.NumeroIdentificacion), "El número de documento solo permite dígitos.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.NombrePaciente)
            && !ClinicaHeridasNombrePattern.IsMatch(model.NombrePaciente))
        {
            ModelState.AddModelError(nameof(model.NombrePaciente), "El nombre del paciente solo permite letras y espacios.");
        }

        if (model.FechaNacimiento.Date >= GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento debe ser anterior a la fecha actual.");
        }

        if (model.FechaIngresoPrograma.Date > GetColombiaNow().Date)
        {
            ModelState.AddModelError(nameof(model.FechaIngresoPrograma), "La fecha de ingreso al programa no puede ser futura.");
        }

        if (!ClinicaHeridasGeneroValues.Contains(model.Genero, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Genero), "Selecciona un género válido.");
        }

        if (ClinicaHeridasCie10Values.TryGetValue(model.CodigoCie10 ?? string.Empty, out var diagnostico))
        {
            model.DiagnosticoDescriptivo = NormalizeClinicaHeridasText(diagnostico);
        }
        else if (EsCie10HeredadoDelRegistro(model))
        {
            // Un registro anterior al recorte del catálogo conserva su código: se puede seguir
            // editando el resto de la ficha sin obligar a reclasificar el diagnóstico.
            model.DiagnosticoDescriptivo = NormalizeClinicaHeridasText(model.DiagnosticoDescriptivo);
        }
        else
        {
            model.DiagnosticoDescriptivo = string.Empty;
            ModelState.AddModelError(
                nameof(model.CodigoCie10),
                "Selecciona un diagnóstico del listado de clínica de heridas.");
        }

        if (!ClinicaHeridasProgramaValues.Contains(model.ProgramaPertenece, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ProgramaPertenece), "Selecciona un programa válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.LlamadaBienvenida)
            && !ClinicaHeridasLlamadaBienvenidaValues.Contains(model.LlamadaBienvenida, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.LlamadaBienvenida), "Selecciona un estado de llamada de bienvenida válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.AuxiliarEnfermeriaAsignado))
        {
            if (!model.AuxiliarEnfermeriaOptions.Any())
            {
                ModelState.AddModelError(nameof(model.AuxiliarEnfermeriaAsignado), "No hay auxiliares OPS activos para asignar.");
            }
            else
            {
                var canonicalAuxiliar = model.AuxiliarEnfermeriaOptions
                    .Select(x => x.Value)
                    .FirstOrDefault(x => string.Equals(x, model.AuxiliarEnfermeriaAsignado, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(canonicalAuxiliar))
                {
                    ModelState.AddModelError(nameof(model.AuxiliarEnfermeriaAsignado), "Selecciona un auxiliar OPS válido.");
                }
                else
                {
                    model.AuxiliarEnfermeriaAsignado = canonicalAuxiliar;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(model.EquipoComodato)
            && !ClinicaHeridasSiNoValues.Contains(model.EquipoComodato, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EquipoComodato), "Selecciona una opción válida para equipo en comodato.");
        }

        if (model.FechaDevolucionEquipo.HasValue
            && model.FechaEntregaEquipo.HasValue
            && model.FechaDevolucionEquipo.Value.Date < model.FechaEntregaEquipo.Value.Date)
        {
            ModelState.AddModelError(nameof(model.FechaDevolucionEquipo), "La fecha de devolución no puede ser anterior a la fecha de entrega.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoHospitalizacion)
            && !ClinicaHeridasMotivoHospitalizacionValues.Contains(model.MotivoHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoHospitalizacion), "Selecciona un motivo de hospitalización válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.RemitidoPorHospitalizacion)
            && !ClinicaHeridasRemitidoPorValues.Contains(model.RemitidoPorHospitalizacion, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.RemitidoPorHospitalizacion), "Selecciona un valor válido para remitido por.");
        }

        if (!string.IsNullOrWhiteSpace(model.IpsIntramural)
            && !IpsQueRemiteValues.Contains(model.IpsIntramural, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.IpsIntramural), "Selecciona una IPS intramural válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos)
            && !MotivoNovedadDevolucionProductosValues.Contains(model.MotivoNovedadDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoNovedadDevolucionProductos), "Selecciona un motivo de la novedad válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos)
            && !ClinicaHeridasSiNoValues.Contains(model.NotificacionAuxiliarDevolucionProductos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.NotificacionAuxiliarDevolucionProductos), "Selecciona una opción válida para notificación al auxiliar.");
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico)
            && !EstadoDevolucionServicioFarmaceuticoValues.Contains(model.EstadoDevolucionServicioFarmaceutico, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoDevolucionServicioFarmaceutico), "Selecciona un estado de la devolución válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.MotivoEgreso)
            && !ClinicaHeridasMotivoEgresoValues.Contains(model.MotivoEgreso, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MotivoEgreso), "Selecciona un motivo del egreso válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !ClinicaHeridasEstadoProgramaValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        ValidatePhoneValue(model.TelefonoPrincipal, nameof(model.TelefonoPrincipal), "teléfono principal");
        ValidatePhoneValue(model.TelefonoAdicional1, nameof(model.TelefonoAdicional1), "teléfono adicional 1");
        ValidatePhoneValue(model.TelefonoAdicional2, nameof(model.TelefonoAdicional2), "teléfono adicional 2");
        ValidatePhoneValue(model.TelefonoContacto, nameof(model.TelefonoContacto), "teléfono de contacto");

        ValidateClinicaHeridasAddressDropdowns(model);
    }

    private void ValidateClinicaHeridasAddressDropdowns(CensoClinicaHeridasViewModel model)
    {
        model.MunicipioResidencia = ToCanonicalMunicipality(model.MunicipioResidencia) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia)
            && !MunicipiosResidenciaValues.Contains(model.MunicipioResidencia, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.MunicipioResidencia), "Selecciona un municipio válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.ClasificacionZonaSura)
            && !ClasificacionZonaSuraValues.Contains(model.ClasificacionZonaSura, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ClasificacionZonaSura), "Selecciona una clasificación zona Sura válida.");
        }

        if (!string.IsNullOrWhiteSpace(model.MunicipioResidencia))
        {
            var zonaInferida = InferZonaDireccionSegunMunicipio(model.MunicipioResidencia, model.Barrio, direccion: model.Direccion);
            if (!string.Equals(zonaInferida, "No Parametrizado", StringComparison.OrdinalIgnoreCase))
            {
                model.ZonaDireccionSegunMunicipio = zonaInferida;
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio)
            && !ZonaDireccionValues.Contains(model.ZonaDireccionSegunMunicipio, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ZonaDireccionSegunMunicipio), "Selecciona una zona de dirección válida.");
        }
    }

    private void ClearClinicaHeridasAddressModelState()
    {
        foreach (var key in new[]
        {
            nameof(CensoClinicaHeridasViewModel.Direccion),
            nameof(CensoClinicaHeridasViewModel.ClasificacionZonaSura),
            nameof(CensoClinicaHeridasViewModel.MunicipioResidencia),
            nameof(CensoClinicaHeridasViewModel.Barrio),
            nameof(CensoClinicaHeridasViewModel.ZonaDireccionSegunMunicipio)
        })
        {
            ModelState.Remove(key);
        }
    }

    private void ApplyClinicaHeridasAddressValidationResult(
        CensoClinicaHeridasViewModel model,
        AddressValidationResult direccionValidation,
        ref string direccionParaGuardar)
    {
        if (direccionValidation.Outcome == AddressValidationOutcome.Valid)
        {
            model.DireccionEsValida = true;
            model.AsumirDireccionErrada = false;
            model.DireccionSugerida = direccionValidation.FormattedAddress;
            model.DireccionMensajeValidacion = direccionValidation.Message;

            if (!string.IsNullOrWhiteSpace(direccionValidation.FormattedAddress))
            {
                direccionParaGuardar = direccionValidation.FormattedAddress;
                model.Direccion = direccionParaGuardar;
            }

            ApplyClinicaHeridasAddressLocationDefaults(model, direccionValidation);
            return;
        }

        model.DireccionEsValida = false;
        model.DireccionSugerida = direccionValidation.SuggestedAddress;
        model.DireccionMensajeValidacion = direccionValidation.Message;
        ApplyClinicaHeridasAddressLocationDefaults(model, direccionValidation);

        if (model.AsumirDireccionErrada)
        {
            return;
        }

        var mensaje = direccionValidation.Message;
        if (!string.IsNullOrWhiteSpace(direccionValidation.SuggestedAddress))
        {
            mensaje += $" Sugerencia: {direccionValidation.SuggestedAddress}.";
        }

        mensaje += " Corrige la dirección o marca 'Asumir dirección errada y continuar'.";
        ModelState.AddModelError(nameof(model.Direccion), mensaje);
    }

    private void ApplyClinicaHeridasAddressLocationDefaults(CensoClinicaHeridasViewModel model, AddressValidationResult validation)
    {
        var canonicalMunicipio = ToCanonicalMunicipality(validation.Municipality);
        if (!string.IsNullOrWhiteSpace(canonicalMunicipio))
        {
            model.MunicipioResidencia = canonicalMunicipio;
            model.ClasificacionZonaSura = InferClasificacionZonaSura(canonicalMunicipio);
        }

        if (string.IsNullOrWhiteSpace(model.Barrio) && !string.IsNullOrWhiteSpace(validation.Neighborhood))
        {
            model.Barrio = validation.Neighborhood.Trim();
        }

        if (!string.IsNullOrWhiteSpace(canonicalMunicipio))
        {
            var zonaInferida = InferZonaDireccionSegunMunicipio(
                canonicalMunicipio,
                model.Barrio,
                validation.District,
                validation.FormattedAddress);

            if (string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio)
                || string.Equals(model.ZonaDireccionSegunMunicipio, "No Parametrizado", StringComparison.OrdinalIgnoreCase))
            {
                model.ZonaDireccionSegunMunicipio = zonaInferida;
            }
        }
    }

    private static void ApplyClinicaHeridasModelToRecord(
        CensoClinicaHeridasViewModel model,
        CensoClinicaHeridasRecord record,
        string direccionParaGuardar,
        bool preserveCreatedAt)
    {
        record.Asegurador = model.Asegurador;
        record.FuenteIngreso = string.IsNullOrWhiteSpace(model.FuenteIngreso) ? null : model.FuenteIngreso;
        record.FechaIngresoPrograma = model.FechaIngresoPrograma.Date;
        record.TipoIdentificacion = model.TipoIdentificacion;
        record.NumeroIdentificacion = model.NumeroIdentificacion;
        record.NombrePaciente = model.NombrePaciente;
        record.FechaNacimiento = model.FechaNacimiento.Date;
        record.Edad = model.Edad;
        record.Genero = model.Genero;
        record.Direccion = NormalizeOptionalClinicaHeridasText(direccionParaGuardar);
        record.DireccionValidada = model.DireccionEsValida;
        record.AsumirDireccionErrada = model.AsumirDireccionErrada;
        record.DetalleDireccion = model.DetalleDireccion;
        record.ClasificacionZonaSura = string.IsNullOrWhiteSpace(model.ClasificacionZonaSura) ? null : model.ClasificacionZonaSura;
        record.MunicipioResidencia = string.IsNullOrWhiteSpace(model.MunicipioResidencia) ? null : model.MunicipioResidencia;
        record.Barrio = string.IsNullOrWhiteSpace(model.Barrio) ? null : model.Barrio;
        record.ZonaDireccionSegunMunicipio = string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio) ? null : model.ZonaDireccionSegunMunicipio;
        record.TelefonoPrincipal = model.TelefonoPrincipal;
        record.TelefonoAdicional1 = model.TelefonoAdicional1;
        record.TelefonoAdicional2 = model.TelefonoAdicional2;
        record.LlamadaBienvenida = string.IsNullOrWhiteSpace(model.LlamadaBienvenida) ? null : model.LlamadaBienvenida;
        record.TelefonoContacto = model.TelefonoContacto;
        record.Observacion = model.Observacion;
        record.CodigoCie10 = model.CodigoCie10;
        record.DiagnosticoDescriptivo = model.DiagnosticoDescriptivo ?? string.Empty;
        record.FechaValoracion = model.FechaValoracion.Date;
        record.ProgramaPertenece = model.ProgramaPertenece;
        record.AuxiliarEnfermeriaAsignado = model.AuxiliarEnfermeriaAsignado;
        record.EquipoComodato = model.EquipoComodato;
        record.NumeroPlacaEquipos = model.NumeroPlacaEquipos;
        record.FechaEntregaEquipo = model.FechaEntregaEquipo?.Date;
        record.FechaDevolucionEquipo = model.FechaDevolucionEquipo?.Date;
        record.FechaHospitalizacion = model.FechaHospitalizacion?.Date;
        record.MotivoHospitalizacion = model.MotivoHospitalizacion;
        record.RemitidoPorHospitalizacion = model.RemitidoPorHospitalizacion;
        record.IpsIntramural = model.IpsIntramural;
        record.FechaPrimerSeguimiento24Horas = model.FechaPrimerSeguimiento24Horas?.Date;
        record.FechaSegundoSeguimiento48Horas = model.FechaSegundoSeguimiento48Horas?.Date;
        record.FechaTercerSeguimiento72Horas = model.FechaTercerSeguimiento72Horas?.Date;
        record.FechaCuartoSeguimientoSemana1 = model.FechaCuartoSeguimientoSemana1?.Date;
        record.FechaQuintoSeguimientoSemana2 = model.FechaQuintoSeguimientoSemana2?.Date;
        record.FechaSextoSeguimientoSemana3 = model.FechaSextoSeguimientoSemana3?.Date;
        record.FechaSeptimoSeguimientoSemana4 = model.FechaSeptimoSeguimientoSemana4?.Date;
        record.FechaNovedadDevolucionProductos = model.FechaNovedadDevolucionProductos?.Date;
        record.MotivoNovedadDevolucionProductos = model.MotivoNovedadDevolucionProductos;
        record.NotificacionAuxiliarDevolucionProductos = model.NotificacionAuxiliarDevolucionProductos;
        record.FechaMaximaDevolucionProductos = model.FechaMaximaDevolucionProductos?.Date;
        record.EstadoDevolucionServicioFarmaceutico = model.EstadoDevolucionServicioFarmaceutico;
        record.MotivoEgreso = model.MotivoEgreso;
        record.FechaEgreso = model.FechaEgreso?.Date;
        record.Estado = string.IsNullOrWhiteSpace(model.Estado)
            ? (preserveCreatedAt ? record.Estado : "Activo")
            : model.Estado;

        if (preserveCreatedAt)
        {
            record.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            record.CreatedAtUtc = DateTime.UtcNow;
        }
    }

    private static void ApplyClinicaHeridasRecordToModel(
        CensoClinicaHeridasViewModel model,
        CensoClinicaHeridasRecord record)
    {
        model.EditingRecordId = record.Id;
        model.Asegurador = record.Asegurador;
        model.FuenteIngreso = record.FuenteIngreso;
        model.FechaIngresoPrograma = record.FechaIngresoPrograma.Date;
        model.TipoIdentificacion = record.TipoIdentificacion;
        model.NumeroIdentificacion = record.NumeroIdentificacion;
        model.NombrePaciente = record.NombrePaciente;
        model.FechaNacimiento = record.FechaNacimiento.Date;
        model.Edad = record.Edad;
        model.Genero = record.Genero;
        model.Direccion = record.Direccion;
        model.DireccionEsValida = record.DireccionValidada;
        model.AsumirDireccionErrada = record.AsumirDireccionErrada;
        model.DetalleDireccion = record.DetalleDireccion;
        model.ClasificacionZonaSura = record.ClasificacionZonaSura;
        model.MunicipioResidencia = record.MunicipioResidencia;
        model.Barrio = record.Barrio;
        model.ZonaDireccionSegunMunicipio = record.ZonaDireccionSegunMunicipio;
        model.TelefonoPrincipal = record.TelefonoPrincipal;
        model.TelefonoAdicional1 = record.TelefonoAdicional1;
        model.TelefonoAdicional2 = record.TelefonoAdicional2;
        model.LlamadaBienvenida = record.LlamadaBienvenida;
        model.TelefonoContacto = record.TelefonoContacto;
        model.Observacion = record.Observacion;
        model.CodigoCie10 = record.CodigoCie10;
        model.DiagnosticoDescriptivo = record.DiagnosticoDescriptivo;
        model.FechaValoracion = record.FechaValoracion.Date;
        model.ProgramaPertenece = record.ProgramaPertenece;
        model.AuxiliarEnfermeriaAsignado = record.AuxiliarEnfermeriaAsignado;
        model.Picc = record.Picc;
        model.Vac = record.Vac;
        model.Npt = record.Npt;
        model.ManejoHerida = record.ManejoHerida;
        model.ApositoMedicamento1 = record.ApositoMedicamento1;
        model.ApositoMedicamento2 = record.ApositoMedicamento2;
        model.ApositoMedicamento3 = record.ApositoMedicamento3;
        model.ApositoMedicamento4 = record.ApositoMedicamento4;
        model.DuracionTratamientoDias = record.DuracionTratamientoDias;
        model.FrecuenciaVisita = CanonicalClinicaHeridasFrecuenciaVisita(record.FrecuenciaVisita);
        model.EquipoComodato = record.EquipoComodato;
        model.NumeroPlacaEquipos = record.NumeroPlacaEquipos;
        model.FechaEntregaEquipo = record.FechaEntregaEquipo?.Date;
        model.FechaDevolucionEquipo = record.FechaDevolucionEquipo?.Date;
        model.FechaHospitalizacion = record.FechaHospitalizacion?.Date;
        model.MotivoHospitalizacion = record.MotivoHospitalizacion;
        model.RemitidoPorHospitalizacion = record.RemitidoPorHospitalizacion;
        model.IpsIntramural = record.IpsIntramural;
        model.FechaPrimerSeguimiento24Horas = record.FechaPrimerSeguimiento24Horas?.Date;
        model.FechaSegundoSeguimiento48Horas = record.FechaSegundoSeguimiento48Horas?.Date;
        model.FechaTercerSeguimiento72Horas = record.FechaTercerSeguimiento72Horas?.Date;
        model.FechaCuartoSeguimientoSemana1 = record.FechaCuartoSeguimientoSemana1?.Date;
        model.FechaQuintoSeguimientoSemana2 = record.FechaQuintoSeguimientoSemana2?.Date;
        model.FechaSextoSeguimientoSemana3 = record.FechaSextoSeguimientoSemana3?.Date;
        model.FechaSeptimoSeguimientoSemana4 = record.FechaSeptimoSeguimientoSemana4?.Date;
        model.FechaNovedadDevolucionProductos = record.FechaNovedadDevolucionProductos?.Date;
        model.MotivoNovedadDevolucionProductos = record.MotivoNovedadDevolucionProductos;
        model.NotificacionAuxiliarDevolucionProductos = record.NotificacionAuxiliarDevolucionProductos;
        model.FechaMaximaDevolucionProductos = record.FechaMaximaDevolucionProductos?.Date;
        model.EstadoDevolucionServicioFarmaceutico = record.EstadoDevolucionServicioFarmaceutico;
        model.MotivoEgreso = record.MotivoEgreso;
        model.FechaEgreso = record.FechaEgreso?.Date;
        model.Estado = record.Estado;
    }
}
