namespace Nexa.Models.ViewModels;

/// <summary>
/// Hoja de vida del paciente: todo lo que la IPS sabe de él, armado de solo lectura desde los cinco
/// censos y el Portal Administrativo. No guarda nada: se calcula en cada consulta, así que una
/// atención en curso se ve con su estado del momento.
/// </summary>
public class HojaVidaPacienteViewModel
{
    /// <summary>Documento tal como quedó tras normalizarlo (solo letras y dígitos, en mayúsculas).</summary>
    public string? Documento { get; set; }

    public bool Consultado { get; set; }

    public string? ErrorBusqueda { get; set; }

    public bool Encontrado { get; set; }

    /// <summary>Hora de Colombia en que se armó la hoja: lo que muestra es el estado de ese momento.</summary>
    public DateTime ConsultadoEn { get; set; }

    public HojaVidaIdentidad Identidad { get; set; } = new();

    public HojaVidaEstadoGeneral Estado { get; set; } = new();

    public IReadOnlyList<HojaVidaFrase> Resumen { get; set; } = [];

    /// <summary>Todos los ingresos del paciente, del más reciente al más antiguo.</summary>
    public IReadOnlyList<HojaVidaIngreso> Ingresos { get; set; } = [];

    public HojaVidaLineaDeVida Linea { get; set; } = new();

    public IReadOnlyList<HojaVidaAlerta> Alertas { get; set; } = [];

    public IReadOnlyList<HojaVidaCifra> Cifras { get; set; } = [];

    public IReadOnlyList<HojaVidaConteo> MedicamentosFrecuentes { get; set; } = [];

    public IReadOnlyList<HojaVidaDiagnostico> Diagnosticos { get; set; } = [];

    public IReadOnlyList<HojaVidaNovedad> Novedades { get; set; } = [];

    public IReadOnlyList<HojaVidaRonda> Rondas { get; set; } = [];

    /// <summary>Si el Portal Administrativo no respondió, el motivo en palabras de usuario.</summary>
    public string? AvisoPortal { get; set; }

    /// <summary>Si los seguimientos de clínica de heridas no se pudieron leer, el motivo.</summary>
    public string? AvisoSeguimientosHeridas { get; set; }
}

public class HojaVidaIdentidad
{
    public string Nombre { get; set; } = string.Empty;

    public string TipoDocumento { get; set; } = string.Empty;

    public string NumeroDocumento { get; set; } = string.Empty;

    public DateTime? FechaNacimiento { get; set; }

    public int? Edad { get; set; }

    public string? Genero { get; set; }

    public string? Asegurador { get; set; }

    public string? Direccion { get; set; }

    public string? DetalleDireccion { get; set; }

    public string? Barrio { get; set; }

    public string? Municipio { get; set; }

    public string? Zona { get; set; }

    public string? Correo { get; set; }

    public IReadOnlyList<string> Telefonos { get; set; } = [];

    public string? IpsQueRemite { get; set; }

    /// <summary>Diagnóstico de la atención en curso (o de la última); lo primero que se pregunta.</summary>
    public string? DiagnosticoActual { get; set; }

    public string? Cie10Actual { get; set; }

    /// <summary>False cuando no existe el maestro y los datos se tomaron del registro más reciente.</summary>
    public bool DesdeMaestro { get; set; }
}

public enum HojaVidaSituacion
{
    /// <summary>Atención abierta: el paciente la está recibiendo.</summary>
    EnCurso,

    /// <summary>Atención terminada con alta o egreso.</summary>
    Cerrada,

    /// <summary>Solicitud cancelada o rechazada: nunca se prestó.</summary>
    NoEfectiva,

    /// <summary>Programa asignado en el censo cuyo formulario nadie ha guardado.</summary>
    SinDiligenciar
}

public class HojaVidaEstadoGeneral
{
    public bool Activo { get; set; }

    /// <summary>Paciente con registro de fallecimiento en un egreso o en una novedad.</summary>
    public bool Fallecido { get; set; }

    public DateTime? FechaFallecimiento { get; set; }

    /// <summary>Rótulo corto: "Activo", "Inactivo", "Fallecido", "Sin ingresos efectivos".</summary>
    public string Rotulo { get; set; } = string.Empty;

    /// <summary>Nombres de los programas con atención en curso, por jerarquía.</summary>
    public IReadOnlyList<string> ProgramasEnCurso { get; set; } = [];

    public DateTime? PrimerIngreso { get; set; }

    public DateTime? UltimaAlta { get; set; }

    /// <summary>Días de atención sin contar dos veces los días en que tuvo dos programas a la vez.</summary>
    public int DiasTotales { get; set; }

    public int IngresosEfectivos { get; set; }
}

