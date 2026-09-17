using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Data.Repositories.Models;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;

namespace Nexa.Services;

/// <summary>
/// Arma la hoja de vida del paciente leyendo los cinco censos de la intranet y el Portal
/// Administrativo. Todo es de solo lectura.
///
/// Por qué no reutiliza la reconciliación de episodios: esa rutina escribe (crea episodios, cierra
/// copias, vincula filas sueltas) y la hoja de vida es una consulta; quien solo tiene permiso de
/// consultar no debe cambiar datos por abrirla. Por eso decide el estado de cada atención leyendo
/// el registro del censo —que es la verdad— con las mismas reglas públicas de
/// <see cref="CensoPacienteService"/>, en vez de confiar en la copia que guarda el episodio.
///
/// Organización: este archivo carga los datos; <c>HojaVidaPacienteService.Ingresos.cs</c> convierte
/// cada fila de censo en un ingreso; <c>HojaVidaPacienteService.Analitica.cs</c> calcula estancias,
/// alertas, cifras y la línea de vida.
/// </summary>
public partial class HojaVidaPacienteService : IHojaVidaPacienteService
{
    /// <summary>
    /// Tiempo máximo para Neon y SharePoint. Son servicios externos: si tardan, la hoja se muestra
    /// con lo del censo y un aviso, en vez de dejar a quien consulta esperando.
    /// </summary>
    private static readonly TimeSpan TiempoMaximoFuentesExternas = TimeSpan.FromSeconds(8);

    private readonly ApplicationDbContext _context;
    private readonly DbContextOptions<ApplicationDbContext> _opcionesContexto;
    private readonly IPortalPacienteRepository _portalRepository;
    private readonly INeonClinicaHeridasRepository _neonClinicaHeridasRepository;
    private readonly ISharePointDocumentService _sharePointDocumentService;
    private readonly ILogger<HojaVidaPacienteService> _logger;

    public HojaVidaPacienteService(
        ApplicationDbContext context,
        DbContextOptions<ApplicationDbContext> opcionesContexto,
        IPortalPacienteRepository portalRepository,
        INeonClinicaHeridasRepository neonClinicaHeridasRepository,
        ISharePointDocumentService sharePointDocumentService,
        ILogger<HojaVidaPacienteService> logger)
    {
        _context = context;
        _opcionesContexto = opcionesContexto;
        _portalRepository = portalRepository;
        _neonClinicaHeridasRepository = neonClinicaHeridasRepository;
        _sharePointDocumentService = sharePointDocumentService;
        _logger = logger;
    }

    /// <summary>
    /// Solo letras y dígitos, en mayúsculas. Quien consulta puede pegar "43.123.456" o
    /// "CC 43123456"; el censo guarda "43123456".
    /// </summary>
    public static string NormalizarDocumento(string? documento)
    {
        var texto = (documento ?? string.Empty).Trim().ToUpperInvariant();

        // "CC 43123456" o "CC. 43123456" → "43123456". Solo cuando el tipo viene separado del
        // número: pegado ("PA0123456") puede ser un pasaporte real que empieza por esas letras.
        var conTipo = TipoDocumentoDelante.Match(texto);
        if (conTipo.Success)
        {
            texto = conTipo.Groups["numero"].Value;
        }

        return new string(texto
            .Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            .ToArray());
    }

