using System.Globalization;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntranetPrueba.Data;
using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.Security;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using IntranetPrueba.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Controllers;

[Authorize(Policy = SystemPermissions.Censo)]
public class CensoController : Controller
{
    private const string AseguradorSuraEps = "Sura EPS";
    private const string GestionPendiente = "Pendiente";
    private const string GestionCompleta = "Completa";
    private const string MunicipioNoParametrizado = "NO PARAMETRIZADO";
    private const string ValorNoAplicaMedicamentoAdicional = "No";
    private static readonly string[] HoraPromesaInicioTtoValues =
    [
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11",
        "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23"
    ];
    private static readonly Regex Cie10Pattern = new("^[A-Z][0-9]{3}$", RegexOptions.Compiled);
    private static readonly Regex HoraPromesaPattern = new(
        "^Entre\\s+(?<desde>\\d{1,2})\\s+y\\s+(?<hasta>\\d{1,2})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HoraPromesaLegacyPattern = new(
        "^Entre\\s+(?<desde>\\d{1,2})\\s+y\\s+(?<hasta>\\d{1,2})\\s+(?<meridiano>AM|PM)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly TimeZoneInfo ColombiaTimeZone = ResolveColombiaTimeZone();
    private static readonly IReadOnlyDictionary<int, string> MandatorySectionNames = new Dictionary<int, string>
    {
        [1] = "Sección 1 - Recepción del paciente",
        [2] = "Sección 2 - Datos básicos del paciente",
        [3] = "Sección 3 - Plan de manejo"
    };
    private static readonly IReadOnlyDictionary<string, int> MandatorySectionFieldMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(CensoReceptionViewModel.FechaIngreso)] = 1,
        [nameof(CensoReceptionViewModel.HoraIngreso)] = 1,
        [nameof(CensoReceptionViewModel.FechaRespuesta)] = 1,
        [nameof(CensoReceptionViewModel.HoraRespuesta)] = 1,
        [nameof(CensoReceptionViewModel.NombreRecepcionaCaso)] = 1,
        [nameof(CensoReceptionViewModel.NombreRealizaKardex)] = 1,

        [nameof(CensoReceptionViewModel.NombrePaciente)] = 2,
        [nameof(CensoReceptionViewModel.TipoIdentificacion)] = 2,
        [nameof(CensoReceptionViewModel.NumeroIdentificacion)] = 2,
        [nameof(CensoReceptionViewModel.CodigoCie10)] = 2,
        [nameof(CensoReceptionViewModel.FechaNacimiento)] = 2,
        [nameof(CensoReceptionViewModel.CorreoElectronico)] = 2,
        [nameof(CensoReceptionViewModel.Direccion)] = 2,
        [nameof(CensoReceptionViewModel.ClasificacionZonaSura)] = 2,
        [nameof(CensoReceptionViewModel.MunicipioResidencia)] = 2,
        [nameof(CensoReceptionViewModel.Barrio)] = 2,
        [nameof(CensoReceptionViewModel.ZonaDireccionSegunMunicipio)] = 2,
        [nameof(CensoReceptionViewModel.Area)] = 2,
        [nameof(CensoReceptionViewModel.IpsQueRemite)] = 2,
        [nameof(CensoReceptionViewModel.VistoBuenoRangoFueraAnexo)] = 2,
        [nameof(CensoReceptionViewModel.Telefono1)] = 2,
        [nameof(CensoReceptionViewModel.Telefono2)] = 2,
        [nameof(CensoReceptionViewModel.Telefono3)] = 2,

