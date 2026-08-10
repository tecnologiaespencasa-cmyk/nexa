using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoKardexReapertura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_kardex_reaperturas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoRecordId = table.Column<long>(type: "bigint", nullable: false),
                    ProrrogaVersionId = table.Column<long>(type: "bigint", nullable: true),
                    TipoDocumento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SolicitadoPorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SolicitadoPorNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SolicitadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResueltoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResueltoPorNombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResueltoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ObservacionResolucion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_kardex_reaperturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_kardex_reaperturas_censo_CensoRecordId",
                        column: x => x.CensoRecordId,
                        principalTable: "censo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_kardex_reaperturas_CensoRecordId_Estado",
                table: "censo_kardex_reaperturas",
                columns: new[] { "CensoRecordId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_censo_kardex_reaperturas_ProrrogaVersionId",
                table: "censo_kardex_reaperturas",
                column: "ProrrogaVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_kardex_reaperturas");
        }
    }
}
