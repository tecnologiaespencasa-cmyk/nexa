using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicamentosCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Medicamentos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NormalizedNombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PresentacionRequisicion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ConcentracionMiligramos = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Jeringa = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SolucionParaDilucion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DilucionRecomendada = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VehiculoReconstitucion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TiempoEstabilidad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TiempoInfusionMinutos = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    BombaInfusion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MarcacionRiesgo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Flebozantes = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    EquipoFotosensible = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicamentos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Medicamentos_NormalizedNombre",
                table: "Medicamentos",
                column: "NormalizedNombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Medicamentos");
        }
    }
}