        [nameof(CensoReceptionViewModel.ClasificacionRiesgo)] = 3,
        [nameof(CensoReceptionViewModel.AdministracionMedicamentos)] = 3,
        [nameof(CensoReceptionViewModel.NombreMedicamentoPrincipalTratante)] = 3,
        [nameof(CensoReceptionViewModel.DosisMedicamentoPrincipal)] = 3,
        [nameof(CensoReceptionViewModel.MedidaMedicamentoPrincipal)] = 3,
        [nameof(CensoReceptionViewModel.ViaAdministracionMedicamentoPrincipal)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAdministracionMxPrincipal)] = 3,
        [nameof(CensoReceptionViewModel.DiasMedicamentoPrincipal)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDosisDiaMedicamentoPrincipal)] = 3,
        [nameof(CensoReceptionViewModel.TieneSegundoMedicamento)] = 3,
        [nameof(CensoReceptionViewModel.NombreMedicamentoNumero2)] = 3,
        [nameof(CensoReceptionViewModel.DosisMedicamento2)] = 3,
        [nameof(CensoReceptionViewModel.MedidaMedicamento2)] = 3,
        [nameof(CensoReceptionViewModel.ViaAdministracionMedicamento2)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAdministracionMedicamento2)] = 3,
        [nameof(CensoReceptionViewModel.DiasMedicamento2)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDosisMedicamento2)] = 3,
        [nameof(CensoReceptionViewModel.TieneTercerMedicamento)] = 3,
        [nameof(CensoReceptionViewModel.NombreMedicamentoNumero3)] = 3,
        [nameof(CensoReceptionViewModel.DosisMedicamento3)] = 3,
        [nameof(CensoReceptionViewModel.MedidaMedicamento3)] = 3,
        [nameof(CensoReceptionViewModel.ViaAdministracionMedicamento3)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAdministracionMedicamento3)] = 3,
        [nameof(CensoReceptionViewModel.DiasMedicamento3)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDosisMedicamento3)] = 3,
        [nameof(CensoReceptionViewModel.TieneCuartoMedicamento)] = 3,
        [nameof(CensoReceptionViewModel.NombreMedicamentoNumero4)] = 3,
        [nameof(CensoReceptionViewModel.DosisMedicamento4)] = 3,
        [nameof(CensoReceptionViewModel.MedidaMedicamento4)] = 3,
        [nameof(CensoReceptionViewModel.ViaAdministracionMedicamento4)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAdministracionMedicamento4)] = 3,
        [nameof(CensoReceptionViewModel.DiasMedicamento4)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDosisMedicamento4)] = 3,
        [nameof(CensoReceptionViewModel.TieneQuintoMedicamento)] = 3,
        [nameof(CensoReceptionViewModel.NombreMedicamentoNumero5)] = 3,
        [nameof(CensoReceptionViewModel.DosisMedicamento5)] = 3,
        [nameof(CensoReceptionViewModel.MedidaMedicamento5)] = 3,
        [nameof(CensoReceptionViewModel.ViaAdministracionMedicamento5)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAdministracionMedicamento5)] = 3,
        [nameof(CensoReceptionViewModel.DiasMedicamento5)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDosisMedicamento5)] = 3,
        [nameof(CensoReceptionViewModel.TieneSextoMedicamento)] = 3,
        [nameof(CensoReceptionViewModel.NombreMedicamentoNumero6)] = 3,
        [nameof(CensoReceptionViewModel.DosisMedicamento6)] = 3,
        [nameof(CensoReceptionViewModel.MedidaMedicamento6)] = 3,
        [nameof(CensoReceptionViewModel.ViaAdministracionMedicamento6)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAdministracionMedicamento6)] = 3,
        [nameof(CensoReceptionViewModel.DiasMedicamento6)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDosisMedicamento6)] = 3,
        [nameof(CensoReceptionViewModel.AplicacionesTotales)] = 3,
        [nameof(CensoReceptionViewModel.DiasTratamientoIv)] = 3,
        [nameof(CensoReceptionViewModel.CambioFrecuenciaAdministracionTto)] = 3,
        [nameof(CensoReceptionViewModel.FrecuenciaAjustada)] = 3,
        [nameof(CensoReceptionViewModel.MedicamentoFrecuenciaAjustada)] = 3,
        [nameof(CensoReceptionViewModel.FechaInicioTratamiento)] = 3,
        [nameof(CensoReceptionViewModel.FechaFinTratamiento)] = 3,
        [nameof(CensoReceptionViewModel.FechaPromesaInicioTto)] = 3,
        [nameof(CensoReceptionViewModel.HoraPromesaInicioTto)] = 3,
        [nameof(CensoReceptionViewModel.HoraPromesaInicioTtoDesde)] = 3,
        [nameof(CensoReceptionViewModel.HoraPromesaInicioTtoHasta)] = 3,
        [nameof(CensoReceptionViewModel.HoraPromesaInicioTtoMeridiano)] = 3,
        [nameof(CensoReceptionViewModel.AuxiliarAsignado)] = 3,
        [nameof(CensoReceptionViewModel.Estado)] = 3,
        [nameof(CensoReceptionViewModel.AutorizacionEvento)] = 3,
        [nameof(CensoReceptionViewModel.ResponsableLlamadaBienvenida)] = 3,
        [nameof(CensoReceptionViewModel.EstadoLlamadaBienvenida)] = 3,
        [nameof(CensoReceptionViewModel.NumeroTelefonoLlamadaBienvenida)] = 3,
        [nameof(CensoReceptionViewModel.ObservacionesPlanManejo)] = 3,
        [nameof(CensoReceptionViewModel.NumeroDiasAutorizado)] = 3,
        [nameof(CensoReceptionViewModel.RequiereServiciosComplementarios)] = 3,
        [nameof(CensoReceptionViewModel.ServicioComplementario)] = 3,
        [nameof(CensoReceptionViewModel.PacienteGestante)] = 3,
        [nameof(CensoReceptionViewModel.Nebulizaciones)] = 3,
        [nameof(CensoReceptionViewModel.SistemasPresionNegativaVac)] = 3,
        [nameof(CensoReceptionViewModel.NutricionParenteral)] = 3,
        [nameof(CensoReceptionViewModel.NutricionEnteral)] = 3,
        [nameof(CensoReceptionViewModel.PacienteAnticoagulado)] = 3,
        [nameof(CensoReceptionViewModel.LaboratorioClinicoProcedimiento)] = 3,
        [nameof(CensoReceptionViewModel.ClinicaHeridas)] = 3,
        [nameof(CensoReceptionViewModel.Aislamiento)] = 3,
        [nameof(CensoReceptionViewModel.TipoAislamiento)] = 3,
        [nameof(CensoReceptionViewModel.CateterismoOSv)] = 3,
        [nameof(CensoReceptionViewModel.CateterPicc)] = 3,
        [nameof(CensoReceptionViewModel.NumeroCalibreSonda)] = 3,
        [nameof(CensoReceptionViewModel.FechaUltimoCambioSonda)] = 3,
        [nameof(CensoReceptionViewModel.AuxiliarAsignadoCateterismo)] = 3,
        [nameof(CensoReceptionViewModel.FechaProximoCambioSonda)] = 3,
        [nameof(CensoReceptionViewModel.FechaUltimaCuracionPicc)] = 3
    };
    private static readonly IReadOnlyDictionary<string, string> OptionDisplayTextOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Valle de aburra"] = "Valle de Aburrá",
        ["Si"] = "Sí",
        ["Aceptado cronico"] = "Aceptado crónico",
        ["Cronico alta"] = "Crónico alta",
        ["Cancelado EPS Respuesta extemporanea"] = "Cancelado EPS Respuesta extemporánea",
        ["Cronico activo agudizado"] = "Crónico activo agudizado",
        ["INESTABILIDAD HEMODINAMICA"] = "INESTABILIDAD HEMODINÁMICA",
        ["INOPORTUNIDAD EN PRESTACION DE SERVICIO"] = "INOPORTUNIDAD EN PRESTACIÓN DE SERVICIO",
        ["MEDICO IPS"] = "MÉDICO IPS",
        ["AUX ENFERMERIA"] = "AUX ENFERMERÍA",
        ["MEDICO ESPECIALISTA"] = "MÉDICO ESPECIALISTA",
        ["HOSPITALIZACION"] = "HOSPITALIZACIÓN",
        ["SUSPEDE TRATAMIENTO"] = "SUSPENDE TRATAMIENTO",
        ["INFUSION CONTINUA"] = "INFUSIÓN CONTINUA",
        ["Sur - La estrella"] = "Sur - La Estrella",
        ["Sur-Amaga"] = "Sur-Amagá",
        ["Oriente AntioqueÃ±o"] = "Oriente Antioqueño",
        ["No Parametrizado"] = "No parametrizado",
        ["MEDELLIN"] = "MEDELLÍN",
        ["ITAGÃœÃ"] = "ITAGÜÍ",
        ["AMAGA"] = "AMAGÁ",
        ["DON MATIAS"] = "DON MATÍAS",
        ["GUATAPE"] = "GUATAPÉ",
        ["LA UNION"] = "LA UNIÓN",
        ["PEÃ‘OL"] = "PEÑOL",
        ["Terapia fisica"] = "Terapia física",
        ["Nutricion"] = "Nutrición"
    };

    private static readonly string[] TiposIdentificacion = ["CC", "RC", "PA", "CE", "TI", "PE", "PPT"];
    private static readonly string[] ClasificacionZonaSuraValues = ["Valle de aburra", "Oriente"];
    private static readonly string[] VistoBuenoValues = ["Si", "No"];
    private static readonly string[] ClasificacionRiesgoValues = ["Bajo", "Medio", "Alto"];
    private static readonly string[] AdministracionMedicamentosValues = ["Si", "No"];
    private static readonly string[] CambioFrecuenciaAdministracionTtoValues = ["Si", "No"];
    private static readonly string[] ServicioComplementarioValues =
    [
        "Terapia fisica",
        "Terapia respiratoria",
        "Terapia ocupacional",
        "Fonoaudiologia",
        "Nutricion"
    ];
    private static readonly string[] TipoAislamientoValues = ["Aislamiento por Aerosoles", "Contacto", "Gotas"];
    private static readonly string[] EstadoValues =
    [
        "Aceptado activo",
        "Aceptado alta",
        "Aceptado cronico",
        "Cronico alta",
        "Activo Estancia prolongada",
        "Alta Estancia Prolongada",
        "Cancelado EPS Cancela IPS remitente",
        "Cancelado EPS Doble prestador",
        "Cancelado EPS Respuesta extemporanea",
        "Rechazado Fuera de cobertura",
        "Rechazado No disponibilidad",
        "Cronico activo agudizado"
    ];
    private static readonly string[] EstadoLlamadaBienvenidaValues = ["Efectivo", "No efectivo"];
    private static readonly string[] MotivoRehospitalizacionValues =
    [
        "INESTABILIDAD HEMODINAMICA",
        "INOPORTUNIDAD EN PRESTACION DE SERVICIO",
        "NO APLICA"
    ];
    private static readonly string[] RemitidoPorRehospitalizacionValues =
    [
        "MEDICO IPS",
        "AUX ENFERMERIA",
        "PROFESIONAL COMPLEMENTARIO",
        "FAMILIAR / PACIENTE",
        "MEDICO ESPECIALISTA",
        "NO APLICA"
    ];
    private static readonly string[] MotivoNovedadDevolucionProductosValues =
    [
        "FALLECE",
        "HOSPITALIZACION",
        "CAMBIA DE TRATAMIENTO",
        "SUSPEDE TRATAMIENTO",
        "SIN NOVEDAD"
    ];
    private static readonly string[] EstadoDevolucionServicioFarmaceuticoValues = ["EFECTIVA", "NO EFECTIVA"];
    private static readonly string[] MedidaMedicamentoValues =
    [
        "Miligramos",
        "Gramos",
        "Unidades",
        "Gotas",
        "Mililitros"
    ];
    private static readonly string[] ViaAdministracionMedicamentoValues =
    [
        "Intravenosa",
        "Intramuscular",
        "Subcutánea",
        "Nebulizada",
        "Oral"
    ];
    private static readonly string[] FrecuenciaAdministracionMxPrincipalValues =
    [
        "INFUSION CONTINUA",
        "CADA 4 HORAS",
        "CADA 6 HORAS",
        "CADA 8 HORAS",
        "CADA 12 HORAS",
        "CADA 24 HORAS",
        "CADA 48 HORAS",
        "CADA 72 HORAS",
        "NO APLICA"
    ];

    private static readonly string[] FrecuenciaAjustadaValues =
    [
        "INFUSION CONTINUA",
        "CADA 4 HORAS",
        "CADA 6 HORAS",
        "CADA 8 HORAS",
        "CADA 12 HORAS",
        "CADA 24 HORAS",
        "CADA 48 HORAS",
        "CADA 72 HORAS"
    ];

    private static readonly string[] NumeroDosisDiaMedicamentoPrincipalValues =
    [
        "CONTINUO",
        "6",
        "4",
        "3",
        "2",
        "1",
        "NO APLICA"
    ];

    private static readonly string[] ZonaDireccionValues =
    [
        "Nor Oriental",
        "Nor Occidental",
        "Centro Oriental",
        "Centro Occidental",
        "Sur Oriental",
        "Sur Occidental",
        "Sur",
        "Norte",
        "Sur - La estrella",
        "Sur-Amaga",
        "Norte-Barbosa",
        "Sur-Caldas",
        "Norte-Copacabana",
        "Meseta",
        "Oriente Antioqueño",
        "Norte-Girardota",
        "No Parametrizado"
    ];

    private static readonly string[] AreaValues = ["Urbana", "Rural"];

    private static readonly string[] IpsQueRemiteValues =
    [
        "CIS COMFAMA ARANJUEZ",
        "CIS COMFAMA BELLO",
        "CIS COMFAMA BUENOS AIRES",
        "CIS COMFAMA CALASANZ",
        "CIS COMFAMA CALDAS",
        "CIS COMFAMA CITY PLAZA",
        "CIS COMFAMA COPACABANA",
        "CIS COMFAMA CORDOBA",
        "CIS COMFAMA CRISTO REY",
        "CIS COMFAMA EL PORVENIR - RIONEGRO",
        "CIS COMFAMA EL RETIRO LOS ROBLES",
        "CIS COMFAMA ENVIGADO",
        "CIS COMFAMA GIRARDOTA",
        "CIS COMFAMA ITAGUI",
        "CIS COMFAMA LA CEJA",
        "CIS COMFAMA LA ESTRELLA",
        "CIS COMFAMA LA UNION",
        "CIS COMFAMA LOPEZ DE MESA",
        "CIS COMFAMA LOS COLORES",
        "CIS COMFAMA MANRIQUE",
        "CIS COMFAMA MONTERREY",
        "CIS COMFAMA PARQUE FABRICATO",
        "CIS COMFAMA RIONEGRO",
        "CIS COMFAMA SABANETA",
        "CIS COMFAMA SAMAN",
        "CIS COMFAMA SAN ANTONIO DE PRADO",
        "CIS COMFAMA SAN CRISTOBAL",
        "CIS COMFAMA SAN IGNACIO",
        "CIS COMFAMA SANTA MARIA - ITAGUI",
        "CLINICA ANTIOQUIA SEDE NORTE",
        "CLINICA ANTIOQUIA SEDE SUR",
        "CLINICA ASTORGA",
        "CLINICA CARDIOVASCULAR SANTA MARIA",
        "CLÍNICA CENTRAL FUNDADORES",
        "CLINICA CES",
        "CLINICA DEL PRADO",
        "CLINICA LAS AMERICAS",
        "CLINICA LAS VEGAS",
        "CLINICA MEDELLIN OCCIDENTE",
        "CLINICA MEDELLIN POBLADO",
        "CLINICA PLASTICA Y ESTETICA NOVA",
        "CLINICA RENACER",
        "CLINICA ROSARIO CENTRO",
        "CLINICA ROSARIO TESORO",
        "CLINICA SAGRADO CORAZON",
        "CLINICA SAN JUAN DE DIOS LA CEJA",
        "CLINICA SOMA",
        "CLINICA SOMER",
        "CLINICA UNIVERSITARIA BOLIVARIANA",
        "CLINICA VICTORIANA",
        "CLINICA VIDA",
        "CLINIQ DERMOESTETICA Y LASER S.A.",
        "COOMSOCIAL BELLO",
        "COOMSOCIAL ESTADIO",
        "COOPSANA - CENTRO",
        "COOPSANA AVENIDA ORIENTAL",
        "COOPSANA CALASANZ",
        "COOPSANA -CENTRO DE ESPECIALISTAS",
        "COOPSANA NORTE",
        "ESE HOSPITAL LA MARIA",
        "ESPECIALISTAS ASESORES PROFESIONALES",
        "ESPECIALISTAS EN CASA MEDICINA DOMICILIARIA SAS",
        "FUNDACION CLINICA DEL NORTE",
        "GRUPO MEDICO ESPECIALIZADO MEDELLIN",
        "HOME GROUP SAS",
        "HOSPITAL ALMA MATER DE ANTIOQUIA",
        "HOSPITAL CONCEJO DE MEDELLIN",
        "HOSPITAL GENERAL DE MEDELIN",
        "HOSPITAL INFANTIL SANTA ANA",
        "HOSPITAL MANUEL URIBE ANGEL",
        "HOSPITAL MARCO FIDEL SUAREZ",
        "HOSPITAL PABLO TOBON URIBE",
        "HOSPITAL SAN JUAN DE DIOS DE RIONEGRO",
        "HOSPITAL SAN VICENTE DE PAUL MEDELLIN",
        "HOSPITAL VENANCIO DIAZ",
        "HUMANITAS - ITAGUI",
        "INSTITUTO COLOMBIANO DE DOLOR",
        "INSTITUTO DE CANCEROLOGIA",
        "INSTITUTO DEL TORAX - LA PAZ",
        "INSTITUTO NEUROLOGICO DE COLOMBIA",
        "IPS DARIEM",
        "IPS ESPECIALIZADA SURA - DIABETES",
        "IPS HOSPITAL LA CEJA",
        "IPS PAC COOPSANA SURAMERICANA",
        "IPS QUIRUSTETIC",
        "IPS SALUD EN CASA",
        "IPS SURA ATEL CITY MEDICA RIONEGRO",
        "IPS SURA BELLO",
        "IPS SURA CENTRO",
        "IPS SURA ERC MEDELLIN",
        "IPS SURA ERC RIONEGRO",
        "IPS SURA LAS VEGAS MEDELLIN",
        "IPS SURA LOS MOLINOS",
        "IPS SURA MAYORCA",
        "IPS SURA OLAYA",
        "IPS SURA PROGRAMA CUIDADO Y VIDA",
        "IPS SURA ROBLEDO",
        "IPS SURA VIRTUAL",
        "MULTICLINIC S.A.S.",
        "PAC ACCESO DIRECTO",
        "PROSALCO BARBOSA",
        "PROSALCO CARMEN DE VIBORAL",
        "PROSALCO GUARNE",
        "PROSALCO MARINILLA",
        "PROSALCO SAN JUAN",
        "SALUD EN CASA MEDELLIN",
        "SALUD EN CASA RIONEGRO",
        "SALUD TRECC",
        "SAN VICENTE FUNDACION RIONEGRO",
        "UROCLIN",
        "URGENCIAS IPS SURA LOS MOLINOS",
        "URGENCIAS IPS SURA LOS ROBLEDO",
        "URGENCIAS IPS SURA LOS VEGAS",
    ];

    private static readonly string[] MunicipiosResidenciaValues =
    [
        "MEDELLIN",
        "BELLO",
        "ENVIGADO",
        "ITAGÜÍ",
        "SABANETA",
        "LA ESTRELLA",
        "AMAGA",
        "BARBOSA",
        "CALDAS",
        "COPACABANA",
        "DON MATIAS",
        "EL CARMEN DE VIBORAL",
        "EL SANTUARIO",
        "GIRARDOTA",
        "GUARNE",
        "GUATAPE",
        "LA CEJA",
        "LA UNION",
        "MARINILLA",
        "PEÑOL",
        "RETIRO",
        "SAN PEDRO DE LOS MILAGROS",
        "SAN VICENTE DE FERRER",
        "SANTA ROSA DE OSOS",
        "RIONEGRO",
        "SAN FELIX",
        MunicipioNoParametrizado
    ];

    private static readonly HashSet<string> OrienteMunicipios = new(
        [
            "EL CARMEN DE VIBORAL",
            "EL SANTUARIO",
            "GUARNE",
            "GUATAPE",
            "LA CEJA",
            "LA UNION",
            "MARINILLA",
            "PEÑOL",
            "RETIRO",
            "RIONEGRO",
            "SAN VICENTE DE FERRER"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly (string Alias, string Zona)[] MedellinZoneHints =
    [
        ("LAURELESESTADIO", "Centro Occidental"),
        ("DOCEDEOCTUBRE", "Nor Occidental"),
        ("12DEOCTUBRE", "Nor Occidental"),
        ("LACANDELARIA", "Centro Oriental"),
        ("VILLAHERMOSA", "Centro Oriental"),
        ("BUENOSAIRES", "Centro Oriental"),
        ("SANTACRUZ", "Nor Oriental"),
        ("ARANJUEZ", "Nor Oriental"),
        ("MANRIQUE", "Nor Oriental"),
        ("POPULAR", "Nor Oriental"),
        ("CASTILLA", "Nor Occidental"),
        ("ROBLEDO", "Nor Occidental"),
        ("SANJAVIER", "Centro Occidental"),
        ("LAAMERICA", "Centro Occidental"),
        ("AMERICA", "Centro Occidental"),
        ("LAURELES", "Centro Occidental"),
        ("ESTADIO", "Centro Occidental"),
        ("ELPOBLADO", "Sur Oriental"),
        ("POBLADO", "Sur Oriental"),
        ("GUAYABAL", "Sur Occidental"),
        ("BELEN", "Sur Occidental"),
        ("LAPRADERA", "Centro Occidental")
    ];

    private readonly ApplicationDbContext _context;
    private readonly IUserAdministrationService _userAdministrationService;
    private readonly IAddressValidationService _addressValidationService;
    private readonly IFarmaciaDispatchNotificationService _farmaciaDispatchNotificationService;
    private readonly IReadOnlyList<string> _medicamentoFallbackValues;
    private readonly IReadOnlyDictionary<string, string> _cie10Catalog;
    private readonly IReadOnlyDictionary<string, string> _medellinNeighborhoodZoneMap;

    public CensoController(
        ApplicationDbContext context,
        IUserAdministrationService userAdministrationService,
        IAddressValidationService addressValidationService,
        IFarmaciaDispatchNotificationService farmaciaDispatchNotificationService,
        IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _userAdministrationService = userAdministrationService;
        _addressValidationService = addressValidationService;
        _farmaciaDispatchNotificationService = farmaciaDispatchNotificationService;
        _medicamentoFallbackValues = LoadMedicamentoPrincipalValues(webHostEnvironment.ContentRootPath);
        _cie10Catalog = LoadCie10Catalog(webHostEnvironment.ContentRootPath);
        _medellinNeighborhoodZoneMap = LoadMedellinNeighborhoodZoneMap(webHostEnvironment.ContentRootPath);
    }

    private static TimeZoneInfo ResolveColombiaTimeZone()
    {
        foreach (var timeZoneId in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }

    private static DateTime GetColombiaNow()
    {
        var value = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ColombiaTimeZone);
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? cedulaPaciente, string? CedulaFiltro, DateTime? fechaIngresoDesde, DateTime? fechaIngresoHasta, long? recordId, CancellationToken cancellationToken)
    {
        var now = GetColombiaNow();
        var model = new CensoReceptionViewModel
        {
            FechaIngreso = now.Date,
            HoraIngreso = new TimeSpan(now.Hour, now.Minute, 0),
            FechaRespuesta = now.Date,
            HoraRespuesta = new TimeSpan(now.Hour, now.Minute, 0),
            FechaNacimiento = now.Date,
            Edad = 0,
            DireccionEsValida = false,
            MunicipioResidencia = MunicipioNoParametrizado,
            ClasificacionZonaSura = InferClasificacionZonaSura(MunicipioNoParametrizado),
            ZonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(MunicipioNoParametrizado),
            Area = AreaValues[0]
        };

        model.CedulaFiltro = NormalizeCedulaFilter(!string.IsNullOrWhiteSpace(cedulaPaciente) ? cedulaPaciente : CedulaFiltro);
        model.FechaIngresoFiltroDesde = fechaIngresoDesde?.Date;
        model.FechaIngresoFiltroHasta = fechaIngresoHasta?.Date;
        NormalizeHistoryFilters(model);
        await PopulateCensoListAndLatestRecordAsync(model, cancellationToken, loadLatestRecordIntoForm: true, selectedRecordId: recordId);
        await PopulateDropdownsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index(CensoReceptionViewModel model, CancellationToken cancellationToken)
    {
        model.CedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.DocumentoProrrogaBusqueda = NormalizeCedulaFilter(model.DocumentoProrrogaBusqueda);
        NormalizeHistoryFilters(model);
        model.Direccion = model.Direccion?.Trim() ?? string.Empty;
        model.CorreoElectronico = model.CorreoElectronico?.Trim() ?? string.Empty;
        model.Barrio = model.Barrio?.Trim() ?? string.Empty;
        model.NombrePaciente = model.NombrePaciente?.Trim() ?? string.Empty;
        model.NumeroIdentificacion = model.NumeroIdentificacion?.Trim() ?? string.Empty;
        model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
        model.DiagnosticoDescriptivo = model.DiagnosticoDescriptivo?.Trim() ?? string.Empty;
        model.IpsQueRemite = model.IpsQueRemite?.Trim() ?? string.Empty;
        model.VistoBuenoRangoFueraAnexo = model.VistoBuenoRangoFueraAnexo?.Trim() ?? string.Empty;
        model.ClasificacionRiesgo = model.ClasificacionRiesgo?.Trim() ?? string.Empty;
        model.AdministracionMedicamentos = model.AdministracionMedicamentos?.Trim() ?? string.Empty;
        model.NombreMedicamentoPrincipalTratante = model.NombreMedicamentoPrincipalTratante?.Trim();
        model.MedidaMedicamentoPrincipal = model.MedidaMedicamentoPrincipal?.Trim();
        model.ViaAdministracionMedicamentoPrincipal = model.ViaAdministracionMedicamentoPrincipal?.Trim();
        model.FrecuenciaAdministracionMxPrincipal = model.FrecuenciaAdministracionMxPrincipal?.Trim() ?? string.Empty;
        model.NumeroDosisDiaMedicamentoPrincipal = CalculateNumeroDosisDiaMedicamentoPrincipal(model.FrecuenciaAdministracionMxPrincipal);
        ModelState.Remove(nameof(model.NumeroDosisDiaMedicamentoPrincipal));
        model.NombreMedicamentoNumero2 = model.NombreMedicamentoNumero2?.Trim();
        model.MedidaMedicamento2 = model.MedidaMedicamento2?.Trim();
        model.ViaAdministracionMedicamento2 = model.ViaAdministracionMedicamento2?.Trim();
        model.FrecuenciaAdministracionMedicamento2 = model.FrecuenciaAdministracionMedicamento2?.Trim();
        model.NumeroDosisMedicamento2 = model.NumeroDosisMedicamento2?.Trim();
        model.NombreMedicamentoNumero3 = model.NombreMedicamentoNumero3?.Trim();
        model.MedidaMedicamento3 = model.MedidaMedicamento3?.Trim();
        model.ViaAdministracionMedicamento3 = model.ViaAdministracionMedicamento3?.Trim();
        model.FrecuenciaAdministracionMedicamento3 = model.FrecuenciaAdministracionMedicamento3?.Trim();
        model.NumeroDosisMedicamento3 = model.NumeroDosisMedicamento3?.Trim();
        model.NombreMedicamentoNumero4 = model.NombreMedicamentoNumero4?.Trim();
        model.MedidaMedicamento4 = model.MedidaMedicamento4?.Trim();
        model.ViaAdministracionMedicamento4 = model.ViaAdministracionMedicamento4?.Trim();
        model.FrecuenciaAdministracionMedicamento4 = model.FrecuenciaAdministracionMedicamento4?.Trim();
        model.NumeroDosisMedicamento4 = model.NumeroDosisMedicamento4?.Trim();
        model.NombreMedicamentoNumero5 = model.NombreMedicamentoNumero5?.Trim();
        model.MedidaMedicamento5 = model.MedidaMedicamento5?.Trim();
        model.ViaAdministracionMedicamento5 = model.ViaAdministracionMedicamento5?.Trim();
        model.FrecuenciaAdministracionMedicamento5 = model.FrecuenciaAdministracionMedicamento5?.Trim();
        model.NumeroDosisMedicamento5 = model.NumeroDosisMedicamento5?.Trim();
        model.NombreMedicamentoNumero6 = model.NombreMedicamentoNumero6?.Trim();
        model.MedidaMedicamento6 = model.MedidaMedicamento6?.Trim();
        model.ViaAdministracionMedicamento6 = model.ViaAdministracionMedicamento6?.Trim();
        model.FrecuenciaAdministracionMedicamento6 = model.FrecuenciaAdministracionMedicamento6?.Trim();
        model.NumeroDosisMedicamento6 = model.NumeroDosisMedicamento6?.Trim();
        model.AplicacionesTotales = model.AplicacionesTotales?.Trim();
        model.DiasTratamientoIv = model.DiasTratamientoIv?.Trim();
        model.CambioFrecuenciaAdministracionTto = model.CambioFrecuenciaAdministracionTto?.Trim();
        model.FrecuenciaAjustada = model.FrecuenciaAjustada?.Trim();
        model.MedicamentoFrecuenciaAjustada = model.MedicamentoFrecuenciaAjustada?.Trim();
        model.HoraPromesaInicioTtoDesde = model.HoraPromesaInicioTtoDesde?.Trim();
        model.HoraPromesaInicioTtoHasta = model.HoraPromesaInicioTtoHasta?.Trim();
        model.HoraPromesaInicioTtoMeridiano = model.HoraPromesaInicioTtoMeridiano?.Trim()?.ToUpperInvariant();
        model.AuxiliarAsignado = model.AuxiliarAsignado?.Trim();
        model.Estado = model.Estado?.Trim();
        model.AutorizacionEvento = model.AutorizacionEvento?.Trim();
        model.ResponsableLlamadaBienvenida = model.ResponsableLlamadaBienvenida?.Trim();
        model.EstadoLlamadaBienvenida = model.EstadoLlamadaBienvenida?.Trim();
        model.ObservacionesPlanManejo = model.ObservacionesPlanManejo?.Trim();
        model.NumeroTelefonoLlamadaBienvenida = model.NumeroTelefonoLlamadaBienvenida?.Trim();
        model.NumeroDiasAutorizado = model.NumeroDiasAutorizado?.Trim();
        model.RequiereServiciosComplementarios = model.RequiereServiciosComplementarios?.Trim();
        model.ServicioComplementario = NormalizeServiciosComplementarios(model.ServicioComplementario);
        model.PacienteGestante = model.PacienteGestante?.Trim();
        model.Nebulizaciones = model.Nebulizaciones?.Trim();
        model.SistemasPresionNegativaVac = model.SistemasPresionNegativaVac?.Trim();
        model.NutricionParenteral = model.NutricionParenteral?.Trim();
        model.NutricionEnteral = model.NutricionEnteral?.Trim();
        model.PacienteAnticoagulado = model.PacienteAnticoagulado?.Trim();
        model.LaboratorioClinicoProcedimiento = model.LaboratorioClinicoProcedimiento?.Trim();
        model.ClinicaHeridas = model.ClinicaHeridas?.Trim();
        model.Aislamiento = model.Aislamiento?.Trim();
        model.TipoAislamiento = model.TipoAislamiento?.Trim();
        model.CateterismoOSv = model.CateterismoOSv?.Trim();
        model.CateterPicc = model.CateterPicc?.Trim();
        model.AuxiliarAsignadoCateterismo = model.AuxiliarAsignadoCateterismo?.Trim();
        model.NombreQuienGestionaAlta = model.NombreQuienGestionaAlta?.Trim();
        model.AltaTardia = model.AltaTardia?.Trim();
        model.ObservacionAltaTardia = model.ObservacionAltaTardia?.Trim();
        model.NombreQuienRealizaSeguimientoAltaTardia = model.NombreQuienRealizaSeguimientoAltaTardia?.Trim();
        model.PacienteRehospitalizado = model.PacienteRehospitalizado?.Trim();
        model.MotivoRehospitalizacion = model.MotivoRehospitalizacion?.Trim();
        model.AmpliacionMotivoRehospitalizacion = model.AmpliacionMotivoRehospitalizacion?.Trim();
        model.RemitidoPorRehospitalizacion = model.RemitidoPorRehospitalizacion?.Trim();
        model.IpsIntramuralRehospitalizacion = model.IpsIntramuralRehospitalizacion?.Trim();
        model.ObservacionRehospitalizacion = model.ObservacionRehospitalizacion?.Trim();
        model.MotivoNovedadDevolucionProductos = model.MotivoNovedadDevolucionProductos?.Trim();
        model.NotificacionAuxiliarDevolucionProductos = model.NotificacionAuxiliarDevolucionProductos?.Trim();
        model.EstadoDevolucionServicioFarmaceutico = model.EstadoDevolucionServicioFarmaceutico?.Trim();
        model.PresentaNovedadKardex = model.PresentaNovedadKardex?.Trim();
        model.PresentaNovedadRequisicion = model.PresentaNovedadRequisicion?.Trim();
        model.PresentaNovedadAutorizacion = model.PresentaNovedadAutorizacion?.Trim();
        model.DescripcionNovedadDocumentosPaciente = model.DescripcionNovedadDocumentosPaciente?.Trim();
        model.Telefono1 = NormalizePhone(model.Telefono1);
        model.Telefono2 = NormalizePhone(model.Telefono2);
        model.Telefono3 = NormalizePhone(model.Telefono3);
        model.Edad = CalculateAge(model.FechaNacimiento, DateTime.Today);
        ApplyPlanManejoDefaultValues(model);
        model.HoraPromesaInicioTto = BuildHoraPromesaInicioTto(
            model.HoraPromesaInicioTtoDesde,
            model.HoraPromesaInicioTtoHasta);

        var hasMedicationAdministration = string.Equals(model.AdministracionMedicamentos, "Si", StringComparison.OrdinalIgnoreCase);
        if (!hasMedicationAdministration)
        {
            model.NombreMedicamentoPrincipalTratante = null;
            model.DosisMedicamentoPrincipal = null;
            model.MedidaMedicamentoPrincipal = null;
            model.ViaAdministracionMedicamentoPrincipal = null;
            model.FrecuenciaAdministracionMxPrincipal = string.Empty;
            model.DiasMedicamentoPrincipal = null;
            model.NumeroDosisDiaMedicamentoPrincipal = string.Empty;
            ModelState.Remove(nameof(model.FrecuenciaAdministracionMxPrincipal));
            ModelState.Remove(nameof(model.DiasMedicamentoPrincipal));
            model.TieneSegundoMedicamento = false;
            model.TieneTercerMedicamento = false;
            model.TieneCuartoMedicamento = false;
            model.TieneQuintoMedicamento = false;
            model.TieneSextoMedicamento = false;
            model.NombreMedicamentoNumero2 = null; model.DosisMedicamento2 = null; model.MedidaMedicamento2 = null;
            model.ViaAdministracionMedicamento2 = null; model.FrecuenciaAdministracionMedicamento2 = null;
            model.DiasMedicamento2 = null; model.NumeroDosisMedicamento2 = null;
            model.NombreMedicamentoNumero3 = null; model.DosisMedicamento3 = null; model.MedidaMedicamento3 = null;
            model.ViaAdministracionMedicamento3 = null; model.FrecuenciaAdministracionMedicamento3 = null;
            model.DiasMedicamento3 = null; model.NumeroDosisMedicamento3 = null;
            model.NombreMedicamentoNumero4 = null; model.DosisMedicamento4 = null; model.MedidaMedicamento4 = null;
            model.ViaAdministracionMedicamento4 = null; model.FrecuenciaAdministracionMedicamento4 = null;
            model.DiasMedicamento4 = null; model.NumeroDosisMedicamento4 = null;
            model.NombreMedicamentoNumero5 = null; model.DosisMedicamento5 = null; model.MedidaMedicamento5 = null;
            model.ViaAdministracionMedicamento5 = null; model.FrecuenciaAdministracionMedicamento5 = null;
            model.DiasMedicamento5 = null; model.NumeroDosisMedicamento5 = null;
            model.NombreMedicamentoNumero6 = null; model.DosisMedicamento6 = null; model.MedidaMedicamento6 = null;
            model.ViaAdministracionMedicamento6 = null; model.FrecuenciaAdministracionMedicamento6 = null;
            model.DiasMedicamento6 = null; model.NumeroDosisMedicamento6 = null;
            model.AplicacionesTotales = string.Empty;
            model.DiasTratamientoIv = null;
            model.CambioFrecuenciaAdministracionTto = null;
            model.FrecuenciaAjustada = null;
            model.MedicamentoFrecuenciaAjustada = null;
        }
        else
        {
            model.TieneSegundoMedicamento = model.TieneSegundoMedicamento
                || HasMedicationData(model.NombreMedicamentoNumero2, model.FrecuenciaAdministracionMedicamento2, model.NumeroDosisMedicamento2, model.DosisMedicamento2, model.MedidaMedicamento2, model.ViaAdministracionMedicamento2);
            model.TieneTercerMedicamento = model.TieneTercerMedicamento
                || HasMedicationData(model.NombreMedicamentoNumero3, model.FrecuenciaAdministracionMedicamento3, model.NumeroDosisMedicamento3, model.DosisMedicamento3, model.MedidaMedicamento3, model.ViaAdministracionMedicamento3);
            model.TieneCuartoMedicamento = model.TieneCuartoMedicamento
                || HasMedicationData(model.NombreMedicamentoNumero4, model.FrecuenciaAdministracionMedicamento4, model.NumeroDosisMedicamento4, model.DosisMedicamento4, model.MedidaMedicamento4, model.ViaAdministracionMedicamento4);
            model.TieneQuintoMedicamento = model.TieneQuintoMedicamento
                || HasMedicationData(model.NombreMedicamentoNumero5, model.FrecuenciaAdministracionMedicamento5, model.NumeroDosisMedicamento5, model.DosisMedicamento5, model.MedidaMedicamento5, model.ViaAdministracionMedicamento5);
            model.TieneSextoMedicamento = model.TieneSextoMedicamento
                || HasMedicationData(model.NombreMedicamentoNumero6, model.FrecuenciaAdministracionMedicamento6, model.NumeroDosisMedicamento6, model.DosisMedicamento6, model.MedidaMedicamento6, model.ViaAdministracionMedicamento6);

            // Enforce hierarchy: each toggle requires the previous ones
            if (model.TieneSextoMedicamento) { model.TieneQuintoMedicamento = true; }
            if (model.TieneQuintoMedicamento) { model.TieneCuartoMedicamento = true; }
            if (model.TieneCuartoMedicamento) { model.TieneTercerMedicamento = true; }
            if (model.TieneTercerMedicamento) { model.TieneSegundoMedicamento = true; }

            if (!model.TieneSegundoMedicamento)
            {
                model.TieneTercerMedicamento = false;
                model.TieneCuartoMedicamento = false;
                model.TieneQuintoMedicamento = false;
                model.TieneSextoMedicamento = false;
                model.NombreMedicamentoNumero2 = null; model.DosisMedicamento2 = null; model.MedidaMedicamento2 = null;
                model.ViaAdministracionMedicamento2 = null; model.FrecuenciaAdministracionMedicamento2 = null;
                model.DiasMedicamento2 = null; model.NumeroDosisMedicamento2 = null;
                model.NombreMedicamentoNumero3 = null; model.DosisMedicamento3 = null; model.MedidaMedicamento3 = null;
                model.ViaAdministracionMedicamento3 = null; model.FrecuenciaAdministracionMedicamento3 = null;
                model.DiasMedicamento3 = null; model.NumeroDosisMedicamento3 = null;
                model.NombreMedicamentoNumero4 = null; model.DosisMedicamento4 = null; model.MedidaMedicamento4 = null;
                model.ViaAdministracionMedicamento4 = null; model.FrecuenciaAdministracionMedicamento4 = null;
                model.DiasMedicamento4 = null; model.NumeroDosisMedicamento4 = null;
                model.NombreMedicamentoNumero5 = null; model.DosisMedicamento5 = null; model.MedidaMedicamento5 = null;
                model.ViaAdministracionMedicamento5 = null; model.FrecuenciaAdministracionMedicamento5 = null;
                model.DiasMedicamento5 = null; model.NumeroDosisMedicamento5 = null;
                model.NombreMedicamentoNumero6 = null; model.DosisMedicamento6 = null; model.MedidaMedicamento6 = null;
                model.ViaAdministracionMedicamento6 = null; model.FrecuenciaAdministracionMedicamento6 = null;
                model.DiasMedicamento6 = null; model.NumeroDosisMedicamento6 = null;
                if (model.MedicamentoFrecuenciaAjustada is "2" or "3" or "4" or "5" or "6")
                    model.MedicamentoFrecuenciaAjustada = null;
            }
            else if (!model.TieneTercerMedicamento)
            {
                model.TieneCuartoMedicamento = false; model.TieneQuintoMedicamento = false; model.TieneSextoMedicamento = false;
                model.NombreMedicamentoNumero3 = null; model.DosisMedicamento3 = null; model.MedidaMedicamento3 = null;
                model.ViaAdministracionMedicamento3 = null; model.FrecuenciaAdministracionMedicamento3 = null;
                model.DiasMedicamento3 = null; model.NumeroDosisMedicamento3 = null;
                model.NombreMedicamentoNumero4 = null; model.DosisMedicamento4 = null; model.MedidaMedicamento4 = null;
                model.ViaAdministracionMedicamento4 = null; model.FrecuenciaAdministracionMedicamento4 = null;
                model.DiasMedicamento4 = null; model.NumeroDosisMedicamento4 = null;
                model.NombreMedicamentoNumero5 = null; model.DosisMedicamento5 = null; model.MedidaMedicamento5 = null;
                model.ViaAdministracionMedicamento5 = null; model.FrecuenciaAdministracionMedicamento5 = null;
                model.DiasMedicamento5 = null; model.NumeroDosisMedicamento5 = null;
                model.NombreMedicamentoNumero6 = null; model.DosisMedicamento6 = null; model.MedidaMedicamento6 = null;
                model.ViaAdministracionMedicamento6 = null; model.FrecuenciaAdministracionMedicamento6 = null;
                model.DiasMedicamento6 = null; model.NumeroDosisMedicamento6 = null;
                if (model.MedicamentoFrecuenciaAjustada is "3" or "4" or "5" or "6")
                    model.MedicamentoFrecuenciaAjustada = null;
            }
            else if (!model.TieneCuartoMedicamento)
            {
                model.TieneQuintoMedicamento = false; model.TieneSextoMedicamento = false;
                model.NombreMedicamentoNumero4 = null; model.DosisMedicamento4 = null; model.MedidaMedicamento4 = null;
                model.ViaAdministracionMedicamento4 = null; model.FrecuenciaAdministracionMedicamento4 = null;
                model.DiasMedicamento4 = null; model.NumeroDosisMedicamento4 = null;
                model.NombreMedicamentoNumero5 = null; model.DosisMedicamento5 = null; model.MedidaMedicamento5 = null;
                model.ViaAdministracionMedicamento5 = null; model.FrecuenciaAdministracionMedicamento5 = null;
                model.DiasMedicamento5 = null; model.NumeroDosisMedicamento5 = null;
                model.NombreMedicamentoNumero6 = null; model.DosisMedicamento6 = null; model.MedidaMedicamento6 = null;
                model.ViaAdministracionMedicamento6 = null; model.FrecuenciaAdministracionMedicamento6 = null;
                model.DiasMedicamento6 = null; model.NumeroDosisMedicamento6 = null;
                if (model.MedicamentoFrecuenciaAjustada is "4" or "5" or "6")
                    model.MedicamentoFrecuenciaAjustada = null;
            }
            else if (!model.TieneQuintoMedicamento)
            {
                model.TieneSextoMedicamento = false;
                model.NombreMedicamentoNumero5 = null; model.DosisMedicamento5 = null; model.MedidaMedicamento5 = null;
                model.ViaAdministracionMedicamento5 = null; model.FrecuenciaAdministracionMedicamento5 = null;
                model.DiasMedicamento5 = null; model.NumeroDosisMedicamento5 = null;
                model.NombreMedicamentoNumero6 = null; model.DosisMedicamento6 = null; model.MedidaMedicamento6 = null;
                model.ViaAdministracionMedicamento6 = null; model.FrecuenciaAdministracionMedicamento6 = null;
                model.DiasMedicamento6 = null; model.NumeroDosisMedicamento6 = null;
                if (model.MedicamentoFrecuenciaAjustada is "5" or "6")
                    model.MedicamentoFrecuenciaAjustada = null;
            }
            else if (!model.TieneSextoMedicamento)
            {
                model.NombreMedicamentoNumero6 = null; model.DosisMedicamento6 = null; model.MedidaMedicamento6 = null;
                model.ViaAdministracionMedicamento6 = null; model.FrecuenciaAdministracionMedicamento6 = null;
                model.DiasMedicamento6 = null; model.NumeroDosisMedicamento6 = null;
                if (model.MedicamentoFrecuenciaAjustada == "6")
                    model.MedicamentoFrecuenciaAjustada = null;
            }

            if (model.TieneSegundoMedicamento)
            {
                model.NumeroDosisMedicamento2 = CalculateNumeroDosisDiaMedicamentoPrincipal(model.FrecuenciaAdministracionMedicamento2);
                ModelState.Remove(nameof(model.NumeroDosisMedicamento2));

                if (model.TieneTercerMedicamento)
                {
                    model.NumeroDosisMedicamento3 = CalculateNumeroDosisDiaMedicamentoPrincipal(model.FrecuenciaAdministracionMedicamento3);
                    ModelState.Remove(nameof(model.NumeroDosisMedicamento3));

                    if (model.TieneCuartoMedicamento)
                    {
                        model.NumeroDosisMedicamento4 = CalculateNumeroDosisDiaMedicamentoPrincipal(model.FrecuenciaAdministracionMedicamento4);
                        ModelState.Remove(nameof(model.NumeroDosisMedicamento4));

                        if (model.TieneQuintoMedicamento)
                        {
                            model.NumeroDosisMedicamento5 = CalculateNumeroDosisDiaMedicamentoPrincipal(model.FrecuenciaAdministracionMedicamento5);
                            ModelState.Remove(nameof(model.NumeroDosisMedicamento5));

                            if (model.TieneSextoMedicamento)
                            {
                                model.NumeroDosisMedicamento6 = CalculateNumeroDosisDiaMedicamentoPrincipal(model.FrecuenciaAdministracionMedicamento6);
                                ModelState.Remove(nameof(model.NumeroDosisMedicamento6));
                            }
                        }
                    }
                }
            }

            if (!string.Equals(model.CambioFrecuenciaAdministracionTto, "Si", StringComparison.OrdinalIgnoreCase))
            {
                model.FrecuenciaAjustada = null;
                model.MedicamentoFrecuenciaAjustada = null;
            }

            model.AplicacionesTotales = CalculateAplicacionesTotales(model).ToString(CultureInfo.InvariantCulture);
            model.DiasTratamientoIv = CalculateDiasTratamientoIv(model);
            model.FechaFinTratamiento = CalculateFechaFinTratamiento(model.FechaInicioTratamiento, model.DiasTratamientoIv);
        }

        if (!string.Equals(model.RequiereServiciosComplementarios, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.ServicioComplementario = null;
        }

        ModelState.Remove(nameof(model.AplicacionesTotales));
        ModelState.Remove(nameof(model.DiasTratamientoIv));

        if (!string.Equals(model.CateterismoOSv, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.NumeroCalibreSonda = null;
            model.FechaUltimoCambioSonda = null;
            model.AuxiliarAsignadoCateterismo = null;
            model.FechaProximoCambioSonda = null;
        }
        else
        {
            model.FechaProximoCambioSonda = model.FechaUltimoCambioSonda?.Date.AddDays(21);
            ModelState.Remove(nameof(model.FechaProximoCambioSonda));
        }

        if (!string.Equals(model.CateterPicc, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.FechaUltimaCuracionPicc = null;
        }

        if (!IsEstadoAlta(model.Estado))
        {
            model.FechaAlta = null;
            model.NombreQuienGestionaAlta = null;
            ModelState.Remove(nameof(model.FechaAlta));
            ModelState.Remove(nameof(model.NombreQuienGestionaAlta));
        }

        await PopulateDropdownsAsync(model, cancellationToken);

        ValidateAssistantSelections(model);
        ValidateBasicPatientData(model);
        ValidateRequiredPlanManejoFields(model);
        ValidateDropdownSelections(model);
        ValidateCie10Fields(model);
        ValidatePhoneFields(model);
        NormalizeGestionCompletaWithoutAuxiliar(model);

        var direccionValidation = await _addressValidationService.ValidateAddressAsync(model.Direccion, cancellationToken);
        var direccionParaGuardar = model.Direccion;
        ApplyAddressValidationResult(model, direccionValidation, ref direccionParaGuardar);

        ValidateDateTimes(model);

        if (!ModelState.IsValid)
        {
            var missingMandatorySections = GetMissingMandatorySectionNames(ModelState);
            ViewData["MissingMandatorySections"] = missingMandatorySections;
            ViewData["ShowSaveErrorModal"] = true;

            if (missingMandatorySections.Count > 0)
            {
                ModelState.AddModelError(string.Empty, BuildMissingMandatorySectionsMessage(missingMandatorySections));
            }

            await PopulateCensoListAndLatestRecordAsync(model, cancellationToken, loadLatestRecordIntoForm: false);
            return View(model);
        }

        var fechaHoraIngreso = model.FechaIngreso.Date + model.HoraIngreso;
        var fechaHoraRespuesta = model.FechaRespuesta.Date + model.HoraRespuesta;
        var indicadorTiempoRespuestaMinutos = (int)Math.Round((fechaHoraRespuesta - fechaHoraIngreso).TotalMinutes, MidpointRounding.AwayFromZero);
        CensoRecord? recordToNotifyAssistant = null;
        long? newRecordId = null;
        if (model.EditingRecordId is long editingRecordId)
        {
            var existingRecord = await _context.Censos.FirstOrDefaultAsync(x => x.Id == editingRecordId, cancellationToken);
            if (existingRecord is null)
            {
                ModelState.AddModelError(string.Empty, "No se encontró la última atención para actualizar. Vuelve a buscar por cédula.");
                ViewData["ShowSaveErrorModal"] = true;
                await PopulateCensoListAndLatestRecordAsync(model, cancellationToken, loadLatestRecordIntoForm: false);
                return View(model);
            }

            var previousAuxiliarAsignado = existingRecord.AuxiliarAsignado;
            ApplyModelToCensoRecord(
                existingRecord,
                model,
                direccionParaGuardar,
                indicadorTiempoRespuestaMinutos,
                existingRecord.IndicadorTiempoGestionMinutos);

            var auxiliarChanged = !string.Equals(
                previousAuxiliarAsignado?.Trim(),
                existingRecord.AuxiliarAsignado?.Trim(),
                StringComparison.OrdinalIgnoreCase);
            if (existingRecord.FarmaciaEnviadoAtUtc.HasValue
                && auxiliarChanged
                && !string.IsNullOrWhiteSpace(existingRecord.AuxiliarAsignado))
            {
                recordToNotifyAssistant = existingRecord;
            }

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = "Registro de censo actualizado correctamente.";
        }
        else
        {
            var censoRecord = new CensoRecord
            {
                CreatedAtUtc = DateTime.UtcNow
            };

            ApplyModelToCensoRecord(
                censoRecord,
                model,
                direccionParaGuardar,
                indicadorTiempoRespuestaMinutos,
                0);

            await _context.Censos.AddAsync(censoRecord, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            newRecordId = censoRecord.Id;
            TempData["SuccessMessage"] = "Registro de censo guardado correctamente.";
        }

        if (recordToNotifyAssistant is not null)
        {
            var warnings = await _farmaciaDispatchNotificationService.NotifyAssistantAssignedAsync(recordToNotifyAssistant, cancellationToken);
            if (warnings.Count > 0)
            {
                TempData["SuccessMessage"] = $"{TempData["SuccessMessage"]} {string.Join(" ", warnings)}";
            }
        }

        if (newRecordId.HasValue)
        {
            return RedirectToAction(nameof(Index), new
            {
                cedulaPaciente = model.NumeroIdentificacion,
                recordId = newRecordId.Value
            });
        }

        if (!string.IsNullOrWhiteSpace(model.CedulaFiltro))
        {
            return RedirectToAction(nameof(Index), new
            {
                cedulaPaciente = model.CedulaFiltro,
                fechaIngresoDesde = model.FechaIngresoFiltroDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                fechaIngresoHasta = model.FechaIngresoFiltroHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
        }

        return RedirectToAction(nameof(Index), new
        {
            fechaIngresoDesde = model.FechaIngresoFiltroDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            fechaIngresoHasta = model.FechaIngresoFiltroHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerDocumentos(long id, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest();
        var record = await _context.Censos
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.KardexEdicionJson, x.RequisicionFarmaciaJson, x.ProrrogaJson })
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null) return NotFound();
        return Json(new { kardexJson = record.KardexEdicionJson, requisicionJson = record.RequisicionFarmaciaJson, prorrogaJson = record.ProrrogaJson });
    }

    [HttpPost]
    public async Task<IActionResult> GuardarDocumentos(long id, string? kardexJson, string? requisicionJson, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro inválido." });
        var record = await _context.Censos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound(new { message = "Registro no encontrado." });

        record.KardexEdicionJson = string.IsNullOrWhiteSpace(kardexJson) ? null : kardexJson.Trim();
        record.RequisicionFarmaciaJson = string.IsNullOrWhiteSpace(requisicionJson) ? null : requisicionJson.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        return Json(new { message = "Documentos guardados correctamente." });
    }

    [HttpPost]
    public async Task<IActionResult> GuardarProrroga(long id, string? prorrogaJson, CancellationToken cancellationToken)
    {
        if (id <= 0) return BadRequest(new { message = "ID de registro inválido." });
        var record = await _context.Censos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null) return NotFound(new { message = "Registro no encontrado." });
        record.ProrrogaJson = string.IsNullOrWhiteSpace(prorrogaJson) ? null : prorrogaJson.Trim();
        record.EsProrroga = record.ProrrogaJson != null;
        if (record.ProrrogaJson != null)
        {
            try
            {
                var pDto = JsonSerializer.Deserialize<ProrrogaExportDto>(record.ProrrogaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (!string.IsNullOrWhiteSpace(pDto?.NombreMedicamentoPrincipal))
                {
                    record.KardexEdicionJson = null;
                    record.RequisicionFarmaciaJson = null;
                }
            }
            catch { }
        }
        await _context.SaveChangesAsync(cancellationToken);
        return Json(new { message = "Prórroga guardada correctamente." });
    }

    [HttpPost]
    public async Task<IActionResult> EnviarAFarmacia(long id, string? kardexJson, string? requisicionJson, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Guarda el censo antes de enviarlo a farmacia." });
        }

        var record = await _context.Censos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null)
        {
            return NotFound(new { message = "No se encontro el registro de censo para enviar a farmacia." });
        }

        var nowUtc = DateTime.UtcNow;
        var colombiaNow = GetColombiaNow();
        var fechaHoraRespuesta = record.FechaRespuesta.Date + record.HoraRespuesta;
        var fechaHoraGestionFarmacia = colombiaNow.Date + colombiaNow.TimeOfDay;
        record.FarmaciaEnviadoAtUtc = nowUtc;
        record.FarmaciaEstado = "Nuevo";
        record.FechaGestionFarmacia = colombiaNow.Date;
        record.HoraGestionFarmacia = colombiaNow.TimeOfDay;
        record.IndicadorTiempoGestionMinutos = (int)Math.Round((fechaHoraGestionFarmacia - fechaHoraRespuesta).TotalMinutes, MidpointRounding.AwayFromZero);
        record.FarmaciaKardexVistoAtUtc = null;
        record.FarmaciaRequisicionVistoAtUtc = null;
        record.KardexEdicionJson = string.IsNullOrWhiteSpace(kardexJson) ? null : kardexJson.Trim();
        record.RequisicionFarmaciaJson = string.IsNullOrWhiteSpace(requisicionJson) ? null : requisicionJson.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        var notificationWarnings = await _farmaciaDispatchNotificationService.NotifyDispatchSentAsync(record, cancellationToken);

        return Json(new
        {
            message = notificationWarnings.Count == 0
                ? "Pedido enviado a farmacia correctamente."
                : $"Pedido enviado a farmacia correctamente. {string.Join(" ", notificationWarnings)}",
            recordId = record.Id,
            enviadoAtUtc = nowUtc,
            fechaGestionFarmacia = colombiaNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            horaGestionFarmacia = colombiaNow.ToString("HH:mm", CultureInfo.InvariantCulture),
            notificationWarnings
        });
    }

    [HttpPost]
    public async Task<IActionResult> SubirAdjunto(long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Guarda el censo antes de subir adjuntos." });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "No se proporcionó ningún archivo." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Solo se permiten archivos PDF." });
        }

        const long maxBytes = 10L * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return BadRequest(new { message = "El archivo no puede superar 10 MB." });
        }

        var record = await _context.Censos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (record is null)
        {
            return NotFound(new { message = "No se encontró el registro de censo." });
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, cancellationToken);
            bytes = ms.ToArray();
        }

        var adjunto = new Data.Entities.CensoAdjunto
        {
            CensoRecordId = id,
            FileName = Path.GetFileName(file.FileName),
            FileData = bytes,
            UploadedAtUtc = DateTime.UtcNow
        };

        _context.CensoAdjuntos.Add(adjunto);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { adjuntoId = adjunto.Id, fileName = adjunto.FileName });
    }

    [HttpPost]
    public async Task<IActionResult> EliminarAdjunto(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoAdjuntos.FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);
        if (adjunto is null)
        {
            return NotFound(new { message = "Adjunto no encontrado." });
        }

        _context.CensoAdjuntos.Remove(adjunto);
        await _context.SaveChangesAsync(cancellationToken);

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> DescargarAdjunto(long adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _context.CensoAdjuntos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound();
        }

        return File(adjunto.FileData, "application/pdf", adjunto.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerAdjuntos(long id, CancellationToken cancellationToken)
    {
        var adjuntos = await _context.CensoAdjuntos
            .AsNoTracking()
            .Where(x => x.CensoRecordId == id)
            .OrderBy(x => x.UploadedAtUtc)
            .Select(x => new { x.Id, x.FileName })
            .ToListAsync(cancellationToken);

        return Json(adjuntos);
    }

    [HttpPost]
    public async Task<IActionResult> ValidarDireccion([FromBody] ValidateAddressRequest request, CancellationToken cancellationToken)
    {
        var direccion = request?.Direccion?.Trim() ?? string.Empty;
        var result = await _addressValidationService.ValidateAddressAsync(direccion, cancellationToken);
        var municipioCanonical = ToCanonicalMunicipality(result.Municipality);
        var barrioSugerido = result.Neighborhood?.Trim();
        var districtSugerido = result.District?.Trim();
        var barrios = !string.IsNullOrWhiteSpace(municipioCanonical)
            ? await _addressValidationService.SearchNeighborhoodsAsync(municipioCanonical, barrioSugerido ?? "a", cancellationToken)
            : [];
        var zonaInferida = !string.IsNullOrWhiteSpace(municipioCanonical)
            ? InferZonaDireccionSegunMunicipio(municipioCanonical, barrioSugerido, districtSugerido, result.FormattedAddress)
            : null;
        var candidateDtos = result.Candidates
            .Select(candidate =>
            {
                var candidateMunicipio = ToCanonicalMunicipality(candidate.Municipality);
                var candidateBarrio = candidate.Neighborhood?.Trim();
                var candidateDistrict = candidate.District?.Trim();
                var candidateZona = !string.IsNullOrWhiteSpace(candidateMunicipio)
                    ? InferZonaDireccionSegunMunicipio(candidateMunicipio, candidateBarrio, candidateDistrict, candidate.FormattedAddress)
                    : null;

                return new
                {
                    formattedAddress = candidate.FormattedAddress,
                    municipality = candidateMunicipio,
                    neighborhood = candidateBarrio,
                    district = candidateDistrict,
                    clasificacionZonaSura = !string.IsNullOrWhiteSpace(candidateMunicipio) ? InferClasificacionZonaSura(candidateMunicipio) : null,
                    zonaDireccionSegunMunicipio = candidateZona,
                    isValid = candidate.IsReliable
                };
            })
            .ToList();

        return Json(new
        {
            outcome = result.Outcome.ToString().ToLowerInvariant(),
            message = result.Message,
            formattedAddress = result.FormattedAddress,
            suggestedAddress = result.SuggestedAddress,
            isValid = result.Outcome == AddressValidationOutcome.Valid,
            municipality = municipioCanonical,
            neighborhood = barrioSugerido,
            district = districtSugerido,
            clasificacionZonaSura = !string.IsNullOrWhiteSpace(municipioCanonical) ? InferClasificacionZonaSura(municipioCanonical) : null,
            zonaDireccionSegunMunicipio = zonaInferida,
            neighborhoodOptions = barrios,
            requiresSelection = result.RequiresSelection,
            candidates = candidateDtos
        });
    }

    [HttpGet]
    public async Task<IActionResult> BuscarBarrios(string municipio, string term, CancellationToken cancellationToken)
    {
        var municipalityCanonical = ToCanonicalMunicipality(municipio);
        if (string.IsNullOrWhiteSpace(municipalityCanonical))
        {
            return Json(new { neighborhoods = Array.Empty<string>() });
        }

        var queryTerm = string.IsNullOrWhiteSpace(term) ? "a" : term.Trim();
        var neighborhoods = await _addressValidationService.SearchNeighborhoodsAsync(municipalityCanonical, queryTerm, cancellationToken);
        if (neighborhoods.Count == 0)
        {
            neighborhoods = ["NO PARAMETRIZADO"];
        }

        return Json(new { neighborhoods });
    }

    [HttpGet]
    public IActionResult ObtenerDefaultsMunicipio(string municipio, string? barrio = null, string? district = null, string? direccion = null)
    {
        var municipalityCanonical = ToCanonicalMunicipality(municipio);
        if (string.IsNullOrWhiteSpace(municipalityCanonical))
        {
            return Json(new
            {
                clasificacionZonaSura = string.Empty,
                zonaDireccionSegunMunicipio = string.Empty
            });
        }

        return Json(new
        {
            clasificacionZonaSura = InferClasificacionZonaSura(municipalityCanonical),
            zonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(municipalityCanonical, barrio, district, direccion)
        });
    }

    [HttpGet]
    public IActionResult BuscarDiagnosticoCie10(string codigo)
    {
        var normalizedCode = NormalizeCie10(codigo);
        if (string.IsNullOrWhiteSpace(normalizedCode) || !Cie10Pattern.IsMatch(normalizedCode))
        {
            return Json(new
            {
                found = false,
                codigo = normalizedCode,
                diagnostico = string.Empty
            });
        }

        var found = _cie10Catalog.TryGetValue(normalizedCode, out var diagnostico);
        return Json(new
        {
            found,
            codigo = normalizedCode,
            diagnostico = found ? diagnostico : string.Empty
        });
    }

    [HttpGet]
    public async Task<IActionResult> BuscarDatosPacienteProrroga(string documento, CancellationToken cancellationToken)
    {
        var normalizedDocument = NormalizeCedulaFilter(documento);
        if (string.IsNullOrWhiteSpace(normalizedDocument))
        {
            return BadRequest(new { message = "Ingresa el documento del paciente." });
        }

        var record = await _context.Censos
            .AsNoTracking()
            .Where(x => x.NumeroIdentificacion == normalizedDocument)
            .OrderByDescending(x => x.FechaIngreso)
            .ThenByDescending(x => x.HoraIngreso)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            return Json(new
            {
                found = false,
                message = "Documento no encontrado, proceder como un nuevo ingreso."
            });
        }

        return Json(new
        {
            found = true,
            sourceRecordId = record.Id,
            data = new
            {
                record.NombrePaciente,
                record.TipoIdentificacion,
                record.NumeroIdentificacion,
                record.CodigoCie10,
                record.DiagnosticoDescriptivo,
                fechaNacimiento = record.FechaNacimiento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                record.Edad,
                record.CorreoElectronico,
                record.Direccion,
                record.ClasificacionZonaSura,
                record.MunicipioResidencia,
                record.Barrio,
                record.ZonaDireccionSegunMunicipio,
                record.Area,
                record.IpsQueRemite,
                record.VistoBuenoRangoFueraAnexo,
                record.Telefono1,
                record.Telefono2,
                record.Telefono3
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerDatosSeccion2(string? numeroDocumento, CancellationToken cancellationToken)
    {
        var normalizedDoc = NormalizeCedulaFilter(numeroDocumento);
        if (string.IsNullOrWhiteSpace(normalizedDoc))
            return BadRequest(new { message = "Ingresa el número de documento del paciente." });

        var record = await _context.Censos
            .AsNoTracking()
            .Where(x => x.NumeroIdentificacion == normalizedDoc)
            .OrderByDescending(x => x.FechaIngreso)
            .ThenByDescending(x => x.HoraIngreso)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
            return Json(new { found = false, message = "Documento no encontrado. Proceda como un nuevo ingreso." });

        return Json(new
        {
            found = true,
            data = new
            {
                record.NombrePaciente,
                record.TipoIdentificacion,
                record.NumeroIdentificacion,
                record.CodigoCie10,
                record.DiagnosticoDescriptivo,
                fechaNacimiento = record.FechaNacimiento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                record.Edad,
                record.CorreoElectronico,
                record.Direccion,
                record.DetalleDireccion,
                record.ClasificacionZonaSura,
                record.MunicipioResidencia,
                record.Barrio,
                record.ZonaDireccionSegunMunicipio,
                record.Area,
                record.Telefono1,
                record.Telefono2,
                record.Telefono3
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportarExcel(CancellationToken cancellationToken)
    {
        var records = await _context.Censos
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var idsConAdjuntos = await BuildIdsConAdjuntosSetAsync(records, cancellationToken);
        var content = BuildExcelXml(records, idsConAdjuntos);
        var bytes = Encoding.UTF8.GetBytes(content);
        var fileName = $"censo_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> ExportarExcelFiltroPersonalizado(
        string? cedulaPaciente,
        DateTime? fechaIngresoDesde,
        DateTime? fechaIngresoHasta,
        CancellationToken cancellationToken)
    {
        var cedulaFiltro = NormalizeCedulaFilter(cedulaPaciente);
        var fechaDesde = fechaIngresoDesde?.Date;
        var fechaHasta = fechaIngresoHasta?.Date;

        if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde > fechaHasta)
        {
            (fechaDesde, fechaHasta) = (fechaHasta, fechaDesde);
        }

        var query = ApplyHistoryFilters(_context.Censos.AsNoTracking(), cedulaFiltro, fechaDesde, fechaHasta);
        var records = await query
            .OrderByDescending(x => x.FechaIngreso)
            .ThenByDescending(x => x.HoraIngreso)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var idsConAdjuntos = await BuildIdsConAdjuntosSetAsync(records, cancellationToken);
        var content = BuildExcelXml(records, idsConAdjuntos);
        var bytes = Encoding.UTF8.GetBytes(content);
        var fileName = BuildFilteredExcelFileName(fechaDesde, fechaHasta);
        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    private async Task<HashSet<long>> BuildIdsConAdjuntosSetAsync(
        IReadOnlyList<CensoRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var ids = records.Select(r => r.Id).ToList();
        var idsConAdjuntos = await _context.CensoAdjuntos
            .Where(a => ids.Contains(a.CensoRecordId))
            .Select(a => a.CensoRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. idsConAdjuntos];
    }

    private async Task PopulateCensoListAndLatestRecordAsync(
        CensoReceptionViewModel model,
        CancellationToken cancellationToken,
        bool loadLatestRecordIntoForm,
        long? selectedRecordId = null)
    {
        var cedulaFiltro = NormalizeCedulaFilter(model.CedulaFiltro);
        model.CedulaFiltro = cedulaFiltro;
        NormalizeHistoryFilters(model);

        var today = DateTime.Today;
        model.IngresosHoyCount = await _context.Censos
            .AsNoTracking()
            .CountAsync(x => x.FechaIngreso >= today && x.FechaIngreso < today.AddDays(1), cancellationToken);

        var query = ApplyHistoryFilters(
            _context.Censos.AsNoTracking(),
            cedulaFiltro,
            model.FechaIngresoFiltroDesde,
            model.FechaIngresoFiltroHasta);

        List<CensoRecord> records;
        if (model.TieneFiltroFechaIngreso)
        {
            records = await query
                .OrderByDescending(x => x.FechaIngreso)
                .ThenByDescending(x => x.HoraIngreso)
                .ThenByDescending(x => x.Id)
                .Take(50)
                .ToListAsync(cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(cedulaFiltro))
        {
            records = await query
                .OrderByDescending(x => x.FechaIngreso)
                .ThenByDescending(x => x.HoraIngreso)
                .ThenByDescending(x => x.Id)
                .Take(100)
                .ToListAsync(cancellationToken);
        }
        else
        {
            records = await query
                .OrderBy(x => x.FechaIngreso)
                .ThenBy(x => x.HoraIngreso)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        model.CensoListItems = records
            .Select(record => new CensoListItemViewModel
            {
                Id = record.Id,
                FechaIngreso = record.FechaIngreso,
                HoraIngreso = record.HoraIngreso,
                NombrePaciente = record.NombrePaciente,
                NumeroIdentificacion = record.NumeroIdentificacion,
                CodigoCie10 = record.CodigoCie10,
                Estado = record.Estado,
                GestionCompletaPendiente = record.GestionCompletaPendiente,
                TieneProrrogaActiva = !string.IsNullOrWhiteSpace(record.ProrrogaJson)
            })
            .ToList();
        model.CensoTableRecords = records;

        var tableRecordIds = records.Select(r => r.Id).ToList();
        if (tableRecordIds.Count > 0)
        {
            var idsConAdjuntos = await _context.CensoAdjuntos
                .Where(a => tableRecordIds.Contains(a.CensoRecordId))
                .Select(a => a.CensoRecordId)
                .Distinct()
                .ToListAsync(cancellationToken);
            model.RecordIdsConAdjuntos = idsConAdjuntos;
        }

        if (!loadLatestRecordIntoForm)
        {
        }

        CensoRecord? latestRecord = null;
        if (selectedRecordId.HasValue)
        {
            latestRecord = records.FirstOrDefault(x => x.Id == selectedRecordId.Value);
            if (latestRecord is null)
            {
                latestRecord = await _context.Censos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == selectedRecordId.Value, cancellationToken);
            }
        }

        if (latestRecord is null && !string.IsNullOrWhiteSpace(cedulaFiltro))
        {
            latestRecord = await query
                .OrderByDescending(x => x.FechaIngreso)
                .ThenByDescending(x => x.HoraIngreso)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (latestRecord is null)
        {
            model.EditingRecordId = null;
            return;
        }

        ApplyCensoRecordToModel(model, latestRecord);
    }

    private static void NormalizeHistoryFilters(CensoReceptionViewModel model)
    {
        model.FechaIngresoFiltroDesde = model.FechaIngresoFiltroDesde?.Date;
        model.FechaIngresoFiltroHasta = model.FechaIngresoFiltroHasta?.Date;

        if (model.FechaIngresoFiltroDesde.HasValue &&
            model.FechaIngresoFiltroHasta.HasValue &&
            model.FechaIngresoFiltroDesde > model.FechaIngresoFiltroHasta)
        {
            (model.FechaIngresoFiltroDesde, model.FechaIngresoFiltroHasta) =
                (model.FechaIngresoFiltroHasta, model.FechaIngresoFiltroDesde);
        }

        model.TieneFiltroFechaIngreso = model.FechaIngresoFiltroDesde.HasValue || model.FechaIngresoFiltroHasta.HasValue;
    }

    private static IQueryable<CensoRecord> ApplyHistoryFilters(
        IQueryable<CensoRecord> query,
        string? cedulaFiltro,
        DateTime? fechaIngresoDesde,
        DateTime? fechaIngresoHasta)
    {
        if (!string.IsNullOrWhiteSpace(cedulaFiltro))
        {
            query = query.Where(x => x.NumeroIdentificacion == cedulaFiltro);
        }

        if (fechaIngresoDesde.HasValue)
        {
            query = query.Where(x => x.FechaIngreso >= fechaIngresoDesde.Value.Date);
        }

        if (fechaIngresoHasta.HasValue)
        {
            var fechaHastaExclusiva = fechaIngresoHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.FechaIngreso < fechaHastaExclusiva);
        }

        return query;
    }

    private static string BuildFilteredExcelFileName(DateTime? fechaIngresoDesde, DateTime? fechaIngresoHasta)
    {
        var suffix = fechaIngresoDesde.HasValue || fechaIngresoHasta.HasValue
            ? $"_{fechaIngresoDesde?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "inicio"}_{fechaIngresoHasta?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "fin"}"
            : string.Empty;

        return $"censo_filtro_personalizado{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}.xls";
    }

    private void ApplyCensoRecordToModel(CensoReceptionViewModel model, CensoRecord record)
    {
        var med2Nombre = NormalizeAdditionalMedicationValueForForm(record.NombreMedicamentoNumero2);
        var med2Frecuencia = NormalizeAdditionalMedicationValueForForm(record.FrecuenciaAdministracionMedicamento2);
        var med2Dosis = NormalizeAdditionalMedicationValueForForm(record.NumeroDosisMedicamento2);
        var med2Medida = NormalizeAdditionalMedicationValueForForm(record.MedidaMedicamento2);
        var med2Via = NormalizeAdditionalMedicationValueForForm(record.ViaAdministracionMedicamento2);
        var med3Nombre = NormalizeAdditionalMedicationValueForForm(record.NombreMedicamentoNumero3);
        var med3Frecuencia = NormalizeAdditionalMedicationValueForForm(record.FrecuenciaAdministracionMedicamento3);
        var med3Dosis = NormalizeAdditionalMedicationValueForForm(record.NumeroDosisMedicamento3);
        var med3Medida = NormalizeAdditionalMedicationValueForForm(record.MedidaMedicamento3);
        var med3Via = NormalizeAdditionalMedicationValueForForm(record.ViaAdministracionMedicamento3);
        var med4Nombre = NormalizeAdditionalMedicationValueForForm(record.NombreMedicamentoNumero4);
        var med4Frecuencia = NormalizeAdditionalMedicationValueForForm(record.FrecuenciaAdministracionMedicamento4);
        var med4Dosis = NormalizeAdditionalMedicationValueForForm(record.NumeroDosisMedicamento4);
        var med4Medida = NormalizeAdditionalMedicationValueForForm(record.MedidaMedicamento4);
        var med4Via = NormalizeAdditionalMedicationValueForForm(record.ViaAdministracionMedicamento4);
        var med5Nombre = NormalizeAdditionalMedicationValueForForm(record.NombreMedicamentoNumero5);
        var med5Frecuencia = NormalizeAdditionalMedicationValueForForm(record.FrecuenciaAdministracionMedicamento5);
        var med5Dosis = NormalizeAdditionalMedicationValueForForm(record.NumeroDosisMedicamento5);
        var med5Medida = NormalizeAdditionalMedicationValueForForm(record.MedidaMedicamento5);
        var med5Via = NormalizeAdditionalMedicationValueForForm(record.ViaAdministracionMedicamento5);
        var med6Nombre = NormalizeAdditionalMedicationValueForForm(record.NombreMedicamentoNumero6);
        var med6Frecuencia = NormalizeAdditionalMedicationValueForForm(record.FrecuenciaAdministracionMedicamento6);
        var med6Dosis = NormalizeAdditionalMedicationValueForForm(record.NumeroDosisMedicamento6);
        var med6Medida = NormalizeAdditionalMedicationValueForForm(record.MedidaMedicamento6);
        var med6Via = NormalizeAdditionalMedicationValueForForm(record.ViaAdministracionMedicamento6);

        model.EditingRecordId = record.Id;
        model.CedulaFiltro = record.NumeroIdentificacion;
        model.EsProrroga = record.EsProrroga;
        model.TieneProrrogaActiva = !string.IsNullOrWhiteSpace(record.ProrrogaJson);
        model.DocumentoProrrogaBusqueda = record.EsProrroga ? record.NumeroIdentificacion : null;

        model.FechaIngreso = record.FechaIngreso.Date;
        model.HoraIngreso = record.HoraIngreso;
        model.FechaRespuesta = record.FechaRespuesta.Date;
        model.HoraRespuesta = record.HoraRespuesta;
        model.NombreRecepcionaCaso = record.NombreRecepcionaCaso;
        model.NombreRealizaKardex = record.NombreRealizaKardex;
        model.NombrePaciente = record.NombrePaciente;
        model.TipoIdentificacion = record.TipoIdentificacion;
        model.NumeroIdentificacion = record.NumeroIdentificacion;
        model.CodigoCie10 = record.CodigoCie10;
        model.DiagnosticoDescriptivo = record.DiagnosticoDescriptivo;
        model.FechaNacimiento = record.FechaNacimiento.Date;
        model.Edad = record.Edad;
        model.CorreoElectronico = record.CorreoElectronico;
        model.Direccion = record.Direccion;
        model.DetalleDireccion = record.DetalleDireccion;
        model.ClasificacionZonaSura = record.ClasificacionZonaSura;
        model.MunicipioResidencia = record.MunicipioResidencia;
        model.Barrio = record.Barrio;
        model.ZonaDireccionSegunMunicipio = record.ZonaDireccionSegunMunicipio;
        model.Area = record.Area;
        model.IpsQueRemite = record.IpsQueRemite;
        model.VistoBuenoRangoFueraAnexo = record.VistoBuenoRangoFueraAnexo;
        model.Telefono1 = record.Telefono1;
        model.Telefono2 = record.Telefono2;
        model.Telefono3 = record.Telefono3;
        model.ClasificacionRiesgo = record.ClasificacionRiesgo;
        model.AdministracionMedicamentos = record.AdministracionMedicamentos;
        model.NombreMedicamentoPrincipalTratante = record.NombreMedicamentoPrincipalTratante;
        model.DosisMedicamentoPrincipal = record.DosisMedicamentoPrincipal;
        model.MedidaMedicamentoPrincipal = record.MedidaMedicamentoPrincipal;
        model.ViaAdministracionMedicamentoPrincipal = record.ViaAdministracionMedicamentoPrincipal;
        model.FrecuenciaAdministracionMxPrincipal = record.FrecuenciaAdministracionMxPrincipal;
        model.DiasMedicamentoPrincipal = record.DiasMedicamentoPrincipal;
        model.NumeroDosisDiaMedicamentoPrincipal = record.NumeroDosisDiaMedicamentoPrincipal;
        model.TieneSegundoMedicamento = HasMedicationData(med2Nombre, med2Frecuencia, med2Dosis, record.DosisMedicamento2, med2Medida, med2Via);
        model.TieneTercerMedicamento = HasMedicationData(med3Nombre, med3Frecuencia, med3Dosis, record.DosisMedicamento3, med3Medida, med3Via);
        model.TieneCuartoMedicamento = HasMedicationData(med4Nombre, med4Frecuencia, med4Dosis, record.DosisMedicamento4, med4Medida, med4Via);
        model.TieneQuintoMedicamento = HasMedicationData(med5Nombre, med5Frecuencia, med5Dosis, record.DosisMedicamento5, med5Medida, med5Via);
        model.TieneSextoMedicamento = HasMedicationData(med6Nombre, med6Frecuencia, med6Dosis, record.DosisMedicamento6, med6Medida, med6Via);
        if (model.TieneSextoMedicamento) { model.TieneQuintoMedicamento = true; }
        if (model.TieneQuintoMedicamento) { model.TieneCuartoMedicamento = true; }
        if (model.TieneCuartoMedicamento) { model.TieneTercerMedicamento = true; }
        if (model.TieneTercerMedicamento) { model.TieneSegundoMedicamento = true; }

        model.NombreMedicamentoNumero2 = med2Nombre;
        model.DosisMedicamento2 = record.DosisMedicamento2;
        model.MedidaMedicamento2 = med2Medida;
        model.ViaAdministracionMedicamento2 = med2Via;
        model.FrecuenciaAdministracionMedicamento2 = med2Frecuencia;
        model.DiasMedicamento2 = record.DiasMedicamento2;
        model.NumeroDosisMedicamento2 = med2Dosis;
        model.NombreMedicamentoNumero3 = med3Nombre;
        model.DosisMedicamento3 = record.DosisMedicamento3;
        model.MedidaMedicamento3 = med3Medida;
        model.ViaAdministracionMedicamento3 = med3Via;
        model.FrecuenciaAdministracionMedicamento3 = med3Frecuencia;
        model.DiasMedicamento3 = record.DiasMedicamento3;
        model.NumeroDosisMedicamento3 = med3Dosis;
        model.NombreMedicamentoNumero4 = med4Nombre;
        model.DosisMedicamento4 = record.DosisMedicamento4;
        model.MedidaMedicamento4 = med4Medida;
        model.ViaAdministracionMedicamento4 = med4Via;
        model.FrecuenciaAdministracionMedicamento4 = med4Frecuencia;
        model.DiasMedicamento4 = record.DiasMedicamento4;
        model.NumeroDosisMedicamento4 = med4Dosis;
        model.NombreMedicamentoNumero5 = med5Nombre;
        model.DosisMedicamento5 = record.DosisMedicamento5;
        model.MedidaMedicamento5 = med5Medida;
        model.ViaAdministracionMedicamento5 = med5Via;
        model.FrecuenciaAdministracionMedicamento5 = med5Frecuencia;
        model.DiasMedicamento5 = record.DiasMedicamento5;
        model.NumeroDosisMedicamento5 = med5Dosis;
        model.NombreMedicamentoNumero6 = med6Nombre;
        model.DosisMedicamento6 = record.DosisMedicamento6;
        model.MedidaMedicamento6 = med6Medida;
        model.ViaAdministracionMedicamento6 = med6Via;
        model.FrecuenciaAdministracionMedicamento6 = med6Frecuencia;
        model.DiasMedicamento6 = record.DiasMedicamento6;
        model.NumeroDosisMedicamento6 = med6Dosis;
        model.AplicacionesTotales = record.AplicacionesTotales;
        model.DiasTratamientoIv = record.DiasTratamientoIv;
        model.CambioFrecuenciaAdministracionTto = record.CambioFrecuenciaAdministracionTto;
        model.FrecuenciaAjustada = record.FrecuenciaAjustada;
        model.MedicamentoFrecuenciaAjustada = record.MedicamentoFrecuenciaAjustada;
        model.FechaInicioTratamiento = record.FechaInicioTratamiento;
        model.FechaFinTratamiento = record.FechaFinTratamiento;
        model.FechaPromesaInicioTto = record.FechaPromesaInicioTto;
        model.HoraPromesaInicioTto = record.HoraPromesaInicioTto;
        model.HoraPromesaInicioTtoDesde = null;
        model.HoraPromesaInicioTtoHasta = null;
        model.HoraPromesaInicioTtoMeridiano = null;
        if (!string.IsNullOrWhiteSpace(record.HoraPromesaInicioTto))
        {
            var match = HoraPromesaPattern.Match(record.HoraPromesaInicioTto.Trim());
            if (match.Success)
            {
                model.HoraPromesaInicioTtoDesde = match.Groups["desde"].Value;
                model.HoraPromesaInicioTtoHasta = match.Groups["hasta"].Value;
            }
            else
            {
                var legacyMatch = HoraPromesaLegacyPattern.Match(record.HoraPromesaInicioTto.Trim());
                if (legacyMatch.Success)
                {
                    model.HoraPromesaInicioTtoDesde = legacyMatch.Groups["desde"].Value;
                    model.HoraPromesaInicioTtoHasta = legacyMatch.Groups["hasta"].Value;
                    model.HoraPromesaInicioTtoMeridiano = legacyMatch.Groups["meridiano"].Value.ToUpperInvariant();
                }
            }
        }

        model.AuxiliarAsignado = record.AuxiliarAsignado;
        model.Estado = record.Estado;
        model.AutorizacionEvento = record.AutorizacionEvento;
        model.ResponsableLlamadaBienvenida = record.ResponsableLlamadaBienvenida;
        model.EstadoLlamadaBienvenida = record.EstadoLlamadaBienvenida;
        model.ObservacionesPlanManejo = record.ObservacionesPlanManejo;
        model.NumeroTelefonoLlamadaBienvenida = record.NumeroTelefonoLlamadaBienvenida;
        model.NumeroDiasAutorizado = record.NumeroDiasAutorizado;
        model.RequiereServiciosComplementarios = record.RequiereServiciosComplementarios;
        model.ServicioComplementario = record.ServicioComplementario;
        model.PacienteGestante = record.PacienteGestante;
        model.Nebulizaciones = record.Nebulizaciones;
        model.SistemasPresionNegativaVac = record.SistemasPresionNegativaVac;
        model.NutricionParenteral = record.NutricionParenteral;
        model.NutricionEnteral = record.NutricionEnteral;
        model.PacienteAnticoagulado = record.PacienteAnticoagulado;
        model.LaboratorioClinicoProcedimiento = record.LaboratorioClinicoProcedimiento;
        model.ClinicaHeridas = record.ClinicaHeridas;
        model.Aislamiento = record.Aislamiento;
        model.TipoAislamiento = record.TipoAislamiento;
        model.CateterismoOSv = record.CateterismoOSv;
        model.CateterPicc = record.CateterPicc;
        model.NumeroCalibreSonda = record.NumeroCalibreSonda;
        model.FechaUltimoCambioSonda = record.FechaUltimoCambioSonda;
        model.AuxiliarAsignadoCateterismo = record.AuxiliarAsignadoCateterismo;
        model.FechaProximoCambioSonda = record.FechaProximoCambioSonda;
        model.FechaUltimaCuracionPicc = record.FechaUltimaCuracionPicc;
        model.FechaAlta = record.FechaAlta;
        model.NombreQuienGestionaAlta = record.NombreQuienGestionaAlta;
        model.AltaTardia = record.AltaTardia;
        model.FechaPrimerSeguimiento24Horas = record.FechaPrimerSeguimiento24Horas;
        model.FechaSegundoSeguimiento48Horas = record.FechaSegundoSeguimiento48Horas;
        model.FechaTercerSeguimiento72Horas = record.FechaTercerSeguimiento72Horas;
        model.ObservacionAltaTardia = record.ObservacionAltaTardia;
        model.NombreQuienRealizaSeguimientoAltaTardia = record.NombreQuienRealizaSeguimientoAltaTardia;
        model.PacienteRehospitalizado = record.PacienteRehospitalizado;
        model.FechaRegistroReporteRehospitalizacion = record.FechaRegistroReporteRehospitalizacion;
        model.FechaRehospitalizacion = record.FechaRehospitalizacion;
        model.MotivoRehospitalizacion = record.MotivoRehospitalizacion;
        model.AmpliacionMotivoRehospitalizacion = record.AmpliacionMotivoRehospitalizacion;
        model.RemitidoPorRehospitalizacion = record.RemitidoPorRehospitalizacion;
        model.IpsIntramuralRehospitalizacion = record.IpsIntramuralRehospitalizacion;
        model.FechaPrimerSeguimientoRehospitalizacion = record.FechaPrimerSeguimientoRehospitalizacion;
        model.FechaSegundoSeguimientoRehospitalizacion = record.FechaSegundoSeguimientoRehospitalizacion;
        model.FechaTercerSeguimientoRehospitalizacion = record.FechaTercerSeguimientoRehospitalizacion;
        model.FechaAltaHospitalizacion = record.FechaAltaHospitalizacion;
        model.ObservacionRehospitalizacion = record.ObservacionRehospitalizacion;
        model.FechaNovedadDevolucionProductos = record.FechaNovedadDevolucionProductos;
        model.MotivoNovedadDevolucionProductos = record.MotivoNovedadDevolucionProductos;
        model.NotificacionAuxiliarDevolucionProductos = record.NotificacionAuxiliarDevolucionProductos;
        model.FechaMaximaDevolucionProductos = record.FechaMaximaDevolucionProductos;
        model.EstadoDevolucionServicioFarmaceutico = record.EstadoDevolucionServicioFarmaceutico;
        model.PresentaNovedadKardex = record.PresentaNovedadKardex;
        model.PresentaNovedadRequisicion = record.PresentaNovedadRequisicion;
        model.PresentaNovedadAutorizacion = record.PresentaNovedadAutorizacion;
        model.DescripcionNovedadDocumentosPaciente = record.DescripcionNovedadDocumentosPaciente;
        model.FechaReporteNovedadDocumentos = record.FechaReporteNovedadDocumentos;
        model.HoraReporteNovedadDocumentos = record.HoraReporteNovedadDocumentos;
        model.HoraGestionSolucionNovedadDocumentos = record.HoraGestionSolucionNovedadDocumentos;
        model.FechaGestionFarmacia = record.FechaGestionFarmacia != default ? record.FechaGestionFarmacia.Date : null;
        model.HoraGestionFarmacia = record.HoraGestionFarmacia != default ? record.HoraGestionFarmacia : null;
        model.GestionCompleta = string.Equals(record.GestionCompletaPendiente, GestionCompleta, StringComparison.OrdinalIgnoreCase);
        model.AsumirDireccionErrada = false;
        model.DireccionEsValida = true;
        model.DireccionSugerida = null;
        model.DireccionMensajeValidacion = null;
    }

    private void ApplyModelToCensoRecord(
        CensoRecord censoRecord,
        CensoReceptionViewModel model,
        string direccionParaGuardar,
        int indicadorTiempoRespuestaMinutos,
        int indicadorTiempoGestionMinutos)
    {
        censoRecord.Asegurador = AseguradorSuraEps;
        // EsProrroga is managed exclusively via GuardarProrroga; never overwrite from main form
        censoRecord.FechaIngreso = model.FechaIngreso.Date;
        censoRecord.HoraIngreso = model.HoraIngreso;
        censoRecord.FechaRespuesta = model.FechaRespuesta.Date;
        censoRecord.HoraRespuesta = model.HoraRespuesta;
        censoRecord.IndicadorTiempoRespuestaMinutos = indicadorTiempoRespuestaMinutos;
        censoRecord.NombrePerfilGestionaCaso = GetCurrentUserProfileName();
        censoRecord.NombreRecepcionaCaso = model.NombreRecepcionaCaso;
        censoRecord.NombreRealizaKardex = model.NombreRealizaKardex;
        censoRecord.NombrePaciente = model.NombrePaciente;
        censoRecord.TipoIdentificacion = model.TipoIdentificacion.ToUpperInvariant();
        censoRecord.NumeroIdentificacion = model.NumeroIdentificacion;
        censoRecord.CodigoCie10 = model.CodigoCie10;
        censoRecord.DiagnosticoDescriptivo = model.DiagnosticoDescriptivo ?? string.Empty;
        censoRecord.FechaNacimiento = model.FechaNacimiento.Date;
        censoRecord.Edad = model.Edad;
        censoRecord.CorreoElectronico = model.CorreoElectronico;
        censoRecord.Direccion = direccionParaGuardar.Trim();
        censoRecord.DetalleDireccion = string.IsNullOrWhiteSpace(model.DetalleDireccion) ? null : model.DetalleDireccion.Trim();
        censoRecord.ClasificacionZonaSura = model.ClasificacionZonaSura;
        censoRecord.MunicipioResidencia = model.MunicipioResidencia;
        censoRecord.Barrio = model.Barrio;
        censoRecord.ZonaDireccionSegunMunicipio = model.ZonaDireccionSegunMunicipio;
        censoRecord.Area = model.Area;
        censoRecord.IpsQueRemite = model.IpsQueRemite;
        censoRecord.VistoBuenoRangoFueraAnexo = model.VistoBuenoRangoFueraAnexo;
        censoRecord.Telefono1 = model.Telefono1;
        censoRecord.Telefono2 = model.Telefono2;
        censoRecord.Telefono3 = string.IsNullOrWhiteSpace(model.Telefono3) ? null : model.Telefono3;
        censoRecord.ClasificacionRiesgo = model.ClasificacionRiesgo;
        censoRecord.AdministracionMedicamentos = model.AdministracionMedicamentos;
        censoRecord.NombreMedicamentoPrincipalTratante = string.IsNullOrWhiteSpace(model.NombreMedicamentoPrincipalTratante) ? null : model.NombreMedicamentoPrincipalTratante;
        censoRecord.DosisMedicamentoPrincipal = model.DosisMedicamentoPrincipal;
        censoRecord.MedidaMedicamentoPrincipal = string.IsNullOrWhiteSpace(model.MedidaMedicamentoPrincipal) ? null : model.MedidaMedicamentoPrincipal;
        censoRecord.ViaAdministracionMedicamentoPrincipal = string.IsNullOrWhiteSpace(model.ViaAdministracionMedicamentoPrincipal) ? null : model.ViaAdministracionMedicamentoPrincipal;
        censoRecord.FrecuenciaAdministracionMxPrincipal = model.FrecuenciaAdministracionMxPrincipal;
        censoRecord.DiasMedicamentoPrincipal = model.DiasMedicamentoPrincipal;
        censoRecord.NumeroDosisDiaMedicamentoPrincipal = model.NumeroDosisDiaMedicamentoPrincipal;
        censoRecord.NombreMedicamentoNumero2 = GetAdditionalMedicationValueForStorage(model.TieneSegundoMedicamento, model.NombreMedicamentoNumero2);
        censoRecord.DosisMedicamento2 = model.TieneSegundoMedicamento ? model.DosisMedicamento2 : null;
        censoRecord.MedidaMedicamento2 = GetAdditionalMedicationValueForStorage(model.TieneSegundoMedicamento, model.MedidaMedicamento2);
        censoRecord.ViaAdministracionMedicamento2 = GetAdditionalMedicationValueForStorage(model.TieneSegundoMedicamento, model.ViaAdministracionMedicamento2);
        censoRecord.FrecuenciaAdministracionMedicamento2 = GetAdditionalMedicationValueForStorage(model.TieneSegundoMedicamento, model.FrecuenciaAdministracionMedicamento2);
        censoRecord.DiasMedicamento2 = model.TieneSegundoMedicamento ? model.DiasMedicamento2 : null;
        censoRecord.NumeroDosisMedicamento2 = GetAdditionalMedicationValueForStorage(model.TieneSegundoMedicamento, model.NumeroDosisMedicamento2);
        censoRecord.NombreMedicamentoNumero3 = GetAdditionalMedicationValueForStorage(model.TieneTercerMedicamento, model.NombreMedicamentoNumero3);
        censoRecord.DosisMedicamento3 = model.TieneTercerMedicamento ? model.DosisMedicamento3 : null;
        censoRecord.MedidaMedicamento3 = GetAdditionalMedicationValueForStorage(model.TieneTercerMedicamento, model.MedidaMedicamento3);
        censoRecord.ViaAdministracionMedicamento3 = GetAdditionalMedicationValueForStorage(model.TieneTercerMedicamento, model.ViaAdministracionMedicamento3);
        censoRecord.FrecuenciaAdministracionMedicamento3 = GetAdditionalMedicationValueForStorage(model.TieneTercerMedicamento, model.FrecuenciaAdministracionMedicamento3);
        censoRecord.DiasMedicamento3 = model.TieneTercerMedicamento ? model.DiasMedicamento3 : null;
        censoRecord.NumeroDosisMedicamento3 = GetAdditionalMedicationValueForStorage(model.TieneTercerMedicamento, model.NumeroDosisMedicamento3);
        censoRecord.NombreMedicamentoNumero4 = GetAdditionalMedicationValueForStorage(model.TieneCuartoMedicamento, model.NombreMedicamentoNumero4);
        censoRecord.DosisMedicamento4 = model.TieneCuartoMedicamento ? model.DosisMedicamento4 : null;
        censoRecord.MedidaMedicamento4 = GetAdditionalMedicationValueForStorage(model.TieneCuartoMedicamento, model.MedidaMedicamento4);
        censoRecord.ViaAdministracionMedicamento4 = GetAdditionalMedicationValueForStorage(model.TieneCuartoMedicamento, model.ViaAdministracionMedicamento4);
        censoRecord.FrecuenciaAdministracionMedicamento4 = GetAdditionalMedicationValueForStorage(model.TieneCuartoMedicamento, model.FrecuenciaAdministracionMedicamento4);
        censoRecord.DiasMedicamento4 = model.TieneCuartoMedicamento ? model.DiasMedicamento4 : null;
        censoRecord.NumeroDosisMedicamento4 = GetAdditionalMedicationValueForStorage(model.TieneCuartoMedicamento, model.NumeroDosisMedicamento4);
        censoRecord.NombreMedicamentoNumero5 = GetAdditionalMedicationValueForStorage(model.TieneQuintoMedicamento, model.NombreMedicamentoNumero5);
        censoRecord.DosisMedicamento5 = model.TieneQuintoMedicamento ? model.DosisMedicamento5 : null;
        censoRecord.MedidaMedicamento5 = GetAdditionalMedicationValueForStorage(model.TieneQuintoMedicamento, model.MedidaMedicamento5);
        censoRecord.ViaAdministracionMedicamento5 = GetAdditionalMedicationValueForStorage(model.TieneQuintoMedicamento, model.ViaAdministracionMedicamento5);
        censoRecord.FrecuenciaAdministracionMedicamento5 = GetAdditionalMedicationValueForStorage(model.TieneQuintoMedicamento, model.FrecuenciaAdministracionMedicamento5);
        censoRecord.DiasMedicamento5 = model.TieneQuintoMedicamento ? model.DiasMedicamento5 : null;
        censoRecord.NumeroDosisMedicamento5 = GetAdditionalMedicationValueForStorage(model.TieneQuintoMedicamento, model.NumeroDosisMedicamento5);
        censoRecord.NombreMedicamentoNumero6 = GetAdditionalMedicationValueForStorage(model.TieneSextoMedicamento, model.NombreMedicamentoNumero6);
        censoRecord.DosisMedicamento6 = model.TieneSextoMedicamento ? model.DosisMedicamento6 : null;
        censoRecord.MedidaMedicamento6 = GetAdditionalMedicationValueForStorage(model.TieneSextoMedicamento, model.MedidaMedicamento6);
        censoRecord.ViaAdministracionMedicamento6 = GetAdditionalMedicationValueForStorage(model.TieneSextoMedicamento, model.ViaAdministracionMedicamento6);
        censoRecord.FrecuenciaAdministracionMedicamento6 = GetAdditionalMedicationValueForStorage(model.TieneSextoMedicamento, model.FrecuenciaAdministracionMedicamento6);
        censoRecord.DiasMedicamento6 = model.TieneSextoMedicamento ? model.DiasMedicamento6 : null;
        censoRecord.NumeroDosisMedicamento6 = GetAdditionalMedicationValueForStorage(model.TieneSextoMedicamento, model.NumeroDosisMedicamento6);
        censoRecord.AplicacionesTotales = string.IsNullOrWhiteSpace(model.AplicacionesTotales) ? null : model.AplicacionesTotales;
        censoRecord.DiasTratamientoIv = string.IsNullOrWhiteSpace(model.DiasTratamientoIv) ? null : model.DiasTratamientoIv;
        censoRecord.CambioFrecuenciaAdministracionTto = string.IsNullOrWhiteSpace(model.CambioFrecuenciaAdministracionTto) ? null : model.CambioFrecuenciaAdministracionTto;
        censoRecord.FrecuenciaAjustada = string.IsNullOrWhiteSpace(model.FrecuenciaAjustada) ? null : model.FrecuenciaAjustada;
        censoRecord.MedicamentoFrecuenciaAjustada = string.IsNullOrWhiteSpace(model.MedicamentoFrecuenciaAjustada) ? null : model.MedicamentoFrecuenciaAjustada;
        censoRecord.FechaInicioTratamiento = model.FechaInicioTratamiento?.Date;
        censoRecord.FechaFinTratamiento = model.FechaFinTratamiento?.Date;
        censoRecord.FechaPromesaInicioTto = model.FechaPromesaInicioTto?.Date;
        censoRecord.HoraPromesaInicioTto = string.IsNullOrWhiteSpace(model.HoraPromesaInicioTto) ? null : model.HoraPromesaInicioTto;
        censoRecord.AuxiliarAsignado = string.IsNullOrWhiteSpace(model.AuxiliarAsignado) ? null : model.AuxiliarAsignado;
        censoRecord.Estado = string.IsNullOrWhiteSpace(model.Estado) ? null : model.Estado;
        censoRecord.AutorizacionEvento = string.IsNullOrWhiteSpace(model.AutorizacionEvento) ? null : model.AutorizacionEvento;
        censoRecord.ResponsableLlamadaBienvenida = string.IsNullOrWhiteSpace(model.ResponsableLlamadaBienvenida) ? null : model.ResponsableLlamadaBienvenida;
        censoRecord.EstadoLlamadaBienvenida = string.IsNullOrWhiteSpace(model.EstadoLlamadaBienvenida) ? null : model.EstadoLlamadaBienvenida;
        censoRecord.ObservacionesPlanManejo = string.IsNullOrWhiteSpace(model.ObservacionesPlanManejo) ? null : model.ObservacionesPlanManejo;
        censoRecord.NumeroTelefonoLlamadaBienvenida = string.IsNullOrWhiteSpace(model.NumeroTelefonoLlamadaBienvenida) ? null : model.NumeroTelefonoLlamadaBienvenida;
        censoRecord.NumeroDiasAutorizado = string.IsNullOrWhiteSpace(model.NumeroDiasAutorizado) ? null : model.NumeroDiasAutorizado;
        censoRecord.RequiereServiciosComplementarios = string.IsNullOrWhiteSpace(model.RequiereServiciosComplementarios) ? null : model.RequiereServiciosComplementarios;
        censoRecord.ServicioComplementario = string.IsNullOrWhiteSpace(model.ServicioComplementario) ? null : model.ServicioComplementario;
        censoRecord.PacienteGestante = string.IsNullOrWhiteSpace(model.PacienteGestante) ? null : model.PacienteGestante;
        censoRecord.Nebulizaciones = string.IsNullOrWhiteSpace(model.Nebulizaciones) ? null : model.Nebulizaciones;
        censoRecord.SistemasPresionNegativaVac = string.IsNullOrWhiteSpace(model.SistemasPresionNegativaVac) ? null : model.SistemasPresionNegativaVac;
        censoRecord.NutricionParenteral = string.IsNullOrWhiteSpace(model.NutricionParenteral) ? null : model.NutricionParenteral;
        censoRecord.NutricionEnteral = string.IsNullOrWhiteSpace(model.NutricionEnteral) ? null : model.NutricionEnteral;
        censoRecord.PacienteAnticoagulado = string.IsNullOrWhiteSpace(model.PacienteAnticoagulado) ? null : model.PacienteAnticoagulado;
        censoRecord.LaboratorioClinicoProcedimiento = string.IsNullOrWhiteSpace(model.LaboratorioClinicoProcedimiento) ? null : model.LaboratorioClinicoProcedimiento;
        censoRecord.ClinicaHeridas = string.IsNullOrWhiteSpace(model.ClinicaHeridas) ? null : model.ClinicaHeridas;
        censoRecord.Aislamiento = string.IsNullOrWhiteSpace(model.Aislamiento) ? null : model.Aislamiento;
        censoRecord.TipoAislamiento = string.IsNullOrWhiteSpace(model.TipoAislamiento) ? null : model.TipoAislamiento;
        censoRecord.CateterismoOSv = string.IsNullOrWhiteSpace(model.CateterismoOSv) ? null : model.CateterismoOSv;
        censoRecord.CateterPicc = string.IsNullOrWhiteSpace(model.CateterPicc) ? null : model.CateterPicc;
        censoRecord.NumeroCalibreSonda = model.NumeroCalibreSonda;
        censoRecord.FechaUltimoCambioSonda = model.FechaUltimoCambioSonda?.Date;
        censoRecord.AuxiliarAsignadoCateterismo = string.IsNullOrWhiteSpace(model.AuxiliarAsignadoCateterismo) ? null : model.AuxiliarAsignadoCateterismo;
        censoRecord.FechaProximoCambioSonda = model.FechaProximoCambioSonda?.Date;
        censoRecord.FechaUltimaCuracionPicc = model.FechaUltimaCuracionPicc?.Date;
        censoRecord.FechaAlta = model.FechaAlta?.Date;
        censoRecord.NombreQuienGestionaAlta = string.IsNullOrWhiteSpace(model.NombreQuienGestionaAlta) ? null : model.NombreQuienGestionaAlta;
        censoRecord.AltaTardia = string.IsNullOrWhiteSpace(model.AltaTardia) ? null : model.AltaTardia;
        censoRecord.FechaPrimerSeguimiento24Horas = model.FechaPrimerSeguimiento24Horas?.Date;
        censoRecord.FechaSegundoSeguimiento48Horas = model.FechaSegundoSeguimiento48Horas?.Date;
        censoRecord.FechaTercerSeguimiento72Horas = model.FechaTercerSeguimiento72Horas?.Date;
        censoRecord.ObservacionAltaTardia = string.IsNullOrWhiteSpace(model.ObservacionAltaTardia) ? null : model.ObservacionAltaTardia;
        censoRecord.NombreQuienRealizaSeguimientoAltaTardia = string.IsNullOrWhiteSpace(model.NombreQuienRealizaSeguimientoAltaTardia) ? null : model.NombreQuienRealizaSeguimientoAltaTardia;
        censoRecord.PacienteRehospitalizado = string.IsNullOrWhiteSpace(model.PacienteRehospitalizado) ? null : model.PacienteRehospitalizado;
        censoRecord.FechaRegistroReporteRehospitalizacion = model.FechaRegistroReporteRehospitalizacion?.Date;
        censoRecord.FechaRehospitalizacion = model.FechaRehospitalizacion?.Date;
        censoRecord.MotivoRehospitalizacion = string.IsNullOrWhiteSpace(model.MotivoRehospitalizacion) ? null : model.MotivoRehospitalizacion;
        censoRecord.AmpliacionMotivoRehospitalizacion = string.IsNullOrWhiteSpace(model.AmpliacionMotivoRehospitalizacion) ? null : model.AmpliacionMotivoRehospitalizacion;
        censoRecord.RemitidoPorRehospitalizacion = string.IsNullOrWhiteSpace(model.RemitidoPorRehospitalizacion) ? null : model.RemitidoPorRehospitalizacion;
        censoRecord.IpsIntramuralRehospitalizacion = string.IsNullOrWhiteSpace(model.IpsIntramuralRehospitalizacion) ? null : model.IpsIntramuralRehospitalizacion;
        censoRecord.FechaPrimerSeguimientoRehospitalizacion = model.FechaPrimerSeguimientoRehospitalizacion?.Date;
        censoRecord.FechaSegundoSeguimientoRehospitalizacion = model.FechaSegundoSeguimientoRehospitalizacion?.Date;
        censoRecord.FechaTercerSeguimientoRehospitalizacion = model.FechaTercerSeguimientoRehospitalizacion?.Date;
        censoRecord.FechaAltaHospitalizacion = model.FechaAltaHospitalizacion?.Date;
        censoRecord.ObservacionRehospitalizacion = string.IsNullOrWhiteSpace(model.ObservacionRehospitalizacion) ? null : model.ObservacionRehospitalizacion;
        censoRecord.FechaNovedadDevolucionProductos = model.FechaNovedadDevolucionProductos?.Date;
        censoRecord.MotivoNovedadDevolucionProductos = string.IsNullOrWhiteSpace(model.MotivoNovedadDevolucionProductos) ? null : model.MotivoNovedadDevolucionProductos;
        censoRecord.NotificacionAuxiliarDevolucionProductos = string.IsNullOrWhiteSpace(model.NotificacionAuxiliarDevolucionProductos) ? null : model.NotificacionAuxiliarDevolucionProductos;
        censoRecord.FechaMaximaDevolucionProductos = model.FechaMaximaDevolucionProductos?.Date;
        censoRecord.EstadoDevolucionServicioFarmaceutico = string.IsNullOrWhiteSpace(model.EstadoDevolucionServicioFarmaceutico) ? null : model.EstadoDevolucionServicioFarmaceutico;
        censoRecord.PresentaNovedadKardex = string.IsNullOrWhiteSpace(model.PresentaNovedadKardex) ? null : model.PresentaNovedadKardex;
        censoRecord.PresentaNovedadRequisicion = string.IsNullOrWhiteSpace(model.PresentaNovedadRequisicion) ? null : model.PresentaNovedadRequisicion;
        censoRecord.PresentaNovedadAutorizacion = string.IsNullOrWhiteSpace(model.PresentaNovedadAutorizacion) ? null : model.PresentaNovedadAutorizacion;
        censoRecord.DescripcionNovedadDocumentosPaciente = string.IsNullOrWhiteSpace(model.DescripcionNovedadDocumentosPaciente) ? null : model.DescripcionNovedadDocumentosPaciente;
        censoRecord.FechaReporteNovedadDocumentos = model.FechaReporteNovedadDocumentos?.Date;
        censoRecord.HoraReporteNovedadDocumentos = model.HoraReporteNovedadDocumentos;
        censoRecord.HoraGestionSolucionNovedadDocumentos = model.HoraGestionSolucionNovedadDocumentos;
        // FechaGestionFarmacia/HoraGestionFarmacia are auto-set when sending to farmacia, not on regular save

        censoRecord.IndicadorTiempoGestionMinutos = indicadorTiempoGestionMinutos;
        censoRecord.GestionCompletaPendiente = model.GestionCompleta ? GestionCompleta : GestionPendiente;
    }

    private static bool HasAdditionalMedicationStoredData(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, ValorNoAplicaMedicamentoAdicional, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeAdditionalMedicationValueForForm(string? value)
    {
        return HasAdditionalMedicationStoredData(value) ? value : null;
    }

    private static bool IsEstadoAlta(string? estado)
    {
        return !string.IsNullOrWhiteSpace(estado)
            && estado.Contains("alta", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCedulaFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Trim();
    }

    private async Task PopulateDropdownsAsync(CensoReceptionViewModel model, CancellationToken cancellationToken)
    {
        ApplyPlanManejoDefaultValues(model);
        model.NursingAssistantOptions = await GetNursingAssistantOptionsAsync(cancellationToken);
        model.OpsAssistantOptions = await GetOpsAssistantOptionsAsync(cancellationToken);
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.AreaOptions = BuildOptions(AreaValues);
        model.IpsQueRemiteOptions = BuildOptions(IpsQueRemiteValues);
        model.VistoBuenoOptions = BuildOptions(VistoBuenoValues);
        model.ClasificacionRiesgoOptions = BuildOptions(ClasificacionRiesgoValues);
        model.AdministracionMedicamentosOptions = BuildOptions(AdministracionMedicamentosValues);
        model.CambioFrecuenciaAdministracionTtoOptions = BuildOptions(CambioFrecuenciaAdministracionTtoValues);
        model.FrecuenciaAjustadaOptions = BuildOptions(FrecuenciaAjustadaValues);
        model.SiNoOptions = BuildOptions(AdministracionMedicamentosValues);
        model.TipoAislamientoOptions = BuildOptions(TipoAislamientoValues);
        model.ServicioComplementarioOptions = BuildOptions(ServicioComplementarioValues);
        model.EstadoOptions = BuildOptions(EstadoValues);
        model.EstadoLlamadaBienvenidaOptions = BuildOptions(EstadoLlamadaBienvenidaValues);
        model.MotivoRehospitalizacionOptions = BuildOptions(MotivoRehospitalizacionValues);
        model.RemitidoPorRehospitalizacionOptions = BuildOptions(RemitidoPorRehospitalizacionValues);
        model.MotivoNovedadDevolucionProductosOptions = BuildOptions(MotivoNovedadDevolucionProductosValues);
        model.EstadoDevolucionServicioFarmaceuticoOptions = BuildOptions(EstadoDevolucionServicioFarmaceuticoValues);
        model.MedidaMedicamentoOptions = BuildOptions(MedidaMedicamentoValues);
        model.ViaAdministracionMedicamentoOptions = BuildOptions(ViaAdministracionMedicamentoValues);
        model.FrecuenciaAdministracionMxPrincipalOptions = BuildOptions(FrecuenciaAdministracionMxPrincipalValues);
        model.NumeroDosisDiaMedicamentoPrincipalOptions = BuildOptions(NumeroDosisDiaMedicamentoPrincipalValues);
        model.MedicamentoCatalog = await GetMedicamentoCatalogAsync(cancellationToken);
        model.MedicamentoPrincipalOptions = model.MedicamentoCatalog.Count > 0
            ? model.MedicamentoCatalog.Select(item => item.Nombre).ToList()
            : _medicamentoFallbackValues;

        model.TieneSegundoMedicamento = model.TieneSegundoMedicamento
            || HasMedicationData(model.NombreMedicamentoNumero2, model.FrecuenciaAdministracionMedicamento2, model.NumeroDosisMedicamento2, model.DosisMedicamento2, model.MedidaMedicamento2, model.ViaAdministracionMedicamento2);
        model.TieneTercerMedicamento = model.TieneTercerMedicamento
            || HasMedicationData(model.NombreMedicamentoNumero3, model.FrecuenciaAdministracionMedicamento3, model.NumeroDosisMedicamento3, model.DosisMedicamento3, model.MedidaMedicamento3, model.ViaAdministracionMedicamento3);
        model.TieneCuartoMedicamento = model.TieneCuartoMedicamento
            || HasMedicationData(model.NombreMedicamentoNumero4, model.FrecuenciaAdministracionMedicamento4, model.NumeroDosisMedicamento4, model.DosisMedicamento4, model.MedidaMedicamento4, model.ViaAdministracionMedicamento4);
        model.TieneQuintoMedicamento = model.TieneQuintoMedicamento
            || HasMedicationData(model.NombreMedicamentoNumero5, model.FrecuenciaAdministracionMedicamento5, model.NumeroDosisMedicamento5, model.DosisMedicamento5, model.MedidaMedicamento5, model.ViaAdministracionMedicamento5);
        model.TieneSextoMedicamento = model.TieneSextoMedicamento
            || HasMedicationData(model.NombreMedicamentoNumero6, model.FrecuenciaAdministracionMedicamento6, model.NumeroDosisMedicamento6, model.DosisMedicamento6, model.MedidaMedicamento6, model.ViaAdministracionMedicamento6);

        var municipioCanonical = ToCanonicalMunicipality(model.MunicipioResidencia) ?? MunicipioNoParametrizado;
        model.MunicipioResidencia = municipioCanonical;

        if (string.IsNullOrWhiteSpace(model.ClasificacionZonaSura))
        {
            model.ClasificacionZonaSura = InferClasificacionZonaSura(municipioCanonical);
        }

        if (string.IsNullOrWhiteSpace(model.ZonaDireccionSegunMunicipio))
        {
            model.ZonaDireccionSegunMunicipio = InferZonaDireccionSegunMunicipio(municipioCanonical, model.Barrio, direccion: model.Direccion);
        }

        if (string.IsNullOrWhiteSpace(model.Area))
        {
            model.Area = AreaValues[0];
        }

        if (!string.IsNullOrWhiteSpace(model.CodigoCie10))
        {
            model.CodigoCie10 = NormalizeCie10(model.CodigoCie10);
            if (string.IsNullOrWhiteSpace(model.DiagnosticoDescriptivo)
                && _cie10Catalog.TryGetValue(model.CodigoCie10, out var diagnostico))
            {
                model.DiagnosticoDescriptivo = diagnostico;
            }
        }

        if (!string.IsNullOrWhiteSpace(model.IpsQueRemite)
            && !IpsQueRemiteValues.Contains(model.IpsQueRemite, StringComparer.OrdinalIgnoreCase))
        {
            model.IpsQueRemiteOptions = model.IpsQueRemiteOptions
                .Append(new SelectListItem { Text = model.IpsQueRemite, Value = model.IpsQueRemite })
                .ToList();
        }

        var barrioOptions = await _addressValidationService.SearchNeighborhoodsAsync(
            municipioCanonical,
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
    }

    private static IReadOnlyList<SelectListItem> BuildOptions(IEnumerable<string> values)
    {
        return values
            .Select(value => new SelectListItem
            {
                Text = GetOptionDisplayText(value),
                Value = value
            })
            .ToList();
    }

    private async Task<IReadOnlyList<MedicamentoCatalogItemViewModel>> GetMedicamentoCatalogAsync(CancellationToken cancellationToken)
    {
        return await _context.Medicamentos
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Nombre)
            .Select(item => new MedicamentoCatalogItemViewModel
            {
                Nombre = item.Nombre,
                NormalizedNombre = item.NormalizedNombre,
                PresentacionRequisicion = item.PresentacionRequisicion,
                ConcentracionMiligramos = item.ConcentracionMiligramos,
                Jeringa = item.Jeringa,
                SolucionParaDilucion = item.SolucionParaDilucion,
                DilucionRecomendada = item.DilucionRecomendada,
                VehiculoReconstitucion = item.VehiculoReconstitucion,
                TiempoEstabilidad = item.TiempoEstabilidad,
                TiempoInfusionMinutos = item.TiempoInfusionMinutos,
                BombaInfusion = item.BombaInfusion,
                MarcacionRiesgo = item.MarcacionRiesgo,
                Flebozantes = item.Flebozantes,
                EquipoFotosensible = item.EquipoFotosensible,
                CadenaFrio = item.CadenaFrio
            })
            .ToListAsync(cancellationToken);
    }

    private static string GetOptionDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (OptionDisplayTextOverrides.TryGetValue(value, out var displayText))
        {
            return displayText;
        }

        return value
            .Replace("Ã¡", "á")
            .Replace("Ã©", "é")
            .Replace("Ã­", "í")
            .Replace("Ã³", "ó")
            .Replace("Ãº", "ú")
            .Replace("Ã", "Á")
            .Replace("Ã‰", "É")
            .Replace("Ã", "Í")
            .Replace("Ã“", "Ó")
            .Replace("Ãš", "Ú")
            .Replace("Ã±", "ñ")
            .Replace("Ã‘", "Ñ")
            .Replace("Ã¼", "ü")
            .Replace("Ãœ", "Ü");
    }

    private async Task<IReadOnlyList<SelectListItem>> GetNursingAssistantOptionsAsync(CancellationToken cancellationToken)
    {
        var assistants = await _userAdministrationService.GetNursingAssistantsAsync(onlyActive: true, cancellationToken);
        return assistants
            .Select(assistant => new SelectListItem
            {
                Text = assistant.Name,
                Value = assistant.Name
            })
            .ToList();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetOpsAssistantOptionsAsync(CancellationToken cancellationToken)
    {
        var assistants = await _userAdministrationService.GetOpsAssistantsAsync(onlyActive: true, cancellationToken);
        return assistants
            .Where(assistant => !string.IsNullOrWhiteSpace(assistant.Name))
            .Select(assistant => new SelectListItem
            {
                Text = assistant.Name,
                Value = assistant.Name
            })
            .ToList();
    }

    private void ValidateAssistantSelections(CensoReceptionViewModel model)
    {
        if (!model.NursingAssistantOptions.Any())
        {
            ModelState.AddModelError(string.Empty, "No hay auxiliares activos. Debes registrarlos en Administracion de usuarios.");
            return;
        }

        var allowed = model.NursingAssistantOptions.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowed.Contains(model.NombreRecepcionaCaso))
        {
            ModelState.AddModelError(nameof(model.NombreRecepcionaCaso), "Selecciona un auxiliar válido.");
        }

        if (!allowed.Contains(model.NombreRealizaKardex))
        {
            ModelState.AddModelError(nameof(model.NombreRealizaKardex), "Selecciona un auxiliar válido.");
        }
    }

    private static void ValidateBasicPatientData(CensoReceptionViewModel model)
    {
        if (!TiposIdentificacion.Contains(model.TipoIdentificacion, StringComparer.OrdinalIgnoreCase))
        {
            model.TipoIdentificacion = string.Empty;
        }

        if (model.FechaNacimiento.Date > DateTime.Today)
        {
            model.Edad = 0;
        }
    }

    private static void NormalizeGestionCompletaWithoutAuxiliar(CensoReceptionViewModel model)
    {
        if (model.GestionCompleta && string.IsNullOrWhiteSpace(model.AuxiliarAsignado))
        {
            model.GestionCompleta = false;
        }
    }

    private void ValidateRequiredPlanManejoFields(CensoReceptionViewModel model)
    {
        var hasMedicationAdministration = string.Equals(model.AdministracionMedicamentos, "Si", StringComparison.OrdinalIgnoreCase);
        if (hasMedicationAdministration)
        {
            AddRequiredFieldErrorIfBlank(model.NombreMedicamentoPrincipalTratante, nameof(model.NombreMedicamentoPrincipalTratante), "Ingresa el nombre del medicamento principal.");
            AddRequiredValueErrorIfMissing(model.DosisMedicamentoPrincipal, nameof(model.DosisMedicamentoPrincipal), "Ingresa la dosis del medicamento principal.");
            AddRequiredFieldErrorIfBlank(model.MedidaMedicamentoPrincipal, nameof(model.MedidaMedicamentoPrincipal), "Selecciona la medida del medicamento principal.");
            AddRequiredFieldErrorIfBlank(model.ViaAdministracionMedicamentoPrincipal, nameof(model.ViaAdministracionMedicamentoPrincipal), "Selecciona la vía de administración del medicamento principal.");
            ValidateMedicationDays(model.DiasMedicamentoPrincipal, nameof(model.DiasMedicamentoPrincipal), "medicamento principal", true);
            AddRequiredFieldErrorIfBlank(model.DiasTratamientoIv, nameof(model.DiasTratamientoIv), "Ingresa los días de tratamiento IV.");
            AddRequiredFieldErrorIfBlank(model.CambioFrecuenciaAdministracionTto, nameof(model.CambioFrecuenciaAdministracionTto), "Ingresa si se realizó cambio de frecuencia de administración de TTO.");
            if (string.Equals(model.CambioFrecuenciaAdministracionTto, "Si", StringComparison.OrdinalIgnoreCase))
            {
                AddRequiredFieldErrorIfBlank(model.MedicamentoFrecuenciaAjustada, nameof(model.MedicamentoFrecuenciaAjustada), "Selecciona el medicamento al que se le ajusto la frecuencia.");
                AddRequiredFieldErrorIfBlank(model.FrecuenciaAjustada, nameof(model.FrecuenciaAjustada), "Ingresa la frecuencia ajustada.");
            }
            AddRequiredDateErrorIfMissing(model.FechaInicioTratamiento, nameof(model.FechaInicioTratamiento), "Selecciona la fecha de inicio de tratamiento.");
            AddRequiredDateErrorIfMissing(model.FechaFinTratamiento, nameof(model.FechaFinTratamiento), "Selecciona la fecha fin de tratamiento.");
            AddRequiredDateErrorIfMissing(model.FechaPromesaInicioTto, nameof(model.FechaPromesaInicioTto), "Selecciona la fecha promesa de inicio de TTO.");
            if (string.IsNullOrWhiteSpace(model.HoraPromesaInicioTtoDesde)
                && string.IsNullOrWhiteSpace(model.HoraPromesaInicioTtoHasta))
            {
                ModelState.AddModelError(nameof(model.HoraPromesaInicioTto), "Ingresa la hora promesa de inicio de TTO.");
            }
        }

        AddRequiredFieldErrorIfBlank(model.Estado, nameof(model.Estado), "Selecciona el estado.");
        AddRequiredFieldErrorIfBlank(model.ResponsableLlamadaBienvenida, nameof(model.ResponsableLlamadaBienvenida), "Selecciona el responsable de llamada de bienvenida.");
        AddRequiredFieldErrorIfBlank(model.EstadoLlamadaBienvenida, nameof(model.EstadoLlamadaBienvenida), "Selecciona el estado de llamada de bienvenida.");
        if (string.Equals(model.EstadoLlamadaBienvenida, "Efectivo", StringComparison.OrdinalIgnoreCase))
        {
            AddRequiredFieldErrorIfBlank(model.NumeroTelefonoLlamadaBienvenida, nameof(model.NumeroTelefonoLlamadaBienvenida), "Ingresa el número de teléfono al que se llama.");
        }
        AddRequiredFieldErrorIfBlank(model.ObservacionesPlanManejo, nameof(model.ObservacionesPlanManejo), "Ingresa las observaciones.");
        AddRequiredFieldErrorIfBlank(model.RequiereServiciosComplementarios, nameof(model.RequiereServiciosComplementarios), "Selecciona si requiere servicios complementarios.");
        if (string.Equals(model.RequiereServiciosComplementarios, "Si", StringComparison.OrdinalIgnoreCase))
        {
            AddRequiredFieldErrorIfBlank(model.ServicioComplementario, nameof(model.ServicioComplementario), "Selecciona el servicio complementario.");
        }
        AddRequiredFieldErrorIfBlank(model.PacienteGestante, nameof(model.PacienteGestante), "Selecciona si el paciente es gestante.");
        AddRequiredFieldErrorIfBlank(model.Nebulizaciones, nameof(model.Nebulizaciones), "Selecciona si tiene nebulizaciones.");
        AddRequiredFieldErrorIfBlank(model.SistemasPresionNegativaVac, nameof(model.SistemasPresionNegativaVac), "Selecciona si tiene sistemas de presion negativa VAC.");
        AddRequiredFieldErrorIfBlank(model.NutricionParenteral, nameof(model.NutricionParenteral), "Selecciona si tiene nutricion parenteral.");
        AddRequiredFieldErrorIfBlank(model.NutricionEnteral, nameof(model.NutricionEnteral), "Selecciona si tiene nutricion enteral.");
        AddRequiredFieldErrorIfBlank(model.PacienteAnticoagulado, nameof(model.PacienteAnticoagulado), "Selecciona si el paciente esta anticoagulado.");
        AddRequiredFieldErrorIfBlank(model.LaboratorioClinicoProcedimiento, nameof(model.LaboratorioClinicoProcedimiento), "Selecciona si tiene laboratorio clinico/procedimiento.");
        AddRequiredFieldErrorIfBlank(model.ClinicaHeridas, nameof(model.ClinicaHeridas), "Selecciona si tiene clinica de heridas.");
        AddRequiredFieldErrorIfBlank(model.Aislamiento, nameof(model.Aislamiento), "Selecciona si requiere aislamiento.");
        if (string.Equals(model.Aislamiento, "Si", StringComparison.OrdinalIgnoreCase))
        {
            AddRequiredFieldErrorIfBlank(model.TipoAislamiento, nameof(model.TipoAislamiento), "Selecciona el tipo de aislamiento.");
        }
        AddRequiredFieldErrorIfBlank(model.CateterismoOSv, nameof(model.CateterismoOSv), "Selecciona si tiene cateterismo o SV.");
        AddRequiredFieldErrorIfBlank(model.CateterPicc, nameof(model.CateterPicc), "Selecciona si tiene cateter PICC.");
    }

    private void AddRequiredFieldErrorIfBlank(string? value, string fieldName, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ModelState.AddModelError(fieldName, errorMessage);
        }
    }

    private void AddRequiredDateErrorIfMissing(DateTime? value, string fieldName, string errorMessage)
    {
        if (!value.HasValue)
        {
            ModelState.AddModelError(fieldName, errorMessage);
        }
    }

    private void AddRequiredValueErrorIfMissing<T>(T? value, string fieldName, string errorMessage)
        where T : struct
    {
        if (!value.HasValue)
        {
            ModelState.AddModelError(fieldName, errorMessage);
        }
    }

    private void ValidateDropdownSelections(CensoReceptionViewModel model)
    {
        model.MunicipioResidencia = ToCanonicalMunicipality(model.MunicipioResidencia) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.MunicipioResidencia))
        {
            ModelState.AddModelError(nameof(model.MunicipioResidencia), "Selecciona un municipio válido.");
        }

        if (!ClasificacionZonaSuraValues.Contains(model.ClasificacionZonaSura, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ClasificacionZonaSura), "Selecciona una clasificación zona Sura válida.");
        }

        var zonaInferida = InferZonaDireccionSegunMunicipio(
            model.MunicipioResidencia,
            model.Barrio,
            direccion: model.Direccion);

        var zonaInferidaEsParametrizada = !string.Equals(
            zonaInferida,
            "No Parametrizado",
            StringComparison.OrdinalIgnoreCase);

        if (zonaInferidaEsParametrizada)
        {
            model.ZonaDireccionSegunMunicipio = zonaInferida;
        }

        if (!ZonaDireccionValues.Contains(model.ZonaDireccionSegunMunicipio, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ZonaDireccionSegunMunicipio), "Selecciona una zona de dirección válida.");
        }

        if (!AreaValues.Contains(model.Area, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Area), "Selecciona un area valida.");
        }

        if (!IpsQueRemiteValues.Contains(model.IpsQueRemite, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.IpsQueRemite), "Selecciona una IPS que remite valida.");
        }

        if (!VistoBuenoValues.Contains(model.VistoBuenoRangoFueraAnexo, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.VistoBuenoRangoFueraAnexo), "Selecciona un valor válido para visto bueno rango fuera del anexo.");
        }

        if (!ClasificacionRiesgoValues.Contains(model.ClasificacionRiesgo, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ClasificacionRiesgo), "Selecciona una clasificación de riesgo válida.");
        }

        if (!AdministracionMedicamentosValues.Contains(model.AdministracionMedicamentos, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.AdministracionMedicamentos), "Selecciona un valor válido para administración de medicamentos.");
        }

        var hasMedicationAdministration = string.Equals(model.AdministracionMedicamentos, "Si", StringComparison.OrdinalIgnoreCase);

        if (hasMedicationAdministration
            && !FrecuenciaAdministracionMxPrincipalValues.Contains(model.FrecuenciaAdministracionMxPrincipal, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.FrecuenciaAdministracionMxPrincipal), "Selecciona una frecuencia de administración MX principal válida.");
        }

        if (hasMedicationAdministration)
        {
            ValidateMedicationDose(model.DosisMedicamentoPrincipal, nameof(model.DosisMedicamentoPrincipal), "medicamento principal");
            ValidateOptionField(
                model.MedidaMedicamentoPrincipal,
                MedidaMedicamentoValues,
                nameof(model.MedidaMedicamentoPrincipal),
                "una medida válida para el medicamento principal");
            ValidateOptionField(
                model.ViaAdministracionMedicamentoPrincipal,
                ViaAdministracionMedicamentoValues,
                nameof(model.ViaAdministracionMedicamentoPrincipal),
                "una vía de administración válida para el medicamento principal");
        }

        if (hasMedicationAdministration
            && !string.IsNullOrWhiteSpace(model.NumeroDosisDiaMedicamentoPrincipal)
            && !NumeroDosisDiaMedicamentoPrincipalValues.Contains(model.NumeroDosisDiaMedicamentoPrincipal, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.NumeroDosisDiaMedicamentoPrincipal), "Selecciona un valor válido para número de dosis día medicamento principal.");
        }

        if (hasMedicationAdministration)
        {
            ValidateAdditionalMedication(
                model.TieneSegundoMedicamento,
                model.NombreMedicamentoNumero2,
                model.DosisMedicamento2,
                model.MedidaMedicamento2,
                model.ViaAdministracionMedicamento2,
                model.FrecuenciaAdministracionMedicamento2,
                model.DiasMedicamento2,
                model.NumeroDosisMedicamento2,
                nameof(model.NombreMedicamentoNumero2),
                nameof(model.DosisMedicamento2),
                nameof(model.MedidaMedicamento2),
                nameof(model.ViaAdministracionMedicamento2),
                nameof(model.FrecuenciaAdministracionMedicamento2),
                nameof(model.DiasMedicamento2),
                nameof(model.NumeroDosisMedicamento2),
                "medicamento 2");

            if (model.TieneTercerMedicamento && !model.TieneSegundoMedicamento)
            {
                ModelState.AddModelError(nameof(model.TieneSegundoMedicamento), "Para registrar un tercer medicamento primero debes activar el segundo medicamento.");
            }

            ValidateAdditionalMedication(
                model.TieneTercerMedicamento,
                model.NombreMedicamentoNumero3, model.DosisMedicamento3, model.MedidaMedicamento3,
                model.ViaAdministracionMedicamento3, model.FrecuenciaAdministracionMedicamento3,
                model.DiasMedicamento3, model.NumeroDosisMedicamento3,
                nameof(model.NombreMedicamentoNumero3), nameof(model.DosisMedicamento3),
                nameof(model.MedidaMedicamento3), nameof(model.ViaAdministracionMedicamento3),
                nameof(model.FrecuenciaAdministracionMedicamento3), nameof(model.DiasMedicamento3),
                nameof(model.NumeroDosisMedicamento3), "medicamento 3");

            ValidateAdditionalMedication(
                model.TieneCuartoMedicamento,
                model.NombreMedicamentoNumero4, model.DosisMedicamento4, model.MedidaMedicamento4,
                model.ViaAdministracionMedicamento4, model.FrecuenciaAdministracionMedicamento4,
                model.DiasMedicamento4, model.NumeroDosisMedicamento4,
                nameof(model.NombreMedicamentoNumero4), nameof(model.DosisMedicamento4),
                nameof(model.MedidaMedicamento4), nameof(model.ViaAdministracionMedicamento4),
                nameof(model.FrecuenciaAdministracionMedicamento4), nameof(model.DiasMedicamento4),
                nameof(model.NumeroDosisMedicamento4), "medicamento 4");

            ValidateAdditionalMedication(
                model.TieneQuintoMedicamento,
                model.NombreMedicamentoNumero5, model.DosisMedicamento5, model.MedidaMedicamento5,
                model.ViaAdministracionMedicamento5, model.FrecuenciaAdministracionMedicamento5,
                model.DiasMedicamento5, model.NumeroDosisMedicamento5,
                nameof(model.NombreMedicamentoNumero5), nameof(model.DosisMedicamento5),
                nameof(model.MedidaMedicamento5), nameof(model.ViaAdministracionMedicamento5),
                nameof(model.FrecuenciaAdministracionMedicamento5), nameof(model.DiasMedicamento5),
                nameof(model.NumeroDosisMedicamento5), "medicamento 5");

            ValidateAdditionalMedication(
                model.TieneSextoMedicamento,
                model.NombreMedicamentoNumero6, model.DosisMedicamento6, model.MedidaMedicamento6,
                model.ViaAdministracionMedicamento6, model.FrecuenciaAdministracionMedicamento6,
                model.DiasMedicamento6, model.NumeroDosisMedicamento6,
                nameof(model.NombreMedicamentoNumero6), nameof(model.DosisMedicamento6),
                nameof(model.MedidaMedicamento6), nameof(model.ViaAdministracionMedicamento6),
                nameof(model.FrecuenciaAdministracionMedicamento6), nameof(model.DiasMedicamento6),
                nameof(model.NumeroDosisMedicamento6), "medicamento 6");
        }

        if (!string.IsNullOrWhiteSpace(model.Estado)
            && !EstadoValues.Contains(model.Estado, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.Estado), "Selecciona un estado válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.AuxiliarAsignado))
        {
            var allowedOpsAssistants = model.OpsAssistantOptions.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (allowedOpsAssistants.Count == 0)
            {
                ModelState.AddModelError(nameof(model.AuxiliarAsignado), "No hay auxiliares OPS activos para asignar.");
            }
            else if (!allowedOpsAssistants.Contains(model.AuxiliarAsignado))
            {
                ModelState.AddModelError(nameof(model.AuxiliarAsignado), "Selecciona un auxiliar OPS válido.");
            }
        }

        if (hasMedicationAdministration)
        {
            ValidateOptionField(
                model.CambioFrecuenciaAdministracionTto,
                CambioFrecuenciaAdministracionTtoValues,
                nameof(model.CambioFrecuenciaAdministracionTto),
                "un valor valido para el cambio de frecuencia de administracion de TTO");

            if (string.Equals(model.CambioFrecuenciaAdministracionTto, "Si", StringComparison.OrdinalIgnoreCase))
            {
                ValidateAdjustedMedicationSelection(model);

                ValidateOptionField(
                    model.FrecuenciaAjustada,
                    FrecuenciaAjustadaValues,
                    nameof(model.FrecuenciaAjustada),
                    "una frecuencia ajustada valida");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.ResponsableLlamadaBienvenida))
        {
            var allowedAssistants = model.NursingAssistantOptions.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!allowedAssistants.Contains(model.ResponsableLlamadaBienvenida))
            {
                ModelState.AddModelError(nameof(model.ResponsableLlamadaBienvenida), "Selecciona un auxiliar administrativo válido.");
            }
        }

        if (!string.IsNullOrWhiteSpace(model.EstadoLlamadaBienvenida)
            && !EstadoLlamadaBienvenidaValues.Contains(model.EstadoLlamadaBienvenida, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.EstadoLlamadaBienvenida), "Selecciona un estado válido para la llamada de bienvenida.");
        }


        ValidateSiNoField(model.AltaTardia, nameof(model.AltaTardia), "alta tardia");
        ValidateSiNoField(model.PacienteRehospitalizado, nameof(model.PacienteRehospitalizado), "paciente rehospitalizado");

        ValidateOptionField(
            model.MotivoRehospitalizacion,
            MotivoRehospitalizacionValues,
            nameof(model.MotivoRehospitalizacion),
            "un motivo de rehospitalización válido");

        ValidateOptionField(
            model.RemitidoPorRehospitalizacion,
            RemitidoPorRehospitalizacionValues,
            nameof(model.RemitidoPorRehospitalizacion),
            "un remitido por válido");

        ValidateOptionField(
            model.MotivoNovedadDevolucionProductos,
            MotivoNovedadDevolucionProductosValues,
            nameof(model.MotivoNovedadDevolucionProductos),
            "un motivo de la novedad válido");

        ValidateSiNoField(
            model.NotificacionAuxiliarDevolucionProductos,
            nameof(model.NotificacionAuxiliarDevolucionProductos),
            "notificacion al auxiliar");

        ValidateOptionField(
            model.EstadoDevolucionServicioFarmaceutico,
            EstadoDevolucionServicioFarmaceuticoValues,
            nameof(model.EstadoDevolucionServicioFarmaceutico),
            "un estado de devolución válido");

        ValidateSiNoField(model.PresentaNovedadKardex, nameof(model.PresentaNovedadKardex), "presenta novedad en kardex");
        ValidateSiNoField(model.PresentaNovedadRequisicion, nameof(model.PresentaNovedadRequisicion), "presenta novedad en requisicion");
        ValidateSiNoField(model.PresentaNovedadAutorizacion, nameof(model.PresentaNovedadAutorizacion), "presenta novedad en la autorizacion");

        if (!string.IsNullOrWhiteSpace(model.NombreQuienRealizaSeguimientoAltaTardia))
        {
            var allowedAssistants = model.NursingAssistantOptions.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!allowedAssistants.Contains(model.NombreQuienRealizaSeguimientoAltaTardia))
            {
                ModelState.AddModelError(nameof(model.NombreQuienRealizaSeguimientoAltaTardia), "Selecciona un auxiliar administrativo válido.");
            }
        }

        ValidateHoraPromesaInicioTto(model);
        ValidateSiNoField(model.RequiereServiciosComplementarios, nameof(model.RequiereServiciosComplementarios), "requiere servicios complementarios");
        if (string.Equals(model.RequiereServiciosComplementarios, "Si", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(model.ServicioComplementario))
            {
                ModelState.AddModelError(nameof(model.ServicioComplementario), "Selecciona al menos un servicio complementario.");
            }
            else
            {
                var selectedServices = model.ServicioComplementario
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var svc in selectedServices)
                {
                    if (!ServicioComplementarioValues.Contains(svc, StringComparer.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError(nameof(model.ServicioComplementario), $"'{svc}' no es un servicio complementario válido.");
                        break;
                    }
                }
            }
        }
        ValidateSiNoField(model.PacienteGestante, nameof(model.PacienteGestante), "paciente gestante");
        ValidateSiNoField(model.Nebulizaciones, nameof(model.Nebulizaciones), "nebulizaciones");
        ValidateSiNoField(model.SistemasPresionNegativaVac, nameof(model.SistemasPresionNegativaVac), "sistemas de presion negativa VAC");
        ValidateSiNoField(model.NutricionParenteral, nameof(model.NutricionParenteral), "nutricion parenteral");
        ValidateSiNoField(model.NutricionEnteral, nameof(model.NutricionEnteral), "nutricion enteral");
        ValidateSiNoField(model.PacienteAnticoagulado, nameof(model.PacienteAnticoagulado), "paciente anticoagulado");
        ValidateSiNoField(model.LaboratorioClinicoProcedimiento, nameof(model.LaboratorioClinicoProcedimiento), "laboratorio clinico/procedimiento");
        ValidateSiNoField(model.ClinicaHeridas, nameof(model.ClinicaHeridas), "clinica de heridas");
        ValidateSiNoField(model.Aislamiento, nameof(model.Aislamiento), "aislamiento");
        if (string.Equals(model.Aislamiento, "Si", StringComparison.OrdinalIgnoreCase))
        {
            ValidateOptionField(
                model.TipoAislamiento,
                TipoAislamientoValues,
                nameof(model.TipoAislamiento),
                "un tipo de aislamiento válido");
        }
        ValidateSiNoField(model.CateterismoOSv, nameof(model.CateterismoOSv), "cateterismo o SV");
        ValidateSiNoField(model.CateterPicc, nameof(model.CateterPicc), "cateter PICC");
        ValidateCateterismoFields(model);
        ValidateCateterPiccFields(model);

        if (string.IsNullOrWhiteSpace(model.Barrio))
        {
            ModelState.AddModelError(nameof(model.Barrio), "Selecciona o escribe un barrio.");
        }
    }

    private void ValidateAdditionalMedication(
        bool enabled,
        string? nombre,
        decimal? dosis,
        string? medida,
        string? viaAdministracion,
        string? frecuencia,
        int? dias,
        string? numeroDosis,
        string nombreField,
        string dosisMedicamentoField,
        string medidaField,
        string viaAdministracionField,
        string frecuenciaField,
        string diasField,
        string numeroDosisField,
        string displayName)
    {
        if (!enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            ModelState.AddModelError(nombreField, $"Ingresa el nombre del {displayName}.");
        }

        if (!dosis.HasValue)
        {
            ModelState.AddModelError(dosisMedicamentoField, $"Ingresa la dosis del {displayName}.");
        }
        else
        {
            ValidateMedicationDose(dosis, dosisMedicamentoField, displayName);
        }

        if (string.IsNullOrWhiteSpace(medida))
        {
            ModelState.AddModelError(medidaField, $"Selecciona la medida del {displayName}.");
        }
        else
        {
            ValidateOptionField(medida, MedidaMedicamentoValues, medidaField, $"una medida valida para el {displayName}");
        }

        if (string.IsNullOrWhiteSpace(viaAdministracion))
        {
            ModelState.AddModelError(viaAdministracionField, $"Selecciona la vía de administración del {displayName}.");
        }
        else
        {
            ValidateOptionField(viaAdministracion, ViaAdministracionMedicamentoValues, viaAdministracionField, $"una via de administracion valida para el {displayName}");
        }

        if (string.IsNullOrWhiteSpace(frecuencia))
        {
            ModelState.AddModelError(frecuenciaField, $"Selecciona la frecuencia de administración del {displayName}.");
        }
        else if (!FrecuenciaAdministracionMxPrincipalValues.Contains(frecuencia, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(frecuenciaField, $"Selecciona una frecuencia de administración válida para el {displayName}.");
        }

        ValidateMedicationDays(dias, diasField, displayName, true);

        if (!string.IsNullOrWhiteSpace(numeroDosis)
            && !NumeroDosisDiaMedicamentoPrincipalValues.Contains(numeroDosis, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(numeroDosisField, $"Selecciona un número de dosis válido para el {displayName}.");
        }
    }

    private void ValidateMedicationDose(decimal? dosis, string fieldName, string displayName)
    {
        if (!dosis.HasValue)
        {
            return;
        }

        if (dosis.Value <= 0 || dosis.Value > 999999.99m)
        {
            ModelState.AddModelError(fieldName, $"La dosis del {displayName} debe estar entre 0.01 y 999999.99.");
        }
    }

    private void ValidateMedicationDays(int? dias, string fieldName, string displayName, bool required)
    {
        if (!dias.HasValue)
        {
            if (required)
            {
                ModelState.AddModelError(fieldName, $"Ingresa los días del {displayName}.");
            }

            return;
        }

        if (dias.Value < 1 || dias.Value > 999)
        {
            ModelState.AddModelError(fieldName, $"Los días del {displayName} deben estar entre 1 y 999.");
        }
    }

    private static bool HasMedicationData(
        string? nombre,
        string? frecuencia,
        string? numeroDosis,
        decimal? dosis = null,
        string? medida = null,
        string? viaAdministracion = null)
    {
        return !string.IsNullOrWhiteSpace(nombre)
            || !string.IsNullOrWhiteSpace(frecuencia)
            || !string.IsNullOrWhiteSpace(numeroDosis)
            || dosis.HasValue
            || !string.IsNullOrWhiteSpace(medida)
            || !string.IsNullOrWhiteSpace(viaAdministracion);
    }

    private void ValidateSiNoField(string? value, string fieldName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!AdministracionMedicamentosValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(fieldName, $"Selecciona un valor válido para {displayName}.");
        }
    }

    private void ValidateOptionField(string? value, IEnumerable<string> allowedValues, string fieldName, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(fieldName, $"Selecciona {errorMessage}.");
        }
    }

    private void ValidateAdjustedMedicationSelection(CensoReceptionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.MedicamentoFrecuenciaAjustada))
        {
            return;
        }

        var allowedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1" };
        if (model.TieneSegundoMedicamento)
        {
            allowedValues.Add("2");
        }

        if (model.TieneTercerMedicamento) { allowedValues.Add("3"); }
        if (model.TieneCuartoMedicamento) { allowedValues.Add("4"); }
        if (model.TieneQuintoMedicamento) { allowedValues.Add("5"); }
        if (model.TieneSextoMedicamento) { allowedValues.Add("6"); }

        if (!allowedValues.Contains(model.MedicamentoFrecuenciaAjustada))
        {
            ModelState.AddModelError(
                nameof(model.MedicamentoFrecuenciaAjustada),
                "Selecciona un medicamento activo para aplicar la frecuencia ajustada.");
        }
    }

    private void ValidateCateterismoFields(CensoReceptionViewModel model)
    {
        if (!string.Equals(model.CateterismoOSv, "Si", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (model.NumeroCalibreSonda.HasValue && model.NumeroCalibreSonda.Value <= 0)
        {
            ModelState.AddModelError(nameof(model.NumeroCalibreSonda), "Ingresa un número calibre de sonda válido.");
        }

        if (!string.IsNullOrWhiteSpace(model.AuxiliarAsignadoCateterismo))
        {
            var allowedOpsAssistants = model.OpsAssistantOptions.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!allowedOpsAssistants.Contains(model.AuxiliarAsignadoCateterismo))
            {
                ModelState.AddModelError(nameof(model.AuxiliarAsignadoCateterismo), "Selecciona un auxiliar OPS válido.");
            }
        }

        model.FechaProximoCambioSonda = model.FechaUltimoCambioSonda?.Date.AddDays(21);
    }

    private void ValidateCateterPiccFields(CensoReceptionViewModel model)
    {
        if (!string.Equals(model.CateterPicc, "Si", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!model.FechaUltimaCuracionPicc.HasValue)
        {
            ModelState.AddModelError(nameof(model.FechaUltimaCuracionPicc), "Selecciona la fecha de última curación.");
        }
    }

    private static void ApplyPlanManejoDefaultValues(CensoReceptionViewModel model)
    {
        model.VistoBuenoRangoFueraAnexo = NormalizeSiNoDefault(model.VistoBuenoRangoFueraAnexo);
        model.AdministracionMedicamentos = NormalizeSiNoDefault(model.AdministracionMedicamentos);
        model.RequiereServiciosComplementarios = NormalizeSiNoDefault(model.RequiereServiciosComplementarios);
        if (!string.Equals(model.RequiereServiciosComplementarios, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.ServicioComplementario = null;
        }
        model.PacienteGestante = NormalizeSiNoDefault(model.PacienteGestante);
        model.Nebulizaciones = NormalizeSiNoDefault(model.Nebulizaciones);
        model.SistemasPresionNegativaVac = NormalizeSiNoDefault(model.SistemasPresionNegativaVac);
        model.NutricionParenteral = NormalizeSiNoDefault(model.NutricionParenteral);
        model.NutricionEnteral = NormalizeSiNoDefault(model.NutricionEnteral);
        model.PacienteAnticoagulado = NormalizeSiNoDefault(model.PacienteAnticoagulado);
        model.LaboratorioClinicoProcedimiento = NormalizeSiNoDefault(model.LaboratorioClinicoProcedimiento);
        model.ClinicaHeridas = NormalizeSiNoDefault(model.ClinicaHeridas);
        model.Aislamiento = NormalizeSiNoDefault(model.Aislamiento);
        if (!string.Equals(model.Aislamiento, "Si", StringComparison.OrdinalIgnoreCase))
        {
            model.TipoAislamiento = null;
        }
        model.CateterismoOSv = NormalizeSiNoDefault(model.CateterismoOSv);
        model.CateterPicc = NormalizeSiNoDefault(model.CateterPicc);
        model.AltaTardia = NormalizeSiNoDefault(model.AltaTardia);
        model.PacienteRehospitalizado = NormalizeSiNoDefault(model.PacienteRehospitalizado);
        model.NotificacionAuxiliarDevolucionProductos = NormalizeSiNoDefault(model.NotificacionAuxiliarDevolucionProductos);
        model.PresentaNovedadKardex = NormalizeSiNoDefault(model.PresentaNovedadKardex);
        model.PresentaNovedadRequisicion = NormalizeSiNoDefault(model.PresentaNovedadRequisicion);
        model.PresentaNovedadAutorizacion = NormalizeSiNoDefault(model.PresentaNovedadAutorizacion);
    }

    private static string NormalizeSiNoDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No";
        }

        return value.Trim();
    }

    private static string? NormalizeServiciosComplementarios(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return items.Length > 0 ? string.Join(", ", items) : null;
    }

    private void ValidateHoraPromesaInicioTto(CensoReceptionViewModel model)
    {
        var hasHoraPromesaData =
            !string.IsNullOrWhiteSpace(model.HoraPromesaInicioTtoDesde)
            || !string.IsNullOrWhiteSpace(model.HoraPromesaInicioTtoHasta);

        if (!hasHoraPromesaData)
        {
            model.HoraPromesaInicioTto = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(model.HoraPromesaInicioTtoDesde))
        {
            ModelState.AddModelError(nameof(model.HoraPromesaInicioTtoDesde), "Selecciona la hora inicial de la promesa de inicio de TTO.");
        }
        else if (!HoraPromesaInicioTtoValues.Contains(model.HoraPromesaInicioTtoDesde, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.HoraPromesaInicioTtoDesde), "Selecciona una hora inicial valida para la promesa de inicio de TTO.");
        }

        if (string.IsNullOrWhiteSpace(model.HoraPromesaInicioTtoHasta))
        {
            ModelState.AddModelError(nameof(model.HoraPromesaInicioTtoHasta), "Selecciona la hora final de la promesa de inicio de TTO.");
        }
        else if (!HoraPromesaInicioTtoValues.Contains(model.HoraPromesaInicioTtoHasta, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.HoraPromesaInicioTtoHasta), "Selecciona una hora final valida para la promesa de inicio de TTO.");
        }

        if (ModelState.ContainsKey(nameof(model.HoraPromesaInicioTtoDesde))
            || ModelState.ContainsKey(nameof(model.HoraPromesaInicioTtoHasta)))
        {
            return;
        }

        if (!int.TryParse(model.HoraPromesaInicioTtoDesde, out var horaDesde)
            || !int.TryParse(model.HoraPromesaInicioTtoHasta, out var horaHasta))
        {
            ModelState.AddModelError(nameof(model.HoraPromesaInicioTto), "La promesa de inicio de TTO no tiene un rango de horas válido.");
            return;
        }

        if (horaDesde > horaHasta)
        {
            ModelState.AddModelError(nameof(model.HoraPromesaInicioTto), "La hora inicial no puede ser mayor que la hora final en la promesa de inicio de TTO.");
        }
    }

    private static string? BuildHoraPromesaInicioTto(string? desde, string? hasta)
    {
        if (string.IsNullOrWhiteSpace(desde) && string.IsNullOrWhiteSpace(hasta))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(desde) || string.IsNullOrWhiteSpace(hasta))
        {
            return null;
        }

        return $"Entre {desde} y {hasta}";
    }

    private static string GetAdditionalMedicationValueForStorage(bool enabled, string? value)
    {
        if (!enabled)
        {
            return ValorNoAplicaMedicamentoAdicional;
        }

        return string.IsNullOrWhiteSpace(value) ? ValorNoAplicaMedicamentoAdicional : value;
    }

    private static string CalculateNumeroDosisDiaMedicamentoPrincipal(string? frecuencia)
    {
        if (string.IsNullOrWhiteSpace(frecuencia))
        {
            return string.Empty;
        }

        var normalized = frecuencia.Trim().ToUpperInvariant();
        if (normalized == "NO APLICA")
        {
            return "NO APLICA";
        }

        if (normalized == "INFUSION CONTINUA")
        {
            return "1";
        }

        const string prefix = "CADA ";
        const string suffix = " HORAS";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal)
            || !normalized.EndsWith(suffix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var hoursText = normalized[prefix.Length..^suffix.Length].Trim();
        if (!int.TryParse(hoursText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
            || hours <= 0)
        {
            return string.Empty;
        }

        return Math.Max(1, 24 / hours).ToString(CultureInfo.InvariantCulture);
    }

    private static int CalculateAplicacionesTotales(CensoReceptionViewModel model)
    {
        var total = CalculateMedicationApplicationsByFrequency(
            GetFrequencyForApplications(model, "1", model.FrecuenciaAdministracionMxPrincipal),
            model.DiasMedicamentoPrincipal);

        if (model.TieneSegundoMedicamento)
        {
            total += CalculateMedicationApplicationsByFrequency(
                GetFrequencyForApplications(model, "2", model.FrecuenciaAdministracionMedicamento2),
                model.DiasMedicamento2);
        }

        if (model.TieneTercerMedicamento)
        {
            total += CalculateMedicationApplicationsByFrequency(
                GetFrequencyForApplications(model, "3", model.FrecuenciaAdministracionMedicamento3),
                model.DiasMedicamento3);
        }

        if (model.TieneCuartoMedicamento)
        {
            total += CalculateMedicationApplicationsByFrequency(
                GetFrequencyForApplications(model, "4", model.FrecuenciaAdministracionMedicamento4),
                model.DiasMedicamento4);
        }

        if (model.TieneQuintoMedicamento)
        {
            total += CalculateMedicationApplicationsByFrequency(
                GetFrequencyForApplications(model, "5", model.FrecuenciaAdministracionMedicamento5),
                model.DiasMedicamento5);
        }

        if (model.TieneSextoMedicamento)
        {
            total += CalculateMedicationApplicationsByFrequency(
                GetFrequencyForApplications(model, "6", model.FrecuenciaAdministracionMedicamento6),
                model.DiasMedicamento6);
        }

        return total;
    }

    private static string? GetFrequencyForApplications(
        CensoReceptionViewModel model,
        string medicationNumber,
        string? originalFrequency)
    {
        if (string.Equals(model.CambioFrecuenciaAdministracionTto, "Si", StringComparison.OrdinalIgnoreCase)
            && string.Equals(model.MedicamentoFrecuenciaAjustada, medicationNumber, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(model.FrecuenciaAjustada) ? originalFrequency : model.FrecuenciaAjustada;
        }

        return originalFrequency;
    }

    private static string? CalculateDiasTratamientoIv(CensoReceptionViewModel model)
    {
        var maxDays = Math.Max(0, model.DiasMedicamentoPrincipal ?? 0);

        if (model.TieneSegundoMedicamento)
        {
            maxDays = Math.Max(maxDays, model.DiasMedicamento2 ?? 0);
        }

        if (model.TieneTercerMedicamento)
        {
            maxDays = Math.Max(maxDays, model.DiasMedicamento3 ?? 0);
        }

        if (model.TieneCuartoMedicamento)
        {
            maxDays = Math.Max(maxDays, model.DiasMedicamento4 ?? 0);
        }

        if (model.TieneQuintoMedicamento)
        {
            maxDays = Math.Max(maxDays, model.DiasMedicamento5 ?? 0);
        }

        if (model.TieneSextoMedicamento)
        {
            maxDays = Math.Max(maxDays, model.DiasMedicamento6 ?? 0);
        }

        return maxDays > 0 ? maxDays.ToString(CultureInfo.InvariantCulture) : null;
    }

    private static DateTime? CalculateFechaFinTratamiento(DateTime? fechaInicioTratamiento, string? diasTratamientoIv)
    {
        if (!fechaInicioTratamiento.HasValue)
        {
            return null;
        }

        if (!int.TryParse(diasTratamientoIv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var diasTratamiento)
            || diasTratamiento < 1)
        {
            return null;
        }

        return fechaInicioTratamiento.Value.Date.AddDays(diasTratamiento);
    }

    private static int CalculateMedicationApplicationsByFrequency(string? frequency, int? days)
    {
        if (!days.HasValue || days.Value < 1 || days.Value > 999 || string.IsNullOrWhiteSpace(frequency))
        {
            return 0;
        }

        var normalized = frequency.Trim().ToUpperInvariant();
        if (normalized == "NO APLICA")
        {
            return 0;
        }

        if (normalized == "INFUSION CONTINUA")
        {
            return days.Value;
        }

        const string prefix = "CADA ";
        const string suffix = " HORAS";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal)
            || !normalized.EndsWith(suffix, StringComparison.Ordinal))
        {
            return 0;
        }

        var hoursText = normalized[prefix.Length..^suffix.Length].Trim();
        if (!decimal.TryParse(hoursText, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours) || hours <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(days.Value * 24m / hours);
    }

    private void ValidatePhoneFields(CensoReceptionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Telefono1))
        {
            ModelState.AddModelError(nameof(model.Telefono1), "Ingresa al menos un teléfono.");
            return;
        }

        if (string.IsNullOrWhiteSpace(model.Telefono2))
        {
            ModelState.AddModelError(nameof(model.Telefono2), "Ingresa el teléfono adicional 1.");
        }

        ValidatePhoneValue(model.Telefono1, nameof(model.Telefono1), "teléfono principal");
        ValidatePhoneValue(model.Telefono2, nameof(model.Telefono2), "teléfono adicional 1");
        ValidatePhoneValue(model.Telefono3, nameof(model.Telefono3), "teléfono adicional 2");

        if (string.IsNullOrWhiteSpace(model.Telefono2) && !string.IsNullOrWhiteSpace(model.Telefono3))
        {
            ModelState.AddModelError(nameof(model.Telefono2), "Para ingresar teléfono adicional 2 primero debes completar teléfono adicional 1.");
        }
    }

    private void ValidateCie10Fields(CensoReceptionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.CodigoCie10))
        {
            model.DiagnosticoDescriptivo = string.Empty;
            return;
        }

        if (!Cie10Pattern.IsMatch(model.CodigoCie10))
        {
            model.DiagnosticoDescriptivo = string.Empty;
            ModelState.AddModelError(nameof(model.CodigoCie10), "El código CIE10 debe iniciar con una letra y tener 3 dígitos (ejemplo: A000).");
            return;
        }

        if (!_cie10Catalog.TryGetValue(model.CodigoCie10, out var diagnostico))
        {
            model.DiagnosticoDescriptivo = string.Empty;
            ModelState.AddModelError(nameof(model.CodigoCie10), "El código CIE10 ingresado no existe en el catálogo parametrizado.");
            return;
        }

        model.DiagnosticoDescriptivo = diagnostico;
    }

    private void ValidatePhoneValue(string? value, string fieldName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Length > 10)
        {
            ModelState.AddModelError(fieldName, $"El {displayName} no puede superar 10 dígitos.");
        }

        if (value.Any(ch => !char.IsDigit(ch)))
        {
            ModelState.AddModelError(fieldName, $"El {displayName} solo permite dígitos.");
        }
    }

    private void ApplyAddressValidationResult(
        CensoReceptionViewModel model,
        AddressValidationResult direccionValidation,
        ref string direccionParaGuardar)
    {
        if (direccionValidation.Outcome == AddressValidationOutcome.Valid)
        {
            model.DireccionEsValida = true;
            model.DireccionSugerida = direccionValidation.FormattedAddress;
            model.DireccionMensajeValidacion = direccionValidation.Message;

            if (!string.IsNullOrWhiteSpace(direccionValidation.FormattedAddress))
            {
                direccionParaGuardar = direccionValidation.FormattedAddress;
                model.Direccion = direccionParaGuardar;
            }

            ApplyAddressLocationDefaults(model, direccionValidation);
            return;
        }

        model.DireccionEsValida = false;
        model.DireccionSugerida = direccionValidation.SuggestedAddress;
        model.DireccionMensajeValidacion = direccionValidation.Message;
        ApplyAddressLocationDefaults(model, direccionValidation);

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

    private void ValidateDateTimes(CensoReceptionViewModel model)
    {
        if (model.FechaNacimiento.Date > DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.FechaNacimiento), "La fecha de nacimiento no puede ser mayor a la fecha actual.");
        }

        var fechaHoraIngreso = model.FechaIngreso.Date + model.HoraIngreso;
        var fechaHoraRespuesta = model.FechaRespuesta.Date + model.HoraRespuesta;

        if (fechaHoraRespuesta < fechaHoraIngreso)
        {
            ModelState.AddModelError(nameof(model.HoraRespuesta), "La fecha/hora de respuesta no puede ser menor a la de ingreso.");
        }
    }

    private static IReadOnlyList<string> GetMissingMandatorySectionNames(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        var missingSectionNumbers = modelState
            .Where(entry => entry.Value is not null && entry.Value.Errors.Count > 0)
            .Select(entry => NormalizeModelStateKey(entry.Key))
            .Where(fieldName => MandatorySectionFieldMap.ContainsKey(fieldName))
            .Select(fieldName => MandatorySectionFieldMap[fieldName])
            .Distinct()
            .OrderBy(section => section)
            .ToList();

        return missingSectionNumbers
            .Where(section => MandatorySectionNames.ContainsKey(section))
            .Select(section => MandatorySectionNames[section])
            .ToList();
    }

    private static string NormalizeModelStateKey(string key)
    {
        const string modelPrefix = "model.";
        if (key.StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return key[modelPrefix.Length..];
        }

        var lastDotIndex = key.LastIndexOf('.');
        return lastDotIndex >= 0 && lastDotIndex < key.Length - 1
            ? key[(lastDotIndex + 1)..]
            : key;
    }

    private static string BuildMissingMandatorySectionsMessage(IReadOnlyList<string> missingSections)
    {
        if (missingSections.Count == 1)
        {
            return $"Para guardar el censo debes completar la seccion obligatoria: {missingSections[0]}.";
        }

        return $"Para guardar el censo debes completar las secciones obligatorias: {string.Join(", ", missingSections)}.";
    }

    private void ApplyAddressLocationDefaults(CensoReceptionViewModel model, AddressValidationResult validation)
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

    private static string InferClasificacionZonaSura(string municipio)
    {
        var key = NormalizeKey(municipio);
        return OrienteMunicipios.Any(m => NormalizeKey(m) == key) ? "Oriente" : "Valle de aburra";
    }

    private string InferZonaDireccionSegunMunicipio(
        string municipio,
        string? barrio = null,
        string? district = null,
        string? direccion = null)
    {
        var key = NormalizeKey(municipio);
        if (key == "MEDELLIN")
        {
            return InferZonaMedellin(barrio, district, direccion);
        }

        return key switch
        {
            "LAESTRELLA" => "Sur - La estrella",
            "AMAGA" => "Sur-Amaga",
            "BARBOSA" => "Norte-Barbosa",
            "CALDAS" => "Sur-Caldas",
            "COPACABANA" => "Norte-Copacabana",
            "GIRARDOTA" => "Norte-Girardota",
            "RIONEGRO" or "ELCARMENDEVIBORAL" or "ELSANTUARIO" or "GUARNE" or "GUATAPE" or "LACEJA" or "LAUNION" or "MARINILLA" or "PEÑOL" or "PENOL" or "RETIRO" or "SANVICENTEDEFERRER" => "Oriente Antioqueño",
            "BELLO" or "DONMATIAS" or "SANPEDRODELOSMILAGROS" or "SANTAROSADEOSOS" => "Norte",
            "ENVIGADO" or "ITAGUI" or "SABANETA" => "Sur",
            "NOPARAMETRIZADO" or "SANFELIX" => "No Parametrizado",
            _ => "No Parametrizado"
        };
    }

    private string InferZonaMedellin(string? barrio, string? district, string? direccion)
    {
        var neighborhoodCandidates = new[] { barrio, district, direccion };
        foreach (var candidate in neighborhoodCandidates)
        {
            if (TryGetMedellinZoneByNeighborhood(candidate, out var zone))
            {
                return zone;
            }
        }

        var candidates = new[] { district, barrio, direccion };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = NormalizeKey(candidate);
            foreach (var hint in MedellinZoneHints)
            {
                if (normalized.Contains(hint.Alias, StringComparison.Ordinal))
                {
                    return hint.Zona;
                }
            }
        }

        return "No Parametrizado";
    }

    private bool TryGetMedellinZoneByNeighborhood(string? value, out string zone)
    {
        zone = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || _medellinNeighborhoodZoneMap.Count == 0)
        {
            return false;
        }

        var normalized = NormalizeKey(value);
        if (_medellinNeighborhoodZoneMap.TryGetValue(normalized, out var foundZone)
            && !string.IsNullOrWhiteSpace(foundZone))
        {
            zone = foundZone;
            return true;
        }

        var tokenSeparators = new[] { ',', ';', '|', '/', '-', '_' };
        foreach (var token in value.Split(tokenSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tokenKey = NormalizeKey(token);
            if (_medellinNeighborhoodZoneMap.TryGetValue(tokenKey, out var tokenZone)
                && !string.IsNullOrWhiteSpace(tokenZone))
            {
                zone = tokenZone;
                return true;
            }
        }

        foreach (var item in _medellinNeighborhoodZoneMap)
        {
            if (normalized.Contains(item.Key, StringComparison.Ordinal))
            {
                zone = item.Value;
                return true;
            }
        }

        return false;
    }

    private static string? ToCanonicalMunicipality(string? municipality)
    {
        if (string.IsNullOrWhiteSpace(municipality))
        {
            return null;
        }

        var key = NormalizeKey(municipality);
        return MunicipiosResidenciaValues.FirstOrDefault(value => NormalizeKey(value) == key);
    }

    private static string NormalizeKey(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private string GetCurrentUserProfileName()
    {
        var fullName = User.FindFirstValue("full_name");
        return !string.IsNullOrWhiteSpace(fullName) ? fullName : User.Identity?.Name ?? "Usuario sin nombre";
    }

    private static string BuildExcelXml(IReadOnlyList<CensoRecord> records, HashSet<long>? idsConAdjuntos = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(" <Worksheet ss:Name=\"Censo\">");
        sb.AppendLine("  <Table>");

        sb.AppendLine("   <Row>");
        AppendHeaderCell(sb, "Id");
        AppendHeaderCell(sb, "Asegurador");
        AppendHeaderCell(sb, "EsProrroga");
        AppendHeaderCell(sb, "FechaIngreso");
        AppendHeaderCell(sb, "HoraIngreso");
        AppendHeaderCell(sb, "FechaRespuesta");
        AppendHeaderCell(sb, "HoraRespuesta");
        AppendHeaderCell(sb, "FechaGestionFarmacia");
        AppendHeaderCell(sb, "HoraGestionFarmacia");
        AppendHeaderCell(sb, "GestionCompletaPendiente");
        AppendHeaderCell(sb, "IndicadorTiempoRespuestaMinutos");
        AppendHeaderCell(sb, "IndicadorTiempoGestionMinutos");
        AppendHeaderCell(sb, "NombrePerfilGestionaCaso");
        AppendHeaderCell(sb, "NombreRecepcionaCaso");
        AppendHeaderCell(sb, "NombreRealizaKardex");
        AppendHeaderCell(sb, "NombrePaciente");
        AppendHeaderCell(sb, "TipoIdentificacion");
        AppendHeaderCell(sb, "NumeroIdentificacion");
        AppendHeaderCell(sb, "CodigoCie10");
        AppendHeaderCell(sb, "DiagnosticoDescriptivo");
        AppendHeaderCell(sb, "FechaNacimiento");
        AppendHeaderCell(sb, "Edad");
        AppendHeaderCell(sb, "CorreoElectronico");
        AppendHeaderCell(sb, "Direccion");
        AppendHeaderCell(sb, "DetalleDireccion");
        AppendHeaderCell(sb, "ClasificacionZonaSura");
        AppendHeaderCell(sb, "MunicipioResidencia");
        AppendHeaderCell(sb, "Barrio");
        AppendHeaderCell(sb, "ZonaDireccionSegunMunicipio");
        AppendHeaderCell(sb, "Area");
        AppendHeaderCell(sb, "IpsQueRemite");
        AppendHeaderCell(sb, "VistoBuenoRangoFueraAnexo");
        AppendHeaderCell(sb, "Telefono1");
        AppendHeaderCell(sb, "Telefono2");
        AppendHeaderCell(sb, "Telefono3");
        AppendHeaderCell(sb, "ClasificacionRiesgo");
        AppendHeaderCell(sb, "AdministracionMedicamentos");
        AppendHeaderCell(sb, "NombreMedicamentoPrincipalTratante");
        AppendHeaderCell(sb, "DosisMedicamentoPrincipal");
        AppendHeaderCell(sb, "MedidaMedicamentoPrincipal");
        AppendHeaderCell(sb, "ViaAdministracionMedicamentoPrincipal");
        AppendHeaderCell(sb, "FrecuenciaAdministracionMxPrincipal");
        AppendHeaderCell(sb, "DiasMedicamentoPrincipal");
        AppendHeaderCell(sb, "NumeroDosisDiaMedicamentoPrincipal");
        AppendHeaderCell(sb, "NombreMedicamentoNumero2");
        AppendHeaderCell(sb, "DosisMedicamento2");
        AppendHeaderCell(sb, "MedidaMedicamento2");
        AppendHeaderCell(sb, "ViaAdministracionMedicamento2");
        AppendHeaderCell(sb, "FrecuenciaAdministracionMedicamento2");
        AppendHeaderCell(sb, "DiasMedicamento2");
        AppendHeaderCell(sb, "NumeroDosisMedicamento2");
        AppendHeaderCell(sb, "NombreMedicamentoNumero3");
        AppendHeaderCell(sb, "DosisMedicamento3");
        AppendHeaderCell(sb, "MedidaMedicamento3");
        AppendHeaderCell(sb, "ViaAdministracionMedicamento3");
        AppendHeaderCell(sb, "FrecuenciaAdministracionMedicamento3");
        AppendHeaderCell(sb, "DiasMedicamento3");
        AppendHeaderCell(sb, "NumeroDosisMedicamento3");
        AppendHeaderCell(sb, "NombreMedicamentoNumero4");
        AppendHeaderCell(sb, "DosisMedicamento4");
        AppendHeaderCell(sb, "MedidaMedicamento4");
        AppendHeaderCell(sb, "ViaAdministracionMedicamento4");
        AppendHeaderCell(sb, "FrecuenciaAdministracionMedicamento4");
        AppendHeaderCell(sb, "DiasMedicamento4");
        AppendHeaderCell(sb, "NumeroDosisMedicamento4");
        AppendHeaderCell(sb, "NombreMedicamentoNumero5");
        AppendHeaderCell(sb, "DosisMedicamento5");
        AppendHeaderCell(sb, "MedidaMedicamento5");
        AppendHeaderCell(sb, "ViaAdministracionMedicamento5");
        AppendHeaderCell(sb, "FrecuenciaAdministracionMedicamento5");
        AppendHeaderCell(sb, "DiasMedicamento5");
        AppendHeaderCell(sb, "NumeroDosisMedicamento5");
        AppendHeaderCell(sb, "NombreMedicamentoNumero6");
        AppendHeaderCell(sb, "DosisMedicamento6");
        AppendHeaderCell(sb, "MedidaMedicamento6");
        AppendHeaderCell(sb, "ViaAdministracionMedicamento6");
        AppendHeaderCell(sb, "FrecuenciaAdministracionMedicamento6");
        AppendHeaderCell(sb, "DiasMedicamento6");
        AppendHeaderCell(sb, "NumeroDosisMedicamento6");
        AppendHeaderCell(sb, "AplicacionesTotales");
        AppendHeaderCell(sb, "DiasTratamientoIv");
        AppendHeaderCell(sb, "CambioFrecuenciaAdministracionTto");
        AppendHeaderCell(sb, "FrecuenciaAjustada");
        AppendHeaderCell(sb, "MedicamentoFrecuenciaAjustada");
        AppendHeaderCell(sb, "FechaInicioTratamiento");
        AppendHeaderCell(sb, "FechaFinTratamiento");
        AppendHeaderCell(sb, "FechaPromesaInicioTto");
        AppendHeaderCell(sb, "HoraPromesaInicioTto");
        AppendHeaderCell(sb, "IndicadorOportunidadInicioTto");
        AppendHeaderCell(sb, "AuxiliarAsignado");
        AppendHeaderCell(sb, "Estado");
        AppendHeaderCell(sb, "AutorizacionEvento");
        AppendHeaderCell(sb, "ResponsableLlamadaBienvenida");
        AppendHeaderCell(sb, "EstadoLlamadaBienvenida");
        AppendHeaderCell(sb, "ObservacionesPlanManejo");
        AppendHeaderCell(sb, "NumeroTelefonoLlamadaBienvenida");
        AppendHeaderCell(sb, "NumeroDiasAutorizado");
        AppendHeaderCell(sb, "RequiereServiciosComplementarios");
        AppendHeaderCell(sb, "ServicioComplementario");
        AppendHeaderCell(sb, "PacienteGestante");
        AppendHeaderCell(sb, "Nebulizaciones");
        AppendHeaderCell(sb, "SistemasPresionNegativaVac");
        AppendHeaderCell(sb, "NutricionParenteral");
        AppendHeaderCell(sb, "NutricionEnteral");
        AppendHeaderCell(sb, "PacienteAnticoagulado");
        AppendHeaderCell(sb, "LaboratorioClinicoProcedimiento");
        AppendHeaderCell(sb, "ClinicaHeridas");
        AppendHeaderCell(sb, "Aislamiento");
        AppendHeaderCell(sb, "TipoAislamiento");
        AppendHeaderCell(sb, "CateterismoOSv");
        AppendHeaderCell(sb, "CateterPicc");
        AppendHeaderCell(sb, "NumeroCalibreSonda");
        AppendHeaderCell(sb, "FechaUltimoCambioSonda");
        AppendHeaderCell(sb, "AuxiliarAsignadoCateterismo");
        AppendHeaderCell(sb, "FechaProximoCambioSonda");
        AppendHeaderCell(sb, "FechaUltimaCuracionPicc");
        AppendHeaderCell(sb, "FechaAlta");
        AppendHeaderCell(sb, "NombreQuienGestionaAlta");
        AppendHeaderCell(sb, "AltaTardia");
        AppendHeaderCell(sb, "FechaPrimerSeguimiento24Horas");
        AppendHeaderCell(sb, "FechaSegundoSeguimiento48Horas");
        AppendHeaderCell(sb, "FechaTercerSeguimiento72Horas");
        AppendHeaderCell(sb, "ObservacionAltaTardia");
        AppendHeaderCell(sb, "NombreQuienRealizaSeguimientoAltaTardia");
        AppendHeaderCell(sb, "PacienteRehospitalizado");
        AppendHeaderCell(sb, "FechaRegistroReporteRehospitalizacion");
        AppendHeaderCell(sb, "FechaRehospitalizacion");
        AppendHeaderCell(sb, "MotivoRehospitalizacion");
        AppendHeaderCell(sb, "AmpliacionMotivoRehospitalizacion");
        AppendHeaderCell(sb, "RemitidoPorRehospitalizacion");
        AppendHeaderCell(sb, "IpsIntramuralRehospitalizacion");
        AppendHeaderCell(sb, "FechaPrimerSeguimientoRehospitalizacion");
        AppendHeaderCell(sb, "FechaSegundoSeguimientoRehospitalizacion");
        AppendHeaderCell(sb, "FechaTercerSeguimientoRehospitalizacion");
        AppendHeaderCell(sb, "FechaAltaHospitalizacion");
        AppendHeaderCell(sb, "ObservacionRehospitalizacion");
        AppendHeaderCell(sb, "FechaNovedadDevolucionProductos");
        AppendHeaderCell(sb, "MotivoNovedadDevolucionProductos");
        AppendHeaderCell(sb, "NotificacionAuxiliarDevolucionProductos");
        AppendHeaderCell(sb, "FechaMaximaDevolucionProductos");
        AppendHeaderCell(sb, "EstadoDevolucionServicioFarmaceutico");
        AppendHeaderCell(sb, "PresentaNovedadKardex");
        AppendHeaderCell(sb, "PresentaNovedadRequisicion");
        AppendHeaderCell(sb, "PresentaNovedadAutorizacion");
        AppendHeaderCell(sb, "DescripcionNovedadDocumentosPaciente");
        AppendHeaderCell(sb, "FechaReporteNovedadDocumentos");
        AppendHeaderCell(sb, "HoraReporteNovedadDocumentos");
        AppendHeaderCell(sb, "HoraGestionSolucionNovedadDocumentos");
        AppendHeaderCell(sb, "CreatedAtUtc");
        AppendHeaderCell(sb, "Adjuntos");
        AppendHeaderCell(sb, "Prorroga_Tipo");
        AppendHeaderCell(sb, "Prorroga_MedicamentoPrincipal");
        AppendHeaderCell(sb, "Prorroga_Dosis");
        AppendHeaderCell(sb, "Prorroga_Medida");
        AppendHeaderCell(sb, "Prorroga_Via");
        AppendHeaderCell(sb, "Prorroga_Frecuencia");
        AppendHeaderCell(sb, "Prorroga_Dias");
        AppendHeaderCell(sb, "Prorroga_SegundoMedicamento");
        AppendHeaderCell(sb, "Prorroga_Medicamento2");
        AppendHeaderCell(sb, "Prorroga_Dosis2");
        AppendHeaderCell(sb, "Prorroga_Medida2");
        AppendHeaderCell(sb, "Prorroga_Via2");
        AppendHeaderCell(sb, "Prorroga_Frecuencia2");
        AppendHeaderCell(sb, "Prorroga_Dias2");
        AppendHeaderCell(sb, "Prorroga_TercerMedicamento");
        AppendHeaderCell(sb, "Prorroga_Medicamento3");
        AppendHeaderCell(sb, "Prorroga_Dosis3");
        AppendHeaderCell(sb, "Prorroga_Medida3");
        AppendHeaderCell(sb, "Prorroga_Via3");
        AppendHeaderCell(sb, "Prorroga_Frecuencia3");
        AppendHeaderCell(sb, "Prorroga_Dias3");
        AppendHeaderCell(sb, "Prorroga_CuartoMedicamento");
        AppendHeaderCell(sb, "Prorroga_Medicamento4");
        AppendHeaderCell(sb, "Prorroga_Dosis4");
        AppendHeaderCell(sb, "Prorroga_Medida4");
        AppendHeaderCell(sb, "Prorroga_Via4");
        AppendHeaderCell(sb, "Prorroga_Frecuencia4");
        AppendHeaderCell(sb, "Prorroga_Dias4");
        AppendHeaderCell(sb, "Prorroga_QuintoMedicamento");
        AppendHeaderCell(sb, "Prorroga_Medicamento5");
        AppendHeaderCell(sb, "Prorroga_Dosis5");
        AppendHeaderCell(sb, "Prorroga_Medida5");
        AppendHeaderCell(sb, "Prorroga_Via5");
        AppendHeaderCell(sb, "Prorroga_Frecuencia5");
        AppendHeaderCell(sb, "Prorroga_Dias5");
        AppendHeaderCell(sb, "Prorroga_SextoMedicamento");
        AppendHeaderCell(sb, "Prorroga_Medicamento6");
        AppendHeaderCell(sb, "Prorroga_Dosis6");
        AppendHeaderCell(sb, "Prorroga_Medida6");
        AppendHeaderCell(sb, "Prorroga_Via6");
        AppendHeaderCell(sb, "Prorroga_Frecuencia6");
        AppendHeaderCell(sb, "Prorroga_Dias6");
        AppendHeaderCell(sb, "Prorroga_AplicacionesTotales");
        AppendHeaderCell(sb, "Prorroga_DiasTratamientoIv");
        AppendHeaderCell(sb, "Prorroga_FechaInicio");
        AppendHeaderCell(sb, "Prorroga_FechaFin");
        AppendHeaderCell(sb, "Prorroga_FechaOrdenamiento");
        AppendHeaderCell(sb, "Prorroga_HoraPromesa");
        AppendHeaderCell(sb, "Prorroga_AuxiliarAsignado");
        AppendHeaderCell(sb, "Prorroga_NumeroDiasExtension");
        AppendHeaderCell(sb, "Prorroga_NotificadoAsegurador");
        sb.AppendLine("   </Row>");

        foreach (var item in records)
        {
            sb.AppendLine("   <Row>");
            AppendDataCell(sb, item.Id.ToString());
            AppendDataCell(sb, item.Asegurador);
            AppendDataCell(sb, item.EsProrroga ? "Si" : "No");
            AppendDataCell(sb, item.FechaIngreso.ToString("yyyy-MM-dd"));
            AppendDataCell(sb, item.HoraIngreso.ToString(@"hh\:mm"));
            AppendDataCell(sb, item.FechaRespuesta.ToString("yyyy-MM-dd"));
            AppendDataCell(sb, item.HoraRespuesta.ToString(@"hh\:mm"));
            AppendDataCell(sb, item.FechaGestionFarmacia.ToString("yyyy-MM-dd"));
            AppendDataCell(sb, item.HoraGestionFarmacia.ToString(@"hh\:mm"));
            AppendDataCell(sb, item.GestionCompletaPendiente);
            AppendDataCell(sb, item.IndicadorTiempoRespuestaMinutos.ToString());
            AppendDataCell(sb, item.IndicadorTiempoGestionMinutos.ToString());
            AppendDataCell(sb, item.NombrePerfilGestionaCaso);
            AppendDataCell(sb, item.NombreRecepcionaCaso);
            AppendDataCell(sb, item.NombreRealizaKardex);
            AppendDataCell(sb, item.NombrePaciente);
            AppendDataCell(sb, item.TipoIdentificacion);
            AppendDataCell(sb, item.NumeroIdentificacion);
            AppendDataCell(sb, item.CodigoCie10);
            AppendDataCell(sb, item.DiagnosticoDescriptivo);
            AppendDataCell(sb, item.FechaNacimiento.ToString("yyyy-MM-dd"));
            AppendDataCell(sb, item.Edad.ToString());
            AppendDataCell(sb, item.CorreoElectronico);
            AppendDataCell(sb, item.Direccion);
            AppendDataCell(sb, item.DetalleDireccion ?? "");
            AppendDataCell(sb, item.ClasificacionZonaSura);
            AppendDataCell(sb, item.MunicipioResidencia);
            AppendDataCell(sb, item.Barrio);
            AppendDataCell(sb, item.ZonaDireccionSegunMunicipio);
            AppendDataCell(sb, item.Area);
            AppendDataCell(sb, item.IpsQueRemite);
            AppendDataCell(sb, item.VistoBuenoRangoFueraAnexo);
            AppendDataCell(sb, item.Telefono1);
            AppendDataCell(sb, item.Telefono2);
            AppendDataCell(sb, item.Telefono3 ?? string.Empty);
            AppendDataCell(sb, item.ClasificacionRiesgo);
            AppendDataCell(sb, item.AdministracionMedicamentos);
            AppendDataCell(sb, item.NombreMedicamentoPrincipalTratante ?? string.Empty);
            AppendDataCell(sb, FormatNullableDecimal(item.DosisMedicamentoPrincipal));
            AppendDataCell(sb, item.MedidaMedicamentoPrincipal ?? string.Empty);
            AppendDataCell(sb, item.ViaAdministracionMedicamentoPrincipal ?? string.Empty);
            AppendDataCell(sb, item.FrecuenciaAdministracionMxPrincipal);
            AppendDataCell(sb, item.DiasMedicamentoPrincipal?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendDataCell(sb, item.NumeroDosisDiaMedicamentoPrincipal);
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NombreMedicamentoNumero2));
            AppendDataCell(sb, FormatNullableDecimal(item.DosisMedicamento2));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.MedidaMedicamento2));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.ViaAdministracionMedicamento2));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.FrecuenciaAdministracionMedicamento2));
            AppendDataCell(sb, item.DiasMedicamento2?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NumeroDosisMedicamento2));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NombreMedicamentoNumero3));
            AppendDataCell(sb, FormatNullableDecimal(item.DosisMedicamento3));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.MedidaMedicamento3));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.ViaAdministracionMedicamento3));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.FrecuenciaAdministracionMedicamento3));
            AppendDataCell(sb, item.DiasMedicamento3?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NumeroDosisMedicamento3));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NombreMedicamentoNumero4));
            AppendDataCell(sb, FormatNullableDecimal(item.DosisMedicamento4));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.MedidaMedicamento4));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.ViaAdministracionMedicamento4));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.FrecuenciaAdministracionMedicamento4));
            AppendDataCell(sb, item.DiasMedicamento4?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NumeroDosisMedicamento4));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NombreMedicamentoNumero5));
            AppendDataCell(sb, FormatNullableDecimal(item.DosisMedicamento5));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.MedidaMedicamento5));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.ViaAdministracionMedicamento5));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.FrecuenciaAdministracionMedicamento5));
            AppendDataCell(sb, item.DiasMedicamento5?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NumeroDosisMedicamento5));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NombreMedicamentoNumero6));
            AppendDataCell(sb, FormatNullableDecimal(item.DosisMedicamento6));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.MedidaMedicamento6));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.ViaAdministracionMedicamento6));
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.FrecuenciaAdministracionMedicamento6));
            AppendDataCell(sb, item.DiasMedicamento6?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            AppendDataCell(sb, GetAdditionalMedicationValueForExport(item.NumeroDosisMedicamento6));
            AppendDataCell(sb, item.AplicacionesTotales ?? string.Empty);
            AppendDataCell(sb, item.DiasTratamientoIv ?? string.Empty);
            AppendDataCell(sb, item.CambioFrecuenciaAdministracionTto ?? string.Empty);
            AppendDataCell(sb, item.FrecuenciaAjustada ?? string.Empty);
            AppendDataCell(sb, item.MedicamentoFrecuenciaAjustada ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaInicioTratamiento));
            AppendDataCell(sb, FormatNullableDate(item.FechaFinTratamiento));
            AppendDataCell(sb, FormatNullableDate(item.FechaPromesaInicioTto));
            AppendDataCell(sb, item.HoraPromesaInicioTto ?? string.Empty);
            AppendDataCell(sb, CalculateIndicadorOportunidadInicioTto(item));
            AppendDataCell(sb, item.AuxiliarAsignado ?? string.Empty);
            AppendDataCell(sb, item.Estado ?? string.Empty);
            AppendDataCell(sb, item.AutorizacionEvento ?? string.Empty);
            AppendDataCell(sb, item.ResponsableLlamadaBienvenida ?? string.Empty);
            AppendDataCell(sb, item.EstadoLlamadaBienvenida ?? string.Empty);
            AppendDataCell(sb, item.ObservacionesPlanManejo ?? string.Empty);
            AppendDataCell(sb, item.NumeroTelefonoLlamadaBienvenida ?? string.Empty);
            AppendDataCell(sb, item.NumeroDiasAutorizado ?? string.Empty);
            AppendDataCell(sb, item.RequiereServiciosComplementarios ?? string.Empty);
            AppendDataCell(sb, item.ServicioComplementario ?? string.Empty);
            AppendDataCell(sb, item.PacienteGestante ?? string.Empty);
            AppendDataCell(sb, item.Nebulizaciones ?? string.Empty);
            AppendDataCell(sb, item.SistemasPresionNegativaVac ?? string.Empty);
            AppendDataCell(sb, item.NutricionParenteral ?? string.Empty);
            AppendDataCell(sb, item.NutricionEnteral ?? string.Empty);
            AppendDataCell(sb, item.PacienteAnticoagulado ?? string.Empty);
            AppendDataCell(sb, item.LaboratorioClinicoProcedimiento ?? string.Empty);
            AppendDataCell(sb, item.ClinicaHeridas ?? string.Empty);
            AppendDataCell(sb, item.Aislamiento ?? string.Empty);
            AppendDataCell(sb, item.TipoAislamiento ?? string.Empty);
            AppendDataCell(sb, item.CateterismoOSv ?? string.Empty);
            AppendDataCell(sb, item.CateterPicc ?? string.Empty);
            AppendDataCell(sb, item.NumeroCalibreSonda?.ToString() ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaUltimoCambioSonda));
            AppendDataCell(sb, item.AuxiliarAsignadoCateterismo ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaProximoCambioSonda));
            AppendDataCell(sb, FormatNullableDate(item.FechaUltimaCuracionPicc));
            AppendDataCell(sb, FormatNullableDate(item.FechaAlta));
            AppendDataCell(sb, item.NombreQuienGestionaAlta ?? string.Empty);
            AppendDataCell(sb, item.AltaTardia ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaPrimerSeguimiento24Horas));
            AppendDataCell(sb, FormatNullableDate(item.FechaSegundoSeguimiento48Horas));
            AppendDataCell(sb, FormatNullableDate(item.FechaTercerSeguimiento72Horas));
            AppendDataCell(sb, item.ObservacionAltaTardia ?? string.Empty);
            AppendDataCell(sb, item.NombreQuienRealizaSeguimientoAltaTardia ?? string.Empty);
            AppendDataCell(sb, item.PacienteRehospitalizado ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaRegistroReporteRehospitalizacion));
            AppendDataCell(sb, FormatNullableDate(item.FechaRehospitalizacion));
            AppendDataCell(sb, item.MotivoRehospitalizacion ?? string.Empty);
            AppendDataCell(sb, item.AmpliacionMotivoRehospitalizacion ?? string.Empty);
            AppendDataCell(sb, item.RemitidoPorRehospitalizacion ?? string.Empty);
            AppendDataCell(sb, item.IpsIntramuralRehospitalizacion ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaPrimerSeguimientoRehospitalizacion));
            AppendDataCell(sb, FormatNullableDate(item.FechaSegundoSeguimientoRehospitalizacion));
            AppendDataCell(sb, FormatNullableDate(item.FechaTercerSeguimientoRehospitalizacion));
            AppendDataCell(sb, FormatNullableDate(item.FechaAltaHospitalizacion));
            AppendDataCell(sb, item.ObservacionRehospitalizacion ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaNovedadDevolucionProductos));
            AppendDataCell(sb, item.MotivoNovedadDevolucionProductos ?? string.Empty);
            AppendDataCell(sb, item.NotificacionAuxiliarDevolucionProductos ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaMaximaDevolucionProductos));
            AppendDataCell(sb, item.EstadoDevolucionServicioFarmaceutico ?? string.Empty);
            AppendDataCell(sb, item.PresentaNovedadKardex ?? string.Empty);
            AppendDataCell(sb, item.PresentaNovedadRequisicion ?? string.Empty);
            AppendDataCell(sb, item.PresentaNovedadAutorizacion ?? string.Empty);
            AppendDataCell(sb, item.DescripcionNovedadDocumentosPaciente ?? string.Empty);
            AppendDataCell(sb, FormatNullableDate(item.FechaReporteNovedadDocumentos));
            AppendDataCell(sb, item.HoraReporteNovedadDocumentos?.ToString(@"hh\:mm") ?? string.Empty);
            AppendDataCell(sb, item.HoraGestionSolucionNovedadDocumentos?.ToString(@"hh\:mm") ?? string.Empty);
            AppendDataCell(sb, item.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            AppendDataCell(sb, idsConAdjuntos?.Contains(item.Id) == true ? "Sí" : "No");
            ProrrogaExportDto? prorroga = null;
            if (!string.IsNullOrWhiteSpace(item.ProrrogaJson))
            {
                try { prorroga = JsonSerializer.Deserialize<ProrrogaExportDto>(item.ProrrogaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }
            }
            AppendDataCell(sb, prorroga?.TipoProrroga ?? "");
            AppendDataCell(sb, prorroga?.NombreMedicamentoPrincipal ?? "");
            AppendDataCell(sb, prorroga?.DosisMedicamentoPrincipal ?? "");
            AppendDataCell(sb, prorroga?.MedidaMedicamentoPrincipal ?? "");
            AppendDataCell(sb, prorroga?.ViaAdministracionMedicamentoPrincipal ?? "");
            AppendDataCell(sb, prorroga?.FrecuenciaAdministracionMxPrincipal ?? "");
            AppendDataCell(sb, prorroga?.DiasMedicamentoPrincipal ?? "");
            AppendDataCell(sb, prorroga?.TieneSegundoMedicamento == true ? "Sí" : "No");
            AppendDataCell(sb, prorroga?.NombreMedicamentoNumero2 ?? "");
            AppendDataCell(sb, prorroga?.DosisMedicamento2 ?? "");
            AppendDataCell(sb, prorroga?.MedidaMedicamento2 ?? "");
            AppendDataCell(sb, prorroga?.ViaAdministracionMedicamento2 ?? "");
            AppendDataCell(sb, prorroga?.FrecuenciaAdministracionMedicamento2 ?? "");
            AppendDataCell(sb, prorroga?.DiasMedicamento2 ?? "");
            AppendDataCell(sb, prorroga?.TieneTercerMedicamento == true ? "Sí" : "No");
            AppendDataCell(sb, prorroga?.NombreMedicamentoNumero3 ?? "");
            AppendDataCell(sb, prorroga?.DosisMedicamento3 ?? "");
            AppendDataCell(sb, prorroga?.MedidaMedicamento3 ?? "");
            AppendDataCell(sb, prorroga?.ViaAdministracionMedicamento3 ?? "");
            AppendDataCell(sb, prorroga?.FrecuenciaAdministracionMedicamento3 ?? "");
            AppendDataCell(sb, prorroga?.DiasMedicamento3 ?? "");
            AppendDataCell(sb, prorroga?.TieneCuartoMedicamento == true ? "Sí" : "No");
            AppendDataCell(sb, prorroga?.NombreMedicamentoNumero4 ?? "");
            AppendDataCell(sb, prorroga?.DosisMedicamento4 ?? "");
            AppendDataCell(sb, prorroga?.MedidaMedicamento4 ?? "");
            AppendDataCell(sb, prorroga?.ViaAdministracionMedicamento4 ?? "");
            AppendDataCell(sb, prorroga?.FrecuenciaAdministracionMedicamento4 ?? "");
            AppendDataCell(sb, prorroga?.DiasMedicamento4 ?? "");
            AppendDataCell(sb, prorroga?.TieneQuintoMedicamento == true ? "Sí" : "No");
            AppendDataCell(sb, prorroga?.NombreMedicamentoNumero5 ?? "");
            AppendDataCell(sb, prorroga?.DosisMedicamento5 ?? "");
            AppendDataCell(sb, prorroga?.MedidaMedicamento5 ?? "");
            AppendDataCell(sb, prorroga?.ViaAdministracionMedicamento5 ?? "");
            AppendDataCell(sb, prorroga?.FrecuenciaAdministracionMedicamento5 ?? "");
            AppendDataCell(sb, prorroga?.DiasMedicamento5 ?? "");
            AppendDataCell(sb, prorroga?.TieneSextoMedicamento == true ? "Sí" : "No");
            AppendDataCell(sb, prorroga?.NombreMedicamentoNumero6 ?? "");
            AppendDataCell(sb, prorroga?.DosisMedicamento6 ?? "");
            AppendDataCell(sb, prorroga?.MedidaMedicamento6 ?? "");
            AppendDataCell(sb, prorroga?.ViaAdministracionMedicamento6 ?? "");
            AppendDataCell(sb, prorroga?.FrecuenciaAdministracionMedicamento6 ?? "");
            AppendDataCell(sb, prorroga?.DiasMedicamento6 ?? "");
            AppendDataCell(sb, prorroga?.AplicacionesTotales ?? "");
            AppendDataCell(sb, prorroga?.DiasTratamientoIv ?? "");
            AppendDataCell(sb, prorroga?.FechaInicioTratamiento ?? "");
            AppendDataCell(sb, prorroga?.FechaFinTratamiento ?? "");
            AppendDataCell(sb, prorroga?.FechaPromesaInicioTto ?? "");
            var prorrogaHoraDisplay = (!string.IsNullOrWhiteSpace(prorroga?.HoraPromesaDesde) && !string.IsNullOrWhiteSpace(prorroga?.HoraPromesaHasta))
                ? $"Entre {prorroga!.HoraPromesaDesde} y {prorroga.HoraPromesaHasta}"
                : string.Empty;
            AppendDataCell(sb, prorrogaHoraDisplay);
            AppendDataCell(sb, prorroga?.AuxiliarAsignado ?? "");
            AppendDataCell(sb, prorroga?.NumeroDiasExtension ?? "");
            AppendDataCell(sb, prorroga?.NotificadoAsegurador == true ? "Sí" : "No");
            sb.AppendLine("   </Row>");
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");
        return sb.ToString();
    }

    private static void AppendHeaderCell(StringBuilder sb, string value)
    {
        sb.AppendLine($"    <Cell><Data ss:Type=\"String\">{EscapeForXml(value)}</Data></Cell>");
    }

    private static string CalculateIndicadorOportunidadInicioTto(CensoRecord item)
    {
        var fechaHoraPromesa = BuildFechaHoraPromesaInicioTtoDesde(item.FechaPromesaInicioTto, item.HoraPromesaInicioTto);
        if (!fechaHoraPromesa.HasValue)
        {
            return string.Empty;
        }

        var fechaHoraRespuesta = item.FechaRespuesta.Date + item.HoraRespuesta;
        var diferencia = fechaHoraPromesa.Value - fechaHoraRespuesta;
        if (diferencia < TimeSpan.Zero)
        {
            return string.Empty;
        }

        var totalMinutos = (int)Math.Round(diferencia.TotalMinutes, MidpointRounding.AwayFromZero);
        var horas = totalMinutos / 60;
        var minutos = totalMinutos % 60;
        return $"{horas.ToString(CultureInfo.InvariantCulture)},{minutos.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static DateTime? BuildFechaHoraPromesaInicioTtoDesde(DateTime? fechaPromesaInicioTto, string? horaPromesaInicioTto)
    {
        if (!fechaPromesaInicioTto.HasValue)
        {
            return null;
        }

        var horaDesde = TryGetHoraPromesaInicioTtoDesde(horaPromesaInicioTto);
        if (!horaDesde.HasValue)
        {
            return null;
        }

        return fechaPromesaInicioTto.Value.Date + horaDesde.Value;
    }

    private static TimeSpan? TryGetHoraPromesaInicioTtoDesde(string? horaPromesaInicioTto)
    {
        if (string.IsNullOrWhiteSpace(horaPromesaInicioTto))
        {
            return null;
        }

        var trimmed = horaPromesaInicioTto.Trim();

        // New 24h format: "Entre H y H"
        var match24 = HoraPromesaPattern.Match(trimmed);
        if (match24.Success && int.TryParse(match24.Groups["desde"].Value, out var horaDesde24))
        {
            return new TimeSpan(horaDesde24, 0, 0);
        }

        // Legacy 12h format: "Entre H y H AM/PM"
        var matchLegacy = HoraPromesaLegacyPattern.Match(trimmed);
        if (!matchLegacy.Success || !int.TryParse(matchLegacy.Groups["desde"].Value, out var horaDesde12))
        {
            return null;
        }

        var meridiano = matchLegacy.Groups["meridiano"].Value;
        var horaDesde = horaDesde12 % 12;
        if (string.Equals(meridiano, "PM", StringComparison.OrdinalIgnoreCase))
        {
            horaDesde += 12;
        }

        return new TimeSpan(horaDesde, 0, 0);
    }

    private static void AppendDataCell(StringBuilder sb, string value)
    {
        sb.AppendLine($"    <Cell><Data ss:Type=\"String\">{EscapeForXml(value)}</Data></Cell>");
    }

    private static string GetAdditionalMedicationValueForExport(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? ValorNoAplicaMedicamentoAdicional : value;
    }

    private static string FormatNullableDecimal(decimal? value)
    {
        return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string FormatNullableDate(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd") ?? string.Empty;
    }

    private static string EscapeForXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static IReadOnlyList<SelectListItem> GetTipoIdentificacionOptions()
    {
        return TiposIdentificacion
            .Select(tipo => new SelectListItem { Text = tipo, Value = tipo })
            .ToList();
    }

    private static int CalculateAge(DateTime fechaNacimiento, DateTime today)
    {
        var age = today.Year - fechaNacimiento.Year;
        if (fechaNacimiento.Date > today.AddYears(-age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }

    private static string NormalizePhone(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeCie10(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        return cleaned.ToUpperInvariant();
    }

    private static IReadOnlyDictionary<string, string> LoadCie10Catalog(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "Seed", "cie10_catalog.json");
        if (!System.IO.File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = System.IO.File.ReadAllText(path, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];

            return parsed
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => new KeyValuePair<string, string>(NormalizeCie10(x.Key), x.Value.Trim()))
                .Where(x => Cie10Pattern.IsMatch(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyDictionary<string, string> LoadMedellinNeighborhoodZoneMap(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "Seed", "neighborhood_catalog.json");
        if (!System.IO.File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = System.IO.File.ReadAllText(path, Encoding.UTF8);
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json) ?? [];
            var medellinEntry = raw.FirstOrDefault(x => NormalizeKey(x.Key) == "MEDELLIN");
            var medellinNeighborhoods = medellinEntry.Value ?? [];
            if (medellinNeighborhoods.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            var normalizedNeighborhoods = medellinNeighborhoods
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .Select(NormalizeKey)
                .ToList();

            var transitionAnchors = new[] { "CASTILLA", "VILLAHERMOSA", "LAURELES", "ELPOBLADO", "GUAYABAL" };
            var lastAnchorIndex = -1;
            foreach (var anchor in transitionAnchors)
            {
                var anchorIndex = normalizedNeighborhoods.FindIndex(x => x == anchor);
                if (anchorIndex <= lastAnchorIndex)
                {
                    return new Dictionary<string, string>(StringComparer.Ordinal);
                }

                lastAnchorIndex = anchorIndex;
            }

            var transitions = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CASTILLA"] = "Nor Occidental",
                ["VILLAHERMOSA"] = "Centro Oriental",
                ["LAURELES"] = "Centro Occidental",
                ["ELPOBLADO"] = "Sur Oriental",
                ["GUAYABAL"] = "Sur Occidental"
            };

            var zoneByNeighborhood = new Dictionary<string, string>(StringComparer.Ordinal);
            var currentZone = "Nor Oriental";
            foreach (var neighborhood in normalizedNeighborhoods)
            {
                if (transitions.TryGetValue(neighborhood, out var transitionZone))
                {
                    currentZone = transitionZone;
                }

                if (!zoneByNeighborhood.ContainsKey(neighborhood))
                {
                    zoneByNeighborhood[neighborhood] = currentZone;
                }
            }

            return zoneByNeighborhood;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<string> LoadMedicamentoPrincipalValues(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "Data", "Seed", "inventario_medicamentos.txt");
        return LoadTextCatalogValues(path);
    }

    private static IReadOnlyList<string> LoadTextCatalogValues(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            return [];
        }

        var bytes = System.IO.File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            return [];
        }

        var utf8Text = Encoding.UTF8.GetString(bytes);
        var latin1Text = Encoding.Latin1.GetString(bytes);
        var content = ScorePotentialMojibake(utf8Text) <= ScorePotentialMojibake(latin1Text) ? utf8Text : latin1Text;

        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static int ScorePotentialMojibake(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return int.MaxValue;
        }

        var score = 0;
        foreach (var ch in text)
        {
            if (ch is 'Ã' or 'Â' or '�')
            {
                score++;
            }
        }

        return score;
    }

    public class ValidateAddressRequest
    {
        public string Direccion { get; set; } = string.Empty;
    }

    private sealed class ProrrogaExportDto
    {
        public string? TipoProrroga { get; set; }
        public string? NombreMedicamentoPrincipal { get; set; }
        public string? DosisMedicamentoPrincipal { get; set; }
        public string? MedidaMedicamentoPrincipal { get; set; }
        public string? ViaAdministracionMedicamentoPrincipal { get; set; }
        public string? FrecuenciaAdministracionMxPrincipal { get; set; }
        public string? DiasMedicamentoPrincipal { get; set; }
        public bool? TieneSegundoMedicamento { get; set; }
        public string? NombreMedicamentoNumero2 { get; set; }
        public string? DosisMedicamento2 { get; set; }
        public string? MedidaMedicamento2 { get; set; }
        public string? ViaAdministracionMedicamento2 { get; set; }
        public string? FrecuenciaAdministracionMedicamento2 { get; set; }
        public string? DiasMedicamento2 { get; set; }
        public bool? TieneTercerMedicamento { get; set; }
        public string? NombreMedicamentoNumero3 { get; set; }
        public string? DosisMedicamento3 { get; set; }
        public string? MedidaMedicamento3 { get; set; }
        public string? ViaAdministracionMedicamento3 { get; set; }
        public string? FrecuenciaAdministracionMedicamento3 { get; set; }
        public string? DiasMedicamento3 { get; set; }
        public bool? TieneCuartoMedicamento { get; set; }
        public string? NombreMedicamentoNumero4 { get; set; }
        public string? DosisMedicamento4 { get; set; }
        public string? MedidaMedicamento4 { get; set; }
        public string? ViaAdministracionMedicamento4 { get; set; }
        public string? FrecuenciaAdministracionMedicamento4 { get; set; }
        public string? DiasMedicamento4 { get; set; }
        public bool? TieneQuintoMedicamento { get; set; }
        public string? NombreMedicamentoNumero5 { get; set; }
        public string? DosisMedicamento5 { get; set; }
        public string? MedidaMedicamento5 { get; set; }
        public string? ViaAdministracionMedicamento5 { get; set; }
        public string? FrecuenciaAdministracionMedicamento5 { get; set; }
        public string? DiasMedicamento5 { get; set; }
        public bool? TieneSextoMedicamento { get; set; }
        public string? NombreMedicamentoNumero6 { get; set; }
        public string? DosisMedicamento6 { get; set; }
        public string? MedidaMedicamento6 { get; set; }
        public string? ViaAdministracionMedicamento6 { get; set; }
        public string? FrecuenciaAdministracionMedicamento6 { get; set; }
        public string? DiasMedicamento6 { get; set; }
        public string? AplicacionesTotales { get; set; }
        public string? DiasTratamientoIv { get; set; }
        public string? FechaInicioTratamiento { get; set; }
        public string? FechaFinTratamiento { get; set; }
        public string? FechaPromesaInicioTto { get; set; }
        public string? HoraPromesaDesde { get; set; }
        public string? HoraPromesaHasta { get; set; }
        public string? HoraPromesaMeridiano { get; set; }
        public string? AuxiliarAsignado { get; set; }
        public string? NumeroDiasExtension { get; set; }
        public bool? NotificadoAsegurador { get; set; }
    }
}
