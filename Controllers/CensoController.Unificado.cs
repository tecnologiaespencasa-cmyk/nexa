using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Helpers;
using Nexa.Data.Entities;
using Nexa.Models.Security;
using Nexa.Models.ViewModels;
using Nexa.Services.Models;

namespace Nexa.Controllers;

/// <summary>
/// Pantalla única de censo: recepción y datos básicos del paciente una sola vez, y desde ahí los
/// programas que le correspondan.
///
/// Los formularios de cada programa siguen enviando a sus propias acciones (ProgramaAgudos,
/// ProgramaCronicos, ClinicaHeridas, Npt, TerapiaAmbulatoria) sin ningún cambio: esta pantalla los
/// hospeda, no los reemplaza. Por eso los kardex, las requisiciones, la bandeja de farmacia, las
/// prórrogas y el puente de Supabase siguen funcionando igual.
/// </summary>
public partial class CensoController
{
    private static readonly string[] GeneroValues = ["Masculino", "Femenino", "Indeterminado"];

    /// <summary>Programas que generan kardex: solo ellos exigen "quien realiza kardex".</summary>
    private static readonly string[] ProgramasConKardex =
    [
        CensoProgramas.Agudos,
        CensoProgramas.Cronicos,
        CensoProgramas.ClinicaHeridas
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        string? cedulaPaciente,
        string? programa,
        string? seccion,
        // Episodio que se quiere ver. Sin él, cada programa abre la atención que corresponde:
        // la que tenga abierta y, si no tiene ninguna, la última que se cerró.
        long? atencion,
        CancellationToken cancellationToken)
    {
        var model = await ConstruirModeloUnificadoAsync(
            cedulaPaciente, programa, cancellationToken, atencionSolicitada: atencion);
        ViewData["SeccionInicial"] = seccion;
        return View(model);
    }

