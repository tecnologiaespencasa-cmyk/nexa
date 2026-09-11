using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nexa.Data.Entities;

namespace Nexa.Models.ViewModels;

/// <summary>
/// Recepción del paciente + datos básicos: lo que se captura una sola vez para todo paciente del
/// censo, sin importar a cuántos programas pertenezca.
///
/// Obligatoriedad: los campos de identidad son obligatorios siempre. Los que hoy solo exige el
/// programa de agudos (quien realiza kardex, correo, IPS que remite, visto bueno y teléfonos) se
/// validan en el controlador según los programas que tenga el paciente, para no obligar a un
/// paciente que solo es crónico o de terapia a diligenciar datos que su censo nunca le pidió.
/// Por eso aquí no llevan [Required]: la regla vive en CensoController.Unificado.
/// </summary>
public class CensoPacienteFormViewModel
{
    public long? PacienteId { get; set; }

    // La recepción ya no está aquí. Es del ingreso, no del paciente: cada ingreso nace de un
    // correo distinto, así que un paciente que reingresa tiene una recepción nueva y la del
    // ingreso anterior debe quedarse con ese ingreso. Vive en CensoRecepcionFormViewModel,
    // contra el episodio. Lo que sigue en este modelo es lo que de verdad es del paciente y se
    // conserva igual entre un ingreso y el siguiente.

