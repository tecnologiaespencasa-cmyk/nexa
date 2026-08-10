using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueCensoTerapiaProrrogaPaciente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_censo_terapia_ambulatoria_prorrogas_CensoTerapiaAmbulatoria~",
                table: "censo_terapia_ambulatoria_prorrogas");

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapia_ambulatoria_prorrogas_CensoTerapiaAmbulatoria~",
                table: "censo_terapia_ambulatoria_prorrogas",
                column: "CensoTerapiaAmbulatoriaRecordId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_censo_terapia_ambulatoria_prorrogas_CensoTerapiaAmbulatoria~",
                table: "censo_terapia_ambulatoria_prorrogas");

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapia_ambulatoria_prorrogas_CensoTerapiaAmbulatoria~",
                table: "censo_terapia_ambulatoria_prorrogas",
                column: "CensoTerapiaAmbulatoriaRecordId");
        }
    }
}
