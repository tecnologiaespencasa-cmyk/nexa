using IntranetPrueba.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260609100000_AddCensoAdjuntos")]
    public partial class AddCensoAdjuntos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo_adjuntos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoRecordId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_adjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_adjuntos_censo_CensoRecordId",
                        column: x => x.CensoRecordId,
                        principalTable: "censo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_adjuntos_CensoRecordId",
                table: "censo_adjuntos",
                column: "CensoRecordId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "censo_adjuntos");
        }
    }
}
