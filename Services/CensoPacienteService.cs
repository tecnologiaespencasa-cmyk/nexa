using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;

namespace Nexa.Services;

/// <inheritdoc />
public class CensoPacienteService : ICensoPacienteService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CensoPacienteService> _logger;

    public CensoPacienteService(ApplicationDbContext context, ILogger<CensoPacienteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Misma normalización de documento que usa el censo de agudos: sin espacios y en mayúsculas,
    /// para que un pasaporte escrito en minúscula no cree un paciente duplicado.
    /// </summary>
    public static string NormalizarDocumento(string? documento) =>
        (documento ?? string.Empty).Trim().ToUpperInvariant();

    public Task<CensoPaciente?> BuscarPorDocumentoAsync(string? documento, CancellationToken cancellationToken)
    {
        var normalizado = NormalizarDocumento(documento);
        if (string.IsNullOrWhiteSpace(normalizado))
        {
            return Task.FromResult<CensoPaciente?>(null);
        }

        return _context.CensoPacientes
            .Include(x => x.Programas)
            .FirstOrDefaultAsync(x => x.NumeroIdentificacion.ToUpper() == normalizado, cancellationToken);
    }

    public Task<CensoPaciente?> ObtenerAsync(long pacienteId, CancellationToken cancellationToken) =>
        _context.CensoPacientes
            .Include(x => x.Programas)
            .FirstOrDefaultAsync(x => x.Id == pacienteId, cancellationToken);

    public async Task<IReadOnlyList<CensoPacientePrograma>> ObtenerProgramasAsync(
        long pacienteId,
        CancellationToken cancellationToken)
    {
        var programas = await _context.CensoPacienteProgramas
            .AsNoTracking()
            .Where(x => x.CensoPacienteId == pacienteId)
            .ToListAsync(cancellationToken);

        return programas
            .OrderBy(x => CensoProgramas.Jerarquia(x.Programa))
            .ThenByDescending(x => x.AgregadoAtUtc)
            .ToList();
    }

    public async Task<ServiceResult<CensoPaciente>> GuardarAsync(
        CensoPacienteFormViewModel formulario,
        string usuario,
        CancellationToken cancellationToken)
    {
        var documento = NormalizarDocumento(formulario.NumeroIdentificacion);
        if (string.IsNullOrWhiteSpace(documento))
        {
            return ServiceResult<CensoPaciente>.Failure("El número de identificación es obligatorio.");
        }

        // Se busca siempre por documento y no solo por id: si el usuario escribe el documento de un
        // paciente que ya existe, se debe actualizar ese maestro y no crear uno nuevo que rompa el
        // índice único.
        var paciente = await _context.CensoPacientes
            .Include(x => x.Programas)
            .FirstOrDefaultAsync(x => x.NumeroIdentificacion.ToUpper() == documento, cancellationToken);

        if (paciente is null && formulario.PacienteId.HasValue)
        {
            paciente = await _context.CensoPacientes
                .Include(x => x.Programas)
                .FirstOrDefaultAsync(x => x.Id == formulario.PacienteId.Value, cancellationToken);
        }

        var esNuevo = paciente is null;
        if (paciente is null)
        {
            paciente = new CensoPaciente
            {
                CreatedAtUtc = DateTime.UtcNow,
                CreadoPor = usuario
            };
            await _context.CensoPacientes.AddAsync(paciente, cancellationToken);
        }

        AplicarFormulario(paciente, formulario, documento);
        if (!esNuevo)
        {
            paciente.UpdatedAtUtc = DateTime.UtcNow;
            paciente.ActualizadoPor = usuario;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // La replicación va después de guardar el maestro: si algo fallara aquí, el maestro ya está
        // salvo y los censos quedan como estaban, nunca a medias.
        await ReplicarAProgramasAbiertosAsync(paciente, cancellationToken);

        return ServiceResult<CensoPaciente>.Success(paciente);
    }

    private static void AplicarFormulario(CensoPaciente paciente, CensoPacienteFormViewModel f, string documento)
    {
        paciente.FechaIngreso = f.FechaIngreso.Date;
        paciente.HoraIngreso = f.HoraIngreso;
        paciente.FechaRespuesta = f.FechaRespuesta?.Date;
        paciente.HoraRespuesta = f.HoraRespuesta;
        paciente.IndicadorTiempoRespuestaMinutos = f.IndicadorTiempoRespuestaMinutos;
        paciente.NombreRecepcionaCaso = Limpiar(f.NombreRecepcionaCaso);
        paciente.NombreRealizaKardex = Limpiar(f.NombreRealizaKardex);

        paciente.TipoIdentificacion = Limpiar(f.TipoIdentificacion) ?? string.Empty;
        paciente.NumeroIdentificacion = documento;
        paciente.NombrePaciente = Limpiar(f.NombrePaciente) ?? string.Empty;
        paciente.FechaNacimiento = f.FechaNacimiento.Date;
        paciente.Edad = f.Edad;
        paciente.Genero = Limpiar(f.Genero);
        paciente.CorreoElectronico = Limpiar(f.CorreoElectronico);
        paciente.CodigoCie10 = Limpiar(f.CodigoCie10)?.ToUpperInvariant();
        paciente.DiagnosticoDescriptivo = Limpiar(f.DiagnosticoDescriptivo);
        paciente.Asegurador = Limpiar(f.Asegurador);

        paciente.Direccion = Limpiar(f.Direccion);
        paciente.DireccionValidada = f.DireccionEsValida;
        paciente.AsumirDireccionErrada = f.AsumirDireccionErrada;
        paciente.DetalleDireccion = Limpiar(f.DetalleDireccion);
        paciente.ClasificacionZonaSura = Limpiar(f.ClasificacionZonaSura);
        paciente.MunicipioResidencia = Limpiar(f.MunicipioResidencia);
        paciente.Barrio = Limpiar(f.Barrio);
        paciente.ZonaDireccionSegunMunicipio = Limpiar(f.ZonaDireccionSegunMunicipio);
        paciente.Area = Limpiar(f.Area);

        paciente.IpsQueRemite = Limpiar(f.IpsQueRemite);
        paciente.VistoBuenoRangoFueraAnexo = Limpiar(f.VistoBuenoRangoFueraAnexo);
        paciente.Telefono1 = Limpiar(f.Telefono1);
        paciente.Telefono2 = Limpiar(f.Telefono2);
        paciente.Telefono3 = Limpiar(f.Telefono3);
    }

    public async Task<ServiceResult<CensoPacientePrograma>> AgregarProgramaAsync(
        long pacienteId,
        string programa,
        string usuario,
        CancellationToken cancellationToken)
    {
        if (!CensoProgramas.EsValido(programa))
        {
            return ServiceResult<CensoPacientePrograma>.Failure("El programa seleccionado no existe.");
        }

        var paciente = await _context.CensoPacientes
            .FirstOrDefaultAsync(x => x.Id == pacienteId, cancellationToken);
        if (paciente is null)
        {
            return ServiceResult<CensoPacientePrograma>.Failure(
                "Guarda primero la recepción y los datos básicos del paciente.");
        }

        var abiertos = await _context.CensoPacienteProgramas
            .Where(x => x.CensoPacienteId == pacienteId && x.CerradoAtUtc == null)
            .ToListAsync(cancellationToken);

        if (abiertos.Any(x => string.Equals(x.Programa, programa, StringComparison.Ordinal)))
        {
            return ServiceResult<CensoPacientePrograma>.Failure(
                $"El paciente ya tiene {CensoProgramas.Nombre(programa)} activo.");
        }

        var bloquea = CensoProgramas.PrimeroQueBloquea(programa, abiertos.Select(x => x.Programa));
        if (bloquea is not null)
        {
            return ServiceResult<CensoPacientePrograma>.Failure(
                $"El paciente tiene {CensoProgramas.Nombre(bloquea)} activo. " +
                $"Cierra ese programa antes de agregar {CensoProgramas.Nombre(programa)}.");
        }

        var episodio = new CensoPacientePrograma
        {
            CensoPacienteId = pacienteId,
            Programa = programa,
            AgregadoAtUtc = DateTime.UtcNow,
            AgregadoPor = usuario
        };

        await _context.CensoPacienteProgramas.AddAsync(episodio, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult<CensoPacientePrograma>.Success(episodio);
    }

    public async Task<ServiceResult> QuitarProgramaAsync(
        long episodioId,
        string usuario,
        CancellationToken cancellationToken)
    {
        var episodio = await _context.CensoPacienteProgramas
            .FirstOrDefaultAsync(x => x.Id == episodioId, cancellationToken);
        if (episodio is null)
        {
            return ServiceResult.Failure("El programa ya no está asociado al paciente.");
        }

        // Un programa con datos guardados no se elimina nunca: eso borraría información clínica de
        // producción. Se cierra desde la sección de alta o egreso del propio programa.
        if (episodio.RegistroId.HasValue)
        {
            return ServiceResult.Failure(
                $"{CensoProgramas.Nombre(episodio.Programa)} ya tiene información guardada. " +
                "Ciérralo desde su sección de alta o egreso en lugar de quitarlo.");
        }

        _context.CensoPacienteProgramas.Remove(episodio);
        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    /// <summary>
    /// Crea el maestro de un documento que ya tiene filas en algún censo pero todavía no está
    /// unificado. Ocurre con los registros que se crearon por fuera de la pantalla única —los que
    /// quedaron entre el backfill y el despliegue, o cualquiera que entre por otra vía—, y evita que
    /// esos pacientes queden fuera del censo unificado hasta que alguien los migre a mano.
    ///
    /// Aplica la misma regla del backfill: gana el valor no vacío del registro más reciente.
    /// </summary>
    public async Task<CensoPaciente?> AsegurarMaestroAsync(string? documento, CancellationToken cancellationToken)
    {
        var doc = NormalizarDocumento(documento);
        if (string.IsNullOrWhiteSpace(doc))
        {
            return null;
        }

        var existente = await BuscarPorDocumentoAsync(doc, cancellationToken);
        if (existente is not null)
        {
            return existente;
        }

        // Candidatos ordenados de más reciente a más antiguo. Las copias internas de despacho a
        // farmacia van al final: son duplicados del flujo de farmacia, no atenciones.
        var candidatos = new List<(DateTime Orden, int EsCopia, CensoPaciente Datos)>();

        foreach (var x in await _context.Censos.AsNoTracking()
                     .Where(x => x.NumeroIdentificacion.ToUpper() == doc).ToListAsync(cancellationToken))
        {
            candidatos.Add((x.FechaIngreso,
                x.FarmaciaProrrogaDeId != null || x.FarmaciaProrrogaVersionId != null ? 1 : 0,
                new CensoPaciente
                {
                    FechaIngreso = x.FechaIngreso, HoraIngreso = x.HoraIngreso,
                    FechaRespuesta = x.FechaRespuesta, HoraRespuesta = x.HoraRespuesta,
                    IndicadorTiempoRespuestaMinutos = x.IndicadorTiempoRespuestaMinutos,
                    NombreRecepcionaCaso = x.NombreRecepcionaCaso, NombreRealizaKardex = x.NombreRealizaKardex,
                    TipoIdentificacion = x.TipoIdentificacion, NumeroIdentificacion = x.NumeroIdentificacion,
                    NombrePaciente = x.NombrePaciente, FechaNacimiento = x.FechaNacimiento, Edad = x.Edad,
                    CorreoElectronico = x.CorreoElectronico, CodigoCie10 = x.CodigoCie10,
                    DiagnosticoDescriptivo = x.DiagnosticoDescriptivo, Asegurador = x.Asegurador,
                    Direccion = x.Direccion, DireccionValidada = x.DireccionValidada,
                    AsumirDireccionErrada = x.AsumirDireccionErrada, DetalleDireccion = x.DetalleDireccion,
                    ClasificacionZonaSura = x.ClasificacionZonaSura, MunicipioResidencia = x.MunicipioResidencia,
                    Barrio = x.Barrio, ZonaDireccionSegunMunicipio = x.ZonaDireccionSegunMunicipio, Area = x.Area,
                    IpsQueRemite = x.IpsQueRemite, VistoBuenoRangoFueraAnexo = x.VistoBuenoRangoFueraAnexo,
                    Telefono1 = x.Telefono1, Telefono2 = x.Telefono2, Telefono3 = x.Telefono3
                }));
        }

        foreach (var x in await _context.CensoCronicos.AsNoTracking()
                     .Where(x => x.NumeroIdentificacion.ToUpper() == doc).ToListAsync(cancellationToken))
        {
            candidatos.Add((x.FechaIngreso, 0, new CensoPaciente
            {
                FechaIngreso = x.FechaIngreso,
                TipoIdentificacion = x.TipoIdentificacion, NumeroIdentificacion = x.NumeroIdentificacion,
                NombrePaciente = x.NombrePaciente, FechaNacimiento = x.FechaNacimiento, Edad = x.Edad,
                Genero = x.Genero, CorreoElectronico = x.CorreoElectronico,
                Direccion = x.Direccion, DireccionValidada = x.DireccionValidada,
                AsumirDireccionErrada = x.AsumirDireccionErrada, DetalleDireccion = x.DetalleDireccion,
                ClasificacionZonaSura = x.ClasificacionZonaSura, MunicipioResidencia = x.MunicipioResidencia,
                Barrio = x.Barrio, ZonaDireccionSegunMunicipio = x.ZonaDireccionSegunMunicipio, Area = x.Area
            }));
        }

        foreach (var x in await _context.CensoClinicaHeridas.AsNoTracking()
                     .Where(x => x.NumeroIdentificacion.ToUpper() == doc).ToListAsync(cancellationToken))
        {
            candidatos.Add((x.FechaIngresoPrograma, 0, new CensoPaciente
            {
                FechaIngreso = x.FechaIngresoPrograma,
                TipoIdentificacion = x.TipoIdentificacion, NumeroIdentificacion = x.NumeroIdentificacion,
                NombrePaciente = x.NombrePaciente, FechaNacimiento = x.FechaNacimiento, Edad = x.Edad,
                Genero = x.Genero, Asegurador = x.Asegurador, CodigoCie10 = x.CodigoCie10,
                DiagnosticoDescriptivo = x.DiagnosticoDescriptivo,
                Direccion = x.Direccion, DireccionValidada = x.DireccionValidada,
                AsumirDireccionErrada = x.AsumirDireccionErrada, DetalleDireccion = x.DetalleDireccion,
                ClasificacionZonaSura = x.ClasificacionZonaSura, MunicipioResidencia = x.MunicipioResidencia,
                Barrio = x.Barrio, ZonaDireccionSegunMunicipio = x.ZonaDireccionSegunMunicipio,
                Telefono1 = x.TelefonoPrincipal, Telefono2 = x.TelefonoAdicional1, Telefono3 = x.TelefonoAdicional2
            }));
        }

        foreach (var x in await _context.CensoNpt.AsNoTracking()
                     .Where(x => x.NumeroIdentificacion.ToUpper() == doc).ToListAsync(cancellationToken))
        {
            candidatos.Add((x.FechaIngresoPrograma, 0, new CensoPaciente
            {
                FechaIngreso = x.FechaIngresoPrograma,
                TipoIdentificacion = x.TipoIdentificacion, NumeroIdentificacion = x.NumeroIdentificacion,
                NombrePaciente = x.NombrePaciente, FechaNacimiento = x.FechaNacimiento, Edad = x.Edad,
                Genero = x.Genero, Asegurador = x.Asegurador, CodigoCie10 = x.CodigoCie10,
                DiagnosticoDescriptivo = x.DiagnosticoDescriptivo,
                Direccion = x.Direccion, DireccionValidada = x.DireccionValidada,
                AsumirDireccionErrada = x.AsumirDireccionErrada,
                ClasificacionZonaSura = x.ClasificacionZonaSura, MunicipioResidencia = x.MunicipioResidencia,
                Barrio = x.Barrio, ZonaDireccionSegunMunicipio = x.ZonaDireccionSegunMunicipio,
                Telefono1 = x.TelefonoPrincipal, Telefono2 = x.TelefonoAdicional1, Telefono3 = x.TelefonoAdicional2
            }));
        }

        foreach (var x in await _context.CensoTerapiasAmbulatorias.AsNoTracking()
                     .Where(x => x.NumeroIdentificacion.ToUpper() == doc).ToListAsync(cancellationToken))
        {
            candidatos.Add((x.FechaIngreso, 0, new CensoPaciente
            {
                FechaIngreso = x.FechaIngreso,
                TipoIdentificacion = x.TipoIdentificacion, NumeroIdentificacion = x.NumeroIdentificacion,
                NombrePaciente = x.NombrePaciente, FechaNacimiento = x.FechaNacimiento, Edad = x.Edad,
                CorreoElectronico = x.CorreoElectronico, CodigoCie10 = x.CodigoCie10,
                DiagnosticoDescriptivo = x.DiagnosticoDescriptivo,
                Direccion = x.Direccion, DireccionValidada = x.DireccionValidada,
                AsumirDireccionErrada = x.AsumirDireccionErrada, DetalleDireccion = x.DetalleDireccion,
                ClasificacionZonaSura = x.ClasificacionZonaSura, MunicipioResidencia = x.MunicipioResidencia,
                Barrio = x.Barrio, ZonaDireccionSegunMunicipio = x.ZonaDireccionSegunMunicipio, Area = x.Area,
                IpsQueRemite = x.IpsQueRemite,
                Telefono1 = x.TelefonoPrincipal, Telefono2 = x.TelefonoAdicional1, Telefono3 = x.TelefonoAdicional2
            }));
        }

        if (candidatos.Count == 0)
        {
            return null;
        }

        var ordenados = candidatos
            .OrderBy(x => x.EsCopia)
            .ThenByDescending(x => x.Orden)
            .Select(x => x.Datos)
            .ToList();

        // Campo a campo: gana el primer valor no vacío del orden anterior, igual que el backfill.
        string? Texto(Func<CensoPaciente, string?> selector) =>
            ordenados.Select(selector).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var principal = ordenados[0];
        var maestro = new CensoPaciente
        {
            FechaIngreso = principal.FechaIngreso,
            HoraIngreso = ordenados.Select(x => x.HoraIngreso).FirstOrDefault(x => x != default),
            FechaRespuesta = ordenados.Select(x => x.FechaRespuesta).FirstOrDefault(x => x.HasValue),
            HoraRespuesta = ordenados.Select(x => x.HoraRespuesta).FirstOrDefault(x => x.HasValue),
            IndicadorTiempoRespuestaMinutos = ordenados.Select(x => x.IndicadorTiempoRespuestaMinutos).FirstOrDefault(x => x.HasValue),
            NombreRecepcionaCaso = Texto(x => x.NombreRecepcionaCaso),
            NombreRealizaKardex = Texto(x => x.NombreRealizaKardex),
            TipoIdentificacion = Texto(x => x.TipoIdentificacion) ?? string.Empty,
            NumeroIdentificacion = Texto(x => x.NumeroIdentificacion) ?? doc,
            NombrePaciente = Texto(x => x.NombrePaciente) ?? string.Empty,
            FechaNacimiento = ordenados.Select(x => x.FechaNacimiento).FirstOrDefault(x => x != default),
            Edad = ordenados.Select(x => x.Edad).FirstOrDefault(x => x > 0),
            Genero = Texto(x => x.Genero),
            CorreoElectronico = Texto(x => x.CorreoElectronico),
            CodigoCie10 = Texto(x => x.CodigoCie10),
            DiagnosticoDescriptivo = Texto(x => x.DiagnosticoDescriptivo),
            Asegurador = Texto(x => x.Asegurador),
            DetalleDireccion = Texto(x => x.DetalleDireccion),
            ClasificacionZonaSura = Texto(x => x.ClasificacionZonaSura),
            MunicipioResidencia = Texto(x => x.MunicipioResidencia),
            Barrio = Texto(x => x.Barrio),
            ZonaDireccionSegunMunicipio = Texto(x => x.ZonaDireccionSegunMunicipio),
            Area = Texto(x => x.Area),
            IpsQueRemite = Texto(x => x.IpsQueRemite),
            VistoBuenoRangoFueraAnexo = Texto(x => x.VistoBuenoRangoFueraAnexo),
            Telefono1 = Texto(x => x.Telefono1),
            Telefono2 = Texto(x => x.Telefono2),
            Telefono3 = Texto(x => x.Telefono3),
            CreatedAtUtc = DateTime.UtcNow,
            CreadoPor = "Unificación automática"
        };

        // La dirección y sus dos banderas salen del mismo registro, para no marcar como validada una
        // dirección que vino de otra fila.
        var conDireccion = ordenados.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Direccion));
        if (conDireccion is not null)
        {
            maestro.Direccion = conDireccion.Direccion;
            maestro.DireccionValidada = conDireccion.DireccionValidada;
            maestro.AsumirDireccionErrada = conDireccion.AsumirDireccionErrada;
        }

        await _context.CensoPacientes.AddAsync(maestro, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Se creó el maestro {PacienteId} para el documento {Documento} a partir de {Filas} registros de censo ya existentes.",
            maestro.Id, doc, candidatos.Count);

        return maestro;
    }

    // ==========================================================================================
    // Replicación del maestro hacia los censos de programa.
    //
    // Es lo que garantiza la continuidad: los kardex, las requisiciones, la bandeja de farmacia, las
    // prórrogas, el puente de Supabase y todos los exportables siguen leyendo de la tabla de su
    // programa, exactamente igual que antes de la unificación.
    //
    // Reglas:
    //  - Solo episodios abiertos. Una atención cerrada conserva los datos con los que se prestó.
    //  - Solo campos compartidos. Nunca se toca una columna propia del programa.
    //  - Un campo vacío en el maestro no borra el valor que ya tenga el programa.
    // ==========================================================================================
    public async Task ReplicarAProgramasAbiertosAsync(CensoPaciente paciente, CancellationToken cancellationToken)
    {
        var episodios = await _context.CensoPacienteProgramas
            .Where(x => x.CensoPacienteId == paciente.Id && x.CerradoAtUtc == null && x.RegistroId != null)
            .ToListAsync(cancellationToken);

        if (episodios.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var episodio in episodios)
            {
                var registroId = episodio.RegistroId!.Value;
                switch (episodio.Programa)
                {
                    case CensoProgramas.Agudos:
                        await ReplicarAgudosAsync(paciente, registroId, cancellationToken);
                        break;
                    case CensoProgramas.Cronicos:
                        await ReplicarCronicosAsync(paciente, registroId, cancellationToken);
                        break;
                    case CensoProgramas.ClinicaHeridas:
                        await ReplicarClinicaHeridasAsync(paciente, registroId, cancellationToken);
                        break;
                    case CensoProgramas.Npt:
                        await ReplicarNptAsync(paciente, registroId, cancellationToken);
                        break;
                    case CensoProgramas.TerapiaAmbulatoria:
                        await ReplicarTerapiaAsync(paciente, registroId, cancellationToken);
                        break;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // La replicación no puede tumbar el guardado del maestro. Si falla se registra y el
            // paciente queda guardado; los censos siguen con los datos que ya tenían.
            _logger.LogError(
                ex,
                "No se pudo replicar el maestro de censo {PacienteId} a sus programas abiertos.",
                paciente.Id);
        }
    }

    private async Task ReplicarAgudosAsync(CensoPaciente p, long registroId, CancellationToken ct)
    {
        var r = await _context.Censos.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null)
        {
            return;
        }

        r.CensoPacienteId = p.Id;

        r.FechaIngreso = p.FechaIngreso;
        r.HoraIngreso = p.HoraIngreso;
        if (p.FechaRespuesta.HasValue) r.FechaRespuesta = p.FechaRespuesta.Value;
        if (p.HoraRespuesta.HasValue) r.HoraRespuesta = p.HoraRespuesta.Value;
        if (p.IndicadorTiempoRespuestaMinutos.HasValue)
            r.IndicadorTiempoRespuestaMinutos = p.IndicadorTiempoRespuestaMinutos.Value;
        r.NombreRecepcionaCaso = Preferir(p.NombreRecepcionaCaso, r.NombreRecepcionaCaso);
        r.NombreRealizaKardex = Preferir(p.NombreRealizaKardex, r.NombreRealizaKardex);

        r.NombrePaciente = Preferir(p.NombrePaciente, r.NombrePaciente);
        r.TipoIdentificacion = Preferir(p.TipoIdentificacion, r.TipoIdentificacion);
        r.NumeroIdentificacion = Preferir(p.NumeroIdentificacion, r.NumeroIdentificacion);
        r.FechaNacimiento = p.FechaNacimiento;
        r.Edad = p.Edad;
        r.CorreoElectronico = Preferir(p.CorreoElectronico, r.CorreoElectronico);
        r.CodigoCie10 = Preferir(p.CodigoCie10, r.CodigoCie10);
        r.DiagnosticoDescriptivo = Preferir(p.DiagnosticoDescriptivo, r.DiagnosticoDescriptivo);
        r.Asegurador = Preferir(p.Asegurador, r.Asegurador);

        r.Direccion = Preferir(p.Direccion, r.Direccion);
        r.DireccionValidada = p.DireccionValidada;
        r.AsumirDireccionErrada = p.AsumirDireccionErrada;
        r.DetalleDireccion = PreferirOpcional(p.DetalleDireccion, r.DetalleDireccion);
        r.ClasificacionZonaSura = Preferir(p.ClasificacionZonaSura, r.ClasificacionZonaSura);
        r.MunicipioResidencia = Preferir(p.MunicipioResidencia, r.MunicipioResidencia);
        r.Barrio = Preferir(p.Barrio, r.Barrio);
        r.ZonaDireccionSegunMunicipio = Preferir(p.ZonaDireccionSegunMunicipio, r.ZonaDireccionSegunMunicipio);
        r.Area = Preferir(p.Area, r.Area);

        r.IpsQueRemite = Preferir(p.IpsQueRemite, r.IpsQueRemite);
        r.VistoBuenoRangoFueraAnexo = Preferir(p.VistoBuenoRangoFueraAnexo, r.VistoBuenoRangoFueraAnexo);
        r.Telefono1 = Preferir(p.Telefono1, r.Telefono1);
        r.Telefono2 = Preferir(p.Telefono2, r.Telefono2);
        r.Telefono3 = PreferirOpcional(p.Telefono3, r.Telefono3);
    }

    private async Task ReplicarCronicosAsync(CensoPaciente p, long registroId, CancellationToken ct)
    {
        var r = await _context.CensoCronicos.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null)
        {
            return;
        }

        r.CensoPacienteId = p.Id;

        r.FechaIngreso = p.FechaIngreso;
        r.NombrePaciente = Preferir(p.NombrePaciente, r.NombrePaciente);
        r.TipoIdentificacion = Preferir(p.TipoIdentificacion, r.TipoIdentificacion);
        r.NumeroIdentificacion = Preferir(p.NumeroIdentificacion, r.NumeroIdentificacion);
        r.FechaNacimiento = p.FechaNacimiento;
        r.Edad = p.Edad;
        r.CorreoElectronico = PreferirOpcional(p.CorreoElectronico, r.CorreoElectronico);
        r.Genero = Preferir(p.Genero, r.Genero);

        r.Direccion = PreferirOpcional(p.Direccion, r.Direccion);
        r.DireccionValidada = p.DireccionValidada;
        r.AsumirDireccionErrada = p.AsumirDireccionErrada;
        r.DetalleDireccion = PreferirOpcional(p.DetalleDireccion, r.DetalleDireccion);
        r.ClasificacionZonaSura = PreferirOpcional(p.ClasificacionZonaSura, r.ClasificacionZonaSura);
        r.MunicipioResidencia = PreferirOpcional(p.MunicipioResidencia, r.MunicipioResidencia);
        r.Barrio = PreferirOpcional(p.Barrio, r.Barrio);
        r.ZonaDireccionSegunMunicipio = PreferirOpcional(p.ZonaDireccionSegunMunicipio, r.ZonaDireccionSegunMunicipio);
        r.Area = PreferirOpcional(p.Area, r.Area);
        r.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task ReplicarClinicaHeridasAsync(CensoPaciente p, long registroId, CancellationToken ct)
    {
        var r = await _context.CensoClinicaHeridas.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null)
        {
            return;
        }

        r.CensoPacienteId = p.Id;

        r.NombrePaciente = Preferir(p.NombrePaciente, r.NombrePaciente);
        r.TipoIdentificacion = Preferir(p.TipoIdentificacion, r.TipoIdentificacion);
        r.NumeroIdentificacion = Preferir(p.NumeroIdentificacion, r.NumeroIdentificacion);
        r.FechaNacimiento = p.FechaNacimiento;
        r.Edad = p.Edad;
        // Clinica de heridas y NPT solo tienen Masculino y Femenino en su catalogo. Si el maestro
        // trae "Indeterminado" (que si existe en cronicos) no se replica, para no dejar el campo
        // con un valor que su propio formulario rechazaria.
        r.Genero = Preferir(GeneroBinario(p.Genero), r.Genero);
        r.Asegurador = Preferir(p.Asegurador, r.Asegurador);
        // Clínica de heridas tiene su propio catálogo de CIE10 (los diagnósticos de herida),
        // distinto del general. Su código y su descriptivo son del programa y no se tocan
        // desde el maestro: hacerlo dejaba un código que su propia validación rechaza.

        r.Direccion = PreferirOpcional(p.Direccion, r.Direccion);
        r.DireccionValidada = p.DireccionValidada;
        r.AsumirDireccionErrada = p.AsumirDireccionErrada;
        r.DetalleDireccion = PreferirOpcional(p.DetalleDireccion, r.DetalleDireccion);
        r.ClasificacionZonaSura = PreferirOpcional(p.ClasificacionZonaSura, r.ClasificacionZonaSura);
        r.MunicipioResidencia = PreferirOpcional(p.MunicipioResidencia, r.MunicipioResidencia);
        r.Barrio = PreferirOpcional(p.Barrio, r.Barrio);
        r.ZonaDireccionSegunMunicipio = PreferirOpcional(p.ZonaDireccionSegunMunicipio, r.ZonaDireccionSegunMunicipio);

        r.TelefonoPrincipal = Preferir(p.Telefono1, r.TelefonoPrincipal);
        r.TelefonoAdicional1 = Preferir(p.Telefono2, r.TelefonoAdicional1);
        r.TelefonoAdicional2 = PreferirOpcional(p.Telefono3, r.TelefonoAdicional2);
        r.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task ReplicarNptAsync(CensoPaciente p, long registroId, CancellationToken ct)
    {
        var r = await _context.CensoNpt.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null)
        {
            return;
        }

        r.CensoPacienteId = p.Id;

        r.NombrePaciente = Preferir(p.NombrePaciente, r.NombrePaciente);
        r.TipoIdentificacion = Preferir(p.TipoIdentificacion, r.TipoIdentificacion);
        r.NumeroIdentificacion = Preferir(p.NumeroIdentificacion, r.NumeroIdentificacion);
        r.FechaNacimiento = p.FechaNacimiento;
        r.Edad = p.Edad;
        // Clinica de heridas y NPT solo tienen Masculino y Femenino en su catalogo. Si el maestro
        // trae "Indeterminado" (que si existe en cronicos) no se replica, para no dejar el campo
        // con un valor que su propio formulario rechazaria.
        r.Genero = Preferir(GeneroBinario(p.Genero), r.Genero);
        r.Asegurador = Preferir(p.Asegurador, r.Asegurador);
        r.CodigoCie10 = Preferir(p.CodigoCie10, r.CodigoCie10);
        r.DiagnosticoDescriptivo = Preferir(p.DiagnosticoDescriptivo, r.DiagnosticoDescriptivo);

        r.Direccion = PreferirOpcional(p.Direccion, r.Direccion);
        r.DireccionValidada = p.DireccionValidada;
        r.AsumirDireccionErrada = p.AsumirDireccionErrada;
        r.ClasificacionZonaSura = PreferirOpcional(p.ClasificacionZonaSura, r.ClasificacionZonaSura);
        r.MunicipioResidencia = PreferirOpcional(p.MunicipioResidencia, r.MunicipioResidencia);
        r.Barrio = PreferirOpcional(p.Barrio, r.Barrio);
        r.ZonaDireccionSegunMunicipio = PreferirOpcional(p.ZonaDireccionSegunMunicipio, r.ZonaDireccionSegunMunicipio);

        r.TelefonoPrincipal = Preferir(p.Telefono1, r.TelefonoPrincipal);
        r.TelefonoAdicional1 = Preferir(p.Telefono2, r.TelefonoAdicional1);
        r.TelefonoAdicional2 = PreferirOpcional(p.Telefono3, r.TelefonoAdicional2);
        r.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task ReplicarTerapiaAsync(CensoPaciente p, long registroId, CancellationToken ct)
    {
        var r = await _context.CensoTerapiasAmbulatorias.FirstOrDefaultAsync(x => x.Id == registroId, ct);
        if (r is null)
        {
            return;
        }

        r.CensoPacienteId = p.Id;

        r.NombrePaciente = Preferir(p.NombrePaciente, r.NombrePaciente);
        r.TipoIdentificacion = Preferir(p.TipoIdentificacion, r.TipoIdentificacion);
        r.NumeroIdentificacion = Preferir(p.NumeroIdentificacion, r.NumeroIdentificacion);
        r.FechaNacimiento = p.FechaNacimiento;
        r.Edad = p.Edad;
        r.CorreoElectronico = Preferir(p.CorreoElectronico, r.CorreoElectronico);
        r.CodigoCie10 = Preferir(p.CodigoCie10, r.CodigoCie10);
        r.DiagnosticoDescriptivo = Preferir(p.DiagnosticoDescriptivo, r.DiagnosticoDescriptivo);

        r.Direccion = PreferirOpcional(p.Direccion, r.Direccion);
        r.DireccionValidada = p.DireccionValidada;
        r.AsumirDireccionErrada = p.AsumirDireccionErrada;
        r.DetalleDireccion = PreferirOpcional(p.DetalleDireccion, r.DetalleDireccion);
        r.ClasificacionZonaSura = PreferirOpcional(p.ClasificacionZonaSura, r.ClasificacionZonaSura);
        r.MunicipioResidencia = PreferirOpcional(p.MunicipioResidencia, r.MunicipioResidencia);
        r.Barrio = PreferirOpcional(p.Barrio, r.Barrio);
        r.ZonaDireccionSegunMunicipio = PreferirOpcional(p.ZonaDireccionSegunMunicipio, r.ZonaDireccionSegunMunicipio);
        r.Area = PreferirOpcional(p.Area, r.Area);
        r.IpsQueRemite = Preferir(p.IpsQueRemite, r.IpsQueRemite);

        r.TelefonoPrincipal = Preferir(p.Telefono1, r.TelefonoPrincipal);
        r.TelefonoAdicional1 = PreferirOpcional(p.Telefono2, r.TelefonoAdicional1);
        r.TelefonoAdicional2 = PreferirOpcional(p.Telefono3, r.TelefonoAdicional2);
        r.UpdatedAtUtc = DateTime.UtcNow;
    }

    // ==========================================================================================
    // Reconciliación de episodios
    //
    // Las pantallas de cada programa siguen guardando por su cuenta y no saben del maestro. En vez
    // de intervenir sus rutas de guardado —que son justo las que mueven kardex, requisiciones y
    // farmacia— la pantalla unificada reconcilia al abrirse: enlaza lo que falte y ajusta el estado
    // abierto/cerrado de cada episodio a partir del estado real del registro.
    //
    // Solo escribe en censo_paciente_programa y en la columna CensoPacienteId. Nunca toca un dato
    // clínico ni el estado del propio censo.
    // ==========================================================================================
    public async Task ReconciliarEpisodiosAsync(long pacienteId, CancellationToken cancellationToken)
    {
        var paciente = await _context.CensoPacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pacienteId, cancellationToken);
        if (paciente is null)
        {
            return;
        }

        var doc = NormalizarDocumento(paciente.NumeroIdentificacion);

        try
        {
            var episodios = await _context.CensoPacienteProgramas
                .Where(x => x.CensoPacienteId == pacienteId)
                .ToListAsync(cancellationToken);

            // --- Agudos: una atención por fila. Las copias internas de despacho a farmacia no son
            // atenciones, y se excluyen con el mismo criterio que usa la pantalla del censo.
            var agudos = await _context.Censos
                .Where(CensoVisibility.EditableRecord(_context))
                .Where(x => x.CensoPacienteId == pacienteId || x.NumeroIdentificacion.ToUpper() == doc)
                .Select(x => new { x.Id, x.Estado })
                .ToListAsync(cancellationToken);
            foreach (var fila in agudos)
            {
                Conciliar(episodios, pacienteId, CensoProgramas.Agudos, fila.Id, EsAgudoCerrado(fila.Estado), fila.Estado);
            }

            var cronicos = await _context.CensoCronicos
                .Where(x => x.CensoPacienteId == pacienteId || x.NumeroIdentificacion.ToUpper() == doc)
                .Select(x => new { x.Id, x.EstadoPaciente, x.FechaEgreso, x.MotivoEgreso })
                .ToListAsync(cancellationToken);
            foreach (var fila in cronicos)
            {
                var cerrado = fila.FechaEgreso.HasValue
                    || string.Equals(fila.EstadoPaciente, "Inactivo", StringComparison.OrdinalIgnoreCase);
                Conciliar(episodios, pacienteId, CensoProgramas.Cronicos, fila.Id, cerrado,
                    fila.MotivoEgreso ?? fila.EstadoPaciente);
            }

            var heridas = await _context.CensoClinicaHeridas
                .Where(x => x.CensoPacienteId == pacienteId || x.NumeroIdentificacion.ToUpper() == doc)
                .Select(x => new { x.Id, x.Estado, x.FechaEgreso, x.MotivoEgreso })
                .ToListAsync(cancellationToken);
            foreach (var fila in heridas)
            {
                Conciliar(episodios, pacienteId, CensoProgramas.ClinicaHeridas, fila.Id,
                    EsProgramaCerrado(fila.Estado, fila.FechaEgreso), fila.MotivoEgreso ?? fila.Estado);
            }

            var npt = await _context.CensoNpt
                .Where(x => x.CensoPacienteId == pacienteId || x.NumeroIdentificacion.ToUpper() == doc)
                .Select(x => new { x.Id, x.Estado, x.FechaEgreso, x.MotivoEgreso })
                .ToListAsync(cancellationToken);
            foreach (var fila in npt)
            {
                Conciliar(episodios, pacienteId, CensoProgramas.Npt, fila.Id,
                    EsProgramaCerrado(fila.Estado, fila.FechaEgreso), fila.MotivoEgreso ?? fila.Estado);
            }

            var terapia = await _context.CensoTerapiasAmbulatorias
                .Where(x => x.CensoPacienteId == pacienteId || x.NumeroIdentificacion.ToUpper() == doc)
                .Select(x => new { x.Id, x.EstadoPaciente, x.EstadoAlta, x.MotivoAlta })
                .ToListAsync(cancellationToken);
            foreach (var fila in terapia)
            {
                var cerrado = string.Equals(fila.EstadoAlta, "Cerrado", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(fila.EstadoPaciente, "Activo", StringComparison.OrdinalIgnoreCase);
                Conciliar(episodios, pacienteId, CensoProgramas.TerapiaAmbulatoria, fila.Id, cerrado,
                    fila.MotivoAlta ?? fila.EstadoPaciente);
            }

            await _context.SaveChangesAsync(cancellationToken);

            // El vínculo con el maestro se completa en una segunda pasada con actualizaciones
            // masivas: no carga las entidades y por eso no puede tocar ninguna otra columna.
            await VincularFilasSueltasAsync(pacienteId, doc, cancellationToken);
        }
        catch (Exception ex)
        {
            // Reconciliar es una comodidad, no un requisito para operar. Si falla, la pantalla se
            // muestra con lo que haya y los censos siguen intactos.
            _logger.LogError(ex, "No se pudieron reconciliar los episodios del paciente {PacienteId}.", pacienteId);
        }
    }

    /// <summary>
    /// Crea el episodio de una fila si no existe y sincroniza su estado abierto/cerrado con el del
    /// registro. Nunca borra episodios: serían trazabilidad perdida.
    /// </summary>
    private void Conciliar(
        List<CensoPacientePrograma> episodios,
        long pacienteId,
        string programa,
        long registroId,
        bool cerrado,
        string? motivo)
    {
        var episodio = episodios.FirstOrDefault(x =>
            string.Equals(x.Programa, programa, StringComparison.Ordinal) && x.RegistroId == registroId);

        if (episodio is null)
        {
            // Un episodio abierto sin registro es el que dejó el selector al agregar el programa: se
            // reutiliza en lugar de crear uno nuevo, para no duplicar el programa en el carril.
            episodio = episodios.FirstOrDefault(x =>
                string.Equals(x.Programa, programa, StringComparison.Ordinal)
                && x.RegistroId is null
                && x.CerradoAtUtc is null);

            if (episodio is null)
            {
                episodio = new CensoPacientePrograma
                {
                    CensoPacienteId = pacienteId,
                    Programa = programa,
                    AgregadoAtUtc = DateTime.UtcNow,
                    AgregadoPor = "Sincronización"
                };
                _context.CensoPacienteProgramas.Add(episodio);
                episodios.Add(episodio);
            }

            episodio.RegistroId = registroId;
        }

        if (cerrado && episodio.CerradoAtUtc is null)
        {
            episodio.CerradoAtUtc = DateTime.UtcNow;
            episodio.CerradoPor = "Sincronización";
            episodio.MotivoCierre = Recortar(motivo, 120);
        }
        else if (!cerrado && episodio.CerradoAtUtc is not null)
        {
            episodio.CerradoAtUtc = null;
            episodio.CerradoPor = null;
            episodio.MotivoCierre = null;
        }
    }

    private async Task VincularFilasSueltasAsync(long pacienteId, string doc, CancellationToken ct)
    {
        await _context.Censos
            .Where(x => x.CensoPacienteId == null && x.NumeroIdentificacion.ToUpper() == doc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CensoPacienteId, pacienteId), ct);
        await _context.CensoCronicos
            .Where(x => x.CensoPacienteId == null && x.NumeroIdentificacion.ToUpper() == doc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CensoPacienteId, pacienteId), ct);
        await _context.CensoClinicaHeridas
            .Where(x => x.CensoPacienteId == null && x.NumeroIdentificacion.ToUpper() == doc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CensoPacienteId, pacienteId), ct);
        await _context.CensoNpt
            .Where(x => x.CensoPacienteId == null && x.NumeroIdentificacion.ToUpper() == doc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CensoPacienteId, pacienteId), ct);
        await _context.CensoTerapiasAmbulatorias
            .Where(x => x.CensoPacienteId == null && x.NumeroIdentificacion.ToUpper() == doc)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CensoPacienteId, pacienteId), ct);
    }

    /// <summary>Mismo criterio de cierre que usa el censo de agudos: alta, cancelación o rechazo.</summary>
    private static bool EsAgudoCerrado(string? estado) =>
        !string.IsNullOrWhiteSpace(estado)
        && (estado.Contains("alta", StringComparison.OrdinalIgnoreCase)
            || estado.Contains("cancelado", StringComparison.OrdinalIgnoreCase)
            || estado.Contains("rechazado", StringComparison.OrdinalIgnoreCase));

    /// <summary>Clínica de heridas y NPT: se cierran con el egreso o con un estado distinto de activo.</summary>
    private static bool EsProgramaCerrado(string? estado, DateTime? fechaEgreso) =>
        fechaEgreso.HasValue
        || (!string.IsNullOrWhiteSpace(estado) && !string.Equals(estado, "Activo", StringComparison.OrdinalIgnoreCase));

    private static string? GeneroBinario(string? genero) =>
        string.Equals(genero, "Masculino", StringComparison.OrdinalIgnoreCase)
            || string.Equals(genero, "Femenino", StringComparison.OrdinalIgnoreCase)
            ? genero
            : null;

    /// <summary>Para columnas obligatorias: un valor vacío en el maestro nunca borra el actual.</summary>
    private static string Preferir(string? nuevo, string actual) =>
        string.IsNullOrWhiteSpace(nuevo) ? actual : nuevo.Trim();

    private static string? PreferirOpcional(string? nuevo, string? actual) =>
        string.IsNullOrWhiteSpace(nuevo) ? actual : nuevo.Trim();

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Recortar(string? valor, int maximo)
    {
        var limpio = Limpiar(valor);
        return limpio is null || limpio.Length <= maximo ? limpio : limpio[..maximo];
    }
}
