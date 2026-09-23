using System.Globalization;
using System.Text.Json;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Data.Repositories.Models;
using Nexa.Helpers;
using Nexa.Models.ViewModels;

namespace Nexa.Services;

/// <summary>Conversión de las filas de cada censo en ingresos de la hoja de vida.</summary>
public partial class HojaVidaPacienteService
{
    private static readonly JsonSerializerOptions JsonOpciones = new() { PropertyNameCaseInsensitive = true };

    private static readonly string[] FormatosFecha =
        ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fffZ", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy"];

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    private List<HojaVidaIngreso> ConstruirIngresos(
        DatosCenso datos,
        IReadOnlyList<ClinicaHeridasSeguimientoRow> seguimientosHeridas,
        DateTime hoy)
    {
        var ingresos = new List<HojaVidaIngreso>();
        ingresos.AddRange(datos.Agudos.Select(r => IngresoAgudos(r, datos, hoy)));
        ingresos.AddRange(datos.Cronicos.Select(r => IngresoCronicos(r, datos, hoy)));
        ingresos.AddRange(datos.Heridas.Select((r, i) => IngresoHeridas(r, i + 1, datos, seguimientosHeridas, hoy)));
        ingresos.AddRange(datos.Npt.Select(r => IngresoNpt(r, datos, hoy)));
        ingresos.AddRange(datos.Terapias.Select(r => IngresoTerapia(r, datos, hoy)));

        // Programas asignados en el carril que nadie ha diligenciado: el informe de activos ya los
        // cuenta, así que la hoja de vida también los muestra, rotulados como lo que son.
        foreach (var episodio in datos.Episodios.Where(x =>
                     x.RegistroId is null && x.CerradoAtUtc is null && CensoProgramas.EsValido(x.Programa)))
        {
            ingresos.Add(new HojaVidaIngreso
            {
                Clave = $"{ClasePrograma(episodio.Programa)}-asignado-{episodio.Id}",
                Programa = episodio.Programa,
                ProgramaNombre = CensoProgramas.Nombre(episodio.Programa),
                ProgramaClase = ClasePrograma(episodio.Programa),
                Situacion = HojaVidaSituacion.SinDiligenciar,
                FechaIngreso = FechaValida(episodio.FechaIngreso) ?? ColombiaTime.Convert(episodio.AgregadoAtUtc).Date,
                EstadoCenso = "Asignado sin diligenciar",
                AsignadoPor = Texto(episodio.AgregadoPor)
            });
        }

        // Número del ingreso dentro de su programa. Clínica de heridas ya viene numerado en el mismo
        // orden que usa el puente (fecha de ingreso al programa y luego Id); los asignados sin
        // diligenciar van al final porque todavía no son un ingreso del puente.
        foreach (var grupo in ingresos.GroupBy(x => x.Programa))
        {
            var ordenados = grupo
                .OrderBy(x => x.Situacion == HojaVidaSituacion.SinDiligenciar)
                .ThenBy(x => x.FechaIngreso ?? DateTime.MaxValue)
                .ThenBy(x => x.RegistroId ?? long.MaxValue)
                .ToList();
            for (var i = 0; i < ordenados.Count; i++)
            {
                if (ordenados[i].NumeroEnPrograma == 0)
                {
                    ordenados[i].NumeroEnPrograma = i + 1;
                }

                ordenados[i].TotalEnPrograma = ordenados.Count;
            }
        }

        return ingresos;
    }

    // ------------------------------------------------------------------------------------------
    // Programa agudos
    // ------------------------------------------------------------------------------------------

    private static HojaVidaIngreso IngresoAgudos(
        CensoRecord r,
        DatosCenso datos,
        DateTime hoy)
    {
        var situacion = CensoPacienteService.EsAgudoNoEfectivo(r.Estado)
            ? HojaVidaSituacion.NoEfectiva
            : CensoPacienteService.EsAgudoCerrado(r.Estado)
                ? HojaVidaSituacion.Cerrada
                : HojaVidaSituacion.EnCurso;

        var ingreso = new HojaVidaIngreso
        {
            Clave = $"agudos-{r.Id}",
            Programa = CensoProgramas.Agudos,
            ProgramaNombre = CensoProgramas.Nombre(CensoProgramas.Agudos),
            ProgramaClase = "agudos",
            RegistroId = r.Id,
            Situacion = situacion,
            EstadoCenso = Texto(r.Estado) ?? "Sin estado",
            FechaIngreso = FechaValida(r.FechaIngreso),
            Cie10 = Texto(r.CodigoCie10),
            Diagnostico = Texto(r.DiagnosticoDescriptivo),
            Asegurador = Texto(r.Asegurador),
            QuienGestionaAlta = Texto(r.NombreQuienGestionaAlta)
        };

        ingreso.Medicamentos = Medicamentos(
            (r.NombreMedicamentoPrincipalTratante, Dosis(r.DosisMedicamentoPrincipal, r.MedidaMedicamentoPrincipal), r.ViaAdministracionMedicamentoPrincipal, r.FrecuenciaAdministracionMxPrincipal, r.DiasMedicamentoPrincipal),
            (r.NombreMedicamentoNumero2, Dosis(r.DosisMedicamento2, r.MedidaMedicamento2), r.ViaAdministracionMedicamento2, r.FrecuenciaAdministracionMedicamento2, r.DiasMedicamento2),
            (r.NombreMedicamentoNumero3, Dosis(r.DosisMedicamento3, r.MedidaMedicamento3), r.ViaAdministracionMedicamento3, r.FrecuenciaAdministracionMedicamento3, r.DiasMedicamento3),
            (r.NombreMedicamentoNumero4, Dosis(r.DosisMedicamento4, r.MedidaMedicamento4), r.ViaAdministracionMedicamento4, r.FrecuenciaAdministracionMedicamento4, r.DiasMedicamento4),
            (r.NombreMedicamentoNumero5, Dosis(r.DosisMedicamento5, r.MedidaMedicamento5), r.ViaAdministracionMedicamento5, r.FrecuenciaAdministracionMedicamento5, r.DiasMedicamento5),
            (r.NombreMedicamentoNumero6, Dosis(r.DosisMedicamento6, r.MedidaMedicamento6), r.ViaAdministracionMedicamento6, r.FrecuenciaAdministracionMedicamento6, r.DiasMedicamento6));

        // Prórrogas: la primera vive en el propio registro y las siguientes en censo_prorrogas.
        // La numeración guardada es por paciente (continúa entre atenciones), así que aquí se
        // renumera dentro del ingreso, que es como se lee.
        var prorrogas = new List<(ProrrogaDatos Datos, long? VersionId, DateTime? Registrada)>();
        if (LeerJson<ProrrogaDatos>(r.ProrrogaJson) is { } baseProrroga)
        {
            prorrogas.Add((baseProrroga, null, null));
        }

        foreach (var fila in datos.Prorrogas.Where(x => x.CensoRecordId == r.Id).OrderBy(x => x.Numero).ThenBy(x => x.Id))
        {
            if (LeerJson<ProrrogaDatos>(fila.ProrrogaJson) is { } version)
            {
                prorrogas.Add((version, fila.Id, ColombiaTime.Convert(fila.CreatedAtUtc).Date));
            }
        }

        ingreso.Prorrogas = prorrogas
            .Select((p, i) => new HojaVidaProrroga
            {
                Numero = i + 1,
                // Los mismos rótulos del formulario de prórroga del censo.
                Tipo = Texto(p.Datos.TipoProrroga)?.ToLowerInvariant() switch
                {
                    "extension" => "Extensión",
                    "adicion" => "Adición de tratamiento",
                    var otro => otro
                },
                DiasExtension = Entero(p.Datos.NumeroDiasExtension),
                FechaInicio = Fecha(p.Datos.FechaInicioTratamiento),
                FechaFin = Fecha(p.Datos.FechaFinTratamiento),
                RegistradaEl = p.Registrada,
                Medicamentos = Medicamentos(
                    (p.Datos.NombreMedicamentoPrincipal, Dosis(p.Datos.DosisMedicamentoPrincipal, p.Datos.MedidaMedicamentoPrincipal), p.Datos.ViaAdministracionMedicamentoPrincipal, p.Datos.FrecuenciaAdministracionMxPrincipal, Entero(p.Datos.DiasMedicamentoPrincipal)),
                    (p.Datos.NombreMedicamentoNumero2, Dosis(p.Datos.DosisMedicamento2, p.Datos.MedidaMedicamento2), p.Datos.ViaAdministracionMedicamento2, p.Datos.FrecuenciaAdministracionMedicamento2, Entero(p.Datos.DiasMedicamento2)),
                    (p.Datos.NombreMedicamentoNumero3, Dosis(p.Datos.DosisMedicamento3, p.Datos.MedidaMedicamento3), p.Datos.ViaAdministracionMedicamento3, p.Datos.FrecuenciaAdministracionMedicamento3, Entero(p.Datos.DiasMedicamento3)),
                    (p.Datos.NombreMedicamentoNumero4, Dosis(p.Datos.DosisMedicamento4, p.Datos.MedidaMedicamento4), p.Datos.ViaAdministracionMedicamento4, p.Datos.FrecuenciaAdministracionMedicamento4, Entero(p.Datos.DiasMedicamento4)),
                    (p.Datos.NombreMedicamentoNumero5, Dosis(p.Datos.DosisMedicamento5, p.Datos.MedidaMedicamento5), p.Datos.ViaAdministracionMedicamento5, p.Datos.FrecuenciaAdministracionMedicamento5, Entero(p.Datos.DiasMedicamento5)),
                    (p.Datos.NombreMedicamentoNumero6, Dosis(p.Datos.DosisMedicamento6, p.Datos.MedidaMedicamento6), p.Datos.ViaAdministracionMedicamento6, p.Datos.FrecuenciaAdministracionMedicamento6, Entero(p.Datos.DiasMedicamento6)))
            })
            .ToList();

        // Alta: la fecha real si está escrita. 1.395 atenciones cerradas de agudos no la tienen
        // (quedaron cerradas antes de poder diligenciar "Gestión de alta"); para esas se usa el
        // último día de tratamiento conocido —el del ingreso o el de su última prórroga— y se
        // rotula como estimada.
        if (situacion == HojaVidaSituacion.Cerrada)
        {
            ingreso.FechaAlta = FechaValida(r.FechaAlta);
            if (ingreso.FechaAlta is null)
            {
                var finConocido = new[] { FechaValida(r.FechaFinTratamiento) }
                    .Concat(ingreso.Prorrogas.Select(p => p.FechaFin))
                    .Where(f => f.HasValue && (ingreso.FechaIngreso is null || f.Value >= ingreso.FechaIngreso))
                    .Max();
                if (finConocido is not null)
                {
                    ingreso.FechaAlta = finConocido;
                    ingreso.FechaAltaEstimada = true;
                }
            }

            ingreso.MotivoAlta = Texto(r.Estado);
        }
        else if (situacion == HojaVidaSituacion.NoEfectiva)
        {
            ingreso.MotivoAlta = Texto(r.Estado);
        }

        CalcularEstancia(ingreso, hoy);

        ingreso.Tratamiento = Datos(
            ("Inicio", FechaTexto(r.FechaInicioTratamiento)),
            ("Fin", FechaTexto(r.FechaFinTratamiento)),
            ("Días autorizados", r.NumeroDiasAutorizado),
            ("Aplicaciones totales", r.AplicacionesTotales),
            ("Días de tratamiento IV", r.DiasTratamientoIv));

        ingreso.Controles = Controles(hoy, ingreso.Situacion,
            ("Cambio de sonda", FechaValida(r.FechaUltimoCambioSonda), FechaValida(r.FechaProximoCambioSonda),
                r.NumeroCalibreSonda is { } calibre and > 0 ? $"Calibre {calibre}" : null),
            ("Curación del catéter PICC", FechaValida(r.FechaUltimaCuracionPicc), null, null));

        ingreso.Servicios = Servicios(
            (EsSi(r.AltaTardia), "Alta tardía", null),
            (EsSi(r.RequiereServiciosComplementarios), "Servicios complementarios", r.ServicioComplementario),
            (EsSi(r.RequiereCuidador), "Requiere cuidador", null),
            (EsSi(r.PacienteGestante), "Paciente gestante", null),
            (EsSi(r.PacienteAnticoagulado), "Paciente anticoagulado", null),
            (EsSi(r.Aislamiento), "Aislamiento", r.TipoAislamiento),
            (EsSi(r.Nebulizaciones), "Nebulizaciones", null),
            (EsSi(r.NutricionParenteral), "Nutrición parenteral", null),
            (EsSi(r.NutricionEnteral), "Nutrición enteral", null),
            (EsSi(r.LaboratorioClinicoProcedimiento), "Laboratorio clínico o procedimiento", null),
            (EsSi(r.CateterismoOSv), "Cateterismo o sonda vesical", null),
            (EsSi(r.CateterPicc), "Catéter PICC", null));

        if (EsSi(r.PacienteRehospitalizado) || FechaValida(r.FechaRehospitalizacion) is not null)
        {
            ingreso.Hospitalizaciones =
            [
                new HojaVidaHospitalizacion
                {
                    Fecha = FechaValida(r.FechaRehospitalizacion),
                    FechaAlta = FechaValida(r.FechaAltaHospitalizacion),
                    Motivo = Texto(r.MotivoRehospitalizacion),
                    Ips = Texto(r.IpsIntramuralRehospitalizacion),
                    RemitidoPor = Texto(r.RemitidoPorRehospitalizacion),
                    Detalle = Texto(r.AmpliacionMotivoRehospitalizacion),
                    EsRehospitalizacion = true
                }
            ];
        }

        var despachos = new List<HojaVidaDespacho>();
        if (r.FarmaciaEnviadoAtUtc.HasValue)
        {
            despachos.Add(Despacho("Kardex y requisición del ingreso", r.FarmaciaEstado, r.FarmaciaEnviadoAtUtc));
        }

        var numeroPorVersion = prorrogas
            .Select((p, i) => (p.VersionId, Numero: i + 1))
            .ToDictionary(x => x.VersionId ?? 0, x => x.Numero);
        foreach (var copia in datos.CopiasDespacho
                     .Where(x => x.ProrrogaDeId == r.Id)
                     .GroupBy(x => x.VersionId ?? 0)
                     .Select(g => g.OrderByDescending(x => x.EnviadoAtUtc ?? DateTime.MinValue).First()))
        {
            var numero = numeroPorVersion.TryGetValue(copia.VersionId ?? 0, out var n) ? n : (int?)null;
            despachos.Add(Despacho(numero is null ? "Prórroga" : $"Prórroga {numero}", copia.FarmaciaEstado, copia.EnviadoAtUtc));
        }

        ingreso.Despachos = despachos;
        return ingreso;
    }

    // ------------------------------------------------------------------------------------------
    // Programa crónicos
    // ------------------------------------------------------------------------------------------

    private static HojaVidaIngreso IngresoCronicos(
        CensoCronicoRecord r,
        DatosCenso datos,
        DateTime hoy)
    {
        var cerrado = CensoPacienteService.EsCronicoCerrado(r.EstadoPaciente, r.FechaEgreso);
        var ingreso = new HojaVidaIngreso
        {
            Clave = $"cronicos-{r.Id}",
            Programa = CensoProgramas.Cronicos,
            ProgramaNombre = CensoProgramas.Nombre(CensoProgramas.Cronicos),
            ProgramaClase = "cronicos",
            RegistroId = r.Id,
            Situacion = cerrado ? HojaVidaSituacion.Cerrada : HojaVidaSituacion.EnCurso,
            EstadoCenso = Texto(r.EstadoPaciente),
            FechaIngreso = FechaValida(r.FechaIngreso),
            FechaAlta = cerrado && CensoVisibility.HayEgreso(r.FechaEgreso) ? r.FechaEgreso!.Value.Date : null,
            MotivoAlta = cerrado ? Texto(r.MotivoEgreso) ?? Texto(r.EstadoPaciente) : null,
            Cie10 = Texto(r.DiagnosticoCronicoCie10),
            Diagnostico = Texto(r.GrupoPatologiaCronica)
        };

        CalcularEstancia(ingreso, hoy);

        // Las escalas no se muestran como números sueltos: cada una viaja con lo que significa.
        ingreso.Escalas = Escalas(
            Barthel(r.CalificacionBarthel),
            Karnofsky(r.Karnofsky),
            Braden(r.Braden, r.RiesgoLesionPiel),
            Morse(r.EscalaMorse, r.RiesgoCaida),
            Fast(r.Fast),
            Rankin(r.Rankin),
            Mmrc(r.DisneaMmrc),
            Nyha(r.Nyha));

        ingreso.Controles = Controles(hoy, ingreso.Situacion,
            ("Cambio de sonda vesical", FechaValida(r.FechaUltimoCambioSondaVesical), FechaValida(r.FechaProximoCambioSondaVesical),
                Unir(Calibre(r.CalibreSondaVesical), Texto(r.FrecuenciaCambioSondaVesical))),
            ("Cambio de sonda nasogástrica", FechaValida(r.FechaUltimoCambioSondaNasogastrica), null,
                Unir(Calibre(r.CalibreSondaNasogastrica), Texto(r.FrecuenciaCambioSondaNasogastrica))),
            ("Prescripción de pañales (MIPRES)", FechaValida(r.FechaUltimaPrescripcionPanales),
                Vence(r.FechaUltimaPrescripcionPanales, r.TiempoPrescripcionPanalesMeses),
                Unir(Texto(r.EstadoMipresPanales), Texto(r.TallaPanales) is { } talla ? $"talla {talla}" : null)),
            ("Prescripción de nutrición (MIPRES)", FechaValida(r.FechaUltimaPrescripcionNutricion),
                Vence(r.FechaUltimaPrescripcionNutricion, r.TiempoPrescripcionNutricionMeses),
                Texto(r.EstadoMipresNutricion)));

        ingreso.DiagnosticoSecundario = Unir(r.DiagnosticoCronicoComplementario, r.GrupoPatologiaCronicaComplementario);

        ingreso.Servicios = Servicios(
            (EsSi(r.EducacionPlanCuidados), "Educación en plan de cuidados", null),
            (EsSi(r.RequiereCuidador), "Requiere cuidador", null),
            (EsSi(r.TerapiaFisica), "Terapia física", null),
            (EsSi(r.TerapiaRespiratoria), "Terapia respiratoria", null),
            (EsSi(r.TerapiaOcupacional), "Terapia ocupacional", null),
            (EsSi(r.Fonoaudiologia), "Fonoaudiología", null),
            (EsSi(r.Nutricion), "Nutrición", null),
            (EsSi(r.Psicologia), "Psicología", null),
            (EsSi(r.Traqueostomia), "Traqueostomía", null),
            (EsSi(r.SondaNasogastrica), "Sonda nasogástrica", Calibre(r.CalibreSondaNasogastrica)),
            (EsSi(r.SondaGastrostomia), "Gastrostomía", null),
            (EsSi(r.Colostomia), "Colostomía", null),
            (EsSi(r.SondaCistostomia), "Cistostomía", null),
            (EsSi(r.SondaVesical), "Sonda vesical", Calibre(r.CalibreSondaVesical)),
            (EsSi(r.CateterPicc), "Catéter PICC", null),
            (EsSi(r.FormulaControl), "Fórmula de control", null));

        var agudizaciones = new List<HojaVidaAgudizacion>();
        var despachos = new List<HojaVidaDespacho>();
        foreach (var fila in datos.Agudizaciones.Where(x => x.RegistroId == r.Id).OrderBy(x => x.Numero))
        {
            var payload = LeerJson<AgudizacionDatos>(fila.AgudizacionJson) ?? new AgudizacionDatos();
            agudizaciones.Add(new HojaVidaAgudizacion
            {
                Numero = fila.Numero,
                Cie10 = Texto(payload.CodigoCie10),
                Diagnostico = Texto(payload.DiagnosticoDescriptivo),
                FechaInicio = Fecha(payload.FechaInicioTratamiento),
                FechaFin = Fecha(payload.FechaFinTratamiento),
                EstadoFarmacia = fila.EnviadoAtUtc.HasValue ? HojaVidaFormato.EstadoFarmacia(fila.FarmaciaEstado) : null,
                Medicamentos = (payload.Medicamentos ?? [])
                    .Where(m => !string.IsNullOrWhiteSpace(m.Nombre))
                    .OrderBy(m => m.Numero)
                    .Select(m => Medicamento(m.Nombre, Dosis(m.Dosis, m.Medida), m.Via, m.Frecuencia, Entero(m.Dias)))
                    .OfType<HojaVidaMedicamento>()
                    .ToList()
            });

            if (fila.EnviadoAtUtc.HasValue)
            {
                despachos.Add(Despacho($"Agudización {fila.Numero}", fila.FarmaciaEstado, fila.EnviadoAtUtc));
            }
        }

        ingreso.Agudizaciones = agudizaciones;
        ingreso.Despachos = despachos;

        ingreso.Hospitalizaciones = datos.HospitalizacionesCronicos
            .Where(x => x.CensoCronicoRecordId == r.Id)
            .OrderBy(x => x.Numero)
            .Select(x => LeerJson<HospitalizacionCronicoDatos>(x.HospitalizacionJson))
            .OfType<HospitalizacionCronicoDatos>()
            .Select(h => new HojaVidaHospitalizacion
            {
                Fecha = Fecha(h.FechaHospitalizacion),
                FechaAlta = Fecha(h.FechaAlta),
                Motivo = Texto(h.MotivoHospitalizacion),
                Ips = Texto(h.IpsIntramural),
                RemitidoPor = Texto(h.RemitidoPor),
                Detalle = Unir(h.CodigoCie10, h.DiagnosticoDescriptivo)
            })
            .Where(h => h.Fecha.HasValue || h.Motivo is not null)
            .ToList();

        return ingreso;
    }

    // ------------------------------------------------------------------------------------------
    // Clínica de heridas
    // ------------------------------------------------------------------------------------------

    private static HojaVidaIngreso IngresoHeridas(
        CensoClinicaHeridasRecord r,
        int numeroIngreso,
        DatosCenso datos,
        IReadOnlyList<ClinicaHeridasSeguimientoRow> seguimientos,
        DateTime hoy)
    {
        var cerrado = CensoPacienteService.EsProgramaCerrado(r.Estado, r.FechaEgreso);
        var ingreso = new HojaVidaIngreso
        {
            Clave = $"heridas-{r.Id}",
            Programa = CensoProgramas.ClinicaHeridas,
            ProgramaNombre = CensoProgramas.Nombre(CensoProgramas.ClinicaHeridas),
            ProgramaClase = "heridas",
            RegistroId = r.Id,
            NumeroEnPrograma = numeroIngreso,
            Situacion = cerrado ? HojaVidaSituacion.Cerrada : HojaVidaSituacion.EnCurso,
            EstadoCenso = Texto(r.Estado),
            FechaIngreso = FechaValida(r.FechaIngresoPrograma),
            FechaAlta = cerrado && CensoVisibility.HayEgreso(r.FechaEgreso) ? r.FechaEgreso!.Value.Date : null,
            MotivoAlta = cerrado ? Texto(r.MotivoEgreso) ?? Texto(r.Estado) : null,
            Cie10 = Texto(r.CodigoCie10),
            Diagnostico = Texto(r.DiagnosticoDescriptivo),
            Asegurador = Texto(r.Asegurador)
        };

        CalcularEstancia(ingreso, hoy);

        ingreso.Tratamiento = Datos(
            ("Duración", r.DuracionTratamientoDias is { } d ? HojaVidaFormato.Dias(d) : null),
            ("Frecuencia de visita", r.FrecuenciaVisita));

        ingreso.Servicios = Servicios(
            (EsSi(r.ManejoHerida), "Manejo de la herida", null),
            (EsSi(r.Vac), "Terapia de presión negativa (VAC)", null),
            (EsSi(r.Picc), "Catéter PICC", null));

        ingreso.Insumos = new[] { r.ApositoMedicamento1, r.ApositoMedicamento2, r.ApositoMedicamento3, r.ApositoMedicamento4 }
            .Select(Texto)
            .OfType<string>()
            .ToList();

        var kardex = datos.KardexHeridas.Where(x => x.RegistroId == r.Id).ToList();
        ingreso.PlanesHeridas = datos.PlanesHeridas
            .Where(x => x.CensoClinicaHeridasRecordId == r.Id)
            .OrderBy(x => x.Numero)
            .Select(plan => new HojaVidaPlanHeridas
            {
                Numero = plan.Numero,
                AbiertoEl = ColombiaTime.Convert(plan.CreadoAtUtc).Date,
                CerradoEl = plan.CerradoAtUtc is { } cierre ? ColombiaTime.Convert(cierre).Date : null,
                Insumos = plan.Apositos,
                DuracionDias = plan.DuracionTratamientoDias,
                FrecuenciaVisita = Texto(plan.FrecuenciaVisita),
                Requisiciones = kardex
                    .Where(k => k.PlanId == plan.Id)
                    .OrderBy(k => k.CreatedAtUtc)
                    .Select(k => Despacho($"Requisición {ClinicaHeridasKardexTipos.Nombre(k.Tipo)}", k.FarmaciaEstado, k.EnviadoAtUtc))
                    .ToList()
            })
            .ToList();

        ingreso.Despachos = ingreso.PlanesHeridas
            .SelectMany(p => p.Requisiciones.Select(req => new HojaVidaDespacho
            {
                Documento = ingreso.PlanesHeridas.Count > 1 ? $"{req.Documento} · plan {p.Numero}" : req.Documento,
                Estado = req.Estado,
                Entregado = req.Entregado,
                EnviadoEl = req.EnviadoEl
            }))
            .Where(d => d.EnviadoEl.HasValue)
            .ToList();

        if (FechaValida(r.FechaHospitalizacion) is not null || Texto(r.MotivoHospitalizacion) is not null)
        {
            ingreso.Hospitalizaciones =
            [
                new HojaVidaHospitalizacion
                {
                    Fecha = FechaValida(r.FechaHospitalizacion),
                    Motivo = Texto(r.MotivoHospitalizacion),
                    Ips = Texto(r.IpsIntramural),
                    RemitidoPor = Texto(r.RemitidoPorHospitalizacion)
                }
            ];
        }

        // Seguimientos del Portal Administrativo: cada uno dice a qué ingreso pertenece, con la
        // misma numeración que se calculó arriba (ver reingreso-al-mismo-programa).
        ingreso.EvolucionHerida = EvolucionHerida(seguimientos.Where(s => s.Ingreso == numeroIngreso).ToList());

        return ingreso;
    }

    private static HojaVidaEvolucionHerida? EvolucionHerida(IReadOnlyList<ClinicaHeridasSeguimientoRow> filas)
    {
        if (filas.Count == 0)
        {
            return null;
        }

        var seguimientos = filas
            .OrderBy(s => s.Numero)
            .Select(s => new HojaVidaSeguimientoHerida
            {
                Numero = s.Numero,
                Fecha = ColombiaTime.Convert(s.CreatedAtUtc),
                Ubicacion = Texto(s.Ubicacion),
                Largo = s.DiametroVerticalCm,
                Ancho = s.DiametroHorizontalCm,
                Profundidad = s.ProfundidadCm,
                Tejido = Texto(s.Tejido),
                Exudado = Unir(s.ExudadoCantidad, s.ExudadoCaracteristicas),
                Auxiliar = Texto(s.AuxiliarNombre),
                FotoDriveItemId = (s.Fotos.FirstOrDefault(f => f.Tipo == "PLANO_GENERAL") ?? s.Fotos.FirstOrDefault())?.DriveItemId
            })
            .ToList();

        var conMedida = seguimientos.Where(s => s.Area > 0).ToList();
        var evolucion = new HojaVidaEvolucionHerida { Seguimientos = seguimientos };
        if (conMedida.Count > 0)
        {
            evolucion.AreaInicial = conMedida[0].Area;
            evolucion.AreaUltima = conMedida[^1].Area;
            if (conMedida.Count > 1 && conMedida[0].Area > 0)
            {
                evolucion.CambioPorcentual = Math.Round((conMedida[^1].Area - conMedida[0].Area) / conMedida[0].Area * 100, 0);

                // Trazo en un lienzo de 120 × 32 con 3 px de margen vertical.
                var max = conMedida.Max(s => s.Area);
                var min = conMedida.Min(s => s.Area);
                var rango = max - min;
                evolucion.Trazo = string.Join(' ', conMedida.Select((s, i) =>
                {
                    var x = conMedida.Count == 1 ? 60 : i * 120.0 / (conMedida.Count - 1);
                    var y = rango <= 0 ? 16 : 29 - (s.Area - min) / rango * 26;
                    return $"{x.ToString("0.#", CultureInfo.InvariantCulture)},{y.ToString("0.#", CultureInfo.InvariantCulture)}";
                }));
            }
        }

        return evolucion;
    }

    // ------------------------------------------------------------------------------------------
    // NPT
    // ------------------------------------------------------------------------------------------

    private static HojaVidaIngreso IngresoNpt(
        CensoNptRecord r,
        DatosCenso datos,
        DateTime hoy)
    {
        var cerrado = CensoPacienteService.EsProgramaCerrado(r.Estado, r.FechaEgreso);
        var ingreso = new HojaVidaIngreso
        {
            Clave = $"npt-{r.Id}",
            Programa = CensoProgramas.Npt,
            ProgramaNombre = CensoProgramas.Nombre(CensoProgramas.Npt),
            ProgramaClase = "npt",
            RegistroId = r.Id,
            Situacion = cerrado ? HojaVidaSituacion.Cerrada : HojaVidaSituacion.EnCurso,
            EstadoCenso = Texto(r.Estado),
            FechaIngreso = FechaValida(r.FechaIngresoPrograma),
            FechaAlta = cerrado && CensoVisibility.HayEgreso(r.FechaEgreso) ? r.FechaEgreso!.Value.Date : null,
            MotivoAlta = cerrado ? Texto(r.MotivoEgreso) ?? Texto(r.Estado) : null,
            Cie10 = Texto(r.CodigoCie10),
            Diagnostico = Texto(r.DiagnosticoDescriptivo),
            Asegurador = Texto(r.Asegurador)
        };

        CalcularEstancia(ingreso, hoy);

        var inicio = FechaValida(r.FechaInicioNpt);
        var fin = FechaValida(r.FechaFinNpt);
        if (inicio is not null || fin is not null || r.DiasTratamiento.HasValue)
        {
            var enCurso = fin is null && !cerrado && inicio is not null;
            ingreso.Npt = new HojaVidaNpt
            {
                Inicio = inicio,
                Fin = fin,
                EnCurso = enCurso,
                Dias = enCurso
                    ? (hoy - inicio!.Value).Days + 1
                    : r.DiasTratamiento ?? (inicio is not null && fin is not null ? (fin.Value - inicio.Value).Days + 1 : null),
                Conexion = r.HoraConexion is { } c ? HojaVidaFormato.Hora(c) : null,
                Desconexion = r.HoraDesconexion is { } dx ? HojaVidaFormato.Hora(dx) : null,
                Picc = EsSi(r.Picc) ? "Sí" : Texto(r.Picc)
            };
        }

        ingreso.Controles = Controles(hoy, ingreso.Situacion,
            ("Curación del catéter PICC/CC", FechaValida(r.FechaUltimaCuracionPicc), null, null));

        ingreso.Servicios = Servicios(
            (EsSi(r.Picc), "Catéter PICC/CC", null),
            (EsSi(r.CargueLaboratorios), "Laboratorios", null),
            (EsSi(r.CargueGlucometria), "Glucometría", null),
            (EsSi(r.CargueServiciosComplementarios), "Servicios complementarios", null),
            (EsSi(r.CargueSeguimientoMedico), "Seguimiento médico", null));

        if (FechaValida(r.FechaHospitalizacion) is not null || Texto(r.MotivoHospitalizacion) is not null)
        {
            ingreso.Hospitalizaciones =
            [
                new HojaVidaHospitalizacion
                {
                    Fecha = FechaValida(r.FechaHospitalizacion),
                    FechaAlta = FechaValida(r.FechaAltaHospitalizacion),
                    Motivo = Texto(r.MotivoHospitalizacion),
                    Ips = Texto(r.IpsIntramural),
                    RemitidoPor = Texto(r.RemitidoPorHospitalizacion)
                }
            ];
        }

        ingreso.Despachos = datos.KardexNpt
            .Where(x => x.RegistroId == r.Id && x.EnviadoAtUtc.HasValue)
            .Select(x => Despacho("Requisición de NPT", x.FarmaciaEstado, x.EnviadoAtUtc))
            .ToList();

        return ingreso;
    }

    // ------------------------------------------------------------------------------------------
    // Terapia ambulatoria
    // ------------------------------------------------------------------------------------------

    private static HojaVidaIngreso IngresoTerapia(
        CensoTerapiaAmbulatoriaRecord r,
        DatosCenso datos,
        DateTime hoy)
    {
        var cerrado = CensoPacienteService.EsTerapiaCerrada(r.EstadoPaciente, r.EstadoAlta);
        var ingreso = new HojaVidaIngreso
        {
            Clave = $"terapia-{r.Id}",
            Programa = CensoProgramas.TerapiaAmbulatoria,
            ProgramaNombre = CensoProgramas.Nombre(CensoProgramas.TerapiaAmbulatoria),
            ProgramaClase = "terapia",
            RegistroId = r.Id,
            Situacion = cerrado ? HojaVidaSituacion.Cerrada : HojaVidaSituacion.EnCurso,
            EstadoCenso = Unir(r.EstadoPaciente, r.EstadoAlta == r.EstadoPaciente ? null : r.EstadoAlta),
            FechaIngreso = FechaValida(r.FechaIngreso) ?? FechaValida(r.FechaInicio),
            MotivoAlta = cerrado ? Texto(r.MotivoAlta) : null,
            Cie10 = Texto(r.CodigoCie10),
            Diagnostico = Texto(r.DiagnosticoDescriptivo),
        };

        if (cerrado)
        {
            ingreso.FechaAlta = FechaValida(r.FechaAlta);
            if (ingreso.FechaAlta is null && FechaValida(r.FechaFin) is { } fin
                && (ingreso.FechaIngreso is null || fin >= ingreso.FechaIngreso))
            {
                ingreso.FechaAlta = fin;
                ingreso.FechaAltaEstimada = true;
            }
        }

        CalcularEstancia(ingreso, hoy);

        var terapias = new List<HojaVidaTerapia> { new(r.TipoTerapia, r.Cantidad, Texto(r.FrecuenciaTerapia)) };
        if (r.TieneSegundoTratamiento && Texto(r.SegundoTratamientoTipoTerapia) is { } segundo)
        {
            terapias.Add(new HojaVidaTerapia(segundo, r.SegundoTratamientoCantidad, Texto(r.SegundoTratamientoFrecuenciaTerapia)));
        }

        if (r.TieneTercerTratamiento && Texto(r.TercerTratamientoTipoTerapia) is { } tercero)
        {
            terapias.Add(new HojaVidaTerapia(tercero, r.TercerTratamientoCantidad, Texto(r.TercerTratamientoFrecuenciaTerapia)));
        }

        ingreso.Terapias = terapias.Where(t => !string.IsNullOrWhiteSpace(t.Tipo)).ToList();

        ingreso.ProrrogasTerapia = datos.ProrrogasTerapia
            .Where(x => x.CensoTerapiaAmbulatoriaRecordId == r.Id)
            .OrderBy(x => x.FechaSolicitudProrroga)
            .ThenBy(x => x.Id)
            .Select(x => new HojaVidaProrrogaTerapia
            {
                Tipo = x.TipoTerapia,
                Cantidad = Texto(x.Cantidad),
                Frecuencia = x.Frecuencia > 0 ? x.Frecuencia : null,
                FechaSolicitud = FechaValida(x.FechaSolicitudProrroga),
                FechaAutorizacion = FechaValida(x.FechaEntregaAutorizacion),
                CodigoAutorizacion = Texto(x.CodigoAutorizacion)
            })
            .ToList();

        ingreso.Tratamiento = Datos(
            ("Inicio de las terapias", FechaTexto(r.FechaInicio)),
            ("Fin de las terapias", FechaTexto(r.FechaFin)));

        return ingreso;
    }

    // ------------------------------------------------------------------------------------------
    // Ayudantes
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Días de estancia: de la fecha de ingreso a la de alta, o a hoy si sigue en curso. Mismo
    /// criterio que los "Días de estancia" del exportable de crónicos (diferencia de fechas).
    /// </summary>
    private static void CalcularEstancia(HojaVidaIngreso ingreso, DateTime hoy)
    {
        if (ingreso.FechaIngreso is not { } inicio)
        {
            return;
        }

        var fin = ingreso.Situacion == HojaVidaSituacion.EnCurso ? hoy : ingreso.FechaAlta;
        if (ingreso.Situacion is HojaVidaSituacion.EnCurso or HojaVidaSituacion.Cerrada && fin is { } f && f >= inicio)
        {
            ingreso.DiasEstancia = (f - inicio).Days;
        }
    }

    private static HojaVidaDespacho Despacho(string documento, string? estado, DateTime? enviadoUtc) => new()
    {
        Documento = documento,
        Estado = enviadoUtc.HasValue ? HojaVidaFormato.EstadoFarmacia(estado) : "Sin enviar a farmacia",
        Entregado = string.Equals(estado, "Despachado", StringComparison.Ordinal),
        EnviadoEl = enviadoUtc is { } e ? ColombiaTime.Convert(e) : null
    };

    private static IReadOnlyList<HojaVidaMedicamento> Medicamentos(
        params (string? Nombre, string? Dosis, string? Via, string? Frecuencia, int? Dias)[] filas) =>
        filas
            .Select(f => Medicamento(f.Nombre, f.Dosis, f.Via, f.Frecuencia, f.Dias))
            .OfType<HojaVidaMedicamento>()
            .ToList();

    private static HojaVidaMedicamento? Medicamento(string? nombre, string? dosis, string? via, string? frecuencia, int? dias)
    {
        var limpio = Texto(nombre);
        // Los medicamentos adicionales que no aplican se guardan como "No".
        if (limpio is null || limpio.Equals("No", StringComparison.OrdinalIgnoreCase)
            || limpio.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new HojaVidaMedicamento
        {
            Nombre = limpio,
            Dosis = dosis,
            Via = Texto(via),
            Frecuencia = Texto(frecuencia),
            Dias = dias is > 0 ? dias : null
        };
    }

    private static string? Dosis(decimal? dosis, string? medida) =>
        dosis is { } d && d > 0 ? UnirCon(" ", d.ToString("0.###", Colombia), Medida(medida, d == 1)) : null;

    private static string? Dosis(string? dosis, string? medida) =>
        Texto(dosis) is { } d ? UnirCon(" ", d, Medida(medida, d is "1" or "1,0" or "1.0")) : null;

    /// <summary>La unidad en minúscula y en singular cuando la dosis es 1: "1 unidad", no "1 unidades".</summary>
    private static string? Medida(string? medida, bool singular)
    {
        var texto = Texto(medida)?.ToLowerInvariant();
        if (texto is null || !singular)
        {
            return texto;
        }

        return texto switch
        {
            "unidades" => "unidad",
            _ when texto.EndsWith('s') => texto[..^1],
            _ => texto
        };
    }

    private static IReadOnlyList<HojaVidaServicio> Servicios(params (bool Activo, string Nombre, string? Detalle)[] filas) =>
        filas
            .Where(f => f.Activo)
            .Select(f => new HojaVidaServicio(f.Nombre, Texto(f.Detalle)))
            .ToList();

    /// <summary>
    /// Controles con fecha: se muestran los que tienen algún dato, y en una atención en curso se
    /// calcula cuánto falta para el próximo (en negativo si ya pasó).
    /// </summary>
    private static IReadOnlyList<HojaVidaControl> Controles(
        DateTime hoy,
        HojaVidaSituacion situacion,
        params (string Nombre, DateTime? Ultimo, DateTime? Proximo, string? Detalle)[] filas) =>
        filas
            .Where(f => f.Ultimo is not null || f.Proximo is not null)
            .Select(f => new HojaVidaControl(f.Nombre, f.Ultimo, f.Proximo, Texto(f.Detalle))
            {
                DiasParaProximo = situacion == HojaVidaSituacion.EnCurso && f.Proximo is { } proximo
                    ? (proximo - hoy).Days
                    : null
            })
            .ToList();

    /// <summary>Fecha en la que se vence una prescripción de N meses.</summary>
    private static DateTime? Vence(DateTime? desde, int? meses) =>
        FechaValida(desde) is { } f && meses is > 0 ? f.AddMonths(meses.Value) : null;

    private static IReadOnlyList<HojaVidaEscala> Escalas(params HojaVidaEscala?[] escalas) =>
        escalas.OfType<HojaVidaEscala>().ToList();

    // Rangos de las escalas tal como se usan en la práctica clínica. El texto es para quien no las
    // conoce: el número solo no le dice nada a quien lee la hoja de vida.

    private static HojaVidaEscala? Barthel(string? valor) => Puntaje(valor) is not { } n ? null : new HojaVidaEscala(
        "Barthel", $"{n} de 100",
        n <= 20 ? "Dependencia total para las actividades diarias"
        : n <= 60 ? "Dependencia grave"
        : n <= 90 ? "Dependencia moderada"
        : n < 100 ? "Dependencia leve"
        : "Independiente",
        n <= 60 ? "alto" : n <= 90 ? "medio" : "bajo") { Porcion = n / 100.0 };

    private static HojaVidaEscala? Karnofsky(string? valor) => Puntaje(valor) is not { } n ? null : new HojaVidaEscala(
        "Karnofsky", $"{n} de 100",
        n >= 80 ? "Hace su vida normal"
        : n >= 50 ? "No puede trabajar, se vale en casa con ayuda"
        : "Necesita cuidado permanente",
        n >= 80 ? "bajo" : n >= 50 ? "medio" : "alto") { Porcion = n / 100.0 };

    private static HojaVidaEscala? Braden(int? valor, string? riesgo) => valor is not { } n || n <= 0 ? null : new HojaVidaEscala(
        "Braden", $"{n} de 23",
        Texto(riesgo) ?? (n <= 12 ? "Riesgo alto de lesiones en la piel"
            : n <= 14 ? "Riesgo moderado de lesiones en la piel"
            : n <= 18 ? "Riesgo leve de lesiones en la piel"
            : "Sin riesgo de lesiones en la piel"),
        n <= 12 ? "alto" : n <= 18 ? "medio" : "bajo") { Porcion = n / 23.0 };

    private static HojaVidaEscala? Morse(int? valor, string? riesgo) => valor is not { } n || n < 0 ? null : new HojaVidaEscala(
        "Morse", $"{n} de 125",
        Texto(riesgo) ?? (n >= 45 ? "Riesgo alto de caídas" : n >= 25 ? "Riesgo moderado de caídas" : "Riesgo bajo de caídas"),
        n >= 45 ? "alto" : n >= 25 ? "medio" : "bajo") { Porcion = n / 125.0 };

    private static HojaVidaEscala? Fast(string? valor) => Puntaje(valor) is not { } n ? null : new HojaVidaEscala(
        "FAST", $"{n} de 7",
        n >= 6 ? "Demencia avanzada" : n >= 4 ? "Demencia moderada" : "Deterioro leve",
        n >= 6 ? "alto" : n >= 4 ? "medio" : "bajo") { Porcion = n / 7.0 };

    private static HojaVidaEscala? Rankin(string? valor) => Puntaje(valor) is not { } n ? null : new HojaVidaEscala(
        "Rankin", $"{n} de 6",
        n >= 4 ? "Discapacidad grave: necesita ayuda permanente"
        : n >= 2 ? "Discapacidad moderada"
        : "Sin discapacidad significativa",
        n >= 4 ? "alto" : n >= 2 ? "medio" : "bajo") { Porcion = n / 6.0 };

    private static HojaVidaEscala? Mmrc(string? valor) => Puntaje(valor) is not { } n ? null : new HojaVidaEscala(
        "Disnea mMRC", $"{n} de 4",
        n >= 3 ? "Se ahoga al caminar pocos metros" : n >= 1 ? "Se ahoga al subir una cuesta o al apurarse" : "Solo se ahoga con ejercicio fuerte",
        n >= 3 ? "alto" : n >= 1 ? "medio" : "bajo") { Porcion = n / 4.0 };

    private static HojaVidaEscala? Nyha(string? valor) => Puntaje(valor) is not { } n ? null : new HojaVidaEscala(
        "NYHA", $"Clase {n}",
        n >= 4 ? "Síntomas del corazón incluso en reposo"
        : n == 3 ? "Síntomas con actividad leve"
        : n == 2 ? "Síntomas con actividad normal"
        : "Sin límites en la actividad",
        n >= 3 ? "alto" : n == 2 ? "medio" : "bajo") { Porcion = n / 4.0 };

    /// <summary>El puntaje guardado como texto ("40", "40%"); null si no es un número.</summary>
    private static int? Puntaje(string? valor)
    {
        var limpio = Texto(valor)?.TrimEnd('%', ' ');
        return int.TryParse(limpio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static IReadOnlyList<HojaVidaDato> Datos(params (string Etiqueta, string? Valor)[] filas) =>
        filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Valor))
            .Select(f => new HojaVidaDato(f.Etiqueta, f.Valor!.Trim()))
            .ToList();

    private static string? Calibre(string? calibre) => Texto(calibre) is { } c ? $"Calibre {c}" : null;

    private static bool EsSi(string? valor) =>
        valor is not null
        && (valor.Trim().Equals("Si", StringComparison.OrdinalIgnoreCase)
            || valor.Trim().Equals("Sí", StringComparison.OrdinalIgnoreCase));

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Une las partes con contenido con " · "; null si ninguna tiene.</summary>
    private static string? Unir(params string?[] partes) => UnirCon(" · ", partes);

    private static string? UnirCon(string separador, params string?[] partes)
    {
        var validas = partes.Select(Texto).OfType<string>().ToList();
        return validas.Count == 0 ? null : string.Join(separador, validas);
    }

    /// <summary>Una fecha anterior a 1900 es el valor por defecto de una carga antigua, no una fecha.</summary>
    private static DateTime? FechaValida(DateTime? fecha) =>
        fecha is { } f && f >= CensoVisibility.FechaEgresoMinima ? f.Date : null;

    private static string? FechaTexto(DateTime? fecha) =>
        FechaValida(fecha) is { } f ? HojaVidaFormato.Fecha(f) : null;

    private static DateTime? Fecha(string? texto)
    {
        if (Texto(texto) is not { } t)
        {
            return null;
        }

        if (DateTime.TryParseExact(t, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exacta)
            || DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out exacta))
        {
            return FechaValida(exacta);
        }

        return null;
    }

    private static int? Entero(string? texto)
    {
        if (Texto(texto) is not { } t)
        {
            return null;
        }

        var digitos = new string(t.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static T? LeerJson<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOpciones);
        }
        catch (JsonException)
        {
            // Un JSON histórico mal formado no debe tumbar la hoja de vida: esa parte se omite.
            return null;
        }
    }

    private static string ClasePrograma(string? programa) => programa switch
    {
        CensoProgramas.Agudos => "agudos",
        CensoProgramas.Cronicos => "cronicos",
        CensoProgramas.ClinicaHeridas => "heridas",
        CensoProgramas.Npt => "npt",
        CensoProgramas.TerapiaAmbulatoria => "terapia",
        _ => "otro"
    };

    // Formas de los JSON que guarda el censo. Todas las propiedades son texto porque así las
    // escribe el formulario; se convierten con Fecha/Entero al leerlas.

    private sealed class ProrrogaDatos
    {
        public string? TipoProrroga { get; set; }
        public string? NumeroDiasExtension { get; set; }
        public string? FechaInicioTratamiento { get; set; }
        public string? FechaFinTratamiento { get; set; }
        public string? NombreMedicamentoPrincipal { get; set; }
        public string? DosisMedicamentoPrincipal { get; set; }
        public string? MedidaMedicamentoPrincipal { get; set; }
        public string? ViaAdministracionMedicamentoPrincipal { get; set; }
        public string? FrecuenciaAdministracionMxPrincipal { get; set; }
        public string? DiasMedicamentoPrincipal { get; set; }
        public string? NombreMedicamentoNumero2 { get; set; }
        public string? DosisMedicamento2 { get; set; }
        public string? MedidaMedicamento2 { get; set; }
        public string? ViaAdministracionMedicamento2 { get; set; }
        public string? FrecuenciaAdministracionMedicamento2 { get; set; }
        public string? DiasMedicamento2 { get; set; }
        public string? NombreMedicamentoNumero3 { get; set; }
        public string? DosisMedicamento3 { get; set; }
        public string? MedidaMedicamento3 { get; set; }
        public string? ViaAdministracionMedicamento3 { get; set; }
        public string? FrecuenciaAdministracionMedicamento3 { get; set; }
        public string? DiasMedicamento3 { get; set; }
        public string? NombreMedicamentoNumero4 { get; set; }
        public string? DosisMedicamento4 { get; set; }
        public string? MedidaMedicamento4 { get; set; }
        public string? ViaAdministracionMedicamento4 { get; set; }
        public string? FrecuenciaAdministracionMedicamento4 { get; set; }
        public string? DiasMedicamento4 { get; set; }
        public string? NombreMedicamentoNumero5 { get; set; }
        public string? DosisMedicamento5 { get; set; }
        public string? MedidaMedicamento5 { get; set; }
        public string? ViaAdministracionMedicamento5 { get; set; }
        public string? FrecuenciaAdministracionMedicamento5 { get; set; }
        public string? DiasMedicamento5 { get; set; }
        public string? NombreMedicamentoNumero6 { get; set; }
        public string? DosisMedicamento6 { get; set; }
        public string? MedidaMedicamento6 { get; set; }
        public string? ViaAdministracionMedicamento6 { get; set; }
        public string? FrecuenciaAdministracionMedicamento6 { get; set; }
        public string? DiasMedicamento6 { get; set; }
    }

    private sealed class AgudizacionDatos
    {
        public string? CodigoCie10 { get; set; }
        public string? DiagnosticoDescriptivo { get; set; }
        public string? FechaInicioTratamiento { get; set; }
        public string? FechaFinTratamiento { get; set; }
        public List<AgudizacionMedicamentoDatos>? Medicamentos { get; set; }
    }

    private sealed class AgudizacionMedicamentoDatos
    {
        public int Numero { get; set; }
        public string? Nombre { get; set; }
        public string? Dosis { get; set; }
        public string? Medida { get; set; }
        public string? Via { get; set; }
        public string? Frecuencia { get; set; }
        public string? Dias { get; set; }
    }

    private sealed class HospitalizacionCronicoDatos
    {
        public string? FechaHospitalizacion { get; set; }
        public string? MotivoHospitalizacion { get; set; }
        public string? RemitidoPor { get; set; }
        public string? IpsIntramural { get; set; }
        public string? CodigoCie10 { get; set; }
        public string? DiagnosticoDescriptivo { get; set; }
        public string? FechaAlta { get; set; }
    }
}
