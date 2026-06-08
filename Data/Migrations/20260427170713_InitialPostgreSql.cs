using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntranetPrueba.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "censo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Asegurador = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "date", nullable: false),
                    HoraIngreso = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "date", nullable: false),
                    HoraRespuesta = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                    IndicadorTiempoRespuestaMinutos = table.Column<int>(type: "integer", nullable: false),
                    NombrePerfilGestionaCaso = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NombreRecepcionaCaso = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NombreRealizaKardex = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NombrePaciente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TipoIdentificacion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    NumeroIdentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodigoCie10 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    DiagnosticoDescriptivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "date", nullable: false),
                    Edad = table.Column<int>(type: "integer", nullable: false),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClasificacionZonaSura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MunicipioResidencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Barrio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ZonaDireccionSegunMunicipio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Area = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IpsQueRemite = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VistoBuenoRangoFueraAnexo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Telefono1 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Telefono2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Telefono3 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ClasificacionRiesgo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AdministracionMedicamentos = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TratamientoFarmacologicoIvNebulizacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PaqueteSura = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NombreMedicamentoPrincipalTratante = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FrecuenciaAdministracionMxPrincipal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NumeroDosisDiaMedicamentoPrincipal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NombreMedicamentoNumero2 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FrecuenciaAdministracionMedicamento2 = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NumeroDosisMedicamento2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NombreMedicamentoNumero3 = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FrecuenciaAdministracionMedicamento3 = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NumeroDosisMedicamento3 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AplicacionesTotales = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DiasTratamientoIv = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CambioFrecuenciaAdministracionTto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FrecuenciaAjustada = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FechaInicioTratamiento = table.Column<DateTime>(type: "date", nullable: true),
                    FechaFinTratamiento = table.Column<DateTime>(type: "date", nullable: true),
                    FechaPromesaInicioTto = table.Column<DateTime>(type: "date", nullable: true),
                    HoraPromesaInicioTto = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AuxiliarAsignado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Estado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AutorizacionEvento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponsableLlamadaBienvenida = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EstadoLlamadaBienvenida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ObservacionesPlanManejo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NumeroTelefonoLlamadaBienvenida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DiasEstancia = table.Column<int>(type: "integer", nullable: true),
                    NumeroDiasAutorizado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RequiereServiciosComplementarios = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PacienteGestante = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Nebulizaciones = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    SistemasPresionNegativaVac = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    NutricionParenteral = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    NutricionEnteral = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PacienteAnticoagulado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    LaboratorioClinicoProcedimiento = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ClinicaHeridas = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CateterismoOSv = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CateterPicc = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    NumeroCalibreSonda = table.Column<int>(type: "integer", nullable: true),
                    FechaUltimoCambioSonda = table.Column<DateTime>(type: "date", nullable: true),
                    AuxiliarAsignadoCateterismo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FechaProximoCambioSonda = table.Column<DateTime>(type: "date", nullable: true),
                    FechaUltimaCuracionPicc = table.Column<DateTime>(type: "date", nullable: true),
                    FechaAlta = table.Column<DateTime>(type: "date", nullable: true),
                    NombreQuienGestionaAlta = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AltaTardia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaPrimerSeguimiento24Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSegundoSeguimiento48Horas = table.Column<DateTime>(type: "date", nullable: true),
                    FechaTercerSeguimiento72Horas = table.Column<DateTime>(type: "date", nullable: true),
                    ObservacionAltaTardia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NombreQuienRealizaSeguimientoAltaTardia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PacienteRehospitalizado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaRegistroReporteRehospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    FechaRehospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoRehospitalizacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    AmpliacionMotivoRehospitalizacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RemitidoPorRehospitalizacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IpsIntramuralRehospitalizacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FechaPrimerSeguimientoRehospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    FechaSegundoSeguimientoRehospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    FechaTercerSeguimientoRehospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    FechaAltaHospitalizacion = table.Column<DateTime>(type: "date", nullable: true),
                    ObservacionRehospitalizacion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaNovedadDevolucionProductos = table.Column<DateTime>(type: "date", nullable: true),
                    MotivoNovedadDevolucionProductos = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NotificacionAuxiliarDevolucionProductos = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FechaMaximaDevolucionProductos = table.Column<DateTime>(type: "date", nullable: true),
                    EstadoDevolucionServicioFarmaceutico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PresentaNovedadKardex = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PresentaNovedadRequisicion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    PresentaNovedadAutorizacion = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    DescripcionNovedadDocumentosPaciente = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FechaReporteNovedadDocumentos = table.Column<DateTime>(type: "date", nullable: true),
                    HoraReporteNovedadDocumentos = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    HoraGestionSolucionNovedadDocumentos = table.Column<TimeSpan>(type: "time without time zone", nullable: true),
                    FechaGestionFarmacia = table.Column<DateTime>(type: "date", nullable: false),
                    HoraGestionFarmacia = table.Column<TimeSpan>(type: "time without time zone", nullable: false),
                    IndicadorTiempoGestionMinutos = table.Column<int>(type: "integer", nullable: false),
                    GestionCompletaPendiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_censo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NursingAssistants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingAssistants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpsAssistants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpsAssistants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastName1 = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LastName2 = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    NationalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedNationalId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Username = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    PerformedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[,]
                {
                    { 1, "USERS_READ", "Consultar usuarios" },
                    { 2, "USERS_WRITE", "Crear, editar y desactivar usuarios" },
                    { 3, "AUDIT_READ", "Consultar bitacora de auditoria" }
                });

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Description", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, "Administracion total del sistema", "Administrador", "ADMINISTRADOR" },
                    { 2, "Consulta de reportes y auditoria", "Auditor", "AUDITOR" },
                    { 3, "Acceso basico a modulos internos", "Colaborador", "COLABORADOR" }
                });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 1 },
                    { 3, 1 },
                    { 3, 2 },
                    { 1, 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PerformedAtUtc",
                table: "AuditLogs",
                column: "PerformedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PerformedByUserId",
                table: "AuditLogs",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_censo_CreatedAtUtc",
                table: "censo",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_censo_FechaIngreso",
                table: "censo",
                column: "FechaIngreso");

            migrationBuilder.CreateIndex(
                name: "IX_NursingAssistants_NormalizedName",
                table: "NursingAssistants",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpsAssistants_NormalizedName",
                table: "OpsAssistants",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_NormalizedName",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedNationalId",
                table: "Users",
                column: "NormalizedNationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "censo");

            migrationBuilder.DropTable(
                name: "NursingAssistants");

            migrationBuilder.DropTable(
                name: "OpsAssistants");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
