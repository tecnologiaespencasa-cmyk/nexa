using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Models;
using Nexa.Helpers;
using Nexa.Models.ViewModels;

namespace Nexa.Services;

/// <summary>
/// Lo que la hoja de vida deduce de los ingresos: estado general, tiempo con la IPS, cruces con el
/// Portal Administrativo, alertas, cifras y la línea de vida.
/// </summary>
public partial class HojaVidaPacienteService
{
    /// <summary>
    /// Volver dentro de este plazo después de un alta se señala como reingreso temprano: es el
    /// indicador habitual de reingreso en atención domiciliaria.
    /// </summary>
    private const int DiasReingresoTemprano = 30;

    /// <summary>Holgura para asociar una novedad reportada justo después del alta a esa atención.</summary>
    private const int DiasGraciaNovedadTrasAlta = 3;

    private static readonly string[] Dispositivos =
    [
        "Catéter PICC", "Catéter PICC/CC", "Terapia de presión negativa (VAC)", "Sonda vesical",
        "Cateterismo o sonda vesical", "Sonda nasogástrica", "Traqueostomía", "Gastrostomía",
        "Colostomía", "Cistostomía"
    ];

    private static readonly string[] CuidadosEspeciales =
        ["Paciente anticoagulado", "Paciente gestante", "Aislamiento"];

    private static bool EsEfectivo(HojaVidaIngreso ingreso) =>
        ingreso.Situacion is HojaVidaSituacion.EnCurso or HojaVidaSituacion.Cerrada;

    // ------------------------------------------------------------------------------------------
    // Cruces con el Portal Administrativo
    // ------------------------------------------------------------------------------------------

    private static IReadOnlyList<HojaVidaNovedad> ConstruirNovedades(
        IReadOnlyList<PortalNovedadPacienteRow> filas,
        List<HojaVidaIngreso> ingresos,
        DateTime ahora)
    {
        var porIngreso = new Dictionary<string, List<HojaVidaNovedad>>(StringComparer.Ordinal);
        var novedades = new List<HojaVidaNovedad>();

        foreach (var fila in filas)
        {
            var novedad = new HojaVidaNovedad
            {
                Id = fila.Id,
                Fecha = ColombiaTime.Convert(fila.CreatedAtUtc),
                UltimaGestion = ColombiaTime.Convert(fila.UpdatedAtUtc),
                Categoria = HojaVidaFormato.Categoria(fila.Categoria),
                Tipo = HojaVidaFormato.TipoNovedad(fila.Tipo),
                TipoCodigo = fila.Tipo,
                Resuelta = string.Equals(fila.Estado, "RESUELTA", StringComparison.OrdinalIgnoreCase),
                Prioridad = fila.Prioridad,
                Descripcion = fila.Descripcion.Trim(),
                Respuesta = Texto(fila.RespuestaPrestador),
                Responsable = Texto(HojaVidaFormato.Responsable(fila.ResponsableGestion)) ?? Texto(fila.AsignadoA),
                ReportadoPor = Unir(fila.PrestadorNombre, HojaVidaFormato.Profesion(fila.PrestadorProfesion)),
                Medicamentos = fila.Medicamentos
            };

            // Una novedad resuelta se cuenta hasta su última gestión; una abierta, hasta ahora.
            var hasta = novedad.Resuelta ? novedad.UltimaGestion : ahora;
            novedad.MinutosTranscurridos = (int)Math.Max((hasta - novedad.Fecha).TotalMinutes, 0);

            // Una novedad de heridas o de terapias pertenece a ese programa aunque el paciente
            // tuviera otro abierto el mismo día; las demás, al programa base.
            var preferido = fila.EsClinicaHeridas
                ? CensoProgramas.ClinicaHeridas
                : string.Equals(fila.Categoria, "TERAPIAS_AMBULATORIAS", StringComparison.Ordinal)
                    ? CensoProgramas.TerapiaAmbulatoria
                    : null;

            if (IngresoEnFecha(ingresos, novedad.Fecha.Date, ahora.Date, preferido) is { } ingreso)
            {
                novedad.IngresoClave = ingreso.Clave;
                novedad.IngresoNombre = NombreIngreso(ingreso);
                if (!porIngreso.TryGetValue(ingreso.Clave, out var lista))
                {
                    lista = [];
                    porIngreso[ingreso.Clave] = lista;
                }

                lista.Add(novedad);
            }

            novedades.Add(novedad);
        }

        foreach (var ingreso in ingresos)
        {
            if (porIngreso.TryGetValue(ingreso.Clave, out var lista))
            {
                ingreso.Novedades = lista;
            }
        }

        return novedades;
    }

    /// <summary>
    /// La atención que estaba en curso ese día. Si había varias (un adicional sobre un base), gana
    /// el programa preferido y luego el base.
    /// </summary>
    private static HojaVidaIngreso? IngresoEnFecha(
        IEnumerable<HojaVidaIngreso> ingresos,
        DateTime dia,
        DateTime hoy,
        string? programaPreferido)
    {
        return ingresos
            .Where(i => i.Situacion != HojaVidaSituacion.NoEfectiva && i.FechaIngreso is { } inicio
                && dia >= inicio.AddDays(-1)
                && dia <= (i.Situacion == HojaVidaSituacion.Cerrada ? i.FechaAlta ?? inicio : hoy).AddDays(DiasGraciaNovedadTrasAlta))
            .OrderByDescending(i => programaPreferido is not null && i.Programa == programaPreferido)
            .ThenByDescending(i => CensoProgramas.EsBase(i.Programa))
            .ThenByDescending(i => i.FechaIngreso)
            .FirstOrDefault();
    }

