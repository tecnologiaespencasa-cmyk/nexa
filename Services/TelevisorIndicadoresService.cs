using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Interfaces;
using Nexa.Helpers;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;

namespace Nexa.Services;

/// <summary>
/// Calcula los indicadores del Televisor para un día, con el estado que tenían en un momento dado.
///
/// Reglas que no son evidentes (decididas el 2026-10-01):
/// - Un ingreso es un episodio del censo (cualquiera de los cinco programas) y pertenece al día y
///   al turno en que se REGISTRÓ en Nexa, no a la hora del correo. Así el corte de la 1:45 p. m.
///   queda congelado de verdad: lo que se registra después cuenta para la tarde, como pidió la
///   operación, aunque el correo sea de la mañana. Agudos cancelados o rechazados no cuentan, igual
///   que en Reportes.
/// - La promesa (15 minutos) se mide con la recepción que se digita: de la hora del correo a la de
///   respuesta. Los correos que llegan de noche (22:00 a 6:00) se muestran pero no suman: nadie de
///   admisiones estaba en turno. Un dato imposible (respuesta antes del correo, más de 24 h, correo
///   posterior al registro) se marca "revisar" y tampoco suma, para no torcer el balance.
/// - "Gestión completa" y "Gestión analista" solo existen en agudos. Su hora sale de las columnas
///   que se empezaron a guardar el 2026-10-01; lo anterior se ve como "sin hora".
/// - Meta de creación (2026-10-06): un paciente de agudos se crea en 10 minutos como máximo,
///   contados desde que se empieza (al subir la remisión o, si no se subió, al primer guardado)
///   hasta la gestión completa. Uno que sigue pendiente pasados los 10 minutos cuenta como fuera
///   de tiempo. Se le atribuye a quien lo registró.
/// - Lo que no recepciona Admisiones (la líder, otra área, la sincronización) va en "Otros" y no
///   cuenta para ninguna de las dos metas ni se marca como dato por corregir.
/// - El turno de cada persona se deduce de su primera actividad del día en la auditoría (inicio de
///   sesión o cualquier acción desde las 5:00 a. m.); si no es el real, se ajusta a mano.
/// - Cada ingreso se le cuenta a quien figura en "Nombre de quien recepciona el caso" (la promesa
///   es de quien respondió el correo). Aparte se cuenta quién lo REGISTRÓ en Nexa, porque cerca de
///   un tercio los registra otra persona (la tarde digita lo que la mañana respondió) y sin ese
///   segundo número parecía que el tablero no le sumaba a quien estaba ingresando pacientes
///   (reporte del 2026-10-05, caso Pilar Vera).
/// - Cada paciente se muestra con su nombre: el 2026-10-02 el usuario pidió nombres y textos que
///   entienda cualquiera, en lugar de las iniciales con los cuatro últimos dígitos del documento.
/// </summary>
public sealed class TelevisorIndicadoresService : ITelevisorIndicadoresService
{
    private const string Sincronizacion = "Sincronización";
    private const string AccionExtraccion = "CENSO_EXTRACCION_REMISION";

    /// <summary>Hasta cuánto antes del primer guardado cuenta una remisión subida al censo.</summary>
    private static readonly TimeSpan VentanaExtraccion = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Un paciente creado hasta una hora antes de agregarle agudos, por la misma persona, es un
    /// paciente nuevo: su creación empezó con ese primer guardado. Medido el 2026-10-06: entre crear
    /// el paciente y agregarle el programa pasan 0,2 min de mediana (2,2 en el percentil 90).
    /// </summary>
    private static readonly TimeSpan PacienteNuevoSi = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan InicioDeteccion = new(5, 0, 0);
    private static readonly TimeSpan ConectadaSi = TimeSpan.FromMinutes(30);
    private static readonly string[] Stopwords = ["DE", "DEL", "LA", "LAS", "LOS", "Y"];

    private readonly ApplicationDbContext _context;
    private readonly IPortalNovedadRepository _portal;
    private readonly ILogger<TelevisorIndicadoresService> _logger;

    public TelevisorIndicadoresService(
        ApplicationDbContext context,
        IPortalNovedadRepository portal,
        ILogger<TelevisorIndicadoresService> logger)
    {
        _context = context;
        _portal = portal;
        _logger = logger;
    }

    public async Task<TelevisorTablero> ConstruirAsync(DateTime dia, DateTime estadoAl, CancellationToken ct)
    {
        dia = dia.Date;
        var finDia = dia.AddDays(1);
        if (estadoAl > finDia)
        {
            estadoAl = finDia;
        }

        var habil = TelevisorHorario.EsHabil(dia);
        var desdeUtc = ColombiaTime.ConvertToUtc(dia);
        var hastaUtc = ColombiaTime.ConvertToUtc(estadoAl);

        var personal = await CargarPersonalAsync(dia, estadoAl, habil, hastaUtc, ct);
        var ingresos = await CargarIngresosAsync(dia, estadoAl, desdeUtc, hastaUtc, habil, personal, ct);
        var novedades = await CargarNovedadesAsync(dia, estadoAl, ct);

        var tablero = new TelevisorTablero
        {
            Dia = dia,
            EsHabil = habil,
            Festivo = FestivosColombia.Nombre(dia),
            EstadoAl = estadoAl,
            GeneradoAtUtc = DateTime.UtcNow,
            Personal = personal.Personas,
            Novedades = novedades,
            Cinta = ingresos.Items
                .Where(x => !x.Cancelado)
                .OrderBy(x => x.Registrado)
                .Select(x => new TelevisorMarca { Registrado = x.Registrado, Promesa = x.Promesa })
                .ToList()
        };

        if (habil)
        {
            var corte = dia + TelevisorHorario.Corte;
            tablero.Bloques.Add(ConstruirBloque(TelevisorBloques.Manana, "Turno mañana", dia, corte, ingresos, personal, novedades, habil));
            if (estadoAl > corte)
            {
                tablero.Bloques.Add(ConstruirBloque(TelevisorBloques.Tarde, "Turno tarde", corte, finDia, ingresos, personal, novedades, habil));
            }

            tablero.Bloques.Add(ConstruirBloque(TelevisorBloques.Dia, "Día completo", dia, finDia, ingresos, personal, novedades, habil));
        }
        else
        {
            tablero.Bloques.Add(ConstruirBloque(TelevisorBloques.Dia, "Turno único", dia, finDia, ingresos, personal, novedades, habil));
        }

        return tablero;
    }

    // ------------------------------------------------------------------ personal y turnos

    private sealed class Personal
    {
        public List<TelevisorPersona> Personas { get; } = [];

        /// <summary>Nombre normalizado de la tabla de personal → persona.</summary>
        public Dictionary<string, TelevisorPersona> PorNombre { get; } = new(StringComparer.Ordinal);

        /// <summary>Usuario de Nexa (en mayúsculas) → persona vinculada.</summary>
        public Dictionary<string, TelevisorPersona> PorUsuario { get; } = new(StringComparer.Ordinal);

