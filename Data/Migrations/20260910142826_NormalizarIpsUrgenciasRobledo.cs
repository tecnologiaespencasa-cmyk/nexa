using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Unifica "URGENCIAS IPS SURA LOS ROBLEDO" en "IPS SURA ROBLEDO".
    ///
    /// El primero nunca estuvo en el catálogo de IPS del código y entró por el campo de texto
    /// libre del censo de agudos, que aceptaba cualquier cosa. Al 2026-09-10 lo tenían **407
    /// pacientes** y **686 filas de agudos** (189 de ellas copias internas de despacho a
    /// farmacia, que se actualizan también para no quedar en desacuerdo con su registro padre).
    /// Otros 315 pacientes ya usaban el nombre bueno; al unir quedan 722.
    ///
    /// Lo decidió el usuario: no son dos IPS distintas, es la misma escrita de dos maneras. Con
    /// el campo convertido en lista cerrada, dejar el valor viejo habría dejado a esos 407
    /// pacientes sin poder guardarse.
    ///
    /// Cambia un solo texto y no toca ninguna otra columna. Es idempotente: correrla de nuevo no
    /// encuentra nada que cambiar.
    ///
    /// Respaldo previo de las 1093 filas afectadas, con su id y su documento:
    /// C:\tmp-nexa\respaldo-ips\urgencias_ips_sura_los_robledo.{json,csv}
    /// </summary>
    public partial class NormalizarIpsUrgenciasRobledo : Migration
    {
        private const string Viejo = "URGENCIAS IPS SURA LOS ROBLEDO";
        private const string Nuevo = "IPS SURA ROBLEDO";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // censo_terapias_ambulatorias también tiene la columna, aunque hoy ninguna de sus
            // filas use el valor viejo. Se incluye para que la normalización no dependa de cómo
            // esté la base el día que esto corra.
            foreach (var tabla in new[] { "censo_paciente", "censo", "censo_terapias_ambulatorias" })
            {
                migrationBuilder.Sql($"""
                    UPDATE {tabla}
                    SET "IpsQueRemite" = '{Nuevo}'
                    WHERE "IpsQueRemite" = '{Viejo}';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: deshacerlo devolvería el valor viejo también a los 315 pacientes
            // que siempre tuvieron el bueno, porque después de unir ya no se distinguen. Para
            // recuperar el estado anterior hay que cargar el respaldo, que sí guarda qué fila
            // tenía cada valor.
        }
    }
}