    private static IReadOnlyList<HojaVidaRonda> ConstruirRondas(
        IReadOnlyList<PortalRondaPacienteRow> filas,
        List<HojaVidaIngreso> ingresos)
    {
        var rondas = new List<HojaVidaRonda>();

        // De la más antigua a la más reciente, para que cada ronda tome el ingreso que la siguió
        // y no el de una ronda posterior.
        foreach (var fila in filas.OrderBy(x => x.CreatedAtUtc))
        {
            var ronda = new HojaVidaRonda
            {
                Id = fila.Id,
                Fecha = ColombiaTime.Convert(fila.CreatedAtUtc),
                FechaIngresoIps = fila.FechaIngresoIps,
                Ips = fila.Ips.Trim(),
                Cie10 = Texto(fila.Cie10Codigo),
                Diagnostico = Texto(fila.DiagnosticoDescriptivo),
                IngresoEfectivo = fila.IngresoEfectivo,
                CausaNoIngreso = Texto(fila.CausaNoIngreso),
                Observacion = Texto(fila.ObservacionNoIngreso),
                ReportadoPor = Texto(fila.ReportadoPor),
                Medicamentos = fila.Medicamentos
            };

            // La ronda precede al ingreso: el médico identifica al paciente en la IPS y el correo de
            // solicitud llega en los días siguientes. Si en el portal ya dijeron que no ingresó, no
            // se asocia a nada.
            if (fila.IngresoEfectivo != false)
            {
                var dia = ronda.Fecha.Date;
                var ingreso = ingresos
                    .Where(i => i.Ronda is null && i.Situacion != HojaVidaSituacion.NoEfectiva
                        && i.FechaIngreso is { } inicio && inicio >= dia.AddDays(-2) && inicio <= dia.AddDays(20))
                    .OrderBy(i => Math.Abs((i.FechaIngreso!.Value - dia).Days))
                    .ThenByDescending(i => CensoProgramas.EsBase(i.Programa))
                    .FirstOrDefault();

                if (ingreso is not null)
                {
                    ingreso.Ronda = ronda;
                    ronda.IngresoClave = ingreso.Clave;
                    ronda.IngresoNombre = NombreIngreso(ingreso);
                }
            }

            rondas.Add(ronda);
        }

        rondas.Reverse();
        return rondas;
    }

    private static string NombreIngreso(HojaVidaIngreso ingreso) =>
        ingreso.TotalEnPrograma > 1
            ? $"{ingreso.ProgramaNombre} · ingreso {ingreso.NumeroEnPrograma}"
            : ingreso.ProgramaNombre;

    // ------------------------------------------------------------------------------------------
    // Identidad y estado
    // ------------------------------------------------------------------------------------------