    // ----- Sección 2: Datos básicos del paciente -----
    [Required(ErrorMessage = "El nombre del paciente es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre del paciente no puede superar 200 caracteres.")]
    [Display(Name = "Nombre del paciente")]
    public string NombrePaciente { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona el tipo de identificación.")]
    [StringLength(3)]
    [Display(Name = "Tipo de identificación")]
    public string TipoIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de identificación es obligatorio.")]
    [StringLength(20, ErrorMessage = "El número de identificación no puede superar 20 caracteres.")]
    [Display(Name = "Número de identificación")]
    public string NumeroIdentificacion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime FechaNacimiento { get; set; } = DateTime.Today;

    [Display(Name = "Edad")]
    public int Edad { get; set; }

    // Subió al maestro desde crónicos, clínica de heridas y NPT.
    [StringLength(20)]
    [Display(Name = "Género")]
    public string? Genero { get; set; }

    [StringLength(150, ErrorMessage = "El correo electrónico no puede superar 150 caracteres.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string? CorreoElectronico { get; set; }

    [StringLength(4, ErrorMessage = "El código CIE10 debe tener 4 caracteres.")]
    [RegularExpression(@"^[A-Za-z][0-9]{3}$", ErrorMessage = "El código CIE10 debe iniciar con letra y continuar con 3 dígitos.")]
    [Display(Name = "Código CIE10")]
    public string? CodigoCie10 { get; set; }

    [StringLength(300)]
    [Display(Name = "Diagnóstico descriptivo")]
    public string? DiagnosticoDescriptivo { get; set; }

    // Subió al maestro: en agudos vivía en la sección 3 (plan de manejo) y en clínica de heridas y
    // NPT en datos básicos. Se sigue replicando a la sección 3 de agudos.
    [StringLength(120, ErrorMessage = "El asegurador no puede superar 120 caracteres.")]
    [Display(Name = "Asegurador")]
    public string? Asegurador { get; set; }

    [StringLength(300, ErrorMessage = "La dirección no puede superar 300 caracteres.")]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    [Display(Name = "Asumir dirección errada y continuar")]
    public bool AsumirDireccionErrada { get; set; }

    public bool DireccionEsValida { get; set; }

    public string? DireccionSugerida { get; set; }

    public string? DireccionMensajeValidacion { get; set; }

    [StringLength(200, ErrorMessage = "El detalle de dirección no puede superar 200 caracteres.")]
    [Display(Name = "Detalle de dirección")]
    public string? DetalleDireccion { get; set; }

    [Display(Name = "Clasificación zona Sura")]
    public string? ClasificacionZonaSura { get; set; }

    [Display(Name = "Municipio de residencia")]
    public string? MunicipioResidencia { get; set; }

    [Display(Name = "Barrio")]
    public string? Barrio { get; set; }

    [Display(Name = "Zona de dirección según municipio")]
    public string? ZonaDireccionSegunMunicipio { get; set; }

    [Display(Name = "Area")]
    public string? Area { get; set; }

    [Display(Name = "IPS que remite")]
    public string? IpsQueRemite { get; set; }

    [Display(Name = "Visto bueno rango fuera del anexo")]
    public string? VistoBuenoRangoFueraAnexo { get; set; }

    [StringLength(10, ErrorMessage = "El teléfono principal no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El teléfono principal solo permite dígitos.")]
    [Display(Name = "Teléfono principal")]
    public string? Telefono1 { get; set; }

    [StringLength(10, ErrorMessage = "El teléfono adicional 1 no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El teléfono adicional 1 solo permite dígitos.")]
    [Display(Name = "Teléfono adicional 1")]
    public string? Telefono2 { get; set; }

    [StringLength(10, ErrorMessage = "El teléfono adicional 2 no puede superar 10 dígitos.")]
    [RegularExpression(@"^[0-9]*$", ErrorMessage = "El teléfono adicional 2 solo permite dígitos.")]
    [Display(Name = "Teléfono adicional 2")]
    public string? Telefono3 { get; set; }
}

/// <summary>Estado de un programa en la tarjeta del selector.</summary>
public class CensoProgramaChipViewModel
{
    public string Programa { get; set; } = string.Empty;

    public string Nombre => CensoProgramas.Nombre(Programa);

    public string NombreCorto => CensoProgramas.NombreCorto(Programa);

    /// <summary>El paciente tiene un episodio abierto de este programa.</summary>
    public bool Agregado { get; set; }

    public long? EpisodioId { get; set; }

    /// <summary>Id de la fila en la tabla propia del programa. Nulo mientras no se ha guardado.</summary>
    public long? RegistroId { get; set; }

    /// <summary>Episodios cerrados del mismo programa (atenciones o tratamientos anteriores).</summary>
    public int EpisodiosCerrados { get; set; }

    /// <summary>Se puede agregar: no está agregado y no choca con el programa excluyente.</summary>
    public bool SePuedeAgregar { get; set; }

    /// <summary>Motivo por el que no se puede agregar, para mostrarlo en la tarjeta.</summary>
    public string? MotivoBloqueo { get; set; }

    /// <summary>
    /// Consecuencia que hay que advertir antes de agregar este programa, si la tiene. Se dice en el
    /// momento de decidir y no como etiqueta permanente en la tarjeta.
    /// </summary>
    public string? AvisoAlAgregar { get; set; }

}

/// <summary>
/// Una atención del paciente dentro de un programa: un episodio de
/// <c>censo_paciente_programa</c> con lo justo para nombrarlo y navegar hasta él.
///
/// Existe porque un paciente puede ingresar varias veces al mismo programa y, hasta ahora, la
/// pantalla solo sabía mostrar la que estuviera abierta: las anteriores quedaban fuera de
/// alcance aunque siguiera habiendo trabajo pendiente sobre ellas, como la devolución de
/// productos o del equipo en comodato, que ocurre días después del alta.
/// </summary>
/// <summary>
/// La recepción de un ingreso: lo que trajo el correo con el que empezó esa atención.
///
/// Se captura y se guarda contra el episodio, no contra el paciente. Antes vivía una sola vez en
/// el maestro y por eso un reingreso mostraba —y al guardar pisaba— la recepción del ingreso
/// anterior: no había dónde poner la segunda.
/// </summary>
public class CensoRecepcionFormViewModel
{
    public long EpisodioId { get; set; }

    /// <summary>Programa del episodio. Decide si se exige quién realiza el kardex.</summary>
    public string Programa { get; set; } = string.Empty;

    /// <summary>Documento del paciente, para volver a su pantalla después de guardar.</summary>
    public string? CedulaPaciente { get; set; }

    [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha y hora de ingreso")]
    public DateTime? FechaIngreso { get; set; }

    [Required(ErrorMessage = "La hora de ingreso es obligatoria.")]
    [DataType(DataType.Time)]
    [Display(Name = "Hora de ingreso")]
    public TimeSpan? HoraIngreso { get; set; }

    [Required(ErrorMessage = "La fecha de respuesta es obligatoria.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha y hora de respuesta")]
    public DateTime? FechaRespuesta { get; set; }

    [Required(ErrorMessage = "La hora de respuesta es obligatoria.")]
    [DataType(DataType.Time)]
    [Display(Name = "Hora de respuesta")]
    public TimeSpan? HoraRespuesta { get; set; }

    [Display(Name = "Indicador tiempo de respuesta (minutos)")]
    public int? IndicadorTiempoRespuestaMinutos { get; set; }

    [Required(ErrorMessage = "Selecciona quien recepciona el caso.")]
    [StringLength(120)]
    [Display(Name = "Nombre de quien recepciona el caso")]
    public string? NombreRecepcionaCaso { get; set; }

    // Solo obligatorio cuando el programa de ESTE episodio genera kardex. Antes la regla miraba
    // todos los programas abiertos del paciente, porque el campo era del paciente.
    [StringLength(120)]
    [Display(Name = "Nombre de quien realiza kardex")]
    public string? NombreRealizaKardex { get; set; }
}

public class CensoAtencionViewModel
{
    public long EpisodioId { get; set; }

    public string Programa { get; set; } = string.Empty;

    /// <summary>Orden cronológico dentro del programa, empezando en 1.</summary>
    public int Numero { get; set; }

    public long? RegistroId { get; set; }

    // ----- Recepción de este ingreso -----
    // Van aquí, y no solo en la atención seleccionada, porque la pantalla tiene que poder mostrar
    // la recepción de los ingresos anteriores sin volver a la base.
    public DateTime? FechaIngresoRecepcion { get; set; }

    public TimeSpan? HoraIngresoRecepcion { get; set; }

    public DateTime? FechaRespuesta { get; set; }

    public TimeSpan? HoraRespuesta { get; set; }

    public int? IndicadorTiempoRespuestaMinutos { get; set; }

    public string? NombreRecepcionaCaso { get; set; }

    public string? NombreRealizaKardex { get; set; }

    /// <summary>
    /// Si este ingreso tiene recepción registrada. Es falso en un ingreso recién abierto y en las
    /// atenciones del histórico anterior al traslado, donde no se pudo recuperar: la pantalla lo
    /// dice con todas sus letras en vez de mostrar campos vacíos, que se leen como un dato borrado.
    /// </summary>
    public bool TieneRecepcion =>
        FechaIngresoRecepcion.HasValue
        || FechaRespuesta.HasValue
        || !string.IsNullOrWhiteSpace(NombreRecepcionaCaso)
        || !string.IsNullOrWhiteSpace(NombreRealizaKardex);

    /// <summary>Fecha de ingreso al programa; la del episodio mientras no haya registro.</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Fecha del alta o egreso. Nula mientras la atención siga abierta.</summary>
    public DateTime? Hasta { get; set; }

    public bool Abierta { get; set; }

    /// <summary>Estado o motivo con el que se cerró, tal como lo guardó su censo.</summary>
    public string? Estado { get; set; }

    public bool EsSeleccionada { get; set; }

    /// <summary>Episodio sin registro: el programa se agregó y nadie ha diligenciado el formulario.</summary>
    public bool SinDiligenciar => RegistroId is null;

    /// <summary>
    /// Rótulo del selector. Una atención abierta se nombra por su ingreso; una cerrada, por el
    /// tramo que duró, que es lo que permite distinguirlas de un vistazo.
    /// </summary>
    public string Rango => (Desde, Hasta) switch
    {
        (null, null) => "Sin fecha",
        ({ } d, null) => d.ToString("dd/MM/yyyy"),
        (null, { } h) => $"hasta {h:dd/MM/yyyy}",
        ({ } d, { } h) => $"{d:dd/MM/yyyy} → {h:dd/MM/yyyy}"
    };
}

/// <summary>Fila del tabulado unificado, en su juego de columnas núcleo.</summary>
public class CensoUnificadoTablaRowViewModel
{
    public string Programa { get; set; } = string.Empty;

    public long RegistroId { get; set; }

    public long? PacienteId { get; set; }

    public string NombrePaciente { get; set; } = string.Empty;

    public string TipoIdentificacion { get; set; } = string.Empty;

    public string NumeroIdentificacion { get; set; } = string.Empty;

    public DateTime? FechaIngreso { get; set; }

    public string? Estado { get; set; }

    public bool Abierto { get; set; }

    public string? Asegurador { get; set; }

    public string? ClasificacionZonaSura { get; set; }

    public string? DiagnosticoDescriptivo { get; set; }

    public string? EstadoFarmacia { get; set; }

    public bool TieneAdjuntos { get; set; }

    public bool TieneProrroga { get; set; }
}

/// <summary>Modelo de la pantalla única de censo.</summary>
public class CensoUnificadoViewModel
{
    public CensoPacienteFormViewModel Paciente { get; set; } = new();

    public IReadOnlyList<CensoProgramaChipViewModel> Programas { get; set; } = [];

    // ----- Formularios de cada programa -----
    // Se llenan solo para los programas que el paciente tiene abiertos. Son los mismos modelos que
    // usaban las pantallas independientes, así que sus formularios siguen enviando a las mismas
    // acciones y toda su lógica —kardex, requisiciones, farmacia y prórrogas— queda intacta.
    public CensoReceptionViewModel? Agudos { get; set; }

    public CensoCronicoViewModel? Cronicos { get; set; }

    public CensoClinicaHeridasViewModel? ClinicaHeridas { get; set; }

    public CensoNptViewModel? Npt { get; set; }

    public CensoTerapiaAmbulatoriaViewModel? TerapiaAmbulatoria { get; set; }

    /// <summary>Programa cuya pestaña se abre al cargar.</summary>
    public string? ProgramaActivo { get; set; }

    // ----- Atenciones de cada programa -----
    // Un paciente puede haber ingresado varias veces al mismo programa. Aquí van todas, abiertas
    // y cerradas, para que el panel pueda moverse entre ellas; el panel se arma con la que quedó
    // seleccionada, que es la del parámetro "atencion" y, si no viene ninguno, la abierta o —si
    // el programa no tiene ninguna abierta— la última que se cerró.

    /// <summary>Atenciones por programa, de la más antigua a la más reciente.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<CensoAtencionViewModel>> Atenciones { get; set; } =
        new Dictionary<string, IReadOnlyList<CensoAtencionViewModel>>(StringComparer.Ordinal);

    /// <summary>Programas cuyo panel está mostrando una atención ya cerrada.</summary>
    public IReadOnlySet<string> ProgramasEnSoloLectura { get; set; } =
        new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyList<CensoAtencionViewModel> AtencionesDe(string? programa) =>
        programa is not null && Atenciones.TryGetValue(programa, out var lista) ? lista : [];

    public CensoAtencionViewModel? AtencionSeleccionadaDe(string? programa) =>
        AtencionesDe(programa).FirstOrDefault(x => x.EsSeleccionada);

    /// <summary>
    /// Recepción reenviada por el formulario cuando su guardado falló. Se conserva para poder
    /// repintar lo que la persona escribió junto a sus errores, en vez de devolverle el formulario
    /// con los valores de la base y el trabajo perdido.
    /// </summary>
    public CensoRecepcionFormViewModel? RecepcionEnviada { get; set; }

    /// <summary>
    /// La recepción editable del programa: la de su atención seleccionada. Si el guardado que
    /// acaba de fallar era el de este mismo programa, devuelve lo enviado y no lo guardado.
    /// </summary>
    public CensoRecepcionFormViewModel? RecepcionDe(string? programa)
    {
        if (programa is null)
        {
            return null;
        }

        if (RecepcionEnviada is not null
            && string.Equals(RecepcionEnviada.Programa, programa, StringComparison.Ordinal))
        {
            return RecepcionEnviada;
        }

        var atencion = AtencionSeleccionadaDe(programa);
        if (atencion is null)
        {
            return null;
        }

        return new CensoRecepcionFormViewModel
        {
            EpisodioId = atencion.EpisodioId,
            Programa = programa,
            FechaIngreso = atencion.FechaIngresoRecepcion,
            HoraIngreso = atencion.HoraIngresoRecepcion,
            FechaRespuesta = atencion.FechaRespuesta,
            HoraRespuesta = atencion.HoraRespuesta,
            IndicadorTiempoRespuestaMinutos = atencion.IndicadorTiempoRespuestaMinutos,
            NombreRecepcionaCaso = atencion.NombreRecepcionaCaso,
            NombreRealizaKardex = atencion.NombreRealizaKardex
        };
    }

    /// <summary>
    /// True cuando el panel de ese programa muestra una atención cerrada. La vista lo usa para
    /// bloquear las secciones que ya no se editan; el permiso por sección lo decide
    /// <see cref="Nexa.Helpers.CensoProgramaSecciones.SeEditaTrasElAlta"/>.
    /// </summary>
    public bool EsSoloLectura(string? programa) =>
        programa is not null && ProgramasEnSoloLectura.Contains(programa);

    /// <summary>El paciente ya está guardado y por tanto se pueden agregar programas.</summary>
    public bool PacienteGuardado => Paciente.PacienteId.HasValue;

    /// <summary>
    /// Se buscó un documento y no apareció ningún paciente. No es lo mismo que entrar a la
    /// pantalla sin buscar nada: ahí no hay nada que avisar, y aquí sí.
    /// </summary>
    public bool BusquedaSinResultados { get; set; }

    /// <summary>
    /// Quien mira tiene el permiso de reapertura, el mismo que aprueba la del kardex. Sin él, el
    /// botón de reabrir una atención cerrada no se dibuja.
    /// </summary>
    public bool PuedeReabrirAtencion { get; set; }

    /// <summary>
    /// Pacientes que arrastran agudos y crónicos abiertos a la vez desde antes de la unificación.
    /// No se rompen: se muestran con aviso y no se pueden agravar.
    /// </summary>
    /// <summary>
    /// Programas abiertos que hoy serían incompatibles entre sí. Vienen de antes de que la regla
    /// existiera; se muestran para que quien atiende sepa por qué el carril no deja agregar nada.
    /// </summary>
    public IReadOnlyList<string> ProgramasEnConflicto { get; set; } = [];

    public bool TieneConflicto => ProgramasEnConflicto.Count > 0;

    // ----- Filtros del tabulado -----
    public string? CedulaFiltro { get; set; }

    public string? ProgramaFiltro { get; set; }

    public DateTime? FechaIngresoFiltroDesde { get; set; }

    public DateTime? FechaIngresoFiltroHasta { get; set; }

    public bool TieneFiltroFechaIngreso => FechaIngresoFiltroDesde.HasValue || FechaIngresoFiltroHasta.HasValue;

    public IReadOnlyList<CensoUnificadoTablaRowViewModel> Filas { get; set; } = [];

    /// <summary>
    /// Registros completos del programa filtrado. Cuando se filtra por un solo programa la tabla
    /// despliega su juego completo de columnas, igual que la pantalla propia de ese censo; con
    /// "todos los programas" solo se muestran las columnas núcleo, porque la unión literal de los
    /// cinco pasaría de seiscientas columnas.
    /// </summary>
    public IReadOnlyList<object> RegistrosDetalle { get; set; } = [];

    /// <summary>Entidad de los registros de <see cref="RegistrosDetalle"/>, para reflejar sus columnas.</summary>
    public Type? TipoDetalle { get; set; }

    /// <summary>Filas que se muestran cuando el resultado se recorta.</summary>
    public int TotalSinRecorte { get; set; }

    public int LimiteFilas { get; set; }

    public int IngresosHoyCount { get; set; }

    public IReadOnlyDictionary<string, int> ConteoPorPrograma { get; set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    // ----- Catálogos compartidos -----
    public IReadOnlyList<SelectListItem> TipoIdentificacionOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> GeneroOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> ClasificacionZonaSuraOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> MunicipioResidenciaOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> ZonaDireccionOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> AreaOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> IpsQueRemiteOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> VistoBuenoOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> AseguradorOptions { get; set; } = [];

    public IReadOnlyList<SelectListItem> NursingAssistantOptions { get; set; } = [];

    public IReadOnlyList<string> BarrioOptions { get; set; } = [];
}
