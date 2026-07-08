using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoTerapiaTratamientosAdicionales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SegundoTratamientoCantidad",
                table: "censo_terapias_ambulatorias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegundoTratamientoFrecuenciaTerapia",
                table: "censo_terapias_ambulatorias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SegundoTratamientoTipoTerapia",
                table: "censo_terapias_ambulatorias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TercerTratamientoCantidad",
                table: "censo_terapias_ambulatorias",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TercerTratamientoFrecuenciaTerapia",
                table: "censo_terapias_ambulatorias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TercerTratamientoTipoTerapia",
                table: "censo_terapias_ambulatorias",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TieneSegundoTratamiento",
                table: "censo_terapias_ambulatorias",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TieneTercerTratamiento",
                table: "censo_terapias_ambulatorias",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SegundoTratamientoCantidad",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "SegundoTratamientoFrecuenciaTerapia",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "SegundoTratamientoTipoTerapia",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "TercerTratamientoCantidad",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "TercerTratamientoFrecuenciaTerapia",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "TercerTratamientoTipoTerapia",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "TieneSegundoTratamiento",
                table: "censo_terapias_ambulatorias");

            migrationBuilder.DropColumn(
                name: "TieneTercerTratamiento",
                table: "censo_terapias_ambulatorias");
        }
    }
}
