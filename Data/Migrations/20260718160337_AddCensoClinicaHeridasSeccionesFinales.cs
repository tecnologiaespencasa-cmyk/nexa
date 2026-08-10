using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexa.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCensoClinicaHeridasSeccionesFinales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "censo_clinica_heridas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoDevolucionServicioFarmaceutico",
                table: "censo_clinica_heridas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCuartoSeguimientoSemana1",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEgreso",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaHospitalizacion",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaMaximaDevolucionProductos",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaNovedadDevolucionProductos",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaPrimerSeguimiento24Horas",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaQuintoSeguimientoSemana2",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSegundoSeguimiento48Horas",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSeptimoSeguimientoSemana4",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaSextoSeguimientoSemana3",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaTercerSeguimiento72Horas",
                table: "censo_clinica_heridas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpsIntramural",
                table: "censo_clinica_heridas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoEgreso",
                table: "censo_clinica_heridas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoHospitalizacion",
                table: "censo_clinica_heridas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoNovedadDevolucionProductos",
                table: "censo_clinica_heridas",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificacionAuxiliarDevolucionProductos",
                table: "censo_clinica_heridas",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemitidoPorHospitalizacion",
                table: "censo_clinica_heridas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estado",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "EstadoDevolucionServicioFarmaceutico",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaCuartoSeguimientoSemana1",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaEgreso",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaHospitalizacion",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaMaximaDevolucionProductos",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaNovedadDevolucionProductos",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaPrimerSeguimiento24Horas",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaQuintoSeguimientoSemana2",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaSegundoSeguimiento48Horas",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaSeptimoSeguimientoSemana4",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaSextoSeguimientoSemana3",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "FechaTercerSeguimiento72Horas",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "IpsIntramural",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "MotivoEgreso",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "MotivoHospitalizacion",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "MotivoNovedadDevolucionProductos",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "NotificacionAuxiliarDevolucionProductos",
                table: "censo_clinica_heridas");

            migrationBuilder.DropColumn(
                name: "RemitidoPorHospitalizacion",
                table: "censo_clinica_heridas");
        }
    }
}
