using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKardexClosureAndProrrogaVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FarmaciaProrrogaVersionId",
                table: "censo",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KardexCerradoAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "KardexCerradoPorFarmaciaId",
                table: "censo",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProrrogaCerradaAtUtc",
                table: "censo",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProrrogaCerradaPorFarmaciaId",
                table: "censo",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "censo_prorrogas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoRecordId = table.Column<long>(type: "bigint", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    ProrrogaJson = table.Column<string>(type: "text", nullable: false),
                    KardexEdicionJson = table.Column<string>(type: "text", nullable: true),
                    RequisicionFarmaciaJson = table.Column<string>(type: "text", nullable: true),
                    FarmaciaDispatchRecordId = table.Column<long>(type: "bigint", nullable: true),
                    CerradaAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CerradaPorFarmaciaId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_prorrogas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_prorrogas_censo_CensoRecordId",
                        column: x => x.CensoRecordId,
                        principalTable: "censo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_FarmaciaProrrogaVersionId",
                table: "censo",
                column: "FarmaciaProrrogaVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_prorrogas_CensoRecordId_Numero",
                table: "censo_prorrogas",
                columns: new[] { "CensoRecordId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_prorrogas_FarmaciaDispatchRecordId",
                table: "censo_prorrogas",
                column: "FarmaciaDispatchRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_prorrogas");

            migrationBuilder.DropIndex(
                name: "IX_censo_FarmaciaProrrogaVersionId",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "FarmaciaProrrogaVersionId",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "KardexCerradoAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "KardexCerradoPorFarmaciaId",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ProrrogaCerradaAtUtc",
                table: "censo");

            migrationBuilder.DropColumn(
                name: "ProrrogaCerradaPorFarmaciaId",
                table: "censo");
        }
    }
}