/// <summary>Una frase del resumen, partida en trozos para resaltar las cifras sin armar HTML.</summary>
public class HojaVidaFrase
{
    public IReadOnlyList<HojaVidaTrozo> Trozos { get; set; } = [];
}

public record HojaVidaTrozo(string Texto, bool Resaltado = false);

public class HojaVidaIngreso
{
    /// <summary>Identificador para el ancla de la página ("agudos-123").</summary>
    public string Clave { get; set; } = string.Empty;

    public string Programa { get; set; } = string.Empty;

    public string ProgramaNombre { get; set; } = string.Empty;

    /// <summary>Sufijo de la clase CSS del programa: agudos, cronicos, heridas, npt, terapia.</summary>
    public string ProgramaClase { get; set; } = string.Empty;

    public long? RegistroId { get; set; }

    /// <summary>Número de este ingreso dentro del programa (1 = el primero).</summary>
    public int NumeroEnPrograma { get; set; }

    public int TotalEnPrograma { get; set; }

    public HojaVidaSituacion Situacion { get; set; }

    /// <summary>El estado tal como está escrito en el censo.</summary>
    public string? EstadoCenso { get; set; }

    public DateTime? FechaIngreso { get; set; }

    public DateTime? FechaAlta { get; set; }

    /// <summary>
    /// Cuando la atención está cerrada pero nadie escribió la fecha de alta, se usa la de fin del
    /// tratamiento y se dice que es estimada.
    /// </summary>
    public bool FechaAltaEstimada { get; set; }

    public int? DiasEstancia { get; set; }

    public string? MotivoAlta { get; set; }

    public string? QuienGestionaAlta { get; set; }

    public string? Cie10 { get; set; }

    public string? Diagnostico { get; set; }

    public string? Asegurador { get; set; }

    /// <summary>Escalas de valoración con su lectura en palabras (Barthel, Braden, Morse…).</summary>
    public IReadOnlyList<HojaVidaEscala> Escalas { get; set; } = [];

    /// <summary>Controles con fecha de vencimiento: cambio de sonda, curación del catéter…</summary>
    public IReadOnlyList<HojaVidaControl> Controles { get; set; } = [];

    /// <summary>Datos propios del programa que no tienen sección aparte (clasificación, fuente…).</summary>
    public IReadOnlyList<HojaVidaDato> Datos { get; set; } = [];

    /// <summary>Medicamentos pactados al ingreso.</summary>
    public IReadOnlyList<HojaVidaMedicamento> Medicamentos { get; set; } = [];

    public IReadOnlyList<HojaVidaProrroga> Prorrogas { get; set; } = [];

    public IReadOnlyList<HojaVidaAgudizacion> Agudizaciones { get; set; } = [];

    /// <summary>Servicios y cuidados marcados en "Sí" durante la atención.</summary>
    public IReadOnlyList<HojaVidaServicio> Servicios { get; set; } = [];

    /// <summary>Clínica de heridas: apósitos e insumos vigentes de la atención.</summary>
    public IReadOnlyList<string> Insumos { get; set; } = [];

    public IReadOnlyList<HojaVidaPlanHeridas> PlanesHeridas { get; set; } = [];

    public IReadOnlyList<HojaVidaTerapia> Terapias { get; set; } = [];

    public IReadOnlyList<HojaVidaProrrogaTerapia> ProrrogasTerapia { get; set; } = [];

    public HojaVidaNpt? Npt { get; set; }

    public IReadOnlyList<HojaVidaHospitalizacion> Hospitalizaciones { get; set; } = [];

    public IReadOnlyList<HojaVidaDespacho> Despachos { get; set; } = [];

    public HojaVidaEvolucionHerida? EvolucionHerida { get; set; }

    /// <summary>Novedades del Portal Administrativo reportadas mientras duraba esta atención.</summary>
    public IReadOnlyList<HojaVidaNovedad> Novedades { get; set; } = [];

    /// <summary>Ronda intramural que precedió a este ingreso, si la hubo.</summary>
    public HojaVidaRonda? Ronda { get; set; }

    public bool TieneDetalle =>
        Medicamentos.Count > 0 || Prorrogas.Count > 0 || Agudizaciones.Count > 0 || Servicios.Count > 0
        || Insumos.Count > 0 || PlanesHeridas.Count > 0 || Terapias.Count > 0 || Npt is not null
        || Hospitalizaciones.Count > 0 || Despachos.Count > 0 || EvolucionHerida is not null
        || Novedades.Count > 0 || Datos.Count > 0 || Escalas.Count > 0 || Controles.Count > 0;
}

