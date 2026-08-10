using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixAmbiguousBaseProrrogaClosures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE public.censo AS parent
                SET "ProrrogaCerradaAtUtc" = NULL,
                    "ProrrogaCerradaPorFarmaciaId" = NULL
                WHERE parent."ProrrogaCerradaAtUtc" IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM public.censo AS pending_dispatch
                      WHERE pending_dispatch."FarmaciaProrrogaDeId" = parent."Id"
                        AND pending_dispatch."FarmaciaProrrogaVersionId" IS NULL
                        AND pending_dispatch."FarmaciaOkKardex" = FALSE
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM public.censo AS approved_dispatch
                      WHERE approved_dispatch."Id" = parent."ProrrogaCerradaPorFarmaciaId"
                        AND approved_dispatch."FarmaciaProrrogaDeId" = parent."Id"
                        AND approved_dispatch."FarmaciaProrrogaVersionId" IS NULL
                        AND approved_dispatch."FarmaciaOkKardex" = TRUE
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
