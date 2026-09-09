using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Helpers;

namespace Nexa.Filters;

/// <summary>
/// Todo modelo de censo que edita una fila sabe cuál es. Lo declara para que el filtro pueda
/// preguntárselo sin reflexión ni suposiciones sobre el nombre del parámetro.
/// </summary>
public interface ICensoRegistroEditable
{
    long? EditingRecordId { get; }

    /// <summary>Documento del paciente, para poder devolver a la ficha donde estaba.</summary>
    string? CedulaFiltro { get; }
}

/// <summary>
/// Impide guardar sobre una atención ya cerrada las secciones que no se diligencian después del
/// alta.
///
/// Desde que la pantalla permite abrir atenciones cerradas para consultarlas y para registrar lo
/// que ocurre después del alta —la devolución de productos, la del equipo en comodato, una
/// rehospitalización—, el formulario de una atención dada de alta vuelve a estar al alcance. La
/// vista deja sus campos visibles pero fuera de alcance y le quita el botón de guardar, porque
/// una atención cerrada se abre justamente para leerla; eso es presentación. El candado de
/// verdad está aquí, del lado del servidor.
///
/// Solo mira las acciones de la tabla. Cualquier otra —farmacia, kardex, adjuntos, prórrogas,
/// reaperturas— pasa sin tocarse: tienen su propio ciclo de vida y bloquearlas rompería flujos
/// que no son de este cambio.
///
/// Agudos no aparece aquí porque su formulario entero viaja en un único POST y no se puede
/// aceptar o rechazar en bloque sin romper el caso legítimo; lo resuelve
/// CensoController.RevertirCamposBloqueadosDeAgudos, campo por campo.
/// </summary>
public sealed class CensoAtencionCerradaFilter(ApplicationDbContext context) : IAsyncActionFilter
{
    private static readonly Dictionary<string, (string Programa, string Seccion)> AccionesDeSeccion =
        new(StringComparer.Ordinal)
        {
            // Crónicos
            ["GuardarCronicoGestionCaso"] = (CensoProgramas.Cronicos, "tab-cronicos-gestion-caso"),
            ["GuardarCronicoHospitalizacion"] = (CensoProgramas.Cronicos, "tab-cronicos-hospitalizacion"),
            ["GuardarCronicoHospitalizacionRegistro"] = (CensoProgramas.Cronicos, "tab-cronicos-hospitalizacion"),
            ["GuardarCronicoAgudizacion"] = (CensoProgramas.Cronicos, "tab-cronicos-agudizaciones"),

            // Clínica de heridas
            ["GuardarClinicaHeridasManejoHerida"] = (CensoProgramas.ClinicaHeridas, "tab-heridas-manejo"),
            ["GuardarClinicaHeridasVac"] = (CensoProgramas.ClinicaHeridas, "tab-heridas-manejo"),
            ["GuardarClinicaHeridasActivoFijo"] = (CensoProgramas.ClinicaHeridas, "tab-heridas-activo-fijo"),
            ["GuardarClinicaHeridasSeguimientoHospitalizado"] = (CensoProgramas.ClinicaHeridas, "tab-heridas-seguimiento-hospitalizado"),
            ["GuardarClinicaHeridasDevolucionProductos"] = (CensoProgramas.ClinicaHeridas, "tab-heridas-devolucion-productos"),
            ["GuardarClinicaHeridasAltaPrograma"] = (CensoProgramas.ClinicaHeridas, "tab-heridas-alta-programa"),

            // NPT
            ["GuardarNptManejo"] = (CensoProgramas.Npt, "tab-npt-manejo"),
            ["GuardarNptCargueServicios"] = (CensoProgramas.Npt, "tab-npt-cargue-servicios"),
            ["GuardarNptActivoFijo"] = (CensoProgramas.Npt, "tab-npt-activo-fijo"),
            ["GuardarNptSeguimientoHospitalizado"] = (CensoProgramas.Npt, "tab-npt-seguimiento-hospitalizado"),
            ["GuardarNptDevolucionProductos"] = (CensoProgramas.Npt, "tab-npt-devolucion-productos"),
            ["GuardarNptAltaPrograma"] = (CensoProgramas.Npt, "tab-npt-alta-programa"),

            // Terapia ambulatoria: ninguna de sus secciones sobrevive al alta.
            ["GuardarTerapiaAmbulatoriaProrroga"] = (CensoProgramas.TerapiaAmbulatoria, "tab-terapia-prorroga"),
            ["GuardarTerapiaAmbulatoriaGestionAlta"] = (CensoProgramas.TerapiaAmbulatoria, "tab-terapia-gestion-alta")
        };

    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var accion = (ctx.ActionDescriptor as ControllerActionDescriptor)?.ActionName;

        if (accion is null
            || !AccionesDeSeccion.TryGetValue(accion, out var destino)
            || CensoProgramaSecciones.SeEditaTrasElAlta(destino.Seccion))
        {
            await next();
            return;
        }

        var registroId = ctx.ActionArguments.Values
            .OfType<ICensoRegistroEditable>()
            .Select(x => x.EditingRecordId)
            .FirstOrDefault(x => x.HasValue);

        // Sin fila todavía no hay atención que proteger: es un ingreso nuevo.
        if (registroId is null)
        {
            await next();
            return;
        }

        var cerrada = await context.CensoPacienteProgramas
            .AsNoTracking()
            .AnyAsync(e => e.Programa == destino.Programa
                    && e.RegistroId == registroId
                    && e.CerradoAtUtc != null,
                ctx.HttpContext.RequestAborted);

        if (!cerrada)
        {
            await next();
            return;
        }

        // Se rechaza sin escribir nada y se devuelve a la misma atención, para que quien lo
        // intentó vea en pantalla la razón y siga en el sitio donde estaba.
        var seccion = CensoProgramaSecciones.De(destino.Programa)
            .FirstOrDefault(s => s.Id == destino.Seccion);

        if (ctx.Controller is Controller controlador)
        {
            controlador.TempData["ErrorMessage"] =
                $"«{seccion?.Titulo ?? "Esta sección"}» no se puede modificar en una atención dada de alta. "
                + "Lo que sí se puede registrar después del alta es la devolución de productos, "
                + "el activo fijo y los seguimientos de hospitalización.";
        }

        var documento = ctx.ActionArguments.Values
            .OfType<ICensoRegistroEditable>()
            .Select(x => x.CedulaFiltro)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        ctx.Result = new RedirectToActionResult("Index", "Censo", new
        {
            cedulaPaciente = documento,
            programa = destino.Programa,
            seccion = destino.Seccion
        });
    }
}
