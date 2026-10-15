using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAdjuntosAgudizacionCronico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_cronico_agudizacion_adjuntos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoCronicoAgudizacionId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_cronico_agudizacion_adjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_cronico_agudizacion_adjuntos_censo_cronico_agudizacio~",
                        column: x => x.CensoCronicoAgudizacionId,
                        principalTable: "censo_cronico_agudizaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_cronico_agudizacion_adjuntos_CensoCronicoAgudizacionId",
                table: "censo_cronico_agudizacion_adjuntos",
                column: "CensoCronicoAgudizacionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_cronico_agudizacion_adjuntos");
        }
    }
}
