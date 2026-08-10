using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillKardexClosureFromOkKardex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.censo
                SET "KardexCerradoAtUtc" = COALESCE("FarmaciaKardexVistoAtUtc", "FarmaciaEnviadoAtUtc", "CreatedAtUtc", NOW()),
                    "KardexCerradoPorFarmaciaId" = "Id"
                WHERE "FarmaciaOkKardex" = TRUE
                  AND "FarmaciaProrrogaDeId" IS NULL
                  AND "FarmaciaProrrogaVersionId" IS NULL
                  AND "KardexCerradoAtUtc" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE public.censo
                SET "ProrrogaCerradaAtUtc" = COALESCE("KardexCerradoAtUtc", "FarmaciaKardexVistoAtUtc", "FarmaciaEnviadoAtUtc", "CreatedAtUtc", NOW()),
                    "ProrrogaCerradaPorFarmaciaId" = "Id"
                WHERE "FarmaciaOkKardex" = TRUE
                  AND "EsProrroga" = TRUE
                  AND COALESCE("ProrrogaJson", '') <> ''
                  AND "FarmaciaProrrogaDeId" IS NULL
                  AND "FarmaciaProrrogaVersionId" IS NULL
                  AND "ProrrogaCerradaAtUtc" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE public.censo parent
                SET "ProrrogaCerradaAtUtc" = COALESCE(dispatch."KardexCerradoAtUtc", dispatch."FarmaciaKardexVistoAtUtc", dispatch."FarmaciaEnviadoAtUtc", dispatch."CreatedAtUtc", NOW()),
                    "ProrrogaCerradaPorFarmaciaId" = dispatch."Id"
                FROM public.censo dispatch
                WHERE dispatch."FarmaciaOkKardex" = TRUE
                  AND dispatch."FarmaciaProrrogaDeId" = parent."Id"
                  AND parent."ProrrogaCerradaAtUtc" IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE public.censo_prorrogas prorroga
                SET "CerradaAtUtc" = COALESCE(dispatch."KardexCerradoAtUtc", dispatch."FarmaciaKardexVistoAtUtc", dispatch."FarmaciaEnviadoAtUtc", dispatch."CreatedAtUtc", NOW()),
                    "CerradaPorFarmaciaId" = dispatch."Id"
                FROM public.censo dispatch
                WHERE dispatch."FarmaciaOkKardex" = TRUE
                  AND dispatch."FarmaciaProrrogaVersionId" = prorroga."Id"
                  AND prorroga."CerradaAtUtc" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
