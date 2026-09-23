using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <summary>
    /// Retira "Estado del alta" (Activo/Pre-Alta/Cerrado) del censo de terapia ambulatoria: el alta
    /// del paciente pasa a ser solo el Estado del paciente.
    ///
    /// Antes de borrar la columna se pasan a "Alta" los registros cerrados solo ahí (Estado del alta
    /// en Cerrado con el paciente todavía "Activo"): hoy todo el sistema ya los trata como cerrados, y
    /// sin este paso volverían a verse activos al desaparecer la columna. El 2026-09-23 se corrigieron
    /// a mano los 9 que había (respaldo en C:\tmp-nexa\respaldo-terapia-estado-alta\); este paso cubre
    /// los que se cierren así en producción hasta que se despliegue esta versión. Si ya no queda
    /// ninguno, no toca nada.
    /// </summary>
    public partial class RetirarEstadoAltaTerapiaAmbulatoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "AuditLogs" ("Action", "Entity", "Details", "PerformedAtUtc")
                SELECT 'CENSO_TERAPIA_AMBULATORIA_ESTADO_CORREGIDO',
                       'CensoTerapiaAmbulatoria',
                       'Paciente: ' || "NombrePaciente" || ', Doc: ' || "NumeroIdentificacion"
                           || ', Estado del paciente: Activo -> Alta. Tenia el Estado del alta en Cerrado; '
                           || 'se unifica en el Estado del paciente al retirar ese campo del censo.',
                       now()
                FROM censo_terapias_ambulatorias
                WHERE "EstadoAlta" ILIKE 'Cerrado' AND "EstadoPaciente" ILIKE 'Activo';

                UPDATE censo_terapias_ambulatorias
                SET "EstadoPaciente" = 'Alta',
                    "UpdatedAtUtc" = now()
                WHERE "EstadoAlta" ILIKE 'Cerrado' AND "EstadoPaciente" ILIKE 'Activo';
                """);

            migrationBuilder.DropIndex(
                name: "IX_censo_terapias_ambulatorias_EstadoAlta",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "EstadoAlta",
                table: "censo_terapias_ambulatorias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Devuelve la columna, pero no su contenido: los valores anteriores están en el respaldo
            // columna_estado_alta_87_registros.json.
            migrationBuilder.AddColumn<string>(
                name: "EstadoAlta",
                table: "censo_terapias_ambulatorias",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Activo");

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapias_ambulatorias_EstadoAlta",
                table: "censo_terapias_ambulatorias",
                column: "EstadoAlta");
        }
    }
}