    private static HojaVidaIdentidad ConstruirIdentidad(
        DatosCenso datos,
        IReadOnlyList<HojaVidaIngreso> ingresos,
        IReadOnlyList<PortalNovedadPacienteRow> novedades,
        IReadOnlyList<PortalRondaPacienteRow> rondas,
        string documento,
        DateTime hoy)
    {
        var identidad = new HojaVidaIdentidad { NumeroDocumento = documento };

        // El diagnóstico de la atención en curso; si no hay ninguna, el de la última que hubo.
        var ingresoVigente = ingresos
            .Where(i => i.Cie10 is not null || i.Diagnostico is not null)
            .OrderByDescending(i => i.Situacion == HojaVidaSituacion.EnCurso)
            .ThenBy(i => CensoProgramas.Jerarquia(i.Programa))
            .ThenByDescending(i => i.FechaIngreso ?? DateTime.MinValue)
            .FirstOrDefault();
        identidad.Cie10Actual = ingresoVigente?.Cie10;
        identidad.DiagnosticoActual = ingresoVigente?.Diagnostico;
        var m = datos.Maestro;

        // Sin maestro, los datos del registro más reciente de cualquier censo.
        var agudo = datos.Agudos.OrderByDescending(x => x.FechaIngreso).ThenByDescending(x => x.Id).FirstOrDefault();
        var herida = datos.Heridas.LastOrDefault();
        var npt = datos.Npt.OrderByDescending(x => x.FechaIngresoPrograma).FirstOrDefault();
        var cronico = datos.Cronicos.OrderByDescending(x => x.FechaIngreso).FirstOrDefault();
        var terapia = datos.Terapias.OrderByDescending(x => x.FechaIngreso).FirstOrDefault();

        identidad.DesdeMaestro = m is not null;
        identidad.Nombre = Primero(m?.NombrePaciente, agudo?.NombrePaciente, cronico?.NombrePaciente, herida?.NombrePaciente,
            npt?.NombrePaciente, terapia?.NombrePaciente,
            rondas.FirstOrDefault()?.PacienteNombre, novedades.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.PacienteNombre))?.PacienteNombre)
            ?? "Paciente sin nombre registrado";
        identidad.TipoDocumento = Primero(m?.TipoIdentificacion, agudo?.TipoIdentificacion, cronico?.TipoIdentificacion,
            herida?.TipoIdentificacion, npt?.TipoIdentificacion, terapia?.TipoIdentificacion) ?? string.Empty;
        identidad.NumeroDocumento = Primero(m?.NumeroIdentificacion, agudo?.NumeroIdentificacion, cronico?.NumeroIdentificacion,
            herida?.NumeroIdentificacion, npt?.NumeroIdentificacion, terapia?.NumeroIdentificacion) ?? documento;

        identidad.FechaNacimiento = FechaValida(m?.FechaNacimiento) ?? FechaValida(agudo?.FechaNacimiento)
            ?? FechaValida(cronico?.FechaNacimiento) ?? FechaValida(herida?.FechaNacimiento)
            ?? FechaValida(npt?.FechaNacimiento) ?? FechaValida(terapia?.FechaNacimiento);
        if (identidad.FechaNacimiento is { } nacimiento && nacimiento <= hoy)
        {
            // La edad guardada se congela el día que se diligenció; aquí se calcula al día de hoy.
            var edad = hoy.Year - nacimiento.Year;
            if (nacimiento.Date > hoy.AddYears(-edad))
            {
                edad--;
            }

            identidad.Edad = edad;
            identidad.EdadTexto = edad >= 1
                ? $"{edad} años"
                : MesesCumplidos(nacimiento, hoy) is var meses && meses >= 1
                    ? $"{meses} {(meses == 1 ? "mes" : "meses")}"
                    : HojaVidaFormato.Dias((hoy - nacimiento).Days);
        }

        identidad.Genero = Primero(m?.Genero, cronico?.Genero, herida?.Genero, npt?.Genero);
        identidad.Asegurador = Primero(m?.Asegurador, agudo?.Asegurador, herida?.Asegurador, npt?.Asegurador);
        identidad.IpsQueRemite = Primero(m?.IpsQueRemite, agudo?.IpsQueRemite, terapia?.IpsQueRemite);

        return identidad;
    }

    private static HojaVidaEstadoGeneral ConstruirEstadoGeneral(
        List<HojaVidaIngreso> ingresos,
        DatosCenso datos,
        IReadOnlyList<HojaVidaNovedad> novedades,
        DateTime hoy)
    {
        var estado = new HojaVidaEstadoGeneral();
        var efectivos = ingresos.Where(EsEfectivo).ToList();

        // Un programa asignado sin diligenciar cuenta como activo solo si es nuevo para el paciente,
        // la misma regla del informe de pacientes activos (ver jerarquia-informe-activos).
        var enCurso = ingresos
            .Where(i => i.Situacion == HojaVidaSituacion.EnCurso
                || (i.Situacion == HojaVidaSituacion.SinDiligenciar
                    && !ingresos.Any(o => o.Programa == i.Programa && o.Situacion == HojaVidaSituacion.Cerrada)))
            .ToList();

        estado.Activo = enCurso.Count > 0;
        estado.ProgramasEnCurso = enCurso
            .OrderBy(i => CensoProgramas.Jerarquia(i.Programa))
            .Select(i => new HojaVidaProgramaEnCurso(i.ProgramaNombre, i.ProgramaClase))
            .Distinct()
            .ToList();
        estado.IngresosEfectivos = efectivos.Count;
        estado.PrimerIngreso = efectivos.Min(i => i.FechaIngreso);
        estado.UltimaAlta = efectivos.Where(i => i.Situacion == HojaVidaSituacion.Cerrada).Max(i => i.FechaAlta);
        estado.DiasTotales = Tramos(ingresos, hoy).Sum(t => (t.Fin - t.Inicio).Days);

        var fallecimiento = EvidenciasFallecimiento(ingresos, datos, novedades)
            .OrderByDescending(e => e.Fecha ?? DateTime.MinValue)
            .FirstOrDefault();
        if (fallecimiento is not null)
        {
            estado.Fallecido = true;
            estado.FechaFallecimiento = fallecimiento.Fecha;
        }

        estado.Rotulo = estado.Activo
            ? "Activo"
            : estado.Fallecido
                ? "Fallecido"
                : efectivos.Count > 0 ? "Inactivo" : "Sin ingresos efectivos";

        return estado;
    }

    private sealed record Evidencia(DateTime? Fecha, string Fuente);

    private static IEnumerable<Evidencia> EvidenciasFallecimiento(
        List<HojaVidaIngreso> ingresos,
        DatosCenso datos,
        IReadOnlyList<HojaVidaNovedad> novedades)
    {
        foreach (var ingreso in ingresos.Where(i => i.MotivoAlta?.Contains("FALLEC", StringComparison.OrdinalIgnoreCase) == true))
        {
            yield return new Evidencia(ingreso.FechaAlta, $"el alta de {NombreIngreso(ingreso)}");
        }

        foreach (var agudo in datos.Agudos.Where(x => x.MotivoNovedadDevolucionProductos?.Contains("FALLEC", StringComparison.OrdinalIgnoreCase) == true))
        {
            yield return new Evidencia(FechaValida(agudo.FechaNovedadDevolucionProductos), "la devolución de productos de agudos");
        }

        foreach (var herida in datos.Heridas.Where(x => x.MotivoNovedadDevolucionProductos?.Contains("FALLEC", StringComparison.OrdinalIgnoreCase) == true))
        {
            yield return new Evidencia(FechaValida(herida.FechaNovedadDevolucionProductos), "la devolución de productos de clínica de heridas");
        }

        foreach (var npt in datos.Npt.Where(x => x.MotivoNovedadDevolucionProductos?.Contains("FALLEC", StringComparison.OrdinalIgnoreCase) == true))
        {
            yield return new Evidencia(FechaValida(npt.FechaNovedadDevolucionProductos), "la devolución de productos de NPT");
        }

        foreach (var novedad in novedades.Where(n => n.TipoCodigo == "FALLECIMIENTO"))
        {
            yield return new Evidencia(novedad.Fecha.Date, "una novedad del Portal Administrativo");
        }
    }

    /// <summary>
    /// Periodos continuos de atención: las atenciones que se tocan o se solapan (un adicional sobre
    /// un base, o un alta y un reingreso el mismo día) se funden en uno. Así el tiempo total no
    /// cuenta dos veces un mismo día y los huecos entre tramos son los reingresos reales.
    /// </summary>
    private static List<(DateTime Inicio, DateTime Fin)> Tramos(IEnumerable<HojaVidaIngreso> ingresos, DateTime hoy)
    {
        var intervalos = ingresos
            .Where(i => EsEfectivo(i) && i.FechaIngreso.HasValue)
            .Select(i => (Inicio: i.FechaIngreso!.Value, Fin: i.Situacion == HojaVidaSituacion.EnCurso ? hoy : i.FechaAlta))
            .Where(x => x.Fin.HasValue && x.Fin.Value >= x.Inicio)
            .Select(x => (x.Inicio, Fin: x.Fin!.Value))
            .OrderBy(x => x.Inicio)
            .ToList();

        var tramos = new List<(DateTime Inicio, DateTime Fin)>();
        foreach (var intervalo in intervalos)
        {
            if (tramos.Count > 0 && intervalo.Inicio <= tramos[^1].Fin)
            {
                var ultimo = tramos[^1];
                tramos[^1] = (ultimo.Inicio, intervalo.Fin > ultimo.Fin ? intervalo.Fin : ultimo.Fin);
            }
            else
            {
                tramos.Add(intervalo);
            }
        }

        return tramos;
    }

    // ------------------------------------------------------------------------------------------
    // Resumen en palabras
    // ------------------------------------------------------------------------------------------

    private static IReadOnlyList<HojaVidaFrase> ConstruirResumen(HojaVidaPacienteViewModel model, DateTime hoy)
    {
        var frases = new List<HojaVidaFrase>();
        var estado = model.Estado;
        var ingresos = model.Ingresos;

        if (estado.Activo)
        {
            // Cada programa con su propia fecha de ingreso. Antes se tomaba la más antigua de todos
            // y la frase se la atribuía a los dos: decía que el paciente estaba en agudos desde el
            // día en que entró a crónicos.
            // Se nombra cada programa una sola vez, con la fecha de la atención más antigua que
            // sigue abierta: un paciente con dos registros abiertos del mismo programa hacía que la
            // frase lo repitiera ("activo en Clínica de heridas, en Clínica de heridas y en...").
            var enCurso = ingresos
                .Where(i => i.Situacion is HojaVidaSituacion.EnCurso or HojaVidaSituacion.SinDiligenciar)
                .GroupBy(i => i.Programa)
                .OrderBy(g => CensoProgramas.Jerarquia(g.Key))
                .Select(g => new
                {
                    Nombre = g.First().ProgramaNombre,
                    Desde = g.Where(i => i.FechaIngreso is not null)
                        .Select(i => i.FechaIngreso!.Value)
                        .DefaultIfEmpty(DateTime.MinValue)
                        .Min(),
                    Abiertas = g.Count()
                })
                .ToList();

            var trozos = new List<HojaVidaTrozo> { new("Está activo en ") };
            for (var i = 0; i < enCurso.Count; i++)
            {
                if (i > 0)
                {
                    trozos.Add(new(i == enCurso.Count - 1 ? " y en " : ", en "));
                }

                var programa = enCurso[i];
                trozos.Add(new(programa.Nombre, true));

                var notas = new List<string>();
                if (programa.Desde != DateTime.MinValue)
                {
                    trozos.Add(new(" desde el "));
                    trozos.Add(new(HojaVidaFormato.FechaLarga(programa.Desde), true));
                    notas.Add(HojaVidaFormato.Dias((hoy - programa.Desde).Days).ToLowerInvariant());
                }

                if (programa.Abiertas > 1)
                {
                    notas.Add($"{programa.Abiertas} atenciones abiertas");
                }

                if (notas.Count > 0)
                {
                    trozos.Add(new($" ({string.Join(", ", notas)})"));
                }
            }

            trozos.Add(new("."));
            frases.Add(new HojaVidaFrase { Trozos = trozos });
        }
        else if (estado.Fallecido)
        {
            frases.Add(Frase(("Tiene registro de fallecimiento", false),
                (estado.FechaFallecimiento is { } f ? " del " : string.Empty, false),
                (estado.FechaFallecimiento is { } f2 ? HojaVidaFormato.FechaLarga(f2) : string.Empty, true),
                (". No tiene atenciones en curso.", false)));
        }
        else if (estado.UltimaAlta is { } alta)
        {
            var ultima = ingresos
                .Where(i => i.Situacion == HojaVidaSituacion.Cerrada && i.FechaAlta == alta)
                .OrderBy(i => CensoProgramas.Jerarquia(i.Programa))
                .First();
            frases.Add(Frase(("No tiene atenciones en curso. Su última alta fue el ", false),
                (HojaVidaFormato.FechaLarga(alta), true),
                (", en ", false),
                (ultima.ProgramaNombre, true),
                (ultima.FechaAltaEstimada ? " (fecha estimada)." : ".", false)));
        }
        else if (estado.IngresosEfectivos == 0)
        {
            frases.Add(Frase(("No tiene ingresos efectivos registrados en el censo.", false)));
        }
        else
        {
            frases.Add(Frase(("No tiene atenciones en curso.", false)));
        }

        if (estado.IngresosEfectivos > 0 && estado.PrimerIngreso is { } primero)
        {
            frases.Add(estado.IngresosEfectivos == 1
                ? Frase((estado.Activo ? "En total lleva " : "En total estuvo ", false), (HojaVidaFormato.Dias(estado.DiasTotales).ToLowerInvariant(), true),
                    (" en atención domiciliaria, en un solo ingreso que empezó en ", false),
                    (HojaVidaFormato.MesAnio(primero), true), (".", false))
                : Frase(("En total ha pasado ", false), (HojaVidaFormato.Dias(estado.DiasTotales).ToLowerInvariant(), true),
                    (" en atención domiciliaria, repartidos en ", false), ($"{estado.IngresosEfectivos} ingresos", true),
                    (" desde ", false), (HojaVidaFormato.MesAnio(primero), true), (".", false)));
        }

        var pendientes = model.Novedades.Count(n => !n.Resuelta);
        if (pendientes > 0)
        {
            frases.Add(Frase(("Tiene ", false),
                (pendientes == 1 ? "1 novedad sin resolver" : $"{pendientes} novedades sin resolver", true),
                (" en el Portal Administrativo.", false)));
        }
        else if (model.Novedades.Count > 0)
        {
            frases.Add(Frase((model.Novedades.Count == 1
                ? "Su única novedad del Portal Administrativo está resuelta."
                : $"Sus {model.Novedades.Count} novedades del Portal Administrativo están resueltas.", false)));
        }

        if (model.Rondas.FirstOrDefault(r => r.IngresoClave is not null) is { } ronda)
        {
            frases.Add(Frase(("Llegó a nosotros por una ronda intramural en ", false), (ronda.Ips, true), (".", false)));
        }

        return frases;
    }

    private static HojaVidaFrase Frase(params (string Texto, bool Resaltado)[] trozos) => new()
    {
        Trozos = trozos.Where(t => t.Texto.Length > 0).Select(t => new HojaVidaTrozo(t.Texto, t.Resaltado)).ToList()
    };

    private static string UnirLista(IReadOnlyList<string> elementos) => elementos.Count switch
    {
        0 => string.Empty,
        1 => elementos[0],
        _ => string.Join(", ", elementos.Take(elementos.Count - 1)) + " y " + elementos[^1]
    };

    // ------------------------------------------------------------------------------------------
    // Alertas
    // ------------------------------------------------------------------------------------------

    private static IReadOnlyList<HojaVidaAlerta> ConstruirAlertas(HojaVidaPacienteViewModel model, DatosCenso datos, DateTime hoy)
    {
        var alertas = new List<HojaVidaAlerta>();
        var ingresos = model.Ingresos;
        var enCurso = ingresos.Where(i => i.Situacion == HojaVidaSituacion.EnCurso).ToList();

        if (model.Estado.Fallecido)
        {
            var evidencia = EvidenciasFallecimiento(ingresos.ToList(), datos, model.Novedades)
                .OrderByDescending(e => e.Fecha ?? DateTime.MinValue)
                .First();
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "critica",
                Icono = "bi-person-x",
                Titulo = "Tiene registro de fallecimiento",
                Detalle = $"Según {evidencia.Fuente}{(evidencia.Fecha is { } f ? $", el {HojaVidaFormato.Fecha(f)}" : string.Empty)}."
                    + (model.Estado.Activo ? " Aún aparece con atenciones en curso: conviene verificar el alta." : string.Empty)
            });
        }

        foreach (var alergia in model.Novedades.Where(n => n.TipoCodigo == "PROBABLE_REACCION_ALERGICA").Take(2))
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "critica",
                Icono = "bi-exclamation-octagon",
                Titulo = "Probable reacción alérgica reportada",
                Detalle = alergia.Medicamentos.Count > 0
                    ? $"{HojaVidaFormato.Fecha(alergia.Fecha)} · {string.Join(", ", alergia.Medicamentos)}"
                    : HojaVidaFormato.Fecha(alergia.Fecha)
            });
        }

        var pendientes = model.Novedades.Where(n => !n.Resuelta).OrderBy(n => n.Fecha).ToList();
        if (pendientes.Count > 0)
        {
            var antigua = pendientes[0];
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "atencion",
                Icono = "bi-bell",
                Titulo = pendientes.Count == 1 ? "1 novedad sin resolver" : $"{pendientes.Count} novedades sin resolver",
                Detalle = $"{(pendientes.Count == 1 ? "Reportada" : "La más antigua es")} del {HojaVidaFormato.Fecha(antigua.Fecha)}: {antigua.Tipo.ToLowerInvariant()}. Lleva {HojaVidaFormato.Dias((hoy - antigua.Fecha.Date).Days).ToLowerInvariant()} abierta."
            });
        }

        var basesAbiertos = enCurso.Where(i => CensoProgramas.EsBase(i.Programa)).Select(i => i.ProgramaNombre).Distinct().ToList();
        if (basesAbiertos.Count > 1)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "atencion",
                Icono = "bi-intersect",
                Titulo = "Dos programas base abiertos a la vez",
                Detalle = $"{UnirLista(basesAbiertos)} no deberían estar abiertos al mismo tiempo."
            });
        }

        // Dos registros del mismo programa sin alta suelen ser un duplicado o un reingreso al que
        // nadie le cerró el anterior. Quien lee la hoja necesita saberlo antes de sacar conclusiones.
        var repetidos = enCurso
            .GroupBy(i => i.Programa)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.First().ProgramaNombre} ({g.Count()} atenciones)")
            .ToList();
        if (repetidos.Count > 0)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "atencion",
                Icono = "bi-layers-half",
                Titulo = repetidos.Count == 1
                    ? "Un programa con más de una atención abierta"
                    : "Varios programas con más de una atención abierta",
                Detalle = $"{UnirLista(repetidos)} sin fecha de alta. Puede ser un registro duplicado o un reingreso al que no se le cerró el anterior."
            });
        }

        var cuidados = enCurso.SelectMany(i => i.Servicios)
            .Where(s => CuidadosEspeciales.Contains(s.Nombre))
            .Select(s => s.Detalle is null ? s.Nombre : $"{s.Nombre} ({s.Detalle})")
            .Distinct()
            .ToList();
        if (cuidados.Count > 0)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "atencion",
                Icono = "bi-shield-exclamation",
                Titulo = "Cuidados especiales en la atención actual",
                Detalle = string.Join(" · ", cuidados)
            });
        }

        var tramos = Tramos(ingresos, hoy);
        for (var i = tramos.Count - 1; i > 0; i--)
        {
            var dias = (tramos[i].Inicio - tramos[i - 1].Fin).Days;
            if (dias <= DiasReingresoTemprano)
            {
                alertas.Add(new HojaVidaAlerta
                {
                    Nivel = "atencion",
                    Icono = "bi-arrow-repeat",
                    Titulo = $"Reingresó a los {HojaVidaFormato.Dias(dias).ToLowerInvariant()} de un alta",
                    Detalle = $"Alta el {HojaVidaFormato.Fecha(tramos[i - 1].Fin)} y nuevo ingreso el {HojaVidaFormato.Fecha(tramos[i].Inicio)}. Un reingreso antes de {DiasReingresoTemprano} días merece revisar la causa."
                });
                break;
            }
        }

        var hospitalizaciones = ingresos.SelectMany(i => i.Hospitalizaciones).OrderByDescending(h => h.Fecha ?? DateTime.MinValue).ToList();
        if (hospitalizaciones.Count > 0)
        {
            var ultima = hospitalizaciones[0];
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "atencion",
                Icono = "bi-hospital",
                Titulo = hospitalizaciones.Count == 1 ? "1 hospitalización registrada" : $"{hospitalizaciones.Count} hospitalizaciones registradas",
                Detalle = Unir(
                    ultima.Fecha is { } f ? $"La última, el {HojaVidaFormato.Fecha(f)}" : "La última no tiene fecha",
                    ultima.Motivo?.ToLowerInvariant())
            });
        }

        // Controles con fecha: lo que ya se venció o está por vencerse en una atención en curso.
        var controles = enCurso
            .SelectMany(i => i.Controles)
            .Where(c => c.DiasParaProximo is { } dias && dias <= 7)
            .OrderBy(c => c.DiasParaProximo)
            .ToList();
        foreach (var control in controles.Take(3))
        {
            var dias = control.DiasParaProximo!.Value;
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = dias < 0 ? "atencion" : "info",
                Icono = "bi-calendar-event",
                Titulo = dias < 0
                    ? $"{control.Nombre}: vencido hace {HojaVidaFormato.Dias(-dias).ToLowerInvariant()}"
                    : dias == 0
                        ? $"{control.Nombre}: es hoy"
                        : $"{control.Nombre}: en {HojaVidaFormato.Dias(dias).ToLowerInvariant()}",
                Detalle = Unir(
                    control.Proximo is { } proximo ? $"Programado para el {HojaVidaFormato.Fecha(proximo)}" : null,
                    control.Ultimo is { } ultimo ? $"último el {HojaVidaFormato.Fecha(ultimo)}" : null)
            });
        }

        // Escalas en nivel alto: es lo que cambia la forma de atender al paciente en la casa.
        var riesgos = enCurso
            .SelectMany(i => i.Escalas)
            .Where(e => e.Nivel == "alto" && e.Interpretacion is not null)
            .Select(e => $"{e.Interpretacion} ({e.Nombre} {e.Valor})")
            .Distinct()
            .ToList();
        if (riesgos.Count > 0)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "atencion",
                Icono = "bi-clipboard2-pulse",
                Titulo = riesgos.Count == 1 ? "Escala de valoración en nivel alto" : "Escalas de valoración en nivel alto",
                Detalle = string.Join(" · ", riesgos)
            });
        }

        var dispositivos = enCurso.SelectMany(i => i.Servicios)
            .Where(s => Dispositivos.Contains(s.Nombre))
            .Select(s => s.Detalle is null ? s.Nombre : $"{s.Nombre} ({s.Detalle.ToLowerInvariant()})")
            .Distinct()
            .ToList();
        if (dispositivos.Count > 0)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "info",
                Icono = "bi-plug",
                Titulo = "Dispositivos en la atención actual",
                Detalle = string.Join(" · ", dispositivos)
            });
        }

        var enTramite = enCurso.SelectMany(i => i.Despachos.Select(d => (i, d)))
            .Where(x => x.d.EnviadoEl.HasValue && !x.d.Entregado)
            .ToList();
        if (enTramite.Count > 0)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "info",
                Icono = "bi-capsule",
                Titulo = enTramite.Count == 1 ? "Pedido a farmacia en trámite" : $"{enTramite.Count} pedidos a farmacia en trámite",
                Detalle = string.Join(" · ", enTramite.Select(x => $"{x.d.Documento}: {x.d.Estado.ToLowerInvariant()}"))
            });
        }

        foreach (var ronda in model.Rondas.Where(r => r.IngresoEfectivo == false).Take(1))
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "info",
                Icono = "bi-signpost-split",
                Titulo = "Ronda intramural sin ingreso",
                Detalle = $"Reportado en {ronda.Ips} el {HojaVidaFormato.Fecha(ronda.Fecha)}"
                    + (ronda.CausaNoIngreso is not null ? $". Causa: {ronda.CausaNoIngreso.ToLowerInvariant()}." : ".")
            });
        }

        var estimadas = ingresos.Count(i => i.FechaAltaEstimada);
        var sinFecha = ingresos.Count(i => i.Situacion == HojaVidaSituacion.Cerrada && i.FechaAlta is null);
        if (estimadas + sinFecha > 0)
        {
            alertas.Add(new HojaVidaAlerta
            {
                Nivel = "info",
                Icono = "bi-calendar-x",
                Titulo = estimadas + sinFecha == 1 ? "1 alta sin fecha registrada" : $"{estimadas + sinFecha} altas sin fecha registrada",
                Detalle = (estimadas > 0 ? "La estancia se estimó con el último día de tratamiento. " : string.Empty)
                    + (sinFecha > 0 ? $"En {(sinFecha == 1 ? "una" : sinFecha.ToString())} no hay dato para calcularla." : string.Empty)
            });
        }

        return alertas;
    }

    // ------------------------------------------------------------------------------------------
    // Cifras
    // ------------------------------------------------------------------------------------------

    private static IReadOnlyList<HojaVidaCifra> ConstruirCifras(HojaVidaPacienteViewModel model, DateTime hoy)
    {
        var cifras = new List<HojaVidaCifra>();
        var ingresos = model.Ingresos;
        var efectivos = ingresos.Where(EsEfectivo).ToList();
        var noEfectivos = ingresos.Count(i => i.Situacion == HojaVidaSituacion.NoEfectiva);

        cifras.Add(new HojaVidaCifra(
            "Ingresos efectivos",
            efectivos.Count.ToString(),
            noEfectivos switch
            {
                0 => null,
                1 => "Además, 1 solicitud cancelada o rechazada",
                _ => $"Además, {noEfectivos} solicitudes canceladas o rechazadas"
            }));

        if (efectivos.Count == 0)
        {
            return cifras;
        }

        var sumaEstancias = efectivos.Where(i => i.DiasEstancia.HasValue).Sum(i => i.DiasEstancia!.Value);
        cifras.Add(new HojaVidaCifra(
            "Tiempo total con nosotros",
            HojaVidaFormato.Dias(model.Estado.DiasTotales),
            sumaEstancias > model.Estado.DiasTotales
                ? "Sin contar dos veces los días con dos programas a la vez"
                : model.Estado.DiasTotales >= 60 ? $"Unos {Math.Round(model.Estado.DiasTotales / 30.4)} meses" : null));

        var conEstancia = efectivos.Where(i => i.DiasEstancia.HasValue).ToList();
        if (conEstancia.Count > 1)
        {
            var larga = conEstancia.OrderByDescending(i => i.DiasEstancia).First();
            cifras.Add(new HojaVidaCifra(
                "Estancia promedio por ingreso",
                HojaVidaFormato.Dias((int)Math.Round(conEstancia.Average(i => i.DiasEstancia!.Value))),
                $"La más larga: {HojaVidaFormato.Dias(larga.DiasEstancia).ToLowerInvariant()} en {larga.ProgramaNombre.ToLowerInvariant()}"));
        }

        var tramos = Tramos(ingresos, hoy);
        if (tramos.Count > 1)
        {
            var huecos = tramos.Zip(tramos.Skip(1), (a, b) => (b.Inicio - a.Fin).Days).ToList();
            cifras.Add(new HojaVidaCifra(
                "Reingresos",
                huecos.Count.ToString(),
                $"El más rápido, a los {HojaVidaFormato.Dias(huecos.Min()).ToLowerInvariant()} del alta"));
        }

        var prorrogas = efectivos.Sum(i => i.Prorrogas.Count + i.ProrrogasTerapia.Count);
        if (prorrogas > 0)
        {
            var diasExtra = efectivos.SelectMany(i => i.Prorrogas).Sum(p => p.DiasExtension ?? 0);
            var ingresosConProrroga = efectivos.Count(i => i.Prorrogas.Count + i.ProrrogasTerapia.Count > 0);
            cifras.Add(new HojaVidaCifra(
                "Prórrogas",
                prorrogas.ToString(),
                diasExtra > 0
                    ? $"Sumaron {HojaVidaFormato.Dias(diasExtra).ToLowerInvariant()} de tratamiento en {ingresosConProrroga} {(ingresosConProrroga == 1 ? "ingreso" : "ingresos")}"
                    : $"En {ingresosConProrroga} {(ingresosConProrroga == 1 ? "ingreso" : "ingresos")}"));
        }

        var agudizaciones = efectivos.Sum(i => i.Agudizaciones.Count);
        if (agudizaciones > 0)
        {
            cifras.Add(new HojaVidaCifra("Agudizaciones en crónicos", agudizaciones.ToString()));
        }

        var hospitalizaciones = ingresos.Sum(i => i.Hospitalizaciones.Count);
        if (hospitalizaciones > 0)
        {
            cifras.Add(new HojaVidaCifra("Hospitalizaciones", hospitalizaciones.ToString(), "Registradas durante la atención domiciliaria"));
        }

        if (model.Novedades.Count > 0)
        {
            var pendientes = model.Novedades.Count(n => !n.Resuelta);
            cifras.Add(new HojaVidaCifra(
                "Novedades reportadas",
                model.Novedades.Count.ToString(),
                pendientes == 0 ? "Todas resueltas" : $"{pendientes} sin resolver"));
        }

        return cifras;
    }

    private static IReadOnlyList<HojaVidaConteo> ConstruirMedicamentosFrecuentes(IEnumerable<HojaVidaIngreso> ingresos)
    {
        // Cuántos ingresos usaron cada medicamento (una prórroga o agudización con el mismo
        // medicamento no lo cuenta dos veces dentro del mismo ingreso).
        return ingresos
            .Where(EsEfectivo)
            .SelectMany(i => i.Medicamentos
                .Concat(i.Prorrogas.SelectMany(p => p.Medicamentos))
                .Concat(i.Agudizaciones.SelectMany(a => a.Medicamentos))
                .Select(m => m.Nombre.Trim())
                .DistinctBy(n => n.ToUpperInvariant()))
            .GroupBy(n => n.ToUpperInvariant())
            .Select(g => new HojaVidaConteo(g.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key, g.Count()))
            .OrderByDescending(x => x.Veces)
            .ThenBy(x => x.Nombre)
            .Take(6)
            .ToList();
    }

    private static IReadOnlyList<HojaVidaDiagnostico> ConstruirDiagnosticos(
        IEnumerable<HojaVidaIngreso> ingresos,
        IEnumerable<HojaVidaRonda> rondas)
    {
        return ingresos
            .Where(i => i.Situacion != HojaVidaSituacion.NoEfectiva)
            .SelectMany(i => new[] { (i.Cie10, i.Diagnostico, i.FechaIngreso) }
                .Concat(i.Agudizaciones.Select(a => (a.Cie10, a.Diagnostico, a.FechaInicio ?? i.FechaIngreso))))
            .Where(x => !string.IsNullOrWhiteSpace(x.Item1))
            .GroupBy(x => x.Item1!.ToUpperInvariant())
            .Select(g => new
            {
                Codigo = g.Key,
                Descripcion = g.OrderByDescending(x => x.Item3 ?? DateTime.MinValue).Select(x => x.Item2).FirstOrDefault(d => d is not null),
                Veces = g.Count(),
                Ultima = g.Max(x => x.Item3)
            })
            .OrderByDescending(x => x.Veces)
            .ThenByDescending(x => x.Ultima)
            .Select(x => new HojaVidaDiagnostico(x.Codigo, x.Descripcion, x.Veces))
            .Take(8)
            .ToList();
    }

    // ------------------------------------------------------------------------------------------
    // Línea de vida
    // ------------------------------------------------------------------------------------------

    private static HojaVidaLineaDeVida ConstruirLineaDeVida(HojaVidaPacienteViewModel model, DateTime hoy)
    {
        var ingresos = model.Ingresos.Where(i => i.FechaIngreso.HasValue).ToList();
        if (ingresos.Count == 0)
        {
            return new HojaVidaLineaDeVida();
        }

        var fechas = ingresos.Select(i => i.FechaIngreso!.Value)
            .Concat(model.Rondas.Select(r => r.Fecha.Date))
            .Concat(model.Novedades.Select(n => n.Fecha.Date))
            .Where(f => f <= hoy)
            .ToList();

        var inicio = fechas.Count > 0 ? fechas.Min() : hoy.AddDays(-30);
        if ((hoy - inicio).TotalDays < 30)
        {
            inicio = hoy.AddDays(-30);
        }

        var margen = Math.Max(2, (hoy - inicio).TotalDays * 0.025);
        var desde = inicio.AddDays(-margen);
        var hasta = hoy.AddDays(margen);
        var total = (hasta - desde).TotalDays;
        double Pct(DateTime fecha) => Math.Clamp((fecha - desde).TotalDays / total * 100, 0, 100);

        var carriles = ingresos
            .GroupBy(i => i.Programa)
            .OrderBy(g => CensoProgramas.Jerarquia(g.Key))
            .Select(g => new HojaVidaCarril
            {
                // Nombre corto: el carril es angosto y el nombre completo lo parte en dos líneas.
                ProgramaNombre = CensoProgramas.NombreCorto(g.Key),
                ProgramaClase = g.First().ProgramaClase,
                Barras = g.OrderBy(i => i.FechaIngreso).Select(i =>
                {
                    var fin = i.Situacion is HojaVidaSituacion.EnCurso or HojaVidaSituacion.SinDiligenciar
                        ? hoy
                        : i.FechaAlta ?? i.FechaIngreso!.Value;
                    var inicioPct = Pct(i.FechaIngreso!.Value);
                    return new HojaVidaBarra
                    {
                        IngresoClave = i.Clave,
                        InicioPct = Math.Round(inicioPct, 3),
                        // Una atención de un día sigue siendo visible y clicable.
                        AnchoPct = Math.Round(Math.Max(Pct(fin) - inicioPct, 0.9), 3),
                        Situacion = i.Situacion,
                        Etiqueta = EtiquetaBarra(i)
                    };
                }).ToList()
            })
            .ToList();

        var marcas = new List<HojaVidaMarca>();
        marcas.AddRange(model.Ingresos
            .SelectMany(i => i.Hospitalizaciones)
            .Where(h => h.Fecha.HasValue && h.Fecha <= hoy)
            .Select(h => new HojaVidaMarca
            {
                Tipo = "hospitalizacion",
                Pct = Math.Round(Pct(h.Fecha!.Value), 3),
                Etiqueta = $"Hospitalización, {HojaVidaFormato.Fecha(h.Fecha)}{(h.Motivo is not null ? $": {h.Motivo.ToLowerInvariant()}" : string.Empty)}"
            }));
        marcas.AddRange(model.Novedades.Select(n => new HojaVidaMarca
        {
            Tipo = "novedad",
            Pct = Math.Round(Pct(n.Fecha.Date), 3),
            Pendiente = !n.Resuelta,
            Etiqueta = $"Novedad {(n.Resuelta ? "resuelta" : "sin resolver")}, {HojaVidaFormato.Fecha(n.Fecha)}: {n.Tipo.ToLowerInvariant()}"
        }));
        marcas.AddRange(model.Rondas.Select(r => new HojaVidaMarca
        {
            Tipo = "ronda",
            Pct = Math.Round(Pct(r.Fecha.Date), 3),
            Etiqueta = $"Ronda intramural en {r.Ips}, {HojaVidaFormato.Fecha(r.Fecha)}"
        }));

        var ordenadas = RepartirEnFilas(marcas);

        return new HojaVidaLineaDeVida
        {
            Desde = desde,
            Hasta = hasta,
            Carriles = carriles,
            Marcas = ordenadas,
            FilasEventos = ordenadas.Count == 0 ? 0 : ordenadas.Max(m => m.Fila) + 1,
            Eje = BandasEje(desde, hasta, Pct),
            HoyPct = Math.Round(Pct(hoy), 3)
        };
    }

    private static string EtiquetaBarra(HojaVidaIngreso i)
    {
        var nombre = NombreIngreso(i);
        var inicio = HojaVidaFormato.Fecha(i.FechaIngreso);
        return i.Situacion switch
        {
            HojaVidaSituacion.EnCurso => $"{nombre}: en curso desde el {inicio} ({HojaVidaFormato.Dias(i.DiasEstancia).ToLowerInvariant()})",
            HojaVidaSituacion.SinDiligenciar => $"{nombre}: asignado el {inicio}, sin diligenciar",
            HojaVidaSituacion.NoEfectiva => $"{nombre}: solicitud del {inicio} que no se prestó ({i.EstadoCenso?.ToLowerInvariant()})",
            _ => i.FechaAlta is null
                ? $"{nombre}: desde el {inicio}, alta sin fecha registrada"
                : $"{nombre}: del {inicio} al {HojaVidaFormato.Fecha(i.FechaAlta)}{(i.FechaAltaEstimada ? " (estimada)" : string.Empty)} ({HojaVidaFormato.Dias(i.DiasEstancia).ToLowerInvariant()})"
        };
    }

    /// <summary>
    /// El eje no son marcas sueltas sino bandas: un mes cada una (un año en historias de más de
    /// tres años), pintadas alternadamente. Así se ve de un vistazo cuánto duró cada cosa sin
    /// tener que leer fechas, que era lo difícil de la versión anterior.
    /// </summary>
    private static IReadOnlyList<HojaVidaBanda> BandasEje(DateTime desde, DateTime hasta, Func<DateTime, double> pct)
    {
        // Ancho mínimo, en porcentaje de la línea, para que quepa el rótulo de la banda.
        const double AnchoParaMes = 3.6;
        const double AnchoParaMesConAnio = 7;

        var bandas = new List<HojaVidaBanda>();
        var porAnio = (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month > 36;
        var paso = porAnio ? 12 : 1;
        var inicio = porAnio ? new DateTime(desde.Year, 1, 1) : new DateTime(desde.Year, desde.Month, 1);
        var alterna = false;
        var anioEscrito = 0;

        for (var banda = inicio; banda <= hasta; banda = banda.AddMonths(paso))
        {
            var fin = banda.AddMonths(paso);
            var x0 = pct(banda < desde ? desde : banda);
            var x1 = pct(fin > hasta ? hasta : fin);
            var ancho = x1 - x0;
            if (ancho <= 0)
            {
                continue;
            }

            var texto = porAnio ? banda.Year.ToString() : HojaVidaFormato.MesCorto(banda.Month);

            // El año se escribe una sola vez por año, en la primera banda donde de verdad quepa:
            // ponerlo siempre en la primera recortaba el rótulo cuando esa banda era angosta.
            string? anio = null;
            if (!porAnio && banda.Year != anioEscrito && ancho >= AnchoParaMesConAnio)
            {
                anio = banda.Year.ToString();
                anioEscrito = banda.Year;
            }

            bandas.Add(new HojaVidaBanda(Math.Round(x0, 3), Math.Round(ancho, 3), texto, anio, alterna)
            {
                SinEtiqueta = ancho < AnchoParaMes
            });
            alterna = !alterna;
        }

        return bandas;
    }

    /// <summary>
    /// Reparte los eventos en hasta tres filas para que dos del mismo día no queden uno encima del
    /// otro. Se recorren en orden y cada uno baja a la primera fila donde haya espacio.
    /// </summary>
    private static IReadOnlyList<HojaVidaMarca> RepartirEnFilas(List<HojaVidaMarca> marcas)
    {
        const double SeparacionMinima = 1.8;
        const int MaximoFilas = 3;
        var ocupadas = new List<double>();

        foreach (var marca in marcas.OrderBy(m => m.Pct))
        {
            var fila = 0;
            while (fila < ocupadas.Count && marca.Pct - ocupadas[fila] < SeparacionMinima)
            {
                fila++;
            }

            if (fila >= MaximoFilas)
            {
                fila = 0;
            }

            if (fila == ocupadas.Count)
            {
                ocupadas.Add(marca.Pct);
            }
            else
            {
                ocupadas[fila] = marca.Pct;
            }

            marca.Fila = fila;
        }

        return marcas.OrderBy(m => m.Pct).ToList();
    }

    /// <summary>Meses cumplidos entre dos fechas.</summary>
    private static int MesesCumplidos(DateTime desde, DateTime hasta)
    {
        var meses = (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month;
        return hasta.Day < desde.Day ? Math.Max(meses - 1, 0) : Math.Max(meses, 0);
    }

    private static string? Primero(params string?[] valores) => valores.Select(Texto).FirstOrDefault(v => v is not null);


}