    private static readonly System.Text.RegularExpressions.Regex TipoDocumentoDelante = new(
        @"^(CC|TI|CE|RC|PA|PPT|NUIP|PE)[\s.:#-]+(?<numero>.+)$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<HojaVidaPacienteViewModel> ConstruirAsync(string? documento, CancellationToken cancellationToken)
    {
        var ahora = ColombiaTime.Convert(DateTime.UtcNow);
        var hoy = ahora.Date;
        var model = new HojaVidaPacienteViewModel
        {
            Consultado = true,
            ConsultadoEn = ahora
        };

        var doc = NormalizarDocumento(documento);
        if (doc.Length is < 3 or > 20)
        {
            model.ErrorBusqueda = "Escribe el número de documento del paciente: entre 3 y 20 letras o números.";
            return model;
        }

        model.Documento = doc;

        // Las fuentes externas arrancan ya, en paralelo con el censo: cada una usa su propia
        // conexión, así que no compiten con el DbContext, que solo admite una consulta a la vez.
        using var externo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        externo.CancelAfter(TiempoMaximoFuentesExternas);

        var novedadesTask = LeerExternoAsync(
            token => _portalRepository.GetNovedadesPorDocumentoAsync(doc, token),
            "novedades", externo.Token, cancellationToken);
        var rondasTask = LeerExternoAsync(
            token => _portalRepository.GetRondasPorDocumentoAsync(doc, token),
            "rondas intramurales", externo.Token, cancellationToken);

        var (datos, seguimientosTask) = await CargarCensoAsync(doc, externo.Token, cancellationToken);

        var novedades = await novedadesTask;
        var rondas = await rondasTask;
        var seguimientos = await seguimientosTask;

        if (novedades.Aviso is not null || rondas.Aviso is not null)
        {
            model.AvisoPortal = "No fue posible consultar el Portal Administrativo en este momento: las novedades y las rondas intramurales pueden estar incompletas.";
        }

        model.AvisoSeguimientosHeridas = seguimientos.Aviso;

        model.Encontrado = datos.Maestro is not null
            || datos.Agudos.Count > 0 || datos.Cronicos.Count > 0 || datos.Heridas.Count > 0
            || datos.Npt.Count > 0 || datos.Terapias.Count > 0
            || novedades.Filas.Count > 0 || rondas.Filas.Count > 0;

        if (!model.Encontrado)
        {
            return model;
        }

        var ingresos = ConstruirIngresos(datos, seguimientos.Filas, hoy);

        model.Novedades = ConstruirNovedades(novedades.Filas, ingresos, hoy);
        model.Rondas = ConstruirRondas(rondas.Filas, ingresos);
        model.Ingresos = ingresos
            .OrderByDescending(x => x.Situacion == HojaVidaSituacion.EnCurso || x.Situacion == HojaVidaSituacion.SinDiligenciar)
            .ThenByDescending(x => x.FechaIngreso ?? DateTime.MinValue)
            .ThenByDescending(x => x.RegistroId ?? 0)
            .ToList();

        model.Identidad = ConstruirIdentidad(datos, model.Ingresos, novedades.Filas, rondas.Filas, doc, hoy);
        model.Estado = ConstruirEstadoGeneral(ingresos, datos, model.Novedades, hoy);
        model.Resumen = ConstruirResumen(model, hoy);
        model.Alertas = ConstruirAlertas(model, datos, hoy);
        model.Cifras = ConstruirCifras(model, hoy);
        model.MedicamentosFrecuentes = ConstruirMedicamentosFrecuentes(ingresos);
        model.Diagnosticos = ConstruirDiagnosticos(ingresos, model.Rondas);
        model.Linea = ConstruirLineaDeVida(model, hoy);

        return model;
    }

    // ------------------------------------------------------------------------------------------
    // Carga del censo
    // ------------------------------------------------------------------------------------------

    /// <summary>Todo lo que se lee del censo para un paciente, antes de convertirlo en ingresos.</summary>
    private sealed class DatosCenso
    {
        public CensoPaciente? Maestro { get; set; }

        /// <summary>Id del maestro, o -1 si no existe (ninguna fila tiene ese vínculo).</summary>
        public long PacienteId { get; set; } = -1;

        /// <summary>El documento consultado y, si difiere en mayúsculas, el del maestro.</summary>
        public string[] Documentos { get; set; } = [];

        public List<CensoPacientePrograma> Episodios { get; set; } = [];

        public List<CensoRecord> Agudos { get; set; } = [];

        public List<ProrrogaFila> Prorrogas { get; set; } = [];

        public List<DespachoCopiaFila> CopiasDespacho { get; set; } = [];

        public List<CensoCronicoRecord> Cronicos { get; set; } = [];

        public List<AgudizacionFila> Agudizaciones { get; set; } = [];

        public List<CensoCronicoHospitalizacion> HospitalizacionesCronicos { get; set; } = [];

        /// <summary>Ordenados por fecha de ingreso al programa y luego por Id, igual que el puente.</summary>
        public List<CensoClinicaHeridasRecord> Heridas { get; set; } = [];

        public List<CensoClinicaHeridasPlan> PlanesHeridas { get; set; } = [];

        public List<KardexFila> KardexHeridas { get; set; } = [];

        public List<CensoNptRecord> Npt { get; set; } = [];

        public List<KardexFila> KardexNpt { get; set; } = [];

        public List<CensoTerapiaAmbulatoriaRecord> Terapias { get; set; } = [];

        public List<CensoTerapiaAmbulatoriaProrroga> ProrrogasTerapia { get; set; } = [];
    }

    private sealed record ProrrogaFila(long Id, long CensoRecordId, int Numero, string ProrrogaJson, DateTime CreatedAtUtc);

    private sealed record DespachoCopiaFila(long ProrrogaDeId, long? VersionId, string FarmaciaEstado, DateTime? EnviadoAtUtc);

    private sealed record AgudizacionFila(long Id, long RegistroId, int Numero, string AgudizacionJson, string FarmaciaEstado, DateTime? EnviadoAtUtc);

    private sealed record KardexFila(long Id, long RegistroId, long? PlanId, string Tipo, string FarmaciaEstado, DateTime? EnviadoAtUtc, DateTime CreatedAtUtc);

    /// <summary>
    /// Carga el maestro en el contexto de la petición y los cinco censos en paralelo, cada uno con
    /// su propio contexto. En serie eran unas doce consultas y, contra una base remota, la espera
    /// era la suma de todas; en paralelo es la del grupo más lento. Cada grupo toma una conexión del
    /// pool y la devuelve al terminar.
    /// </summary>
    private async Task<(DatosCenso Datos, Task<ResultadoExterno<ClinicaHeridasSeguimientoRow>> Seguimientos)> CargarCensoAsync(
        string doc,
        CancellationToken externo,
        CancellationToken ct)
    {
        var datos = new DatosCenso();

        // El índice único de censo_paciente es sobre el documento tal cual. Solo si no aparece se
        // prueba sin distinguir mayúsculas, que no usa el índice pero cubre pasaportes antiguos.
        datos.Maestro = await _context.CensoPacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NumeroIdentificacion == doc, ct)
            ?? await _context.CensoPacientes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NumeroIdentificacion.ToUpper() == doc, ct);