        /// <summary>Usuario de Nexa (en mayúsculas) → nombre completo, para quien no está en la tabla.</summary>
        public Dictionary<string, string> NombresDeUsuario { get; } = new(StringComparer.Ordinal);

        /// <summary>Id del usuario de Nexa → su usuario en mayúsculas (la auditoría guarda el id).</summary>
        public Dictionary<Guid, string> UsuarioPorId { get; } = [];
    }

    private async Task<Personal> CargarPersonalAsync(
        DateTime dia,
        DateTime estadoAl,
        bool habil,
        DateTime hastaUtc,
        CancellationToken ct)
    {
        var resultado = new Personal();

        var tabla = await _context.NursingAssistants.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Role })
            .ToListAsync(ct);

        var usuarios = await _context.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.Username, u.FullName, u.FirstName, u.LastName1, u.LastName2 })
            .ToListAsync(ct);

        foreach (var u in usuarios)
        {
            resultado.NombresDeUsuario[u.Username.ToUpperInvariant()] = NombreDeUsuario(u.FirstName, u.LastName1, u.FullName);
            resultado.UsuarioPorId[u.Id] = u.Username.ToUpperInvariant();
        }

        var tokensUsuario = usuarios.ToDictionary(
            u => u.Id,
            u => Tokens($"{u.FullName} {u.FirstName} {u.LastName1} {u.LastName2}"));

        // Cada persona de la tabla se vincula con el usuario de Nexa cuyo nombre contiene todas sus
        // palabras ("PILAR VERA" ⊂ "Julieth Pilar Vera Ochoa"). Si hay dos candidatos no se elige
        // ninguno: un vínculo equivocado le atribuiría a alguien la conexión de otra persona.
        var vinculos = new Dictionary<int, (Guid Id, string Username)>();
        foreach (var persona in tabla)
        {
            var propios = Tokens(persona.Name);
            if (propios.Count < 2)
            {
                continue;
            }

            var candidatos = usuarios.Where(u => propios.IsSubsetOf(tokensUsuario[u.Id])).ToList();
            if (candidatos.Count == 1)
            {
                vinculos[persona.Id] = (candidatos[0].Id, candidatos[0].Username);
            }
        }

        var idsVinculados = vinculos.Values.Select(v => (Guid?)v.Id).ToList();
        var desdeDeteccionUtc = ColombiaTime.ConvertToUtc(dia + InicioDeteccion);
        var actividad = idsVinculados.Count == 0
            ? []
            : await _context.AuditLogs.AsNoTracking()
                .Where(a => a.PerformedByUserId != null
                    && idsVinculados.Contains(a.PerformedByUserId)
                    && a.PerformedAtUtc >= desdeDeteccionUtc
                    && a.PerformedAtUtc < hastaUtc)
                .GroupBy(a => a.PerformedByUserId)
                .Select(g => new { UserId = g.Key, Primera = g.Min(x => x.PerformedAtUtc), Ultima = g.Max(x => x.PerformedAtUtc) })
                .ToListAsync(ct);

        var ajustes = await _context.TelevisorTurnoAjustes.AsNoTracking()
            .Where(x => x.Dia == dia)
            .ToDictionaryAsync(x => x.NursingAssistantId, x => x.Turno, ct);

        foreach (var persona in tabla)
        {
            var rol = NursingAssistantRoles.Normalize(persona.Role) ?? string.Empty;
            DateTime? primera = null;
            DateTime? ultima = null;
            string? usuario = null;
            if (vinculos.TryGetValue(persona.Id, out var vinculo))
            {
                usuario = vinculo.Username;
                var act = actividad.FirstOrDefault(a => a.UserId == vinculo.Id);
                if (act is not null)
                {
                    primera = ColombiaTime.Convert(act.Primera);
                    ultima = ColombiaTime.Convert(act.Ultima);
                }
            }

            var ajustado = ajustes.TryGetValue(persona.Id, out var turnoAjustado);
            var item = new TelevisorPersona
            {
                Id = persona.Id,
                Nombre = NombreCorto(persona.Name),
                Rol = rol,
                Usuario = usuario,
                PrimeraConexion = primera,
                UltimaActividad = ultima,
                Conectada = ultima is not null && estadoAl - ultima.Value <= ConectadaSi,
                TurnoAjustado = ajustado,
                Turno = ajustado ? turnoAjustado! : DetectarTurno(primera, habil, rol)
            };

            resultado.Personas.Add(item);
            resultado.PorNombre[Normalizar(persona.Name)] = item;
            if (usuario is not null)
            {
                resultado.PorUsuario[usuario.ToUpperInvariant()] = item;
            }
        }

        return resultado;
    }

    /// <summary>
    /// Turno según la primera actividad del día (desde las 5:00 a. m., para que una sesión que quedó
    /// abierta de la víspera no se lea como turno de la mañana). Medido contra tres semanas de
    /// conexiones reales: la mañana entra antes de las 7:00, el turno único hacia las 8:00 y la
    /// tarde entre la 1:30 y las 3:00 p. m. Las analistas no tienen turno único.
    /// </summary>
    private static string DetectarTurno(DateTime? primera, bool habil, string rol)
    {
        if (primera is null)
        {
            return TelevisorTurnos.SinConexion;
        }

        if (!habil)
        {
            return TelevisorTurnos.FinDeSemana;
        }

        var hora = primera.Value.TimeOfDay;
        if (hora < new TimeSpan(7, 30, 0))
        {
            return TelevisorTurnos.Manana;
        }

        if (hora < new TimeSpan(11, 30, 0))
        {
            return rol == NursingAssistantRoles.Admisiones ? TelevisorTurnos.Unico : TelevisorTurnos.Manana;
        }

        return TelevisorTurnos.Tarde;
    }

    // ------------------------------------------------------------------ ingresos

    private sealed class Ingresos
    {
        public List<TelevisorIngreso> Items { get; } = [];

        /// <summary>Datos que no se pintan pero sirven para las alertas de calidad.</summary>
        public Dictionary<long, (long PacienteId, bool SinFormulario)> Extra { get; } = [];
    }

    private async Task<Ingresos> CargarIngresosAsync(
        DateTime dia,
        DateTime estadoAl,
        DateTime desdeUtc,
        DateTime hastaUtc,
        bool habil,
        Personal personal,
        CancellationToken ct)
    {
        var resultado = new Ingresos();

        // Un episodio que creó la sincronización nace cuando alguien abre la ficha, no cuando se
        // registró el ingreso: para esos manda la creación del registro del programa. Por eso se
        // traen también los creados hasta tres días después y se filtra por la hora corregida.
        var episodios = await _context.CensoPacienteProgramas.AsNoTracking()
            .Where(e => e.AgregadoAtUtc >= desdeUtc && e.AgregadoAtUtc < hastaUtc.AddDays(3))
            .Select(e => new
            {
                e.Id,
                e.Programa,
                e.RegistroId,
                e.CensoPacienteId,
                e.AgregadoAtUtc,
                e.AgregadoPor,
                e.FechaIngreso,
                e.HoraIngreso,
                e.FechaRespuesta,
                e.HoraRespuesta,
                e.NombreRecepcionaCaso,
                Nombre = e.CensoPaciente.NombrePaciente,
                Documento = e.CensoPaciente.NumeroIdentificacion,
                PacienteCreadoAtUtc = e.CensoPaciente.CreatedAtUtc,
                PacienteCreadoPor = e.CensoPaciente.CreadoPor
            })
            .ToListAsync(ct);

        if (episodios.Count == 0)
        {
            return resultado;
        }

        var idsAgudos = episodios
            .Where(e => e.Programa == CensoProgramas.Agudos && e.RegistroId != null)
            .Select(e => e.RegistroId!.Value)
            .ToList();
        var agudos = await _context.Censos.AsNoTracking()
            .Where(c => idsAgudos.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.CreatedAtUtc,
                c.Estado,
                c.FechaIngreso,
                c.HoraIngreso,
                c.FechaRespuesta,
                c.HoraRespuesta,
                c.NombreRecepcionaCaso,
                c.GestionCompletaPendiente,
                c.GestionCompletaAtUtc,
                c.GestionAnalistaAsistencial,
                c.GestionAnalistaAtUtc,
                c.GestionAnalistaPor
            })
            .ToDictionaryAsync(c => c.Id, ct);

        var creacionOtros = await CreacionDeRegistrosSincronizadosAsync(
            episodios
                .Where(e => e.AgregadoPor == Sincronizacion && e.Programa != CensoProgramas.Agudos && e.RegistroId != null)
                .Select(e => (e.Programa, e.RegistroId!.Value))
                .ToList(),
            ct);

        // Para saber cuándo se empezó a crear cada paciente (ver InicioDeCreacion): las remisiones
        // subidas al censo y, por persona, cada paso de creación del día (paciente creado, programa
        // agregado).
        var extracciones = await CargarExtraccionesAsync(desdeUtc, hastaUtc, personal, ct);
        var pasos = episodios
            .Where(e => !string.IsNullOrWhiteSpace(e.AgregadoPor) && e.AgregadoPor != Sincronizacion)
            .Select(e => (Usuario: e.AgregadoPor!.Trim().ToUpperInvariant(), AtUtc: e.AgregadoAtUtc, e.CensoPacienteId))
            .Concat(episodios
                .Where(e => !string.IsNullOrWhiteSpace(e.PacienteCreadoPor))
                .Select(e => (Usuario: e.PacienteCreadoPor!.Trim().ToUpperInvariant(), AtUtc: e.PacienteCreadoAtUtc, e.CensoPacienteId)))
            .Distinct()
            .ToLookup(x => x.Usuario, x => (x.AtUtc, x.CensoPacienteId), StringComparer.Ordinal);

        foreach (var e in episodios)
        {
            var registro = e.Programa == CensoProgramas.Agudos && e.RegistroId is long idAgudos
                && agudos.TryGetValue(idAgudos, out var fila)
                ? fila
                : null;

            var registradoUtc = e.AgregadoAtUtc;
            if (e.AgregadoPor == Sincronizacion && e.RegistroId is long idRegistro)
            {
                DateTime? creado = registro?.CreatedAtUtc
                    ?? (creacionOtros.TryGetValue((e.Programa, idRegistro), out var c) ? c : null);
                if (creado is DateTime cu && cu < registradoUtc)
                {
                    registradoUtc = cu;
                }
            }

            if (registradoUtc < desdeUtc || registradoUtc >= hastaUtc)
            {
                continue;
            }

            var registrado = ColombiaTime.Convert(registradoUtc);

            // La recepción vive en el episodio; agudos conserva su copia en censo y la
            // conciliación la sube al episodio cuando falta. Se usa la del episodio y, si no hay,
            // la de agudos.
            var correo = Combinar(e.FechaIngreso, e.HoraIngreso)
                ?? (registro is null ? null : Combinar(registro.FechaIngreso, registro.HoraIngreso));
            var respuesta = Combinar(e.FechaRespuesta, e.HoraRespuesta)
                ?? (registro is null ? null : Combinar(registro.FechaRespuesta, registro.HoraRespuesta));
            var recepciona = Limpio(e.NombreRecepcionaCaso) ?? Limpio(registro?.NombreRecepcionaCaso) ?? string.Empty;

            var item = new TelevisorIngreso
            {
                EpisodioId = e.Id,
                Programa = e.Programa,
                ProgramaCorto = CensoProgramas.NombreCorto(e.Programa),
                Paciente = NombrePaciente(e.Nombre, e.Documento),
                Registrado = registrado,
                Bloque = habil
                    ? (registrado.TimeOfDay < TelevisorHorario.Corte ? TelevisorBloques.Manana : TelevisorBloques.Tarde)
                    : TelevisorBloques.Dia,
                Correo = correo,
                Respuesta = respuesta,
                Recepciona = recepciona,
                Cancelado = registro is not null && EsCanceladoORechazado(registro.Estado),
                EnFranjaAnalistas = registrado.TimeOfDay >= TelevisorHorario.InicioFranja
                    && registrado.TimeOfDay < TelevisorHorario.FinFranja
            };

            // Quién lo registró: el usuario que agregó el programa. Es otra persona que la que lo
            // recepcionó cuando no son la misma fila de la tabla de personal.
            var usuarioRegistra = e.AgregadoPor?.Trim().ToUpperInvariant();
            var conRegistrador = !string.IsNullOrEmpty(usuarioRegistra) && e.AgregadoPor != Sincronizacion;
            TelevisorPersona? quienRegistra = null;
            if (conRegistrador)
            {
                personal.PorUsuario.TryGetValue(usuarioRegistra!, out quienRegistra);
            }

            personal.PorNombre.TryGetValue(Normalizar(recepciona), out var quienRecepciona);
            if (conRegistrador)
            {
                item.RegistradoPor = quienRegistra?.Nombre
                    ?? (personal.NombresDeUsuario.TryGetValue(usuarioRegistra!, out var nombreUsuario) ? nombreUsuario : e.AgregadoPor!.Trim());
                item.RegistradoPorOtro = quienRegistra is null || quienRecepciona is null || quienRegistra.Id != quienRecepciona.Id;
            }

            ClasificarPromesa(item);

            var respuestaValida = item.Promesa is TelevisorPromesa.Cumple or TelevisorPromesa.NoCumple or TelevisorPromesa.Nocturno;
            item.EsperaDesde = respuestaValida && respuesta is DateTime r && r <= registrado ? r : registrado;

            // La promesa de 15 minutos es de Admisiones. Lo que recepciona la líder u otra área va
            // en "Otros" y ni se mide ni se marca por corregir: ellas no llenan la hora del correo ni
            // la de la respuesta (pedido del 2026-10-06). Se sigue marcando cuando el campo quedó
            // vacío y quien lo registró sí es de Admisiones: ese dato es suyo, y sin esta salvedad
            // bastaría dejarlo en blanco para que un ingreso no contara.
            var recepcionaAdmisiones = quienRecepciona?.Rol == NursingAssistantRoles.Admisiones;
            var registraAdmisiones = quienRegistra?.Rol == NursingAssistantRoles.Admisiones;
            if (!recepcionaAdmisiones)
            {
                item.MinutosRespuesta = null;
                if (recepciona.Length == 0 && registraAdmisiones)
                {
                    item.Promesa = TelevisorPromesa.SinDato;
                    item.Revision = "Falta el nombre de quien recepcionó el caso";
                }
                else
                {
                    item.Promesa = TelevisorPromesa.NoAplica;
                    item.Revision = null;
                }
            }

            if (e.Programa == CensoProgramas.Agudos)
            {
                // La meta de creación también es de Admisiones, y de quien digita: aplica si el
                // ingreso lo recepcionó o lo registró alguien del área. Lo que creó la sincronización
                // no tiene a nadie creándolo.
                DateTime? inicioCreacionUtc = null;
                if (conRegistrador && !item.Cancelado && (recepcionaAdmisiones || registraAdmisiones))
                {
                    var creador = e.PacienteCreadoPor?.Trim().ToUpperInvariant();
                    var esNuevo = creador == usuarioRegistra
                        && e.PacienteCreadoAtUtc <= e.AgregadoAtUtc
                        && e.AgregadoAtUtc - e.PacienteCreadoAtUtc <= PacienteNuevoSi;
                    inicioCreacionUtc = InicioDeCreacion(
                        esNuevo ? e.PacienteCreadoAtUtc : e.AgregadoAtUtc,
                        usuarioRegistra!,
                        SoloLetrasYDigitos(e.Documento),
                        e.CensoPacienteId,
                        extracciones,
                        pasos[usuarioRegistra!]);
                }

                AplicarGestionDeAgudos(item, registro is null
                    ? null
                    : (registro.GestionCompletaPendiente, registro.GestionCompletaAtUtc,
                        registro.GestionAnalistaAsistencial, registro.GestionAnalistaAtUtc, registro.GestionAnalistaPor),
                    inicioCreacionUtc, hastaUtc, personal);
            }

            resultado.Items.Add(item);
            resultado.Extra[e.Id] = (e.CensoPacienteId, e.RegistroId is null && estadoAl - registrado > TimeSpan.FromHours(1));
        }

        return resultado;
    }

    private async Task<Dictionary<(string, long), DateTime>> CreacionDeRegistrosSincronizadosAsync(
        List<(string Programa, long Id)> registros,
        CancellationToken ct)
    {
        var resultado = new Dictionary<(string, long), DateTime>();
        foreach (var grupo in registros.GroupBy(x => x.Programa))
        {
            var ids = grupo.Select(x => x.Id).ToList();
            var filas = grupo.Key switch
            {
                CensoProgramas.Cronicos => await _context.CensoCronicos.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CreatedAtUtc }).ToListAsync(ct),
                CensoProgramas.ClinicaHeridas => await _context.CensoClinicaHeridas.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CreatedAtUtc }).ToListAsync(ct),
                CensoProgramas.Npt => await _context.CensoNpt.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CreatedAtUtc }).ToListAsync(ct),
                CensoProgramas.TerapiaAmbulatoria => await _context.CensoTerapiasAmbulatorias.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CreatedAtUtc }).ToListAsync(ct),
                _ => []
            };

            foreach (var f in filas)
            {
                resultado[(grupo.Key, f.Id)] = f.CreatedAtUtc;
            }
        }

        return resultado;
    }

    private sealed record Extraccion(string Usuario, DateTime AtUtc, string? Documento);

    private async Task<List<Extraccion>> CargarExtraccionesAsync(
        DateTime desdeUtc,
        DateTime hastaUtc,
        Personal personal,
        CancellationToken ct)
    {
        var filas = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.Action == AccionExtraccion
                && a.PerformedByUserId != null
                && a.PerformedAtUtc >= desdeUtc - PacienteNuevoSi - VentanaExtraccion
                && a.PerformedAtUtc < hastaUtc)
            .Select(a => new { a.PerformedByUserId, a.PerformedAtUtc, a.Details })
            .ToListAsync(ct);

        var resultado = new List<Extraccion>(filas.Count);
        foreach (var f in filas)
        {
            if (!personal.UsuarioPorId.TryGetValue(f.PerformedByUserId!.Value, out var usuario))
            {
                continue;
            }

            // "Archivo PDF: remision.pdf, Doc: 123": el documento va al final desde el 2026-10-06.
            const string marca = ", Doc: ";
            var corte = f.Details?.LastIndexOf(marca, StringComparison.Ordinal) ?? -1;
            var documento = corte < 0 ? null : SoloLetrasYDigitos(f.Details![(corte + marca.Length)..]);
            resultado.Add(new Extraccion(usuario, f.PerformedAtUtc, string.IsNullOrEmpty(documento) ? null : documento));
        }

        return resultado;
    }

    /// <summary>
    /// Cuándo se empezó a crear el paciente en el censo. El primer rastro seguro es el primer
    /// guardado (el del paciente si es nuevo; si ya existía, cuando se le agregó agudos). Pero casi
    /// siempre, antes de guardar, se sube la remisión para que el censo se llene solo: en la semana
    /// del 2026-09-29 pasó en el 94 % de los pacientes nuevos, y entre subirla y guardar hay 5
    /// minutos de mediana, más de lo que toma todo lo demás. Si la misma persona subió la remisión
    /// en la media hora anterior, el reloj arranca ahí.
    /// La auditoría de la extracción trae el documento desde el 2026-10-06: con documento se toma
    /// la primera de ESE paciente. Sin documento (lo anterior, o una remisión que no se dejó leer)
    /// se toma la última antes de guardar y posterior al paso de creación anterior de esa persona:
    /// la más cercana, para no cargarle a este paciente la remisión de otro.
    /// </summary>
    private static DateTime InicioDeCreacion(
        DateTime primerGuardadoUtc,
        string usuario,
        string documento,
        long pacienteId,
        List<Extraccion> extracciones,
        IEnumerable<(DateTime AtUtc, long CensoPacienteId)> pasosDelUsuario)
    {
        var desde = primerGuardadoUtc - VentanaExtraccion;
        var propias = extracciones
            .Where(x => x.Usuario == usuario && x.AtUtc >= desde && x.AtUtc < primerGuardadoUtc)
            .ToList();
        if (propias.Count == 0)
        {
            return primerGuardadoUtc;
        }

        var delPaciente = propias.Where(x => x.Documento is not null && x.Documento == documento).ToList();
        if (delPaciente.Count > 0)
        {
            return delPaciente.Min(x => x.AtUtc);
        }

        var pasoAnterior = pasosDelUsuario
            .Where(p => p.CensoPacienteId != pacienteId && p.AtUtc < primerGuardadoUtc)
            .Select(p => (DateTime?)p.AtUtc)
            .Max();
        var sinDocumento = propias
            .Where(x => x.Documento is null && (pasoAnterior is null || x.AtUtc > pasoAnterior))
            .ToList();
        return sinDocumento.Count > 0 ? sinDocumento.Max(x => x.AtUtc) : primerGuardadoUtc;
    }

    private static string SoloLetrasYDigitos(string? texto) =>
        new((texto ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static void ClasificarPromesa(TelevisorIngreso item)
    {
        if (item.Correo is not DateTime correo || item.Respuesta is not DateTime respuesta)
        {
            item.Promesa = TelevisorPromesa.SinDato;
            item.Revision = "Falta la hora del correo o de la respuesta";
            return;
        }

        var minutos = (int)Math.Round((respuesta - correo).TotalMinutes, MidpointRounding.AwayFromZero);
        item.MinutosRespuesta = minutos;

        if (minutos < 0)
        {
            item.Promesa = TelevisorPromesa.Revisar;
            item.Revision = "La respuesta quedó antes que el correo";
        }
        else if (minutos >= 24 * 60)
        {
            item.Promesa = TelevisorPromesa.Revisar;
            item.Revision = "Hay más de un día entre el correo y la respuesta: revise la fecha";
        }
        else if (correo > item.Registrado.AddHours(2))
        {
            // Nadie registra un ingreso dos horas antes de que llegue su correo: es la fecha o la
            // hora mal digitada (p. ej. 22:36 cuando el registro es de las 9:10 a. m.).
            item.Promesa = TelevisorPromesa.Revisar;
            item.Revision = "La hora del correo es posterior a su registro en Nexa";
        }
        else if (TelevisorHorario.EsHoraNocturna(correo.TimeOfDay))
        {
            item.Promesa = TelevisorPromesa.Nocturno;
        }
        else
        {
            item.Promesa = minutos <= TelevisorPromesa.Minutos ? TelevisorPromesa.Cumple : TelevisorPromesa.NoCumple;
        }
    }

    private static void AplicarGestionDeAgudos(
        TelevisorIngreso item,
        (string Completa, DateTime? CompletaAt, bool Analista, DateTime? AnalistaAt, string? AnalistaPor)? gestion,
        DateTime? inicioCreacionUtc,
        DateTime hastaUtc,
        Personal personal)
    {
        if (gestion is not { } g)
        {
            // Agregaron agudos pero todavía no guardan su formulario: no hay dónde estar la marca.
            item.Gestion = TelevisorGestion.Pendiente;
            item.Analista = false;
            MedirCreacion(item, inicioCreacionUtc, completa: false, completaAt: null, hastaUtc);
            return;
        }

        // Para una foto de un momento pasado, una marca puesta después de ese momento todavía no
        // existía. Sin hora (lo anterior al 2026-10-01) se toma el valor actual.
        var completa = string.Equals(g.Completa, "Completa", StringComparison.OrdinalIgnoreCase)
            && (g.CompletaAt is null || g.CompletaAt <= hastaUtc);
        item.Gestion = completa ? TelevisorGestion.Completa : TelevisorGestion.Pendiente;
        MedirCreacion(item, inicioCreacionUtc, completa, g.CompletaAt, hastaUtc);

        var analista = g.Analista && (g.AnalistaAt is null || g.AnalistaAt <= hastaUtc);
        item.Analista = analista;
        if (analista)
        {
            if (g.AnalistaAt is DateTime analistaAt)
            {
                item.AnalistaAt = ColombiaTime.Convert(analistaAt);
                item.MinutosAnalista = Math.Max(0, (int)Math.Round((item.AnalistaAt.Value - item.EsperaDesde).TotalMinutes));
            }

            var clave = g.AnalistaPor?.Trim().ToUpperInvariant();
            item.AnalistaPor = clave is null
                ? null
                : personal.PorUsuario.TryGetValue(clave, out var p) ? p.Nombre
                : personal.NombresDeUsuario.TryGetValue(clave, out var n) ? n
                : g.AnalistaPor;
        }
    }

    /// <summary>
    /// Meta de creación: 10 minutos desde que se empieza a crear el paciente hasta la gestión
    /// completa. Uno que sigue pendiente cuenta como fuera de tiempo en cuanto pasa de los 10: si
    /// no, dejarlo pendiente lo sacaría de la cuenta.
    /// </summary>
    private static void MedirCreacion(TelevisorIngreso item, DateTime? inicioUtc, bool completa, DateTime? completaAt, DateTime hastaUtc)
    {
        if (inicioUtc is not DateTime inicio)
        {
            return;
        }

        if (completa && completaAt is DateTime fin)
        {
            item.MinutosCreacion = Math.Max(0, (int)Math.Round((fin - inicio).TotalMinutes, MidpointRounding.AwayFromZero));
            item.Creacion = item.MinutosCreacion <= TelevisorCreacion.Minutos ? TelevisorCreacion.Cumple : TelevisorCreacion.NoCumple;
        }
        else if (completa)
        {
            item.Creacion = TelevisorCreacion.SinHora;
        }
        else
        {
            item.MinutosCreacion = Math.Max(0, (int)Math.Floor((hastaUtc - inicio).TotalMinutes));
            item.Creacion = item.MinutosCreacion > TelevisorCreacion.Minutos ? TelevisorCreacion.Vencida : TelevisorCreacion.EnCurso;
        }
    }

    /// <summary>
    /// Cuántos se pueden medir y cuántos cumplen. Un bloque sin ninguna gestión completa con hora
    /// y con alguna sin hora es anterior a que la hora se guardara: solo le quedarían los
    /// pendientes vencidos y daría un 0 % que no dice nada, así que no se da porcentaje.
    /// </summary>
    private static (int Medibles, int Cumplen, double? Porcentaje) ResumirCreacion(IEnumerable<TelevisorIngreso> ingresos)
    {
        var lista = ingresos.Where(x => !x.Cancelado && x.Creacion is not null).ToList();
        var terminados = lista.Count(x => x.Creacion is TelevisorCreacion.Cumple or TelevisorCreacion.NoCumple);
        if (terminados == 0 && lista.Any(x => x.Creacion == TelevisorCreacion.SinHora))
        {
            return (0, 0, null);
        }

        var medibles = lista.Where(x => TelevisorCreacion.Cuenta(x.Creacion)).ToList();
        var cumplen = medibles.Count(x => x.Creacion == TelevisorCreacion.Cumple);
        return (medibles.Count, cumplen, medibles.Count == 0 ? null : Math.Round(cumplen * 100d / medibles.Count, 1));
    }

    // ------------------------------------------------------------------ novedades del portal

    private async Task<TelevisorNovedades> CargarNovedadesAsync(DateTime dia, DateTime estadoAl, CancellationToken ct)
    {
        var resultado = new TelevisorNovedades();
        var ocho = dia.AddHours(8);

        try
        {
            var resumen = await _portal.GetResumenDiaAsync(
                ColombiaTime.ConvertToUtc(dia),
                ColombiaTime.ConvertToUtc(estadoAl),
                ColombiaTime.ConvertToUtc(ocho),
                ct);

            resultado.Ingresadas = resumen.Areas.Sum(a => a.Ingresadas);
            resultado.Resueltas = resumen.Areas.Sum(a => a.Resueltas);
            resultado.PendientesAlCorte = resumen.Areas.Sum(a => a.Pendientes);
            var masAntigua = resumen.Areas.Where(a => a.MasAntiguaPendienteUtc.HasValue).Select(a => a.MasAntiguaPendienteUtc!.Value).DefaultIfEmpty().Min();
            resultado.DiasMasAntigua = masAntigua == default ? null : (estadoAl.Date - ColombiaTime.Convert(masAntigua).Date).Days;
            resultado.ResueltasAt = resumen.ResueltasUtc.Select(ColombiaTime.Convert).ToList();
            resultado.PorArea = resumen.Areas
                .Select(a => new TelevisorNovedadArea
                {
                    Area = a.Area,
                    Etiqueta = EtiquetaArea(a.Area),
                    Pendientes = a.Pendientes,
                    Ingresadas = a.Ingresadas,
                    Resueltas = a.Resueltas
                })
                .OrderBy(a => OrdenArea(a.Area))
                .ToList();

            if (estadoAl >= ocho)
            {
                var foto = await _context.TelevisorCortes.AsNoTracking()
                    .Where(x => x.Dia == dia && x.Tipo == TelevisorCorteTipos.Novedades8am)
                    .Select(x => new { x.DatosJson, x.Recuperado })
                    .FirstOrDefaultAsync(ct);
                var saldo = foto is null ? null : JsonSerializer.Deserialize<TelevisorSaldoNovedades>(foto.DatosJson);
                if (saldo is not null)
                {
                    resultado.Saldo8am = saldo.Saldo;
                    resultado.Saldo8amOrigen = foto!.Recuperado ? "reconstruido" : "foto";
                }
                else
                {
                    resultado.Saldo8am = resumen.Areas.Sum(a => a.Saldo8am);
                    resultado.Saldo8amOrigen = "reconstruido";
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sin el portal, admisiones y analistas se siguen viendo.
            _logger.LogError(ex, "Televisor: no se pudieron leer las novedades del Portal Administrativo.");
            resultado.Disponible = false;
            resultado.Error = "No se pudo consultar el Portal Administrativo.";
        }

        return resultado;
    }

    private static string EtiquetaArea(string area) => area switch
    {
        "ADMISIONES" => "Admisiones",
        "ANALISTA_ASISTENCIAL" => "Analistas asistenciales",
        "CLINICA_HERIDAS" => "Clínica de heridas",
        "DIRECCION_ASISTENCIAL" => "Dirección asistencial",
        "" => "Sin área asignada",
        _ => CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(area.Replace('_', ' ').ToLowerInvariant())
    };

    private static int OrdenArea(string area) => area switch
    {
        "ADMISIONES" => 0,
        "ANALISTA_ASISTENCIAL" => 1,
        "CLINICA_HERIDAS" => 2,
        "DIRECCION_ASISTENCIAL" => 3,
        "" => 9,
        _ => 5
    };

    // ------------------------------------------------------------------ bloques

    private static TelevisorBloque ConstruirBloque(
        string clave,
        string titulo,
        DateTime desde,
        DateTime hasta,
        Ingresos ingresos,
        Personal personal,
        TelevisorNovedades novedades,
        bool habil)
    {
        // "todos" incluye los cancelados y solo alimenta las listas por persona; toda cifra sale
        // de "items", que no los trae.
        var todos = ingresos.Items
            .Where(x => x.Registrado >= desde && x.Registrado < hasta)
            .OrderByDescending(x => x.Registrado)
            .ToList();
        var items = todos.Where(x => !x.Cancelado).ToList();

        return new TelevisorBloque
        {
            Clave = clave,
            Titulo = titulo,
            Desde = desde,
            Hasta = hasta,
            Admisiones = ConstruirAdmisiones(clave, items, todos, personal, habil),
            Analistas = ConstruirAnalistas(clave, items, personal),
            NovedadesResueltas = novedades.ResueltasAt.Count(x => x >= desde && x < hasta),
            Calidad = ConstruirCalidad(items, ingresos)
        };
    }

    private static TelevisorAdmisiones ConstruirAdmisiones(
        string clave,
        List<TelevisorIngreso> items,
        List<TelevisorIngreso> todos,
        Personal personal,
        bool habil)
    {
        var medibles = items.Where(x => TelevisorPromesa.Cuenta(x.Promesa)).ToList();
        var resultado = new TelevisorAdmisiones
        {
            Ingresos = items.Count,
            Cancelados = todos.Count - items.Count,
            Medibles = medibles.Count,
            Cumplen = medibles.Count(x => x.Promesa == TelevisorPromesa.Cumple),
            NoCumplen = medibles.Count(x => x.Promesa == TelevisorPromesa.NoCumple),
            Nocturnos = items.Count(x => x.Promesa == TelevisorPromesa.Nocturno),
            PorRevisar = items.Count(x => x.Promesa is TelevisorPromesa.Revisar or TelevisorPromesa.SinDato),
            MinutosReales = medibles.Sum(x => x.MinutosRespuesta ?? 0),
            MinutosPromesa = medibles.Count * TelevisorPromesa.Minutos,
            PromedioRespuesta = medibles.Count == 0 ? null : medibles.Average(x => x.MinutosRespuesta ?? 0),
            GestionCompletas = items.Count(x => x.Gestion == TelevisorGestion.Completa),
            GestionPendientes = items.Count(x => x.Gestion == TelevisorGestion.Pendiente),
            GestionNoAplica = items.Count(x => x.Gestion == TelevisorGestion.NoAplica)
        };
        resultado.Porcentaje = medibles.Count == 0 ? null : Math.Round(resultado.Cumplen * 100d / medibles.Count, 1);

        (resultado.CreacionMedibles, resultado.CreacionCumplen, resultado.CreacionPorcentaje) = ResumirCreacion(items);
        var creados = items.Where(x => x.Creacion is TelevisorCreacion.Cumple or TelevisorCreacion.NoCumple).ToList();
        resultado.CreacionPromedio = creados.Count == 0 ? null : creados.Average(x => x.MinutosCreacion ?? 0);
        resultado.CreacionEnCurso = items.Count(x => x.Creacion == TelevisorCreacion.EnCurso);

        var demoras = items
            .Where(x => x.Promesa is TelevisorPromesa.Cumple or TelevisorPromesa.NoCumple or TelevisorPromesa.Nocturno
                && x.Respuesta is not null && x.Registrado >= x.Respuesta)
            .Select(x => (int)Math.Round((x.Registrado - x.Respuesta!.Value).TotalMinutes))
            .OrderBy(x => x)
            .ToList();
        resultado.MedianaDemoraRegistro = demoras.Count == 0 ? null : demoras[demoras.Count / 2];

        // Pacientes por turno. Mañana y tarde se parten por la hora de registro, con el corte de
        // la 1:45 p. m. ("todo lo que ingrese después de la 1:45 cuenta para la tarde", aunque lo
        // registre alguien del turno de la mañana). El turno único no tiene corte: cuenta lo que
        // recepcionó la persona de admisiones que está en ese turno, a cualquier hora.
        string TurnoDe(TelevisorIngreso x)
        {
            if (!habil)
            {
                return TelevisorTurnos.FinDeSemana;
            }

            if (personal.PorNombre.TryGetValue(Normalizar(x.Recepciona), out var p)
                && p.Rol == NursingAssistantRoles.Admisiones
                && p.Turno == TelevisorTurnos.Unico)
            {
                return TelevisorTurnos.Unico;
            }

            return x.Bloque == TelevisorBloques.Manana ? TelevisorTurnos.Manana : TelevisorTurnos.Tarde;
        }

        var turnos = items.GroupBy(TurnoDe).ToDictionary(g => g.Key, g => g.Count());
        string[] visibles = !habil ? [TelevisorTurnos.FinDeSemana]
            : clave == TelevisorBloques.Manana ? [TelevisorTurnos.Manana, TelevisorTurnos.Unico]
            : clave == TelevisorBloques.Tarde ? [TelevisorTurnos.Tarde, TelevisorTurnos.Unico]
            : [TelevisorTurnos.Manana, TelevisorTurnos.Tarde, TelevisorTurnos.Unico];
        resultado.PorTurno = visibles
            .Concat(turnos.Keys.Where(k => !visibles.Contains(k)))
            .OrderBy(OrdenTurno)
            .Select(t => new TelevisorConteoTurno
            {
                Turno = t,
                Etiqueta = TelevisorTurnos.Corta(t),
                Pacientes = turnos.GetValueOrDefault(t)
            })
            .ToList();

        // Una tarjeta por persona de admisiones. La lista y la promesa son de lo que figura a su
        // nombre en "quien recepciona el caso"; "registró" cuenta lo que entró con su usuario, a
        // nombre de quien sea. Basta cualquiera de los dos para tener tarjeta.
        var deAdmisiones = personal.Personas.Where(p => p.Rol == NursingAssistantRoles.Admisiones).ToList();
        var porPersona = todos
            .GroupBy(x => personal.PorNombre.TryGetValue(Normalizar(x.Recepciona), out var p) && p.Rol == NursingAssistantRoles.Admisiones ? p : null)
            .ToDictionary(g => g.Key?.Id ?? -1, g => g.ToList());
        var registradosPor = items
            .Where(x => x.RegistradoPor is not null)
            .GroupBy(x => x.RegistradoPor!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var p in deAdmisiones.OrderBy(p => OrdenTurno(p.Turno)).ThenBy(p => p.Nombre, StringComparer.Ordinal))
        {
            var lista = porPersona.GetValueOrDefault(p.Id) ?? [];
            var registradosPorElla = p.Usuario is null ? null : registradosPor.GetValueOrDefault(p.Nombre) ?? [];
            int? registrados = registradosPorElla?.Count;
            if (lista.Count == 0 && registrados is null or 0)
            {
                if (TrabajaEnBloque(p.Turno, clave))
                {
                    resultado.EnTurnoSinIngresos.Add($"{p.Nombre} ({TelevisorTurnos.Chip(p.Turno)})");
                }
                else
                {
                    resultado.SinActividad.Add(p.Nombre);
                }

                continue;
            }

            var tarjeta = TarjetaAdmisiones(p.Nombre, false, p.Turno, p.Conectada, lista);
            tarjeta.SinUsuario = p.Usuario is null;
            tarjeta.Registrados = registrados;
            (tarjeta.CreacionMedibles, tarjeta.CreacionCumplen, tarjeta.CreacionPorcentaje) = ResumirCreacion(registradosPorElla ?? []);
            resultado.Personas.Add(tarjeta);
        }

        if (porPersona.TryGetValue(-1, out var otros) && otros.Count > 0)
        {
            resultado.Personas.Add(TarjetaAdmisiones("Otros", true, TelevisorTurnos.SinConexion, false, otros));
        }

        return resultado;
    }

    private static TelevisorPersonaAdmisiones TarjetaAdmisiones(
        string nombre,
        bool esOtros,
        string turno,
        bool conectada,
        List<TelevisorIngreso> lista)
    {
        var medibles = lista.Where(x => !x.Cancelado && TelevisorPromesa.Cuenta(x.Promesa)).ToList();
        var cumplen = medibles.Count(x => x.Promesa == TelevisorPromesa.Cumple);
        return new TelevisorPersonaAdmisiones
        {
            Nombre = nombre,
            EsOtros = esOtros,
            Turno = turno,
            Conectada = conectada,
            Ingresos = lista.Count(x => !x.Cancelado),
            Medibles = medibles.Count,
            Cumplen = cumplen,
            Porcentaje = medibles.Count == 0 ? null : Math.Round(cumplen * 100d / medibles.Count, 1),
            Balance = medibles.Sum(x => x.MinutosRespuesta ?? 0) - medibles.Count * TelevisorPromesa.Minutos,
            Lista = lista
        };
    }

    private static TelevisorAnalistas ConstruirAnalistas(string clave, List<TelevisorIngreso> items, Personal personal)
    {
        var agudos = items.Where(x => x.Analista is not null).ToList();
        var universo = agudos.Where(x => x.EnFranjaAnalistas).ToList();
        var conCheck = universo.Where(x => x.Analista == true).ToList();
        var conHora = agudos.Where(x => x.MinutosAnalista.HasValue).ToList();

        var resultado = new TelevisorAnalistas
        {
            Universo = universo.Count,
            ConCheck = conCheck.Count,
            Porcentaje = universo.Count == 0 ? null : Math.Round(conCheck.Count * 100d / universo.Count, 1),
            FueraDeFranja = agudos.Count - universo.Count,
            ConHora = conHora.Count,
            PromedioMinutos = conHora.Count == 0 ? null : conHora.Average(x => x.MinutosAnalista!.Value),
            Pendientes = universo.Where(x => x.Analista != true).OrderBy(x => x.EsperaDesde).ToList()
        };

        var gestionados = agudos.Where(x => x.Analista == true).ToList();
        var analistas = personal.Personas.Where(p => p.Rol == NursingAssistantRoles.AnalistaAsistencial).ToList();
        var nombresAnalistas = analistas.Select(p => p.Nombre).ToHashSet(StringComparer.Ordinal);

        foreach (var p in analistas.OrderBy(p => OrdenTurno(p.Turno)).ThenBy(p => p.Nombre, StringComparer.Ordinal))
        {
            var lista = gestionados.Where(x => x.AnalistaPor == p.Nombre).OrderByDescending(x => x.AnalistaAt ?? x.Registrado).ToList();
            if (lista.Count == 0 && !TrabajaEnBloque(p.Turno, clave))
            {
                resultado.SinActividad.Add(p.Nombre);
                continue;
            }

            resultado.Personas.Add(TarjetaAnalista(p.Nombre, false, p.Turno, p.Conectada, lista));
        }

        var otros = gestionados.Where(x => x.AnalistaPor is null || !nombresAnalistas.Contains(x.AnalistaPor)).ToList();
        if (otros.Count > 0)
        {
            resultado.Personas.Add(TarjetaAnalista("Otros usuarios", true, TelevisorTurnos.SinConexion, false, otros));
        }

        return resultado;
    }

    private static TelevisorPersonaAnalista TarjetaAnalista(string nombre, bool esOtros, string turno, bool conectada, List<TelevisorIngreso> lista)
    {
        var conHora = lista.Where(x => x.MinutosAnalista.HasValue).ToList();
        return new TelevisorPersonaAnalista
        {
            Nombre = nombre,
            EsOtros = esOtros,
            Turno = turno,
            Conectada = conectada,
            Gestionados = lista.Count,
            PromedioMinutos = conHora.Count == 0 ? null : conHora.Average(x => x.MinutosAnalista!.Value),
            Lista = lista
        };
    }

    private static List<TelevisorAlerta> ConstruirCalidad(List<TelevisorIngreso> items, Ingresos ingresos)
    {
        var alertas = new List<TelevisorAlerta>();

        foreach (var x in items.OrderBy(x => x.Registrado))
        {
            if (x.Promesa is TelevisorPromesa.Revisar or TelevisorPromesa.SinDato)
            {
                alertas.Add(new TelevisorAlerta { Tipo = x.Promesa, Texto = x.Revision ?? "Revisar", Paciente = x.Paciente, Persona = NullSiVacio(x.Recepciona) });
            }

            if (string.IsNullOrWhiteSpace(x.Recepciona) && x.Promesa is not (TelevisorPromesa.SinDato or TelevisorPromesa.NoAplica))
            {
                alertas.Add(new TelevisorAlerta { Tipo = "sin-recepciona", Texto = "No dice quién recepcionó el caso", Paciente = x.Paciente });
            }

            if (x.Respuesta is DateTime r
                && x.Promesa is not (TelevisorPromesa.Revisar or TelevisorPromesa.NoAplica)
                && x.Registrado - r > TimeSpan.FromHours(24))
            {
                alertas.Add(new TelevisorAlerta
                {
                    Tipo = "registro-tardio",
                    Texto = $"Se registró {Duracion(x.Registrado - r)} después de responder",
                    Paciente = x.Paciente,
                    Persona = NullSiVacio(x.Recepciona)
                });
            }

            if (ingresos.Extra.TryGetValue(x.EpisodioId, out var extra) && extra.SinFormulario)
            {
                alertas.Add(new TelevisorAlerta
                {
                    Tipo = "sin-formulario",
                    Texto = $"Se agregó {x.ProgramaCorto} pero no se ha guardado su formulario",
                    Paciente = x.Paciente,
                    Persona = NullSiVacio(x.Recepciona)
                });
            }
        }

        // El mismo paciente con dos ingresos al mismo programa en el bloque.
        foreach (var grupo in items
            .Where(x => ingresos.Extra.ContainsKey(x.EpisodioId))
            .GroupBy(x => (ingresos.Extra[x.EpisodioId].PacienteId, x.Programa))
            .Where(g => g.Count() > 1))
        {
            var primero = grupo.First();
            alertas.Add(new TelevisorAlerta
            {
                Tipo = "duplicado",
                Texto = $"Aparece {grupo.Count()} veces en {primero.ProgramaCorto}: ¿ingreso duplicado?",
                Paciente = primero.Paciente
            });
        }

        return alertas;
    }

    private static bool TrabajaEnBloque(string turno, string bloque) => bloque switch
    {
        TelevisorBloques.Manana => turno is TelevisorTurnos.Manana or TelevisorTurnos.Unico,
        TelevisorBloques.Tarde => turno is TelevisorTurnos.Tarde or TelevisorTurnos.Unico,
        _ => turno is TelevisorTurnos.Manana or TelevisorTurnos.Tarde or TelevisorTurnos.Unico or TelevisorTurnos.FinDeSemana
    };

    private static int OrdenTurno(string turno) => turno switch
    {
        TelevisorTurnos.Manana => 0,
        TelevisorTurnos.Unico => 1,
        TelevisorTurnos.FinDeSemana => 1,
        TelevisorTurnos.Tarde => 2,
        _ => 3
    };

    // ------------------------------------------------------------------ utilidades

    private static bool EsCanceladoORechazado(string? estado) =>
        estado is not null
        && (estado.Contains("cancelad", StringComparison.OrdinalIgnoreCase)
            || estado.Contains("rechazad", StringComparison.OrdinalIgnoreCase));

    private static DateTime? Combinar(DateTime? fecha, TimeSpan? hora) =>
        fecha is DateTime f && f.Year > 1900 && hora is TimeSpan h ? f.Date + h : null;

    private static string? Limpio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NullSiVacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// "MARIA ELENA  GOMEZ RUIZ" → "Maria Elena Gomez Ruiz". Sin nombre en el maestro, el documento,
    /// para que la fila no quede en blanco.
    /// </summary>
    internal static string NombrePaciente(string? nombre, string? documento)
    {
        var partes = (nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
        {
            return string.IsNullOrWhiteSpace(documento) ? "Sin nombre" : $"Documento {documento.Trim()}";
        }

        return NombreVisible(string.Join(' ', partes));
    }

    internal static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        var espacio = false;
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                if (!espacio && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                espacio = true;
                continue;
            }

            sb.Append(c);
            espacio = false;
        }

        return sb.ToString().TrimEnd();
    }

    private static HashSet<string> Tokens(string texto) =>
        Tokens(texto, conservarOrden: false).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> Tokens(string texto, bool conservarOrden) =>
        Normalizar(texto)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !Stopwords.Contains(t));

    /// <summary>
    /// "BRENDA SARRAZOLA LOPEZ" → "Brenda Sarrazola": en la tarjeta del televisor no cabe el nombre
    /// completo. Si la segunda palabra es de enlace ("AUX DE APOYO") se deja entero.
    /// </summary>
    private static string NombreCorto(string nombre)
    {
        var partes = nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length >= 3 && !Stopwords.Contains(Normalizar(partes[1])))
        {
            return NombreVisible($"{partes[0]} {partes[1]}");
        }

        return NombreVisible(nombre);
    }

    /// <summary>"Karina Paola" + "Suarez" → "Karina Suarez", para quien no está en la tabla de personal.</summary>
    private static string NombreDeUsuario(string? nombres, string? apellido, string? completo)
    {
        var nombre = (nombres ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var ape = (apellido ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return nombre is null || ape is null ? NombreVisible(completo ?? string.Empty) : NombreVisible($"{nombre} {ape}");
    }

    private static string NombreVisible(string nombre) =>
        CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(nombre.Trim().ToLowerInvariant());

    private static string Duracion(TimeSpan t) =>
        t.TotalHours >= 48 ? $"{(int)t.TotalDays} días" : $"{(int)t.TotalHours} h";
}

/// <summary>Foto del saldo de novedades a las 8:00 a. m. (televisor_cortes, tipo NOVEDADES_8AM).</summary>
public class TelevisorSaldoNovedades
{
    public int Saldo { get; set; }

    public List<TelevisorNovedadArea> PorArea { get; set; } = [];
}