    /// <summary>
    /// Cuenta los registros que ya tiene un documento dentro de un programa. La pantalla lo consulta
    /// antes de crear uno nuevo, para avisar que el paciente ya existe y pedir confirmación.
    ///
    /// A propósito no bloquea: un paciente puede reingresar y tener varias atenciones en el mismo
    /// programa, así que la decisión es de quien registra. Aquí solo se le da el dato.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VerificarPacienteEnPrograma(
        string? documento,
        string? programa,
        CancellationToken cancellationToken)
    {
        var doc = NormalizeCedulaFilter(documento);
        if (string.IsNullOrWhiteSpace(doc) || !CensoProgramas.EsValido(programa))
        {
            return Json(new { existe = false });
        }

        int total;
        int abiertos;
        string? nombre;

        switch (programa)
        {
            case CensoProgramas.Agudos:
            {
                var q = _context.Censos.AsNoTracking()
                    .Where(CensoVisibility.EditableRecord(_context))
                    .Where(x => x.NumeroIdentificacion == doc);
                total = await q.CountAsync(cancellationToken);
                abiertos = await q.CountAsync(x => x.Estado != null && (
                    EF.Functions.ILike(x.Estado, "Aceptado activo")
                    || EF.Functions.ILike(x.Estado, "Aceptado cronico")
                    || EF.Functions.ILike(x.Estado, "Aceptado crónico")
                    || EF.Functions.ILike(x.Estado, "Activo Estancia prolongada")
                    || EF.Functions.ILike(x.Estado, "Aceptado estancia prolongada")), cancellationToken);
                nombre = await q.OrderByDescending(x => x.Id).Select(x => x.NombrePaciente).FirstOrDefaultAsync(cancellationToken);
                break;
            }
            case CensoProgramas.Cronicos:
            {
                var q = _context.CensoCronicos.AsNoTracking().Where(x => x.NumeroIdentificacion == doc);
                total = await q.CountAsync(cancellationToken);
                abiertos = await q.CountAsync(x => x.FechaEgreso == null
                    && (x.EstadoPaciente == null || !EF.Functions.ILike(x.EstadoPaciente, "Inactivo")), cancellationToken);
                nombre = await q.OrderByDescending(x => x.Id).Select(x => x.NombrePaciente).FirstOrDefaultAsync(cancellationToken);
                break;
            }
            case CensoProgramas.ClinicaHeridas:
            {
                var q = _context.CensoClinicaHeridas.AsNoTracking().Where(x => x.NumeroIdentificacion == doc);
                total = await q.CountAsync(cancellationToken);
                abiertos = await q.CountAsync(x => x.FechaEgreso == null
                    && x.Estado != null && EF.Functions.ILike(x.Estado, "Activo"), cancellationToken);
                nombre = await q.OrderByDescending(x => x.Id).Select(x => x.NombrePaciente).FirstOrDefaultAsync(cancellationToken);
                break;
            }
            case CensoProgramas.Npt:
            {
                var q = _context.CensoNpt.AsNoTracking().Where(x => x.NumeroIdentificacion == doc);
                total = await q.CountAsync(cancellationToken);
                abiertos = await q.CountAsync(x => x.FechaEgreso == null
                    && x.Estado != null && EF.Functions.ILike(x.Estado, "Activo"), cancellationToken);
                nombre = await q.OrderByDescending(x => x.Id).Select(x => x.NombrePaciente).FirstOrDefaultAsync(cancellationToken);
                break;
            }
            default:
            {
                var q = _context.CensoTerapiasAmbulatorias.AsNoTracking().Where(x => x.NumeroIdentificacion == doc);
                total = await q.CountAsync(cancellationToken);
                abiertos = await q.CountAsync(x => EF.Functions.ILike(x.EstadoPaciente, "Activo")
                    && !EF.Functions.ILike(x.EstadoAlta, "Cerrado"), cancellationToken);
                nombre = await q.OrderByDescending(x => x.Id).Select(x => x.NombrePaciente).FirstOrDefaultAsync(cancellationToken);
                break;
            }
        }

        return Json(new
        {
            existe = total > 0,
            total,
            abiertos,
            nombre,
            programa = CensoProgramas.Nombre(programa)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarPaciente(
        CensoPacienteFormViewModel paciente,
        CancellationToken cancellationToken)
    {
        TraducirErroresDeFormato();
        NormalizarPaciente(paciente);

        // La dirección se valida con el mismo servicio que usa el censo de agudos, para que el
        // maestro y los programas apliquen exactamente el mismo criterio.
        if (!string.IsNullOrWhiteSpace(paciente.Direccion))
        {
            var validacion = await _addressValidationService.ValidateAddressAsync(
                paciente.Direccion,
                cancellationToken);
            paciente.DireccionEsValida = validacion.Outcome == AddressValidationOutcome.Valid;
            paciente.DireccionSugerida = validacion.SuggestedAddress;
            paciente.DireccionMensajeValidacion = validacion.Message;

            // La dirección se valida aquí con el mismo rigor que la exigen los censos de programa.
            // Si el maestro la aceptara sin validar, cada programa la rechazaría después al guardar
            // y el usuario vería el error en un formulario donde el campo ni siquiera se muestra.
            if (!paciente.DireccionEsValida
                && !paciente.AsumirDireccionErrada
                && validacion.Outcome != AddressValidationOutcome.Unavailable)
            {
                var sugerencia = string.IsNullOrWhiteSpace(validacion.SuggestedAddress)
                    ? string.Empty
                    : $" Sugerencia: {validacion.SuggestedAddress}.";
                ModelState.AddModelError(
                    nameof(CensoUnificadoViewModel.Paciente) + "." + nameof(paciente.Direccion),
                    $"{validacion.Message}{sugerencia} Corrige la dirección o marca \"Asumir dirección errada y continuar\".");
            }
        }
        else
        {
            paciente.DireccionEsValida = false;
        }

        ValidarUbicacionResuelta(paciente);

        await ValidarObligatoriosPorProgramaAsync(paciente, cancellationToken);

        if (!ModelState.IsValid)
        {
            var invalido = await ConstruirModeloUnificadoAsync(
                paciente.NumeroIdentificacion,
                null,
                cancellationToken,
                paciente);
            return View("Index", invalido);
        }

        var resultado = await _censoPacienteService.GuardarAsync(paciente, UsuarioActual(), cancellationToken);
        if (!resultado.Succeeded)
        {
            ModelState.AddModelError(string.Empty, resultado.ErrorMessage ?? "No se pudo guardar el paciente.");
            var invalido = await ConstruirModeloUnificadoAsync(
                paciente.NumeroIdentificacion,
                null,
                cancellationToken,
                paciente);
            return View("Index", invalido);
        }

        await _auditService.LogAsync(
            paciente.PacienteId.HasValue ? "CENSO_PACIENTE_ACTUALIZADO" : "CENSO_PACIENTE_CREADO",
            "CensoPaciente",
            $"Paciente: {resultado.Value!.NombrePaciente}, Doc: {resultado.Value.NumeroIdentificacion}",
            UsuarioActualId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        TempData["SuccessMessage"] = "Datos del paciente guardados. Selecciona los programas que le corresponden.";
        return RedirectToAction(nameof(Index), new { cedulaPaciente = resultado.Value.NumeroIdentificacion });
    }

    /// <summary>
    /// Guarda la recepción de un ingreso. Es propia del episodio: cada ingreso nace de su propio
    /// correo, así que el paciente conserva sus datos básicos y la recepción se captura de nuevo.
    ///
    /// No entra sobre una atención cerrada. La recepción es el arranque de la atención, no algo
    /// que se registre después del alta, así que no está entre las secciones que
    /// <see cref="CensoProgramaSecciones.SeEditaTrasElAlta"/> deja abiertas.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarRecepcion(
        CensoRecepcionFormViewModel recepcion,
        CancellationToken cancellationToken)
    {
        TraducirErroresDeFormato();

        var episodio = await _context.CensoPacienteProgramas
            .Include(x => x.CensoPaciente)
            .FirstOrDefaultAsync(x => x.Id == recepcion.EpisodioId, cancellationToken);

        if (episodio is null)
        {
            TempData["ErrorMessage"] = "No se encontró el ingreso.";
            return RedirectToAction(nameof(Index), new { cedulaPaciente = recepcion.CedulaPaciente });
        }

        var documento = episodio.CensoPaciente.NumeroIdentificacion;

        if (episodio.CerradoAtUtc is not null)
        {
            TempData["ErrorMessage"] =
                "Esa atención ya está cerrada: su recepción se conserva tal como se registró.";
            return RedirectToAction(nameof(Index), new
            {
                cedulaPaciente = documento,
                programa = episodio.Programa,
                atencion = episodio.Id
            });
        }

        recepcion.Programa = episodio.Programa;
        recepcion.NombreRecepcionaCaso = recepcion.NombreRecepcionaCaso?.Trim();
        recepcion.NombreRealizaKardex = recepcion.NombreRealizaKardex?.Trim();

        // Quien realiza el kardex solo se exige si ESTE programa lo genera. Antes la regla miraba
        // todos los programas abiertos del paciente, porque el campo era del paciente y no del
        // ingreso: a un ingreso de terapia se le pedía el kardex de una clínica de heridas ajena.
        if (ProgramasConKardex.Contains(episodio.Programa, StringComparer.Ordinal)
            && string.IsNullOrWhiteSpace(recepcion.NombreRealizaKardex))
        {
            ModelState.AddModelError(
                nameof(recepcion.NombreRealizaKardex),
                $"{CensoProgramas.Nombre(episodio.Programa)} genera kardex: selecciona quien lo realiza.");
        }

        var auxiliares = await GetNursingAssistantOptionsAsync(cancellationToken);
        var permitidos = auxiliares.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Un auxiliar que ya se inactivó sigue siendo válido si es el que está guardado: lo
        // contrario obligaría a cambiar el responsable de un ingreso pasado para poder corregir
        // cualquier otro campo de la misma recepción.
        if (!string.IsNullOrWhiteSpace(recepcion.NombreRecepcionaCaso)
            && !permitidos.Contains(recepcion.NombreRecepcionaCaso)
            && !string.Equals(recepcion.NombreRecepcionaCaso, episodio.NombreRecepcionaCaso, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(recepcion.NombreRecepcionaCaso), "Selecciona un auxiliar válido.");
        }

        if (!string.IsNullOrWhiteSpace(recepcion.NombreRealizaKardex)
            && !permitidos.Contains(recepcion.NombreRealizaKardex)
            && !string.Equals(recepcion.NombreRealizaKardex, episodio.NombreRealizaKardex, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(recepcion.NombreRealizaKardex), "Selecciona un auxiliar válido.");
        }

        int? indicador = null;
        if (recepcion.FechaIngreso.HasValue && recepcion.HoraIngreso.HasValue
            && recepcion.FechaRespuesta.HasValue && recepcion.HoraRespuesta.HasValue)
        {
            var ingreso = recepcion.FechaIngreso.Value.Date + recepcion.HoraIngreso.Value;
            var respuesta = recepcion.FechaRespuesta.Value.Date + recepcion.HoraRespuesta.Value;
            if (respuesta < ingreso)
            {
                ModelState.AddModelError(
                    nameof(recepcion.HoraRespuesta),
                    "La fecha/hora de respuesta no puede ser menor a la de ingreso.");
            }
            else
            {
                indicador = (int)Math.Round(
                    (respuesta - ingreso).TotalMinutes, MidpointRounding.AwayFromZero);
            }
        }

        if (!ModelState.IsValid)
        {
            var invalido = await ConstruirModeloUnificadoAsync(documento, episodio.Programa, cancellationToken);
            invalido.RecepcionEnviada = recepcion;
            return View("Index", invalido);
        }

        episodio.FechaIngreso = recepcion.FechaIngreso?.Date;
        episodio.HoraIngreso = recepcion.HoraIngreso;
        episodio.FechaRespuesta = recepcion.FechaRespuesta?.Date;
        episodio.HoraRespuesta = recepcion.HoraRespuesta;
        episodio.IndicadorTiempoRespuestaMinutos = indicador;
        episodio.NombreRecepcionaCaso = string.IsNullOrWhiteSpace(recepcion.NombreRecepcionaCaso)
            ? null : recepcion.NombreRecepcionaCaso;
        episodio.NombreRealizaKardex = string.IsNullOrWhiteSpace(recepcion.NombreRealizaKardex)
            ? null : recepcion.NombreRealizaKardex;

        await _censoPacienteService.ReplicarRecepcionAlProgramaAsync(episodio, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            "CENSO_RECEPCION_GUARDADA",
            "CensoPacientePrograma",
            $"Doc: {documento}, Programa: {CensoProgramas.Nombre(episodio.Programa)}, Ingreso: {episodio.Id}",
            UsuarioActualId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        TempData["SuccessMessage"] = "Recepción del ingreso guardada.";
        return RedirectToAction(nameof(Index), new
        {
            cedulaPaciente = documento,
            programa = episodio.Programa,
            atencion = episodio.Id
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarPrograma(
        long pacienteId,
        string programa,
        CancellationToken cancellationToken)
    {
        var paciente = await _censoPacienteService.ObtenerAsync(pacienteId, cancellationToken);
        if (paciente is null)
        {
            TempData["ErrorMessage"] = "No se encontró el paciente.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = await _censoPacienteService.AgregarProgramaAsync(
            pacienteId,
            programa,
            UsuarioActual(),
            cancellationToken);

        if (!resultado.Succeeded)
        {
            TempData["ErrorMessage"] = resultado.ErrorMessage;
            return RedirectToAction(nameof(Index), new { cedulaPaciente = paciente.NumeroIdentificacion });
        }

        await _auditService.LogAsync(
            "CENSO_PACIENTE_PROGRAMA_AGREGADO",
            "CensoPaciente",
            $"Doc: {paciente.NumeroIdentificacion}, Programa: {CensoProgramas.Nombre(programa)}",
            UsuarioActualId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        // Clinica de heridas, cronicos y NPT los atiende programas especiales: se les avisa por
        // correo en cuanto el paciente entra. El aviso nunca puede tumbar el guardado, asi que si
        // el correo falla se dice en pantalla y el programa queda agregado igual.
        var avisoCorreo = await _censoProgramaNotificationService.NotificarProgramaAgregadoAsync(
            paciente, programa, UsuarioActual(), cancellationToken);

        TempData["SuccessMessage"] = $"{CensoProgramas.Nombre(programa)} agregado al paciente.";
        if (!string.IsNullOrWhiteSpace(avisoCorreo))
        {
            TempData["ErrorMessage"] = avisoCorreo;
        }

        return RedirectToAction(nameof(Index), new
        {
            cedulaPaciente = paciente.NumeroIdentificacion,
            programa
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuitarPrograma(
        long episodioId,
        string? cedulaPaciente,
        CancellationToken cancellationToken)
    {
        var resultado = await _censoPacienteService.QuitarProgramaAsync(
            episodioId,
            UsuarioActual(),
            cancellationToken);

        if (resultado.Succeeded)
        {
            TempData["SuccessMessage"] = "Programa retirado del paciente.";
        }
        else
        {
            TempData["ErrorMessage"] = resultado.ErrorMessage;
        }

        return RedirectToAction(nameof(Index), new { cedulaPaciente });
    }

    // ==========================================================================================
    // Construcción del modelo
    // ==========================================================================================
    private async Task<CensoUnificadoViewModel> ConstruirModeloUnificadoAsync(
        string? cedulaPaciente,
        string? programaSolicitado,
        CancellationToken cancellationToken,
        CensoPacienteFormViewModel? formularioEnCurso = null,
        long? atencionSolicitada = null)
    {
        var documento = NormalizeCedulaFilter(cedulaPaciente);
        var ahora = GetColombiaNow();

        var model = new CensoUnificadoViewModel
        {
            CedulaFiltro = documento,
            Paciente = formularioEnCurso ?? NuevoFormularioPaciente(ahora)
        };

        CensoPaciente? paciente = null;
        if (!string.IsNullOrWhiteSpace(documento))
        {
            paciente = await _censoPacienteService.BuscarPorDocumentoAsync(documento, cancellationToken);

            // Si el documento no tiene maestro pero sí filas en algún censo, se unifica en el acto.
            // Cubre a los pacientes que se crearon por fuera de esta pantalla y evita que queden
            // invisibles aquí hasta que alguien los migre a mano.
            paciente ??= await _censoPacienteService.AsegurarMaestroAsync(documento, cancellationToken);
        }

        if (paciente is not null && formularioEnCurso is null)
        {
            model.Paciente = AFormulario(paciente);
        }
        else if (paciente is not null && formularioEnCurso is not null)
        {
            model.Paciente.PacienteId = paciente.Id;
        }

        if (paciente is not null)
        {
            // Las pantallas de cada programa guardan por su cuenta y no conocen el maestro. Antes de
            // pintar el carril se pone al día: enlaza las filas que todavía no tienen episodio y
            // ajusta el estado abierto/cerrado al del registro real.
            await _censoPacienteService.ReconciliarEpisodiosAsync(paciente.Id, cancellationToken);
        }

        var episodios = paciente is null
            ? []
            : await _censoPacienteService.ObtenerProgramasAsync(paciente.Id, cancellationToken);

        model.Programas = paciente is null ? ConstruirChipsVacios() : ConstruirChips(episodios);

        // Todas las atenciones del paciente, abiertas y cerradas, con la seleccionada ya marcada.
        // El carril sigue mostrando solo las abiertas —agregar un programa no es lo mismo que
        // consultar su historia— pero los paneles se arman desde aquí.
        model.Atenciones = paciente is null
            ? new Dictionary<string, IReadOnlyList<CensoAtencionViewModel>>(StringComparer.Ordinal)
            : await ConstruirAtencionesAsync(episodios, atencionSolicitada, cancellationToken);

        // Se buscó y no hubo paciente. Distinto de entrar sin buscar: eso no se avisa.
        model.BusquedaSinResultados = paciente is null && !string.IsNullOrWhiteSpace(model.CedulaFiltro);

        model.PuedeReabrirAtencion = await _currentUserPermissionService.HasPermissionAsync(
            User, SystemPermissions.Aprobacion, cancellationToken);

        var abiertos = model.Programas.Where(x => x.Agregado).Select(x => x.Programa).ToList();

        // Programas con panel: los abiertos y, además, aquellos cuya última atención está cerrada
        // pero sigue siendo consultable.
        var conPanel = model.Atenciones
            .Where(par => par.Value.Any(a => a.EsSeleccionada))
            .Select(par => par.Key)
            .ToList();

        model.ProgramasEnSoloLectura = model.Atenciones
            .Where(par => par.Value.Any(a => a.EsSeleccionada && !a.Abierta))
            .Select(par => par.Key)
            .ToHashSet(StringComparer.Ordinal);

        // Combinaciones que hoy ya no se pueden crear pero que existen de antes: se avisan sin
        // estorbar, igual que se hacía con agudos y crónicos. Nunca se cierra nada por cuenta
        // propia: son atenciones reales en curso.
        model.ProgramasEnConflicto = abiertos
            .Where(programa => abiertos.Any(otro => CensoProgramas.SonExcluyentes(programa, otro)))
            .Select(CensoProgramas.Nombre)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        // Abre el programa pedido si tiene panel; si no, el abierto de mayor jerarquía, y solo
        // cuando el paciente no tiene ninguno abierto se cae en el que quedó en consulta.
        model.ProgramaActivo = CensoProgramas.EsValido(programaSolicitado) && conPanel.Contains(programaSolicitado!)
            ? programaSolicitado
            : abiertos.OrderBy(CensoProgramas.Jerarquia).FirstOrDefault()
                ?? conPanel.OrderBy(CensoProgramas.Jerarquia).FirstOrDefault();

        await ConstruirModelosDeProgramaAsync(model, paciente, cancellationToken);
        await PoblarCatalogosUnificadosAsync(model, cancellationToken);
        return model;
    }

    /// <summary>
    /// Devuelve la pantalla unica con el formulario del programa tal como quedo, conservando los
    /// errores de validacion. Es lo que rendericen los guardados de cada censo cuando el modelo no
    /// pasa: antes cada uno volvia a su pantalla suelta, que ya no se usa.
    /// </summary>
    private async Task<IActionResult> VistaUnificadaConProgramaAsync(
        string programa,
        object submodelo,
        string? documento,
        CancellationToken cancellationToken)
    {
        var model = await ConstruirModeloUnificadoAsync(documento, programa, cancellationToken);

        switch (programa)
        {
            case CensoProgramas.Agudos:
                model.Agudos = (CensoReceptionViewModel)submodelo;
                break;
            case CensoProgramas.Cronicos:
                model.Cronicos = (CensoCronicoViewModel)submodelo;
                break;
            case CensoProgramas.ClinicaHeridas:
                model.ClinicaHeridas = (CensoClinicaHeridasViewModel)submodelo;
                break;
            case CensoProgramas.Npt:
                model.Npt = (CensoNptViewModel)submodelo;
                break;
            case CensoProgramas.TerapiaAmbulatoria:
                model.TerapiaAmbulatoria = (CensoTerapiaAmbulatoriaViewModel)submodelo;
                break;
        }

        return View("Index", model);
    }

    /// <summary>
    /// Arma la lista de atenciones de cada programa y marca cuál queda seleccionada.
    ///
    /// Las fechas no salen del episodio sino del registro de cada censo: el episodio guarda cuándo
    /// se agregó el programa, que en los sembrados por la migración inicial es la fecha en que se
    /// creó la fila, y puede diferir en días del ingreso real. Como el rótulo del selector es lo
    /// que la persona compara con lo que ve en pantalla, se leen las fechas verdaderas.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<CensoAtencionViewModel>>> ConstruirAtencionesAsync(
        IReadOnlyList<CensoPacientePrograma> episodios,
        long? atencionSolicitada,
        CancellationToken ct)
    {
        var resultado = new Dictionary<string, IReadOnlyList<CensoAtencionViewModel>>(StringComparer.Ordinal);
        if (episodios.Count == 0)
        {
            return resultado;
        }

        // Una consulta por programa, solo con las columnas del rótulo y solo para los registros
        // que algún episodio referencia.
        var datos = new Dictionary<(string Programa, long RegistroId), (DateTime? Desde, DateTime? Hasta, string? Estado)>();

        async Task CargarAsync<T>(
            string programa,
            IQueryable<T> origen,
            Func<T, long> id,
            Func<T, DateTime?> desde,
            Func<T, DateTime?> hasta,
            Func<T, string?> estado) where T : class
        {
            var ids = episodios
                .Where(e => e.Programa == programa && e.RegistroId.HasValue)
                .Select(e => e.RegistroId!.Value)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
            {
                return;
            }

            foreach (var fila in await origen.AsNoTracking()
                         .Where(x => ids.Contains(EF.Property<long>(x, "Id")))
                         .ToListAsync(ct))
            {
                datos[(programa, id(fila))] = (desde(fila), hasta(fila), estado(fila));
            }
        }

        await CargarAsync(CensoProgramas.Agudos, _context.Censos,
            x => x.Id, x => x.FechaIngreso, x => x.FechaAlta, x => x.Estado);
        await CargarAsync(CensoProgramas.Cronicos, _context.CensoCronicos,
            x => x.Id, x => x.FechaIngreso,
            x => CensoVisibility.HayEgreso(x.FechaEgreso) ? x.FechaEgreso : null,
            x => x.MotivoEgreso ?? x.EstadoPaciente);
        await CargarAsync(CensoProgramas.ClinicaHeridas, _context.CensoClinicaHeridas,
            x => x.Id, x => x.FechaIngresoPrograma,
            x => CensoVisibility.HayEgreso(x.FechaEgreso) ? x.FechaEgreso : null,
            x => x.MotivoEgreso ?? x.Estado);
        await CargarAsync(CensoProgramas.Npt, _context.CensoNpt,
            x => x.Id, x => x.FechaIngresoPrograma,
            x => CensoVisibility.HayEgreso(x.FechaEgreso) ? x.FechaEgreso : null,
            x => x.MotivoEgreso ?? x.Estado);
        await CargarAsync(CensoProgramas.TerapiaAmbulatoria, _context.CensoTerapiasAmbulatorias,
            x => x.Id, x => x.FechaInicio, x => x.FechaAlta,
            x => x.MotivoAlta ?? x.EstadoPaciente);

        foreach (var programa in CensoProgramas.Todos)
        {
            var delPrograma = episodios
                .Where(e => string.Equals(e.Programa, programa, StringComparison.Ordinal))
                .ToList();
            if (delPrograma.Count == 0)
            {
                continue;
            }

            var atenciones = delPrograma
                .Select(e =>
                {
                    var fila = e.RegistroId.HasValue
                        && datos.TryGetValue((programa, e.RegistroId.Value), out var d)
                            ? d
                            : (Desde: (DateTime?)null, Hasta: (DateTime?)null, Estado: (string?)null);

                    return new CensoAtencionViewModel
                    {
                        EpisodioId = e.Id,
                        Programa = programa,
                        RegistroId = e.RegistroId,
                        // Sin registro todavía, la fecha que hay es la del día en que se agregó.
                        Desde = fila.Desde ?? ColombiaTime.Convert(e.AgregadoAtUtc).Date,
                        Hasta = fila.Hasta,
                        Abierta = e.CerradoAtUtc is null,
                        Estado = fila.Estado ?? e.MotivoCierre,
                        // La recepción de este ingreso viaja con la atención, también en las
                        // anteriores: es lo que deja consultarlas sin volver a la base.
                        FechaIngresoRecepcion = e.FechaIngreso,
                        HoraIngresoRecepcion = e.HoraIngreso,
                        FechaRespuesta = e.FechaRespuesta,
                        HoraRespuesta = e.HoraRespuesta,
                        IndicadorTiempoRespuestaMinutos = e.IndicadorTiempoRespuestaMinutos,
                        NombreRecepcionaCaso = e.NombreRecepcionaCaso,
                        NombreRealizaKardex = e.NombreRealizaKardex
                    };
                })
                .OrderBy(a => a.Desde)
                .ThenBy(a => a.EpisodioId)
                .ToList();

            for (var i = 0; i < atenciones.Count; i++)
            {
                atenciones[i].Numero = i + 1;
            }

            // La seleccionada: la pedida si es de este programa; si no, la abierta; y si el
            // programa no tiene ninguna abierta, la última que se cerró, que es lo que deja
            // seguir registrando lo que ocurre después del alta.
            var seleccionada = atenciones.FirstOrDefault(a => a.EpisodioId == atencionSolicitada)
                ?? atenciones.FirstOrDefault(a => a.Abierta)
                ?? atenciones[^1];
            seleccionada.EsSeleccionada = true;

            resultado[programa] = atenciones;
        }

        return resultado;
    }

    /// <summary>
    /// Sobre una atención de agudos ya cerrada deja entrar únicamente los campos de las secciones
    /// posteriores al alta y devuelve todos los demás al valor que tienen guardado.
    ///
    /// Los otros cuatro programas guardan sección por sección, así que allí el filtro
    /// <see cref="Nexa.Filters.CensoAtencionCerradaFilter"/> puede rechazar la acción entera.
    /// Agudos manda su formulario completo en un solo POST: rechazarlo dejaría sin registrar la
    /// devolución de productos y los seguimientos, que es justo lo que hay que poder hacer después
    /// del alta. Se resuelve campo por campo, y quien decide qué es un cambio no es el modelo
    /// enviado sino el registro de cambios de EF, que compara contra lo que hay en la base.
    ///
    /// Devuelve true si tuvo que revertir algo, para poder decírselo a quien guardó.
    /// </summary>
    private async Task<bool> RevertirCamposBloqueadosDeAgudosAsync(
        CensoRecord registro,
        CancellationToken ct)
    {
        var cerrada = await _context.CensoPacienteProgramas
            .AsNoTracking()
            .AnyAsync(e => e.Programa == CensoProgramas.Agudos
                    && e.RegistroId == registro.Id
                    && e.CerradoAtUtc != null,
                ct);
        if (!cerrada)
        {
            return false;
        }

        var revertidos = 0;
        foreach (var propiedad in _context.Entry(registro).Properties)
        {
            if (!propiedad.IsModified
                || CensoProgramaSecciones.CamposDeAgudosTrasElAlta.Contains(propiedad.Metadata.Name))
            {
                continue;
            }

            propiedad.CurrentValue = propiedad.OriginalValue;
            propiedad.IsModified = false;
            revertidos++;
        }

        return revertidos > 0;
    }

    /// <summary>
    /// Construye el formulario de cada programa que el paciente tiene abierto, reutilizando los
    /// mismos constructores de modelo que usaban las pantallas independientes. Así los formularios
    /// que se muestran dentro de la pantalla unificada son idénticos a los de siempre y siguen
    /// enviando a sus propias acciones: kardex, requisiciones, farmacia y prórrogas no cambian.
    /// </summary>
    private async Task ConstruirModelosDeProgramaAsync(
        CensoUnificadoViewModel model,
        CensoPaciente? paciente,
        CancellationToken ct)
    {
        if (paciente is null)
        {
            return;
        }

        var doc = paciente.NumeroIdentificacion;

        // El panel se arma para la atención seleccionada de cada programa, esté abierta o
        // cerrada. Antes solo se armaba para los episodios abiertos, y por eso una atención dada
        // de alta quedaba fuera de alcance: no había forma de volver a ella ni para consultarla
        // ni para registrar lo que ocurre después del alta.
        var seleccionadas = model.Atenciones
            .Select(par => par.Value.FirstOrDefault(a => a.EsSeleccionada))
            .Where(a => a is not null)
            .ToDictionary(a => a!.Programa, a => a!, StringComparer.Ordinal);

        if (seleccionadas.TryGetValue(CensoProgramas.Agudos, out var atencionAgudos))
        {
            // La recepción sale de la atención, no del paciente. Un ingreso nuevo todavía no la
            // tiene: el formulario arranca con la fecha y hora de hoy, que es lo que hacía el
            // formulario en blanco, y no con la del ingreso anterior.
            var ahora = ColombiaTime.Convert(DateTime.UtcNow);
            var agudos = new CensoReceptionViewModel
            {
                CedulaFiltro = doc,
                FechaIngreso = atencionAgudos.FechaIngresoRecepcion ?? ahora.Date,
                HoraIngreso = atencionAgudos.HoraIngresoRecepcion
                    ?? new TimeSpan(ahora.Hour, ahora.Minute, 0),
                FechaRespuesta = atencionAgudos.FechaRespuesta
                    ?? atencionAgudos.FechaIngresoRecepcion ?? ahora.Date,
                HoraRespuesta = atencionAgudos.HoraRespuesta
                    ?? atencionAgudos.HoraIngresoRecepcion ?? new TimeSpan(ahora.Hour, ahora.Minute, 0),
                FechaNacimiento = paciente.FechaNacimiento,
                Edad = paciente.Edad
            };
            await PopulateCensoListAndLatestRecordAsync(
                agudos,
                ct,
                loadLatestRecordIntoForm: true,
                selectedRecordId: atencionAgudos.RegistroId,
                // El episodio manda: si no tiene registro, la atención es nueva y el formulario
                // arranca en blanco, como en los otros cuatro programas.
                permitirUltimaAtencion: false);
            await PopulateDropdownsAsync(agudos, ct);
            PreserveInactiveNursingAssistantSelections(agudos);
            AplicarMaestroAModeloAgudos(agudos, paciente);
            AplicarRecepcionAModeloAgudos(agudos, atencionAgudos);
            model.Agudos = agudos;
        }

        if (seleccionadas.TryGetValue(CensoProgramas.Cronicos, out var atencionCronicos))
        {
            var cronicos = BuildDefaultCronicoModel();
            cronicos.CedulaFiltro = doc;
            var registro = await BuscarRegistroAsync(_context.CensoCronicos, atencionCronicos.RegistroId, ct);
            if (registro is not null)
            {
                ApplyCronicoRecordToModel(cronicos, registro);
                cronicos.CedulaFiltro = registro.NumeroIdentificacion;
            }
            await PopulateCronicoDropdownsAsync(cronicos, ct);
            AplicarMaestroAModeloCronicos(cronicos, paciente);
            model.Cronicos = cronicos;
        }

        if (seleccionadas.TryGetValue(CensoProgramas.ClinicaHeridas, out var atencionHeridas))
        {
            var heridas = BuildDefaultClinicaHeridasModel();
            heridas.CedulaFiltro = doc;
            var registro = await BuscarRegistroAsync(_context.CensoClinicaHeridas, atencionHeridas.RegistroId, ct);
            if (registro is not null)
            {
                ApplyClinicaHeridasRecordToModel(heridas, registro);
                heridas.CedulaFiltro = registro.NumeroIdentificacion;
            }
            await PopulateClinicaHeridasDropdownsAsync(heridas, ct);
            await PopulateClinicaHeridasLatestRecordsAsync(heridas, ct);
            AplicarMaestroAModeloHeridas(heridas, paciente);
            // Solo al pintar el formulario: si el guardado falla, la vista se rearma con el modelo
            // enviado y estas propuestas no llegan a pisar lo que la persona eligió. Y solo en una
            // atención abierta: proponer valores en una sección cerrada mostraría como diligenciado
            // algo que nadie diligenció y que ya no se puede guardar.
            if (!model.EsSoloLectura(CensoProgramas.ClinicaHeridas))
            {
                ProponerDispositivosSinDiligenciar(heridas);
            }
            model.ClinicaHeridas = heridas;
        }

        if (seleccionadas.TryGetValue(CensoProgramas.Npt, out var atencionNpt))
        {
            var npt = BuildDefaultNptModel();
            npt.CedulaFiltro = doc;
            var registro = await BuscarRegistroAsync(_context.CensoNpt, atencionNpt.RegistroId, ct);
            if (registro is not null)
            {
                ApplyNptRecordToModel(npt, registro);
                npt.CedulaFiltro = registro.NumeroIdentificacion;
            }
            await PopulateNptDropdownsAsync(npt, ct);
            AplicarMaestroAModeloNpt(npt, paciente);
            model.Npt = npt;
        }

        if (seleccionadas.TryGetValue(CensoProgramas.TerapiaAmbulatoria, out var atencionTerapia))
        {
            var terapia = BuildDefaultTerapiaAmbulatoriaModel();
            terapia.CedulaFiltro = doc;
            var registro = await BuscarRegistroAsync(_context.CensoTerapiasAmbulatorias, atencionTerapia.RegistroId, ct);
            if (registro is not null)
            {
                ApplyTerapiaAmbulatoriaRecordToModel(terapia, registro);
                terapia.CedulaFiltro = registro.NumeroIdentificacion;
            }
            await PopulateTerapiaAmbulatoriaDropdownsAsync(terapia, ct);
            await PopulateTerapiaAmbulatoriaLatestRecordsAsync(terapia, ct);
            await PopulateTerapiaAmbulatoriaProrrogasAsync(terapia, ct);
            await PopulateTerapiaAmbulatoriaAdjuntosAsync(terapia, ct);
            AplicarMaestroAModeloTerapia(terapia, paciente);
            model.TerapiaAmbulatoria = terapia;
        }
    }

    /// <summary>
    /// Trae la fila del programa: por id de episodio si ya está enlazado, y si no por documento
    /// tomando la más reciente, igual que hacían las pantallas independientes.
    /// </summary>
    private static async Task<T?> BuscarRegistroAsync<T>(
        IQueryable<T> origen,
        long? registroId,
        CancellationToken ct)
        where T : class
    {
        // Un episodio sin registro es un ingreso nuevo: el formulario arranca en blanco. Buscar
        // "el último registro del documento" era un atajo peligroso, porque a un paciente se le
        // puede dar de alta y volver a ingresar al mismo programa: el segundo ingreso cargaba la
        // atención anterior —ya egresada— con su EditingRecordId, y al guardar la sobrescribía en
        // lugar de crear la atención 2. No hace falta ese atajo: ReconciliarEpisodiosAsync recorre
        // todas las filas del paciente antes de construir esta pantalla y enlaza cada una con su
        // episodio, así que un RegistroId nulo aquí significa que de verdad no hay fila propia.
        if (!registroId.HasValue)
        {
            return null;
        }

        return await origen.AsNoTracking()
            .FirstOrDefaultAsync(x => EF.Property<long>(x, "Id") == registroId.Value, ct);
    }

    // ==========================================================================================
    // Los campos compartidos los manda el maestro.
    //
    // Cada formulario de programa sigue enviando su modelo completo, pero los campos que ahora se
    // capturan una sola vez viajan como campos ocultos con el valor del maestro. Así la validación
    // de cada censo, que no cambió, recibe todo lo que espera.
    // ==========================================================================================
    private static void AplicarMaestroAModeloAgudos(CensoReceptionViewModel m, CensoPaciente p)
    {
        // La recepción ya no viaja desde el maestro: es del ingreso y la aplica
        // AplicarRecepcionAModeloAgudos con los datos del episodio. Mientras salía de aquí, un
        // reingreso mostraba la recepción del ingreso anterior en cada render.
        m.NombrePaciente = Elegir(p.NombrePaciente, m.NombrePaciente);
        m.TipoIdentificacion = Elegir(p.TipoIdentificacion, m.TipoIdentificacion);
        m.NumeroIdentificacion = Elegir(p.NumeroIdentificacion, m.NumeroIdentificacion);
        m.FechaNacimiento = p.FechaNacimiento;
        m.Edad = p.Edad;
        m.CorreoElectronico = Elegir(p.CorreoElectronico, m.CorreoElectronico);
        m.CodigoCie10 = Elegir(p.CodigoCie10, m.CodigoCie10);
        m.DiagnosticoDescriptivo = ElegirOpcional(p.DiagnosticoDescriptivo, m.DiagnosticoDescriptivo);
        m.Asegurador = Elegir(p.Asegurador, m.Asegurador);
        m.Direccion = Elegir(p.Direccion, m.Direccion);
        m.DireccionEsValida = p.DireccionValidada;
        m.AsumirDireccionErrada = p.AsumirDireccionErrada;
        m.DetalleDireccion = ElegirOpcional(p.DetalleDireccion, m.DetalleDireccion);
        m.ClasificacionZonaSura = Elegir(p.ClasificacionZonaSura, m.ClasificacionZonaSura);
        m.MunicipioResidencia = Elegir(p.MunicipioResidencia, m.MunicipioResidencia);
        m.Barrio = Elegir(p.Barrio, m.Barrio);
        m.ZonaDireccionSegunMunicipio = Elegir(p.ZonaDireccionSegunMunicipio, m.ZonaDireccionSegunMunicipio);
        m.Area = Elegir(p.Area, m.Area);
        m.IpsQueRemite = Elegir(p.IpsQueRemite, m.IpsQueRemite);
        m.VistoBuenoRangoFueraAnexo = Elegir(p.VistoBuenoRangoFueraAnexo, m.VistoBuenoRangoFueraAnexo);
        m.Telefono1 = Elegir(p.Telefono1, m.Telefono1);
        m.Telefono2 = Elegir(p.Telefono2, m.Telefono2);
        m.Telefono3 = ElegirOpcional(p.Telefono3, m.Telefono3);
    }

    /// <summary>
    /// Pone en el formulario de agudos la recepción del ingreso que se está mostrando.
    ///
    /// Agudos manda su formulario entero en un POST y su validación exige estos campos, así que
    /// siguen viajando en su modelo; lo que cambió es de dónde salen. Un ingreso sin recepción
    /// todavía conserva lo que el modelo ya traía —hoy—, para que el formulario nuevo no arranque
    /// con fechas vacías que su propia validación rechaza.
    /// </summary>
    private static void AplicarRecepcionAModeloAgudos(CensoReceptionViewModel m, CensoAtencionViewModel a)
    {
        if (a.FechaIngresoRecepcion.HasValue) m.FechaIngreso = a.FechaIngresoRecepcion.Value;
        if (a.HoraIngresoRecepcion.HasValue) m.HoraIngreso = a.HoraIngresoRecepcion.Value;
        if (a.FechaRespuesta.HasValue) m.FechaRespuesta = a.FechaRespuesta.Value;
        if (a.HoraRespuesta.HasValue) m.HoraRespuesta = a.HoraRespuesta.Value;
        m.NombreRecepcionaCaso = Elegir(a.NombreRecepcionaCaso, m.NombreRecepcionaCaso);
        m.NombreRealizaKardex = Elegir(a.NombreRealizaKardex, m.NombreRealizaKardex);
    }

    private static void AplicarMaestroAModeloCronicos(CensoCronicoViewModel m, CensoPaciente p)
    {
        // La fecha de ingreso al programa es de la atención y la trae su propio registro (o el
        // valor de hoy, si la atención es nueva). Venía del maestro solo porque allí vivía la
        // fecha de la recepción, que es otra cosa y ahora está en el episodio.
        m.NombrePaciente = Elegir(p.NombrePaciente, m.NombrePaciente);
        m.TipoIdentificacion = Elegir(p.TipoIdentificacion, m.TipoIdentificacion);
        m.NumeroIdentificacion = Elegir(p.NumeroIdentificacion, m.NumeroIdentificacion);
        m.FechaNacimiento = p.FechaNacimiento;
        m.Edad = p.Edad;
        m.CorreoElectronico = ElegirOpcional(p.CorreoElectronico, m.CorreoElectronico);
        m.Genero = Elegir(p.Genero, m.Genero);
        m.Direccion = ElegirOpcional(p.Direccion, m.Direccion);
        m.DireccionEsValida = p.DireccionValidada;
        m.AsumirDireccionErrada = p.AsumirDireccionErrada;
        m.DetalleDireccion = ElegirOpcional(p.DetalleDireccion, m.DetalleDireccion);
        m.ClasificacionZonaSura = ElegirOpcional(p.ClasificacionZonaSura, m.ClasificacionZonaSura);
        m.MunicipioResidencia = ElegirOpcional(p.MunicipioResidencia, m.MunicipioResidencia);
        m.Barrio = ElegirOpcional(p.Barrio, m.Barrio);
        m.ZonaDireccionSegunMunicipio = ElegirOpcional(p.ZonaDireccionSegunMunicipio, m.ZonaDireccionSegunMunicipio);
        m.Area = ElegirOpcional(p.Area, m.Area);
    }

    private static void AplicarMaestroAModeloHeridas(CensoClinicaHeridasViewModel m, CensoPaciente p)
    {
        m.NombrePaciente = Elegir(p.NombrePaciente, m.NombrePaciente);
        m.TipoIdentificacion = Elegir(p.TipoIdentificacion, m.TipoIdentificacion);
        m.NumeroIdentificacion = Elegir(p.NumeroIdentificacion, m.NumeroIdentificacion);
        m.FechaNacimiento = p.FechaNacimiento;
        m.Edad = p.Edad;
        m.Genero = Elegir(GeneroBinarioMaestro(p.Genero), m.Genero);
        // Cada censo tiene su propio catalogo de asegurador ("EPS SURA" en agudos y heridas,
        // "SURA" en NPT). El maestro guarda uno solo, asi que al bajarlo hay que traducirlo con
        // el mismo normalizador que ya usaba la precarga de clinica de heridas.
        m.Asegurador = Elegir(
            MapearAsegurador(p.Asegurador, ClinicaHeridasAseguradorValues), m.Asegurador);
        // Clínica de heridas tiene su propio catálogo de CIE10 (los diagnósticos de herida),
        // distinto del general. Su código y su descriptivo son del programa y no se tocan
        // desde el maestro: hacerlo dejaba un código que su propia validación rechaza.
        m.Direccion = ElegirOpcional(p.Direccion, m.Direccion);
        m.DireccionEsValida = p.DireccionValidada;
        m.AsumirDireccionErrada = p.AsumirDireccionErrada;
        m.DetalleDireccion = ElegirOpcional(p.DetalleDireccion, m.DetalleDireccion);
        m.ClasificacionZonaSura = ElegirOpcional(p.ClasificacionZonaSura, m.ClasificacionZonaSura);
        m.MunicipioResidencia = ElegirOpcional(p.MunicipioResidencia, m.MunicipioResidencia);
        m.Barrio = ElegirOpcional(p.Barrio, m.Barrio);
        m.ZonaDireccionSegunMunicipio = ElegirOpcional(p.ZonaDireccionSegunMunicipio, m.ZonaDireccionSegunMunicipio);
        m.TelefonoPrincipal = Elegir(p.Telefono1, m.TelefonoPrincipal);
        m.TelefonoAdicional1 = Elegir(p.Telefono2, m.TelefonoAdicional1);
        m.TelefonoAdicional2 = ElegirOpcional(p.Telefono3, m.TelefonoAdicional2);
    }

    private static void AplicarMaestroAModeloNpt(CensoNptViewModel m, CensoPaciente p)
    {
        m.NombrePaciente = Elegir(p.NombrePaciente, m.NombrePaciente);
        m.TipoIdentificacion = Elegir(p.TipoIdentificacion, m.TipoIdentificacion);
        m.NumeroIdentificacion = Elegir(p.NumeroIdentificacion, m.NumeroIdentificacion);
        m.FechaNacimiento = p.FechaNacimiento;
        m.Edad = p.Edad;
        m.Genero = Elegir(GeneroBinarioMaestro(p.Genero), m.Genero);
        // Cada censo tiene su propio catalogo de asegurador ("EPS SURA" en agudos y heridas,
        // "SURA" en NPT). El maestro guarda uno solo, asi que al bajarlo hay que traducirlo con
        // el mismo normalizador que ya usaba la precarga de clinica de heridas.
        m.Asegurador = Elegir(
            MapearAsegurador(p.Asegurador, NptAseguradorValues), m.Asegurador);
        // El código CIE10 y el diagnóstico de NPT son propios de la atención, igual que en
        // clínica de heridas: no se tocan desde el maestro. Antes se sobrescribían en cada
        // render con Elegir(), lo que hacía invisible el valor propio del programa —el campo
        // vivía oculto en el formulario y siempre mostraba el del maestro.
        m.Direccion = ElegirOpcional(p.Direccion, m.Direccion);
        m.DireccionEsValida = p.DireccionValidada;
        m.AsumirDireccionErrada = p.AsumirDireccionErrada;
        m.ClasificacionZonaSura = ElegirOpcional(p.ClasificacionZonaSura, m.ClasificacionZonaSura);
        m.MunicipioResidencia = ElegirOpcional(p.MunicipioResidencia, m.MunicipioResidencia);
        m.Barrio = ElegirOpcional(p.Barrio, m.Barrio);
        m.ZonaDireccionSegunMunicipio = ElegirOpcional(p.ZonaDireccionSegunMunicipio, m.ZonaDireccionSegunMunicipio);
        m.TelefonoPrincipal = Elegir(p.Telefono1, m.TelefonoPrincipal);
        m.TelefonoAdicional1 = Elegir(p.Telefono2, m.TelefonoAdicional1);
        m.TelefonoAdicional2 = ElegirOpcional(p.Telefono3, m.TelefonoAdicional2);
    }

    private static void AplicarMaestroAModeloTerapia(CensoTerapiaAmbulatoriaViewModel m, CensoPaciente p)
    {
        m.NombrePaciente = Elegir(p.NombrePaciente, m.NombrePaciente);
        m.TipoIdentificacion = Elegir(p.TipoIdentificacion, m.TipoIdentificacion);
        m.NumeroIdentificacion = Elegir(p.NumeroIdentificacion, m.NumeroIdentificacion);
        m.FechaNacimiento = p.FechaNacimiento;
        m.Edad = p.Edad;
        m.CorreoElectronico = Elegir(p.CorreoElectronico, m.CorreoElectronico);
        m.CodigoCie10 = Elegir(p.CodigoCie10, m.CodigoCie10);
        m.DiagnosticoDescriptivo = Elegir(p.DiagnosticoDescriptivo, m.DiagnosticoDescriptivo);
        m.Direccion = ElegirOpcional(p.Direccion, m.Direccion);
        m.DireccionEsValida = p.DireccionValidada;
        m.AsumirDireccionErrada = p.AsumirDireccionErrada;
        m.DetalleDireccion = ElegirOpcional(p.DetalleDireccion, m.DetalleDireccion);
        m.ClasificacionZonaSura = ElegirOpcional(p.ClasificacionZonaSura, m.ClasificacionZonaSura);
        m.MunicipioResidencia = ElegirOpcional(p.MunicipioResidencia, m.MunicipioResidencia);
        m.Barrio = ElegirOpcional(p.Barrio, m.Barrio);
        m.ZonaDireccionSegunMunicipio = ElegirOpcional(p.ZonaDireccionSegunMunicipio, m.ZonaDireccionSegunMunicipio);
        m.Area = ElegirOpcional(p.Area, m.Area);
        m.IpsQueRemite = Elegir(p.IpsQueRemite, m.IpsQueRemite);
        m.TelefonoPrincipal = Elegir(p.Telefono1, m.TelefonoPrincipal);
        m.TelefonoAdicional1 = ElegirOpcional(p.Telefono2, m.TelefonoAdicional1);
        m.TelefonoAdicional2 = ElegirOpcional(p.Telefono3, m.TelefonoAdicional2);
    }

    /// <summary>
    /// Traduce el asegurador del maestro al catálogo de cada censo. Los tres catálogos nombran a las
    /// mismas aseguradoras de forma distinta — "EPS SURA" en agudos y clínica de heridas, "SURA" en
    /// NPT; "PAN-AMERICAN LIFE DE COLOMBIA" frente a "PANAMERICAN LIFE" — así que se compara por
    /// familia y no por texto exacto.
    /// </summary>
    private static string? MapearAsegurador(string? valor, IReadOnlyList<string> catalogo)
    {
        var familia = FamiliaAsegurador(valor);
        return familia is null ? null : catalogo.FirstOrDefault(x => FamiliaAsegurador(x) == familia);
    }

    private static string? FamiliaAsegurador(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var clave = new string(valor.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (clave.Contains("SURA", StringComparison.Ordinal)) return "SURA";
        if (clave.Contains("PANAMERICAN", StringComparison.Ordinal)) return "PANAMERICAN";
        if (clave.Contains("PARTICULAR", StringComparison.Ordinal)) return "PARTICULAR";
        return clave;
    }

    /// <summary>Clínica de heridas y NPT solo admiten Masculino y Femenino en su catálogo.</summary>
    private static string? GeneroBinarioMaestro(string? genero) =>
        string.Equals(genero, "Masculino", StringComparison.OrdinalIgnoreCase)
        || string.Equals(genero, "Femenino", StringComparison.OrdinalIgnoreCase)
            ? genero
            : null;

    private static string Elegir(string? maestro, string? actual) =>
        string.IsNullOrWhiteSpace(maestro) ? actual ?? string.Empty : maestro;

    private static string? ElegirOpcional(string? maestro, string? actual) =>
        string.IsNullOrWhiteSpace(maestro) ? actual : maestro;

    private CensoPacienteFormViewModel NuevoFormularioPaciente(DateTime ahora) => new()
    {
        FechaNacimiento = ahora.Date,
        Edad = 0,
        // Sin municipio de partida a proposito: nacer en "no parametrizado" era lo que dejaba
        // pacientes guardados con la direccion sin resolver, porque el formulario ya venia lleno
        // con un valor que parecia valido.
        MunicipioResidencia = string.Empty,
        ClasificacionZonaSura = string.Empty,
        ZonaDireccionSegunMunicipio = string.Empty,
        Area = AreaValues[0]
    };

    private static CensoPacienteFormViewModel AFormulario(CensoPaciente p) => new()
    {
        PacienteId = p.Id,
        NombrePaciente = p.NombrePaciente,
        TipoIdentificacion = p.TipoIdentificacion,
        NumeroIdentificacion = p.NumeroIdentificacion,
        FechaNacimiento = p.FechaNacimiento,
        Edad = p.Edad,
        Genero = p.Genero,
        CorreoElectronico = p.CorreoElectronico,
        CodigoCie10 = p.CodigoCie10,
        DiagnosticoDescriptivo = p.DiagnosticoDescriptivo,
        Asegurador = p.Asegurador,
        Direccion = p.Direccion,
        DireccionEsValida = p.DireccionValidada,
        AsumirDireccionErrada = p.AsumirDireccionErrada,
        DetalleDireccion = p.DetalleDireccion,
        ClasificacionZonaSura = p.ClasificacionZonaSura,
        MunicipioResidencia = p.MunicipioResidencia,
        Barrio = p.Barrio,
        ZonaDireccionSegunMunicipio = p.ZonaDireccionSegunMunicipio,
        Area = p.Area,
        IpsQueRemite = p.IpsQueRemite,
        VistoBuenoRangoFueraAnexo = p.VistoBuenoRangoFueraAnexo,
        Telefono1 = p.Telefono1,
        Telefono2 = p.Telefono2,
        Telefono3 = p.Telefono3
    };

    private static IReadOnlyList<CensoProgramaChipViewModel> ConstruirChipsVacios() =>
        CensoProgramas.Todos
            .Select(programa => new CensoProgramaChipViewModel
            {
                Programa = programa,
                SePuedeAgregar = false,
                MotivoBloqueo = "Guarda primero los datos del paciente."
            })
            .ToList();

    private static IReadOnlyList<CensoProgramaChipViewModel> ConstruirChips(
        IReadOnlyList<CensoPacientePrograma> episodios)
    {
        var abiertos = episodios.Where(x => x.CerradoAtUtc == null).ToList();

        return CensoProgramas.Todos.Select(programa =>
        {
            var abierto = abiertos.FirstOrDefault(x => string.Equals(x.Programa, programa, StringComparison.Ordinal));
            var excluyente = CensoProgramas.PrimeroQueBloquea(programa, abiertos.Select(x => x.Programa));
            var chocaConExcluyente = excluyente is not null;

            return new CensoProgramaChipViewModel
            {
                Programa = programa,
                Agregado = abierto is not null,
                EpisodioId = abierto?.Id,
                RegistroId = abierto?.RegistroId,
                EpisodiosCerrados = episodios.Count(x =>
                    string.Equals(x.Programa, programa, StringComparison.Ordinal) && x.CerradoAtUtc != null),
                SePuedeAgregar = abierto is null && !chocaConExcluyente,
                // Cabe en la ranura de estado de la tarjeta, donde al lado dice "Activo" o
                // "Agregar". El marco compartido ya dice que son alternativas; aqui solo falta
                // cual de las dos esta ocupando el puesto.
                MotivoBloqueo = chocaConExcluyente
                    ? $"{CensoProgramas.NombreCorto(excluyente!)} está activo"
                    : null,
                // Terapia ambulatoria es la única que deja al paciente sin poder sumar nada más.
                // Se advierte al confirmar, que es cuando la persona está decidiendo.
                AvisoAlAgregar = programa == CensoProgramas.TerapiaAmbulatoria
                    ? "Mientras esté activa, el paciente no podrá tener ningún otro programa."
                    : null
            };
        }).ToList();
    }

    private async Task PoblarCatalogosUnificadosAsync(
        CensoUnificadoViewModel model,
        CancellationToken cancellationToken)
    {
        model.TipoIdentificacionOptions = BuildOptions(TiposIdentificacion);
        model.GeneroOptions = BuildOptions(GeneroValues);
        model.ClasificacionZonaSuraOptions = BuildOptions(ClasificacionZonaSuraValues);
        model.MunicipioResidenciaOptions = BuildOptions(MunicipiosResidenciaValues);
        model.ZonaDireccionOptions = BuildOptions(ZonaDireccionValues);
        model.AreaOptions = BuildOptions(AreaValues);
        model.IpsQueRemiteOptions = BuildOptions(IpsQueRemiteValues);

        // Una IPS guardada que ya no figura en el catálogo se agrega como opción para este
        // paciente. Con la lista cerrada, sin esto el campo se pintaría vacío y el primer
        // guardado borraría un dato que nadie quiso borrar. No abre la puerta a valores nuevos:
        // solo sobrevive el que la fila ya tenía. Es el mismo criterio que aplica
        // PopulateDropdownsAsync en el formulario de agudos.
        var ipsGuardada = model.Paciente.IpsQueRemite;
        if (!string.IsNullOrWhiteSpace(ipsGuardada)
            && !IpsQueRemiteValues.Contains(ipsGuardada, StringComparer.OrdinalIgnoreCase))
        {
            model.IpsQueRemiteOptions = model.IpsQueRemiteOptions
                .Append(new SelectListItem { Text = ipsGuardada, Value = ipsGuardada })
                .ToList();
        }
        model.VistoBuenoOptions = BuildOptions(VistoBuenoValues);
        model.AseguradorOptions = BuildOptions(AseguradorValues);
        model.NursingAssistantOptions = await GetNursingAssistantOptionsAsync(cancellationToken);
        model.BarrioOptions = await ResolverBarriosAsync(
            model.Paciente.MunicipioResidencia,
            model.Paciente.Barrio,
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ResolverBarriosAsync(
        string? municipio,
        string? barrio,
        CancellationToken cancellationToken)
    {
        var canonico = ToCanonicalMunicipality(municipio ?? string.Empty);
        if (string.IsNullOrWhiteSpace(canonico))
        {
            return string.IsNullOrWhiteSpace(barrio) ? ["NO PARAMETRIZADO"] : [barrio];
        }

        var barrios = await _addressValidationService.SearchNeighborhoodsAsync(
            canonico,
            string.IsNullOrWhiteSpace(barrio) ? "a" : barrio,
            cancellationToken);

        if (barrios.Count == 0)
        {
            barrios = ["NO PARAMETRIZADO"];
        }

        if (!string.IsNullOrWhiteSpace(barrio) && !barrios.Contains(barrio, StringComparer.OrdinalIgnoreCase))
        {
            barrios = barrios.Concat([barrio]).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        }

        return barrios;
    }

    // ==========================================================================================
    // Normalización y validación
    // ==========================================================================================
    private void NormalizarPaciente(CensoPacienteFormViewModel p)
    {
        p.NombrePaciente = p.NombrePaciente?.Trim() ?? string.Empty;
        p.TipoIdentificacion = p.TipoIdentificacion?.Trim() ?? string.Empty;
        p.NumeroIdentificacion = NormalizeIdentificationNumber(p.TipoIdentificacion, p.NumeroIdentificacion);
        p.CodigoCie10 = string.IsNullOrWhiteSpace(p.CodigoCie10) ? null : NormalizeCie10(p.CodigoCie10);
        p.DiagnosticoDescriptivo = p.DiagnosticoDescriptivo?.Trim();
        p.CorreoElectronico = p.CorreoElectronico?.Trim();
        p.Direccion = p.Direccion?.Trim();
        p.DetalleDireccion = p.DetalleDireccion?.Trim();
        p.Barrio = p.Barrio?.Trim();
        p.IpsQueRemite = p.IpsQueRemite?.Trim();
        p.Telefono1 = p.Telefono1?.Trim();
        p.Telefono2 = p.Telefono2?.Trim();
        p.Telefono3 = p.Telefono3?.Trim();
        p.Edad = CalculateAge(p.FechaNacimiento, DateTime.Today);
        // El indicador de tiempo de respuesta se calcula en GuardarRecepcion: es de la recepción
        // del ingreso, y aquí ya no hay de dónde sacar sus fechas.
    }

    /// <summary>
    /// Reemplaza los mensajes que produce el enlazador de modelo cuando un campo no tiene el formato
    /// esperado. Sin esto, escribir cualquier cosa en una hora o una fecha devuelve el texto tecnico
    /// en inglés que trae el framework, que al usuario no le dice qué corregir.
    /// </summary>
    private void TraducirErroresDeFormato()
    {
        foreach (var (clave, entrada) in ModelState)
        {
            if (entrada.ValidationState != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
            {
                continue;
            }

            var esHora = clave.EndsWith("HoraIngreso", StringComparison.Ordinal)
                || clave.EndsWith("HoraRespuesta", StringComparison.Ordinal);
            var esFecha = clave.Contains("Fecha", StringComparison.Ordinal);
            if (!esHora && !esFecha)
            {
                continue;
            }

            // Solo se traducen los fallos de conversión: los mensajes propios del modelo ya están
            // escritos para el usuario y no hay que tocarlos.
            var deFormato = entrada.Errors.Any(x => x.Exception is not null || x.ErrorMessage.Contains("is not valid", StringComparison.OrdinalIgnoreCase));
            if (!deFormato)
            {
                continue;
            }

            entrada.Errors.Clear();
            entrada.Errors.Add(esHora
                ? "Ingresa la hora en formato HH:mm, por ejemplo 14:30."
                : "Ingresa una fecha válida.");
        }
    }

    /// <summary>
    /// Obligatoriedad condicionada por los programas que ya tiene el paciente.
    ///
    /// La identidad se exige siempre. Los campos que hoy solo pide el censo de agudos (correo, IPS
    /// que remite, visto bueno y teléfonos) se exigen únicamente si el paciente tiene agudos, para
    /// no obligar a un paciente que solo es crónico o de terapia a diligenciar datos que su censo
    /// nunca le pidió. "Quien realiza kardex" se exige si tiene cualquier programa que genere
    /// kardex: agudos, crónicos o clínica de heridas.
    /// </summary>
    private async Task ValidarObligatoriosPorProgramaAsync(
        CensoPacienteFormViewModel p,
        CancellationToken cancellationToken)
    {
        var abiertos = new List<string>();
        var paciente = await _censoPacienteService.BuscarPorDocumentoAsync(p.NumeroIdentificacion, cancellationToken);
        if (paciente is not null)
        {
            p.PacienteId = paciente.Id;
            abiertos = (await _censoPacienteService.ObtenerProgramasAsync(paciente.Id, cancellationToken))
                .Where(x => x.CerradoAtUtc == null)
                .Select(x => x.Programa)
                .ToList();
        }

        // "Quien realiza kardex" ya no se valida aquí: es un campo de la recepción, y la recepción
        // es de cada ingreso. Lo exige GuardarRecepcion según el programa de ESE episodio, que es
        // la pregunta correcta —antes bastaba con que el paciente tuviera abierto en cualquier
        // parte un programa con kardex para exigirlo en un formulario que era de todos—.

        if (!abiertos.Contains(CensoProgramas.Agudos, StringComparer.Ordinal))
        {
            return;
        }

        void Exigir(string propiedad, string? valor, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                ModelState.AddModelError(
                    nameof(CensoUnificadoViewModel.Paciente) + "." + propiedad,
                    mensaje);
            }
        }

        Exigir(nameof(p.CorreoElectronico), p.CorreoElectronico, "El correo electrónico es obligatorio en programa agudos.");
        Exigir(nameof(p.IpsQueRemite), p.IpsQueRemite, "Selecciona la IPS que remite.");
        Exigir(nameof(p.VistoBuenoRangoFueraAnexo), p.VistoBuenoRangoFueraAnexo, "Selecciona el visto bueno de rango fuera del anexo.");
        Exigir(nameof(p.Telefono1), p.Telefono1, "Ingresa el teléfono principal.");
        Exigir(nameof(p.Telefono2), p.Telefono2, "Ingresa el teléfono adicional 1.");
        Exigir(nameof(p.CodigoCie10), p.CodigoCie10, "El código CIE10 es obligatorio en programa agudos.");
    }

    /// <summary>
    /// El municipio y el barrio tienen que quedar resueltos, sea porque la validacion de direccion
    /// los trajo o porque alguien los eligio a mano. "No parametrizado" es el estado del que hay
    /// que salir, no un valor con el que se pueda guardar: dejarlo pasar es lo que llenaba el censo
    /// de pacientes sin ubicacion.
    /// </summary>
    private void ValidarUbicacionResuelta(CensoPacienteFormViewModel p)
    {
        void Exigir(string propiedad, string? valor, string mensaje)
        {
            var vacio = string.IsNullOrWhiteSpace(valor);
            var sinParametrizar = string.Equals(
                valor?.Trim(), MunicipioNoParametrizado, StringComparison.OrdinalIgnoreCase);

            if (vacio || sinParametrizar)
            {
                ModelState.AddModelError(
                    nameof(CensoUnificadoViewModel.Paciente) + "." + propiedad,
                    mensaje);
            }
        }

        Exigir(nameof(p.MunicipioResidencia), p.MunicipioResidencia,
            "Valida la dirección o elige el municipio: no se puede guardar sin parametrizar.");
        Exigir(nameof(p.Barrio), p.Barrio,
            "Valida la dirección o elige el barrio: no se puede guardar sin parametrizar.");
    }

    private string UsuarioActual() =>
        User.Identity?.Name
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? "Sistema";

    private Guid? UsuarioActualId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
