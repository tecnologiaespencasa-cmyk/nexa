using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoTerapiaAmbulatoriaProrrogas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_terapia_ambulatoria_prorrogas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoTerapiaAmbulatoriaRecordId = table.Column<long>(type: "bigint", nullable: false),
                    FechaSolicitudProrroga = table.Column<DateTime>(type: "date", nullable: false),
                    FechaSolicitudAsegurador = table.Column<DateTime>(type: "date", nullable: false),
                    FechaEntregaAutorizacion = table.Column<DateTime>(type: "date", nullable: false),
                    CodigoAutorizacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Frecuencia = table.Column<int>(type: "integer", nullable: false),
                    Cantidad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_terapia_ambulatoria_prorrogas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_terapia_ambulatoria_prorrogas_censo_terapias_ambulato~",
                        column: x => x.CensoTerapiaAmbulatoriaRecordId,
                        principalTable: "censo_terapias_ambulatorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapia_ambulatoria_prorrogas_CensoTerapiaAmbulatoria~",
                table: "censo_terapia_ambulatoria_prorrogas",
                column: "CensoTerapiaAmbulatoriaRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_terapia_ambulatoria_prorrogas_CreatedAtUtc",
                table: "censo_terapia_ambulatoria_prorrogas",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_terapia_ambulatoria_prorrogas");
        }
    }
}