        datos.PacienteId = datos.Maestro?.Id ?? -1;
        datos.Documentos = new[] { doc, datos.Maestro?.NumeroIdentificacion }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var pid = datos.PacienteId;
        var documentos = datos.Documentos;

        var episodiosTask = datos.Maestro is null
            ? Task.FromResult(new List<CensoPacientePrograma>())
            : _context.CensoPacienteProgramas
                .AsNoTracking()
                .Where(x => x.CensoPacienteId == pid)
                .ToListAsync(ct);
        var heridasTask = ConContextoPropioAsync(contexto => CargarHeridasAsync(contexto, pid, documentos, ct));
        var agudosTask = ConContextoPropioAsync(contexto => CargarAgudosAsync(contexto, pid, documentos, ct));
        var cronicosTask = ConContextoPropioAsync(contexto => CargarCronicosAsync(contexto, pid, documentos, ct));
        var nptTask = ConContextoPropioAsync(contexto => CargarNptAsync(contexto, pid, documentos, ct));
        var terapiasTask = ConContextoPropioAsync(contexto => CargarTerapiasAsync(contexto, pid, documentos, ct));

        // Los seguimientos de heridas salen de SharePoint + Neon y solo tienen sentido si el
        // paciente pasó por clínica de heridas: arrancan en cuanto se sabe, sin esperar al resto.
        var seguimientosTask = SeguimientosSiHayHeridasAsync(heridasTask, externo, ct);

        await Task.WhenAll(episodiosTask, heridasTask, agudosTask, cronicosTask, nptTask, terapiasTask);

        datos.Episodios = episodiosTask.Result;
        (datos.Heridas, datos.PlanesHeridas, datos.KardexHeridas) = heridasTask.Result;
        (datos.Agudos, datos.Prorrogas, datos.CopiasDespacho) = agudosTask.Result;
        (datos.Cronicos, datos.Agudizaciones, datos.HospitalizacionesCronicos) = cronicosTask.Result;
        (datos.Npt, datos.KardexNpt) = nptTask.Result;
        (datos.Terapias, datos.ProrrogasTerapia) = terapiasTask.Result;