/// <summary>
/// Una escala de valoración con su lectura: el número solo no dice nada a quien no la conoce, así
/// que siempre viaja con lo que significa ("Barthel 35 de 100 · dependencia grave").
/// </summary>
public record HojaVidaEscala(string Nombre, string Valor, string? Interpretacion, string? Nivel = null)
{
    /// <summary>Parte del máximo de la escala, 0 a 1, para dibujar la barra. Null si no es puntaje.</summary>
    public double? Porcion { get; init; }
}

/// <summary>Un control con fecha: última vez que se hizo y cuándo toca el siguiente.</summary>
public record HojaVidaControl(string Nombre, DateTime? Ultimo, DateTime? Proximo, string? Detalle = null)
{
    /// <summary>Días que faltan (negativo si ya pasó). Solo tiene sentido en atenciones en curso.</summary>
    public int? DiasParaProximo { get; init; }

    public bool Vencido => DiasParaProximo is < 0;
}

public record HojaVidaDato(string Etiqueta, string Valor);

public class HojaVidaMedicamento
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Dosis con su unidad, p. ej. "1 g".</summary>
    public string? Dosis { get; set; }

    public string? Via { get; set; }

    public string? Frecuencia { get; set; }

    /// <summary>Días de tratamiento pactados para este medicamento.</summary>
    public int? Dias { get; set; }
}

public class HojaVidaProrroga
{
    public int Numero { get; set; }

    public string? Tipo { get; set; }

    public int? DiasExtension { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public IReadOnlyList<HojaVidaMedicamento> Medicamentos { get; set; } = [];

    public DateTime? RegistradaEl { get; set; }
}

public class HojaVidaAgudizacion
{
    public int Numero { get; set; }

    public string? Cie10 { get; set; }

    public string? Diagnostico { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }

    public IReadOnlyList<HojaVidaMedicamento> Medicamentos { get; set; } = [];

    public string? EstadoFarmacia { get; set; }
}

public record HojaVidaServicio(string Nombre, string? Detalle = null);

public class HojaVidaPlanHeridas
{
    public int Numero { get; set; }

    public DateTime AbiertoEl { get; set; }

    public DateTime? CerradoEl { get; set; }

    public IReadOnlyList<string> Insumos { get; set; } = [];

    public int? DuracionDias { get; set; }

    public string? FrecuenciaVisita { get; set; }

    public IReadOnlyList<HojaVidaDespacho> Requisiciones { get; set; } = [];
}

public record HojaVidaTerapia(string Tipo, int? Cantidad, string? Frecuencia);

public class HojaVidaProrrogaTerapia
{
    public string Tipo { get; set; } = string.Empty;

    public string? Cantidad { get; set; }

    public int? Frecuencia { get; set; }

    public DateTime? FechaSolicitud { get; set; }

    public DateTime? FechaAutorizacion { get; set; }

    public string? CodigoAutorizacion { get; set; }
}

public class HojaVidaNpt
{
    public DateTime? Inicio { get; set; }

    public DateTime? Fin { get; set; }

    public int? Dias { get; set; }

    /// <summary>Días corridos desde el inicio cuando la nutrición sigue en curso sin fecha fin.</summary>
    public bool EnCurso { get; set; }

    public string? Conexion { get; set; }

    public string? Desconexion { get; set; }

    public string? Picc { get; set; }
}

public class HojaVidaHospitalizacion
{
    public DateTime? Fecha { get; set; }

    public DateTime? FechaAlta { get; set; }

    public string? Motivo { get; set; }

    public string? Ips { get; set; }

    public string? RemitidoPor { get; set; }

    public string? Detalle { get; set; }

    /// <summary>Agudos lo registra como rehospitalización durante la atención domiciliaria.</summary>
    public bool EsRehospitalizacion { get; set; }
}

public class HojaVidaDespacho
{
    /// <summary>Qué se envió: "Kardex del ingreso", "Prórroga 2", "Requisición VAC"…</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>Estado en farmacia en palabras de usuario.</summary>
    public string Estado { get; set; } = string.Empty;

    public bool Entregado { get; set; }

    public DateTime? EnviadoEl { get; set; }
}

public class HojaVidaEvolucionHerida
{
    public IReadOnlyList<HojaVidaSeguimientoHerida> Seguimientos { get; set; } = [];

    /// <summary>Área del primer seguimiento con medidas, en cm² (largo × ancho).</summary>
    public double? AreaInicial { get; set; }

    public double? AreaUltima { get; set; }

    /// <summary>Cambio porcentual del área entre el primer y el último seguimiento.</summary>
    public double? CambioPorcentual { get; set; }

    /// <summary>Puntos del trazo "x,y" ya escalados a un lienzo de 120 × 32.</summary>
    public string? Trazo { get; set; }
}

public class HojaVidaSeguimientoHerida
{
    public int Numero { get; set; }

