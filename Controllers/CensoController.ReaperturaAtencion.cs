using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexa.Data;
using Nexa.Data.Entities;
using Nexa.Models.Security;
using Nexa.Services.Models;

namespace Nexa.Controllers;

/// <summary>
/// Reapertura de una atención cerrada.
///
/// Una atención dada de alta se puede consultar y admite lo que ocurre después del alta, pero sus
/// datos clínicos no se editan. Cuando el alta fue un error —o el paciente vuelve al mismo
/// episodio y no a uno nuevo— hay que poder devolverla a activa.
///
/// Lo hace directamente quien tiene el permiso de reapertura, el mismo que aprueba la del kardex.
/// A diferencia de aquella no hay solicitud previa: quien puede aprobar, reabre.
/// </summary>
public partial class CensoController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = SystemPermissions.Aprobacion)]
    public async Task<IActionResult> ReabrirAtencion(
        long episodioId,
        string? cedulaPaciente,
        CancellationToken cancellationToken)
    {
        var episodio = await _context.CensoPacienteProgramas
            .Include(x => x.CensoPaciente)
            .FirstOrDefaultAsync(x => x.Id == episodioId, cancellationToken);

        if (episodio is null)
        {
            TempData["ErrorMessage"] = "No se encontró la atención que se quiere reabrir.";
            return RedirectToAction(nameof(Index), new { cedulaPaciente });
        }

        if (episodio.CerradoAtUtc is null)
        {
            TempData["ErrorMessage"] = "Esta atención ya está abierta.";
            return RedirectToAction(nameof(Index), new { cedulaPaciente, programa = episodio.Programa });
        }

        // Reabrir no puede crear una combinación que agregar no permitiría. La regla vive en
        // CensoProgramas y aquí se consulta igual que en el carril: si el paciente ya tiene
        // abierto un programa que choca con este, primero hay que cerrar aquel.
        var abiertos = await _context.CensoPacienteProgramas
            .AsNoTracking()
            .Where(x => x.CensoPacienteId == episodio.CensoPacienteId
                && x.CerradoAtUtc == null
                && x.Id != episodio.Id)
            .Select(x => x.Programa)
            .ToListAsync(cancellationToken);

        if (CensoProgramas.PrimeroQueBloquea(episodio.Programa, abiertos) is { } bloquea)
        {
            TempData["ErrorMessage"] =
                $"No se puede reabrir {CensoProgramas.Nombre(episodio.Programa)}: el paciente tiene "
                + $"{CensoProgramas.Nombre(bloquea)} activo, y los dos no pueden convivir.";
            return RedirectToAction(nameof(Index), new { cedulaPaciente, programa = bloquea });
        }

        // Reabrir el episodio no basta: su estado es una copia del que vive en el registro del
        // censo, y la reconciliación lo volvería a cerrar en la siguiente visita. Lo que hay que
        // devolver a activo es el registro; el episodio se abre en el mismo movimiento.
        var descripcion = await ReabrirRegistroAsync(episodio, cancellationToken);
        if (descripcion is null)
        {
            TempData["ErrorMessage"] =
                "Esta atención no tiene un registro que reabrir. Agrega el programa para iniciar una nueva.";
            return RedirectToAction(nameof(Index), new { cedulaPaciente, programa = episodio.Programa });
        }

        episodio.CerradoAtUtc = null;
        episodio.CerradoPor = null;
        episodio.MotivoCierre = null;

        await _context.SaveChangesAsync(cancellationToken);

        // Clinica de heridas es el unico programa espejado en Supabase. Si se reabre y no se
        // avisa, el portal seguiria teniendo al paciente como egresado y rechazando sus
        // seguimientos. Solo encola: la reapertura no espera al puente ni falla si no responde.
        if (episodio.Programa == CensoProgramas.ClinicaHeridas)
        {
            _bridgeSyncQueue.Enqueue(new BridgePatient(
                episodio.CensoPaciente.NumeroIdentificacion,
                episodio.CensoPaciente.NombrePaciente));
        }

        await _auditService.LogAsync(
            "CENSO_ATENCION_REABIERTA",
            "Censo",
            $"Programa: {CensoProgramas.Nombre(episodio.Programa)}, "
                + $"Paciente: {episodio.CensoPaciente.NombrePaciente}, "
                + $"Doc: {episodio.CensoPaciente.NumeroIdentificacion}, "
                + $"Episodio: {episodio.Id}, {descripcion}",
            UsuarioActualId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        TempData["SuccessMessage"] =
            $"Atención de {CensoProgramas.Nombre(episodio.Programa)} reabierta. {descripcion}";

        return RedirectToAction(nameof(Index), new
        {
            cedulaPaciente = cedulaPaciente ?? episodio.CensoPaciente.NumeroIdentificacion,
            programa = episodio.Programa,
            atencion = episodio.Id
        });
    }

    /// <summary>
    /// Devuelve a activo el registro de la atención, con las reglas de su propio censo: lo que se
    /// deshace es exactamente lo que <c>Conciliar</c> mira para darla por cerrada. Si no se
    /// limpiara el egreso, la reconciliación volvería a cerrarla al pintar la pantalla.
    ///
    /// Devuelve una frase con lo que cambió, para el mensaje y la bitácora, o null si el episodio
    /// no tiene registro.
    /// </summary>
    private async Task<string?> ReabrirRegistroAsync(
        CensoPacientePrograma episodio,
        CancellationToken ct)
    {
        if (episodio.RegistroId is not { } registroId)
        {
            return null;
        }

        switch (episodio.Programa)
        {
            case CensoProgramas.Agudos:
            {
                var r = await _context.Censos.FirstOrDefaultAsync(x => x.Id == registroId, ct);
                if (r is null) { return null; }
                var antes = r.Estado;
                r.Estado = "Aceptado activo";
                r.FechaAlta = null;
                return $"Estado: «{antes}» → «Aceptado activo»";
            }

            case CensoProgramas.Cronicos:
            {
                var r = await _context.CensoCronicos.FirstOrDefaultAsync(x => x.Id == registroId, ct);
                if (r is null) { return null; }
                var antes = r.MotivoEgreso ?? r.EstadoPaciente;
                r.EstadoPaciente = "Activo";
                r.FechaEgreso = null;
                r.MotivoEgreso = null;
                return $"Egreso retirado (era «{antes}»), estado: «Activo»";
            }

            case CensoProgramas.ClinicaHeridas:
            {
                var r = await _context.CensoClinicaHeridas.FirstOrDefaultAsync(x => x.Id == registroId, ct);
                if (r is null) { return null; }
                var antes = r.MotivoEgreso ?? r.Estado;
                r.Estado = "Activo";
                r.FechaEgreso = null;
                r.MotivoEgreso = null;
                return $"Egreso retirado (era «{antes}»), estado: «Activo»";
            }

            case CensoProgramas.Npt:
            {
                var r = await _context.CensoNpt.FirstOrDefaultAsync(x => x.Id == registroId, ct);
                if (r is null) { return null; }
                var antes = r.MotivoEgreso ?? r.Estado;
                r.Estado = "Activo";
                r.FechaEgreso = null;
                r.MotivoEgreso = null;
                return $"Egreso retirado (era «{antes}»), estado: «Activo»";
            }

            case CensoProgramas.TerapiaAmbulatoria:
            {
                var r = await _context.CensoTerapiasAmbulatorias.FirstOrDefaultAsync(x => x.Id == registroId, ct);
                if (r is null) { return null; }
                var antes = r.MotivoAlta ?? r.EstadoAlta;
                r.EstadoPaciente = "Activo";
                // "Activo" es el primero de TerapiaAmbulatoriaEstadoAltaValues; "Abierto" no
                // existe en ese catalogo y su formulario lo habria rechazado al guardar.
                r.EstadoAlta = "Activo";
                r.FechaAlta = null;
                r.MotivoAlta = null;
                return $"Alta retirada (era «{antes}»), estado: «Activo»";
            }

            default:
                return null;
        }
    }
}
