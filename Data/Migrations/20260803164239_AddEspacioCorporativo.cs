using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEspacioCorporativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "espacio_activos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TipoActivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NombreEquipo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Marca = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Serie = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Serial = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Especificaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CodigoActivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ResponsableUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsableNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    FechaAsignacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Estado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Disponible"),
                    Nota = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EliminadoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ActualizadoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_activos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_espacio_activos_Users_ResponsableUserId",
                        column: x => x.ResponsableUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "espacio_documentos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Categoria = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TipoDocumento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TipoContenido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CodigoDocumento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ArchivoNombre = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ArchivoContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ArchivoTamanoBytes = table.Column<long>(type: "bigint", nullable: true),
                    ArchivoContenido = table.Column<byte[]>(type: "bytea", nullable: true),
                    EnlaceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContenidoTexto = table.Column<string>(type: "text", nullable: true),
                    Etiquetas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Publicado = table.Column<bool>(type: "boolean", nullable: false),
                    Destacado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    FechaVigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Descargas = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreadoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ActualizadoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_documentos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "espacio_activo_movimientos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EspacioActivoId = table.Column<long>(type: "bigint", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Detalle = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    RegistradoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    RegistradoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_activo_movimientos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_espacio_activo_movimientos_espacio_activos_EspacioActivoId",
                        column: x => x.EspacioActivoId,
                        principalTable: "espacio_activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "espacio_activo_novedades",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EspacioActivoId = table.Column<long>(type: "bigint", nullable: true),
                    EquipoReferencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReportadoPorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportadoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ReportadoPorEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Reportada"),
                    Prioridad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Clasificacion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    RespuestaAdmin = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AtendidoPorNombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ResueltoAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificacionEnviada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_activo_novedades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_espacio_activo_novedades_espacio_activos_EspacioActivoId",
                        column: x => x.EspacioActivoId,
                        principalTable: "espacio_activos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "espacio_documento_favoritos",
                columns: table => new
                {
                    EspacioDocumentoId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_espacio_documento_favoritos", x => new { x.EspacioDocumentoId, x.UserId });
                    table.ForeignKey(
                        name: "FK_espacio_documento_favoritos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_espacio_documento_favoritos_espacio_documentos_EspacioDocum~",
                        column: x => x.EspacioDocumentoId,
                        principalTable: "espacio_documentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activo_movimientos_EspacioActivoId_RegistradoAtUtc",
                table: "espacio_activo_movimientos",
                columns: new[] { "EspacioActivoId", "RegistradoAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activo_novedades_CreatedAtUtc",
                table: "espacio_activo_novedades",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activo_novedades_EspacioActivoId",
                table: "espacio_activo_novedades",
                column: "EspacioActivoId");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activo_novedades_Estado",
                table: "espacio_activo_novedades",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activo_novedades_ReportadoPorUserId",
                table: "espacio_activo_novedades",
                column: "ReportadoPorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activos_CodigoActivo",
                table: "espacio_activos",
                column: "CodigoActivo");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activos_Eliminado",
                table: "espacio_activos",
                column: "Eliminado");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activos_Estado",
                table: "espacio_activos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activos_ResponsableUserId",
                table: "espacio_activos",
                column: "ResponsableUserId");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_activos_Serial",
                table: "espacio_activos",
                column: "Serial");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_documento_favoritos_UserId",
                table: "espacio_documento_favoritos",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_documentos_Categoria",
                table: "espacio_documentos",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_documentos_CreatedAtUtc",
                table: "espacio_documentos",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_documentos_Eliminado",
                table: "espacio_documentos",
                column: "Eliminado");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_documentos_Publicado",
                table: "espacio_documentos",
                column: "Publicado");

            migrationBuilder.CreateIndex(
                name: "IX_espacio_documentos_TipoDocumento",
                table: "espacio_documentos",
                column: "TipoDocumento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "espacio_activo_movimientos");

            migrationBuilder.DropTable(
                name: "espacio_activo_novedades");

            migrationBuilder.DropTable(
                name: "espacio_documento_favoritos");

            migrationBuilder.DropTable(
                name: "espacio_activos");

            migrationBuilder.DropTable(
                name: "espacio_documentos");
        }
    }
}
