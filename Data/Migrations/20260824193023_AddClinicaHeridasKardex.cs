using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicaHeridasKardex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManejoHerida",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Npt",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "censo_clinica_heridas_kardex",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoClinicaHeridasRecordId = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KardexJson = table.Column<string>(type: "text", nullable: true),
                    ElaboradoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FarmaciaEnviadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FarmaciaEstado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FarmaciaOkKardex = table.Column<bool>(type: "boolean", nullable: false),
                    FarmaciaKardexVistoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KardexCerradoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_clinica_heridas_kardex", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_clinica_heridas_kardex_censo_clinica_heridas_CensoCli~",
                        column: x => x.CensoClinicaHeridasRecordId,
                        principalTable: "censo_clinica_heridas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "censo_clinica_heridas_kardex_adjuntos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoClinicaHeridasKardexId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_clinica_heridas_kardex_adjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_clinica_heridas_kardex_adjuntos_censo_clinica_heridas~",
                        column: x => x.CensoClinicaHeridasKardexId,
                        principalTable: "censo_clinica_heridas_kardex",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasRecordId_Ti~",
                table: "censo_clinica_heridas_kardex",
                columns: new[] { "CensoClinicaHeridasRecordId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_kardex_FarmaciaEnviadoAtUtc",
                table: "censo_clinica_heridas_kardex",
                column: "FarmaciaEnviadoAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_kardex_adjuntos_CensoClinicaHeridasKa~",
                table: "censo_clinica_heridas_kardex_adjuntos",
                column: "CensoClinicaHeridasKardexId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_clinica_heridas_kardex_adjuntos");

            migrationBuilder.DropTable(
                name: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "ManejoHerida",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "Npt",
                table: "censo_clinica_heridas");
        }
    }
}