        return (datos, seguimientosTask);
    }

    private async Task<T> ConContextoPropioAsync<T>(Func<ApplicationDbContext, Task<T>> consulta)
    {
        await using var contexto = new ApplicationDbContext(_opcionesContexto);
        return await consulta(contexto);
    }

    private async Task<ResultadoExterno<ClinicaHeridasSeguimientoRow>> SeguimientosSiHayHeridasAsync(
        Task<(List<CensoClinicaHeridasRecord> Registros, List<CensoClinicaHeridasPlan> Planes, List<KardexFila> Kardex)> heridasTask,
        CancellationToken externo,
        CancellationToken ct)
    {
        var heridas = await heridasTask;
        return heridas.Registros.Count == 0
            ? new ResultadoExterno<ClinicaHeridasSeguimientoRow>([], null)
            : await LeerSeguimientosHeridasAsync(heridas.Registros[^1].NumeroIdentificacion, externo, ct);
    }

    // Cada censo se busca por vínculo con el maestro o por documento: las dos columnas tienen
    // índice. Las filas escritas en minúscula que no tengan vínculo quedan fuera, pero la
    // reconciliación del censo las vincula en cuanto alguien abre la ficha del paciente.

    private static async Task<(List<CensoClinicaHeridasRecord> Registros, List<CensoClinicaHeridasPlan> Planes, List<KardexFila> Kardex)> CargarHeridasAsync(
        ApplicationDbContext contexto, long pid, string[] documentos, CancellationToken ct)
    {
        // Ordenados por fecha de ingreso al programa y luego por Id: es la numeración de ingresos
        // del puente, con la que el portal etiqueta cada seguimiento.
        var registros = await contexto.CensoClinicaHeridas
            .AsNoTracking()
            .Where(x => x.CensoPacienteId == pid || documentos.Contains(x.NumeroIdentificacion))
            .OrderBy(x => x.FechaIngresoPrograma)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var ids = registros.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return (registros, [], []);
        }

        var planes = await contexto.CensoClinicaHeridasPlanes
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoClinicaHeridasRecordId))
            .ToListAsync(ct);

        var kardex = await contexto.CensoClinicaHeridasKardex
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoClinicaHeridasRecordId))
            .Select(x => new KardexFila(
                x.Id, x.CensoClinicaHeridasRecordId, x.CensoClinicaHeridasPlanId, x.Tipo,
                x.FarmaciaEstado, x.FarmaciaEnviadoAtUtc, x.CreatedAtUtc))
            .ToListAsync(ct);

        return (registros, planes, kardex);
    }

    private static async Task<(List<CensoRecord> Registros, List<ProrrogaFila> Prorrogas, List<DespachoCopiaFila> Copias)> CargarAgudosAsync(
        ApplicationDbContext contexto, long pid, string[] documentos, CancellationToken ct)
    {
        // Agudos: se proyecta a mano para no traer las firmas en base64 ni los JSON del kardex,
        // que pesan mucho y la hoja de vida no muestra. Las copias internas de despacho a farmacia
        // no son atenciones y se excluyen con el mismo filtro que usa el censo.
        var registros = await contexto.Censos
            .AsNoTracking()
            .Where(CensoVisibility.EditableRecord(contexto))
            .Where(x => x.CensoPacienteId == pid || documentos.Contains(x.NumeroIdentificacion))
            .Select(x => new CensoRecord
            {
                Id = x.Id,
                Estado = x.Estado,
                Asegurador = x.Asegurador,
                FechaIngreso = x.FechaIngreso,
                HoraIngreso = x.HoraIngreso,
                IndicadorTiempoRespuestaMinutos = x.IndicadorTiempoRespuestaMinutos,
                NombreRecepcionaCaso = x.NombreRecepcionaCaso,
                NombreRealizaKardex = x.NombreRealizaKardex,
                NombrePaciente = x.NombrePaciente,
                TipoIdentificacion = x.TipoIdentificacion,
                NumeroIdentificacion = x.NumeroIdentificacion,
                FechaNacimiento = x.FechaNacimiento,
                CorreoElectronico = x.CorreoElectronico,
                Direccion = x.Direccion,
                DetalleDireccion = x.DetalleDireccion,
                Barrio = x.Barrio,
                MunicipioResidencia = x.MunicipioResidencia,
                ZonaDireccionSegunMunicipio = x.ZonaDireccionSegunMunicipio,
                Telefono1 = x.Telefono1,
                Telefono2 = x.Telefono2,
                Telefono3 = x.Telefono3,
                IpsQueRemite = x.IpsQueRemite,
                CodigoCie10 = x.CodigoCie10,
                DiagnosticoDescriptivo = x.DiagnosticoDescriptivo,
                ClasificacionRiesgo = x.ClasificacionRiesgo,
                AdministracionMedicamentos = x.AdministracionMedicamentos,
                NombreMedicamentoPrincipalTratante = x.NombreMedicamentoPrincipalTratante,
                DosisMedicamentoPrincipal = x.DosisMedicamentoPrincipal,
                MedidaMedicamentoPrincipal = x.MedidaMedicamentoPrincipal,
                ViaAdministracionMedicamentoPrincipal = x.ViaAdministracionMedicamentoPrincipal,
                FrecuenciaAdministracionMxPrincipal = x.FrecuenciaAdministracionMxPrincipal,
                DiasMedicamentoPrincipal = x.DiasMedicamentoPrincipal,
                NombreMedicamentoNumero2 = x.NombreMedicamentoNumero2,
                DosisMedicamento2 = x.DosisMedicamento2,
                MedidaMedicamento2 = x.MedidaMedicamento2,
                ViaAdministracionMedicamento2 = x.ViaAdministracionMedicamento2,
                FrecuenciaAdministracionMedicamento2 = x.FrecuenciaAdministracionMedicamento2,
                DiasMedicamento2 = x.DiasMedicamento2,
                NombreMedicamentoNumero3 = x.NombreMedicamentoNumero3,
                DosisMedicamento3 = x.DosisMedicamento3,
                MedidaMedicamento3 = x.MedidaMedicamento3,
                ViaAdministracionMedicamento3 = x.ViaAdministracionMedicamento3,
                FrecuenciaAdministracionMedicamento3 = x.FrecuenciaAdministracionMedicamento3,
                DiasMedicamento3 = x.DiasMedicamento3,
                NombreMedicamentoNumero4 = x.NombreMedicamentoNumero4,
                DosisMedicamento4 = x.DosisMedicamento4,
                MedidaMedicamento4 = x.MedidaMedicamento4,
                ViaAdministracionMedicamento4 = x.ViaAdministracionMedicamento4,
                FrecuenciaAdministracionMedicamento4 = x.FrecuenciaAdministracionMedicamento4,
                DiasMedicamento4 = x.DiasMedicamento4,
                NombreMedicamentoNumero5 = x.NombreMedicamentoNumero5,
                DosisMedicamento5 = x.DosisMedicamento5,
                MedidaMedicamento5 = x.MedidaMedicamento5,
                ViaAdministracionMedicamento5 = x.ViaAdministracionMedicamento5,
                FrecuenciaAdministracionMedicamento5 = x.FrecuenciaAdministracionMedicamento5,
                DiasMedicamento5 = x.DiasMedicamento5,
                NombreMedicamentoNumero6 = x.NombreMedicamentoNumero6,
                DosisMedicamento6 = x.DosisMedicamento6,
                MedidaMedicamento6 = x.MedidaMedicamento6,
                ViaAdministracionMedicamento6 = x.ViaAdministracionMedicamento6,
                FrecuenciaAdministracionMedicamento6 = x.FrecuenciaAdministracionMedicamento6,
                DiasMedicamento6 = x.DiasMedicamento6,
                AplicacionesTotales = x.AplicacionesTotales,
                DiasTratamientoIv = x.DiasTratamientoIv,
                FechaInicioTratamiento = x.FechaInicioTratamiento,
                FechaFinTratamiento = x.FechaFinTratamiento,
                AuxiliarAsignado = x.AuxiliarAsignado,
                NumeroDiasAutorizado = x.NumeroDiasAutorizado,
                EstadoLlamadaBienvenida = x.EstadoLlamadaBienvenida,
                RequiereServiciosComplementarios = x.RequiereServiciosComplementarios,
                ServicioComplementario = x.ServicioComplementario,
                RequiereCuidador = x.RequiereCuidador,
                PacienteGestante = x.PacienteGestante,
                Nebulizaciones = x.Nebulizaciones,
                NutricionParenteral = x.NutricionParenteral,
                NutricionEnteral = x.NutricionEnteral,
                PacienteAnticoagulado = x.PacienteAnticoagulado,
                LaboratorioClinicoProcedimiento = x.LaboratorioClinicoProcedimiento,
                Aislamiento = x.Aislamiento,
                TipoAislamiento = x.TipoAislamiento,
                CateterismoOSv = x.CateterismoOSv,
                CateterPicc = x.CateterPicc,
                NumeroCalibreSonda = x.NumeroCalibreSonda,
                FechaUltimoCambioSonda = x.FechaUltimoCambioSonda,
                FechaProximoCambioSonda = x.FechaProximoCambioSonda,
                FechaUltimaCuracionPicc = x.FechaUltimaCuracionPicc,
                ObservacionesPlanManejo = x.ObservacionesPlanManejo,
                FechaAlta = x.FechaAlta,
                NombreQuienGestionaAlta = x.NombreQuienGestionaAlta,
                AltaTardia = x.AltaTardia,
                PacienteRehospitalizado = x.PacienteRehospitalizado,
                FechaRehospitalizacion = x.FechaRehospitalizacion,
                MotivoRehospitalizacion = x.MotivoRehospitalizacion,
                AmpliacionMotivoRehospitalizacion = x.AmpliacionMotivoRehospitalizacion,
                RemitidoPorRehospitalizacion = x.RemitidoPorRehospitalizacion,
                IpsIntramuralRehospitalizacion = x.IpsIntramuralRehospitalizacion,
                FechaAltaHospitalizacion = x.FechaAltaHospitalizacion,
                FechaNovedadDevolucionProductos = x.FechaNovedadDevolucionProductos,
                MotivoNovedadDevolucionProductos = x.MotivoNovedadDevolucionProductos,
                EstadoDevolucionServicioFarmaceutico = x.EstadoDevolucionServicioFarmaceutico,
                ProrrogaJson = x.ProrrogaJson,
                FarmaciaEnviadoAtUtc = x.FarmaciaEnviadoAtUtc,
                FarmaciaEstado = x.FarmaciaEstado,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(ct);

        var ids = registros.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return (registros, [], []);
        }

        var prorrogas = await contexto.CensoProrrogas
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoRecordId))
            .Select(x => new ProrrogaFila(x.Id, x.CensoRecordId, x.Numero, x.ProrrogaJson, x.CreatedAtUtc))
            .ToListAsync(ct);

        // El estado en farmacia de una prórroga vive en su copia de despacho, no en el registro.
        var copias = await contexto.Censos
            .AsNoTracking()
            .Where(x => x.FarmaciaProrrogaDeId != null && ids.Contains(x.FarmaciaProrrogaDeId.Value))
            .Select(x => new DespachoCopiaFila(
                x.FarmaciaProrrogaDeId!.Value, x.FarmaciaProrrogaVersionId, x.FarmaciaEstado, x.FarmaciaEnviadoAtUtc))
            .ToListAsync(ct);

        return (registros, prorrogas, copias);
    }

    private static async Task<(List<CensoCronicoRecord> Registros, List<AgudizacionFila> Agudizaciones, List<CensoCronicoHospitalizacion> Hospitalizaciones)> CargarCronicosAsync(
        ApplicationDbContext contexto, long pid, string[] documentos, CancellationToken ct)
    {
        var registros = await contexto.CensoCronicos
            .AsNoTracking()
            .Where(x => x.CensoPacienteId == pid || documentos.Contains(x.NumeroIdentificacion))
            .ToListAsync(ct);

        var ids = registros.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return (registros, [], []);
        }

        var agudizaciones = await contexto.CensoCronicoAgudizaciones
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoCronicoRecordId))
            .Select(x => new AgudizacionFila(
                x.Id, x.CensoCronicoRecordId, x.Numero, x.AgudizacionJson, x.FarmaciaEstado, x.FarmaciaEnviadoAtUtc))
            .ToListAsync(ct);

        var hospitalizaciones = await contexto.CensoCronicoHospitalizaciones
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoCronicoRecordId))
            .ToListAsync(ct);

        return (registros, agudizaciones, hospitalizaciones);
    }

    private static async Task<(List<CensoNptRecord> Registros, List<KardexFila> Kardex)> CargarNptAsync(
        ApplicationDbContext contexto, long pid, string[] documentos, CancellationToken ct)
    {
        var registros = await contexto.CensoNpt
            .AsNoTracking()
            .Where(x => x.CensoPacienteId == pid || documentos.Contains(x.NumeroIdentificacion))
            .ToListAsync(ct);

        var ids = registros.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return (registros, []);
        }

        var kardex = await contexto.CensoNptKardex
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoNptRecordId))
            .Select(x => new KardexFila(
                x.Id, x.CensoNptRecordId, null, "NPT", x.FarmaciaEstado, x.FarmaciaEnviadoAtUtc, x.CreatedAtUtc))
            .ToListAsync(ct);

        return (registros, kardex);
    }

    private static async Task<(List<CensoTerapiaAmbulatoriaRecord> Registros, List<CensoTerapiaAmbulatoriaProrroga> Prorrogas)> CargarTerapiasAsync(
        ApplicationDbContext contexto, long pid, string[] documentos, CancellationToken ct)
    {
        var registros = await contexto.CensoTerapiasAmbulatorias
            .AsNoTracking()
            .Where(x => x.CensoPacienteId == pid || documentos.Contains(x.NumeroIdentificacion))
            .ToListAsync(ct);

        var ids = registros.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return (registros, []);
        }

        var prorrogas = await contexto.CensoTerapiaAmbulatoriaProrrogas
            .AsNoTracking()
            .Where(x => ids.Contains(x.CensoTerapiaAmbulatoriaRecordId))
            .ToListAsync(ct);

        return (registros, prorrogas);
    }

    // ------------------------------------------------------------------------------------------
    // Fuentes externas
    // ------------------------------------------------------------------------------------------

    private sealed record ResultadoExterno<T>(IReadOnlyList<T> Filas, string? Aviso);

    private async Task<ResultadoExterno<T>> LeerExternoAsync<T>(
        Func<CancellationToken, Task<IReadOnlyList<T>>> leer,
        string fuente,
        CancellationToken externo,
        CancellationToken peticion)
    {
        try
        {
            return new ResultadoExterno<T>(await leer(externo), null);
        }
        catch (OperationCanceledException) when (peticion.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hoja de vida: no fue posible leer {Fuente} del Portal Administrativo.", fuente);
            return new ResultadoExterno<T>([], $"No fue posible consultar {fuente}.");
        }
    }

    private async Task<ResultadoExterno<ClinicaHeridasSeguimientoRow>> LeerSeguimientosHeridasAsync(
        string documento,
        CancellationToken externo,
        CancellationToken peticion)
    {
        const string aviso = "No fue posible consultar los seguimientos de la aplicación de clínica de heridas en este momento.";
        try
        {
            var carpeta = await _sharePointDocumentService.FindClinicaHeridasPatientFolderAsync(documento, externo);
            if (!carpeta.Succeeded)
            {
                return new ResultadoExterno<ClinicaHeridasSeguimientoRow>([], aviso);
            }

            if (carpeta.Value is null || string.IsNullOrWhiteSpace(carpeta.Value.Id))
            {
                return new ResultadoExterno<ClinicaHeridasSeguimientoRow>([], null);
            }

            var filas = await _neonClinicaHeridasRepository.GetSeguimientosPorCarpetaAsync(carpeta.Value.Id, externo);
            return new ResultadoExterno<ClinicaHeridasSeguimientoRow>(filas, null);
        }
        catch (OperationCanceledException) when (peticion.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hoja de vida: no fue posible leer los seguimientos de clínica de heridas.");
            return new ResultadoExterno<ClinicaHeridasSeguimientoRow>([], aviso);
        }
    }
}