    public DateTime Fecha { get; set; }

    public string? Ubicacion { get; set; }

    public double Largo { get; set; }

    public double Ancho { get; set; }

    public double Profundidad { get; set; }

    public double Area => Math.Round(Largo * Ancho, 1);

    public string? Tejido { get; set; }

    public string? Exudado { get; set; }

    public string? Auxiliar { get; set; }

    public string? FotoDriveItemId { get; set; }
}

public class HojaVidaNovedad
{
    public string Id { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public DateTime UltimaGestion { get; set; }

    public string Categoria { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    /// <summary>Valor del enum en el portal (FALLECIMIENTO…), para las reglas; Tipo es el rótulo.</summary>
    public string TipoCodigo { get; set; } = string.Empty;

    public bool Resuelta { get; set; }

    public string Prioridad { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public string? Respuesta { get; set; }

    public string? Responsable { get; set; }

    public string? ReportadoPor { get; set; }

    public IReadOnlyList<string> Medicamentos { get; set; } = [];

    /// <summary>Ingreso durante el cual se reportó, si cae dentro de alguno.</summary>
    public string? IngresoClave { get; set; }

    public string? IngresoNombre { get; set; }
}

public class HojaVidaRonda
{
    public string Id { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public DateTime FechaIngresoIps { get; set; }

    public string Ips { get; set; } = string.Empty;

    public string? Cie10 { get; set; }

    public string? Diagnostico { get; set; }

    public bool? IngresoEfectivo { get; set; }

    public string? CausaNoIngreso { get; set; }

    public string? Observacion { get; set; }

    public string? ReportadoPor { get; set; }

    public IReadOnlyList<string> Medicamentos { get; set; } = [];

    public string? IngresoClave { get; set; }

    public string? IngresoNombre { get; set; }
}

public class HojaVidaAlerta
{
    /// <summary>critica, atencion o info.</summary>
    public string Nivel { get; set; } = "info";

    /// <summary>Clase de Bootstrap Icons, p. ej. "bi-exclamation-octagon".</summary>
    public string Icono { get; set; } = "bi-info-circle";

    public string Titulo { get; set; } = string.Empty;

    public string? Detalle { get; set; }
}

public record HojaVidaCifra(string Etiqueta, string Valor, string? Nota = null);

public record HojaVidaConteo(string Nombre, int Veces);

public record HojaVidaDiagnostico(string Codigo, string? Descripcion, int Veces);

public class HojaVidaLineaDeVida
{
    public DateTime Desde { get; set; }

    public DateTime Hasta { get; set; }

    public IReadOnlyList<HojaVidaCarril> Carriles { get; set; } = [];

    public IReadOnlyList<HojaVidaMarca> Marcas { get; set; } = [];

    /// <summary>Bandas del eje: un mes (o un año en historias largas) cada una.</summary>
    public IReadOnlyList<HojaVidaBanda> Eje { get; set; } = [];

    /// <summary>Cuántas filas necesita el carril de eventos para que no se tapen entre sí.</summary>
    public int FilasEventos { get; set; }

    public double HoyPct { get; set; }

    public bool TieneDatos => Carriles.Count > 0;
}

public class HojaVidaCarril
{
    public string ProgramaNombre { get; set; } = string.Empty;

    public string ProgramaClase { get; set; } = string.Empty;

    public IReadOnlyList<HojaVidaBarra> Barras { get; set; } = [];
}

public class HojaVidaBarra
{
    public string IngresoClave { get; set; } = string.Empty;

    public double InicioPct { get; set; }

    public double AnchoPct { get; set; }

    public HojaVidaSituacion Situacion { get; set; }

    /// <summary>Descripción completa para lectores de pantalla y para el globo de ayuda.</summary>
    public string Etiqueta { get; set; } = string.Empty;
}

public class HojaVidaMarca
{
    /// <summary>novedad, hospitalizacion o ronda.</summary>
    public string Tipo { get; set; } = string.Empty;

    public double Pct { get; set; }

    public string Etiqueta { get; set; } = string.Empty;

    public bool Pendiente { get; set; }

    /// <summary>Fila dentro del carril de eventos: evita que dos marcas del mismo día se tapen.</summary>
    public int Fila { get; set; }
}

/// <summary>
/// Una banda del eje del tiempo. Se pintan alternadas para que el ojo separe un mes del siguiente
/// sin tener que leer las fechas.
/// </summary>
public record HojaVidaBanda(double InicioPct, double AnchoPct, string Texto, string? Anio, bool Alterna)
{
    /// <summary>La banda es muy angosta para su rótulo: se pinta, pero sin texto.</summary>
    public bool SinEtiqueta { get; init; }
}
