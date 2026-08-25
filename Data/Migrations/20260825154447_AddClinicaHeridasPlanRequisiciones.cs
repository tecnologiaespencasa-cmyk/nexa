using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicaHeridasPlanRequisiciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La tabla de planes va primero: las requisiciones que ya existen tienen que poder
            // engancharse a un plan antes de que la columna pase a obligatoria.
            migrationBuilder.CreateTable(
                name: "censo_clinica_heridas_plan",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CensoClinicaHeridasRecordId = table.Column<long>(type: "bigint", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    CreadoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CerradoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CerradoPor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApositoMedicamento1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApositoMedicamento2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApositoMedicamento3 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApositoMedicamento4 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DuracionTratamientoDias = table.Column<int>(type: "integer", nullable: true),
                    FrecuenciaVisita = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo_clinica_heridas_plan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_censo_clinica_heridas_plan_censo_clinica_heridas_CensoClini~",
                        column: x => x.CensoClinicaHeridasRecordId,
                        principalTable: "censo_clinica_heridas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_plan_CensoClinicaHeridasRecordId_Nume~",
                table: "censo_clinica_heridas_plan",
                columns: new[] { "CensoClinicaHeridasRecordId", "Numero" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasRecordId_Ti~",
                table: "censo_clinica_heridas_kardex");

            // Nullable de momento: se llena con el backfill de abajo.
            migrationBuilder.AddColumn<long>(
                name: "CensoClinicaHeridasPlanId",
                table: "censo_clinica_heridas_kardex",
                type: "bigint",
                nullable: true);

            // Backfill: todo lo que ya existia pasa a ser el Plan 1 de su paciente, abierto y con
            // los apositos y el tratamiento que hoy tiene el censo.
            migrationBuilder.Sql("""
                insert into censo_clinica_heridas_plan
                    ("CensoClinicaHeridasRecordId", "Numero", "CreadoPor", "CreadoAtUtc",
                     "ApositoMedicamento1", "ApositoMedicamento2", "ApositoMedicamento3",
                     "ApositoMedicamento4", "DuracionTratamientoDias", "FrecuenciaVisita")
                select
                    r."Id",
                    1,
                    'Migración',
                    coalesce(min(k."CreatedAtUtc"), now() at time zone 'utc'),
                    r."ApositoMedicamento1",
                    r."ApositoMedicamento2",
                    r."ApositoMedicamento3",
                    r."ApositoMedicamento4",
                    r."DuracionTratamientoDias",
                    r."FrecuenciaVisita"
                from censo_clinica_heridas r
                join censo_clinica_heridas_kardex k on k."CensoClinicaHeridasRecordId" = r."Id"
                group by r."Id";
                """);

            migrationBuilder.Sql("""
                update censo_clinica_heridas_kardex k
                set "CensoClinicaHeridasPlanId" = p."Id"
                from censo_clinica_heridas_plan p
                where p."CensoClinicaHeridasRecordId" = k."CensoClinicaHeridasRecordId"
                  and p."Numero" = 1;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "CensoClinicaHeridasPlanId",
                table: "censo_clinica_heridas_kardex",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasPlanId_Tipo",
                table: "censo_clinica_heridas_kardex",
                columns: new[] { "CensoClinicaHeridasPlanId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasRecordId",
                table: "censo_clinica_heridas_kardex",
                column: "CensoClinicaHeridasRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_censo_clinica_heridas_kardex_censo_clinica_heridas_plan_Cen~",
                table: "censo_clinica_heridas_kardex",
                column: "CensoClinicaHeridasPlanId",
                principalTable: "censo_clinica_heridas_plan",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_censo_clinica_heridas_kardex_censo_clinica_heridas_plan_Cen~",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropTable(
                name: "censo_clinica_heridas_plan");

            migrationBuilder.DropIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasPlanId_Tipo",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasRecordId",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.DropColumn(
                name: "CensoClinicaHeridasPlanId",
                table: "censo_clinica_heridas_kardex");

            migrationBuilder.CreateIndex(
                name: "IX_censo_clinica_heridas_kardex_CensoClinicaHeridasRecordId_Ti~",
                table: "censo_clinica_heridas_kardex",
                columns: new[] { "CensoClinicaHeridasRecordId", "Tipo" },
                unique: true);
        }
    }
}
