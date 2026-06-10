using IntranetPrueba.Data.Entities;
using IntranetPrueba.Models.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IntranetPrueba.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<AppPermission> Permissions => Set<AppPermission>();
    public DbSet<AppUserRole> UserRoles => Set<AppUserRole>();
    public DbSet<AppRolePermission> RolePermissions => Set<AppRolePermission>();
    public DbSet<AppUserPermission> UserPermissions => Set<AppUserPermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CensoRecord> Censos => Set<CensoRecord>();
    public DbSet<CensoAdjunto> CensoAdjuntos => Set<CensoAdjunto>();
    public DbSet<Medicamento> Medicamentos => Set<Medicamento>();
    public DbSet<NursingAssistant> NursingAssistants => Set<NursingAssistant>();
    public DbSet<OpsAssistant> OpsAssistants => Set<OpsAssistant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName1).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName2).HasMaxLength(80);
            entity.Property(x => x.NationalId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.NormalizedNationalId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150).IsRequired();
            entity.Property(x => x.NormalizedEmail).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(80).IsRequired();
            entity.Property(x => x.NormalizedUsername).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(x => x.LastLoginAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.NormalizedEmail);
            entity.HasIndex(x => x.NormalizedUsername).IsUnique();
            entity.HasIndex(x => x.NormalizedNationalId).IsUnique();
        });

        modelBuilder.Entity<AppRole>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(250);
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<AppPermission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(250).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<AppUserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => new { x.UserId, x.RoleId });

            entity.HasOne(x => x.User)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppRolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(x => new { x.RoleId, x.PermissionId });

            entity.HasOne(x => x.Role)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUserPermission>(entity =>
        {
            entity.ToTable("UserPermissions");
            entity.HasKey(x => new { x.UserId, x.PermissionId });
            entity.Property(x => x.GrantedAtUtc)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(x => x.User)
                .WithMany(x => x.UserPermissions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Permission)
                .WithMany(x => x.UserPermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(2000);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.PerformedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.PerformedAtUtc);

            entity.HasOne(x => x.PerformedByUser)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.PerformedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CensoRecord>(entity =>
        {
            entity.ToTable("censo");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Asegurador).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NombrePerfilGestionaCaso).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NombreRecepcionaCaso).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NombreRealizaKardex).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NombrePaciente).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TipoIdentificacion).HasMaxLength(3).IsRequired();
            entity.Property(x => x.NumeroIdentificacion).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CodigoCie10).HasMaxLength(4).IsRequired();
            entity.Property(x => x.DiagnosticoDescriptivo).HasMaxLength(300).IsRequired();
            entity.Property(x => x.CorreoElectronico).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Direccion).HasMaxLength(300).IsRequired();
            entity.Property(x => x.ClasificacionZonaSura).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MunicipioResidencia).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Barrio).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ZonaDireccionSegunMunicipio).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Area).HasMaxLength(10).IsRequired();
            entity.Property(x => x.IpsQueRemite).HasMaxLength(200).IsRequired();
            entity.Property(x => x.VistoBuenoRangoFueraAnexo).HasMaxLength(2).IsRequired();
            entity.Property(x => x.Telefono1).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Telefono2).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Telefono3).HasMaxLength(10);
            entity.Property(x => x.ClasificacionRiesgo).HasMaxLength(10).IsRequired();
            entity.Property(x => x.AdministracionMedicamentos).HasMaxLength(2).IsRequired();
            entity.Property(x => x.NombreMedicamentoPrincipalTratante).HasMaxLength(300);
            entity.Property(x => x.DosisMedicamentoPrincipal).HasPrecision(10, 2);
            entity.Property(x => x.MedidaMedicamentoPrincipal).HasMaxLength(20);
            entity.Property(x => x.ViaAdministracionMedicamentoPrincipal).HasMaxLength(30);
            entity.Property(x => x.FrecuenciaAdministracionMxPrincipal).HasMaxLength(30).IsRequired();
            entity.Property(x => x.DiasMedicamentoPrincipal);
            entity.Property(x => x.NumeroDosisDiaMedicamentoPrincipal).HasMaxLength(10).IsRequired();
            entity.Property(x => x.NombreMedicamentoNumero2).HasMaxLength(300);
            entity.Property(x => x.DosisMedicamento2).HasPrecision(10, 2);
            entity.Property(x => x.MedidaMedicamento2).HasMaxLength(20);
            entity.Property(x => x.ViaAdministracionMedicamento2).HasMaxLength(30);
            entity.Property(x => x.FrecuenciaAdministracionMedicamento2).HasMaxLength(30);
            entity.Property(x => x.DiasMedicamento2);
            entity.Property(x => x.NumeroDosisMedicamento2).HasMaxLength(10);
            entity.Property(x => x.NombreMedicamentoNumero3).HasMaxLength(300);
            entity.Property(x => x.DosisMedicamento3).HasPrecision(10, 2);
            entity.Property(x => x.MedidaMedicamento3).HasMaxLength(20);
            entity.Property(x => x.ViaAdministracionMedicamento3).HasMaxLength(30);
            entity.Property(x => x.FrecuenciaAdministracionMedicamento3).HasMaxLength(30);
            entity.Property(x => x.DiasMedicamento3);
            entity.Property(x => x.NumeroDosisMedicamento3).HasMaxLength(10);
            entity.Property(x => x.AplicacionesTotales).HasMaxLength(50);
            entity.Property(x => x.DiasTratamientoIv).HasMaxLength(50);
            entity.Property(x => x.CambioFrecuenciaAdministracionTto).HasMaxLength(200);
            entity.Property(x => x.FrecuenciaAjustada).HasMaxLength(100);
            entity.Property(x => x.HoraPromesaInicioTto).HasMaxLength(50);
            entity.Property(x => x.AuxiliarAsignado).HasMaxLength(120);
            entity.Property(x => x.Estado).HasMaxLength(80);
            entity.Property(x => x.AutorizacionEvento).HasMaxLength(100);
            entity.Property(x => x.ResponsableLlamadaBienvenida).HasMaxLength(120);
            entity.Property(x => x.EstadoLlamadaBienvenida).HasMaxLength(20);
            entity.Property(x => x.ObservacionesPlanManejo).HasMaxLength(2000);
            entity.Property(x => x.NumeroTelefonoLlamadaBienvenida).HasMaxLength(20);
            entity.Property(x => x.NumeroDiasAutorizado).HasMaxLength(50);
            entity.Property(x => x.RequiereServiciosComplementarios).HasMaxLength(2);
            entity.Property(x => x.ServicioComplementario).HasMaxLength(80);
            entity.Property(x => x.PacienteGestante).HasMaxLength(2);
            entity.Property(x => x.Nebulizaciones).HasMaxLength(2);
            entity.Property(x => x.SistemasPresionNegativaVac).HasMaxLength(2);
            entity.Property(x => x.NutricionParenteral).HasMaxLength(2);
            entity.Property(x => x.NutricionEnteral).HasMaxLength(2);
            entity.Property(x => x.PacienteAnticoagulado).HasMaxLength(2);
            entity.Property(x => x.LaboratorioClinicoProcedimiento).HasMaxLength(2);
            entity.Property(x => x.ClinicaHeridas).HasMaxLength(2);
            entity.Property(x => x.Aislamiento).HasMaxLength(2);
            entity.Property(x => x.TipoAislamiento).HasMaxLength(20);
            entity.Property(x => x.CateterismoOSv).HasMaxLength(2);
            entity.Property(x => x.CateterPicc).HasMaxLength(2);
            entity.Property(x => x.AuxiliarAsignadoCateterismo).HasMaxLength(120);
            entity.Property(x => x.NombreQuienGestionaAlta).HasMaxLength(200);
            entity.Property(x => x.AltaTardia).HasMaxLength(2);
            entity.Property(x => x.ObservacionAltaTardia).HasMaxLength(2000);
            entity.Property(x => x.NombreQuienRealizaSeguimientoAltaTardia).HasMaxLength(120);
            entity.Property(x => x.PacienteRehospitalizado).HasMaxLength(2);
            entity.Property(x => x.MotivoRehospitalizacion).HasMaxLength(80);
            entity.Property(x => x.AmpliacionMotivoRehospitalizacion).HasMaxLength(2000);
            entity.Property(x => x.RemitidoPorRehospitalizacion).HasMaxLength(50);
            entity.Property(x => x.IpsIntramuralRehospitalizacion).HasMaxLength(200);
            entity.Property(x => x.ObservacionRehospitalizacion).HasMaxLength(2000);
            entity.Property(x => x.MotivoNovedadDevolucionProductos).HasMaxLength(40);
            entity.Property(x => x.NotificacionAuxiliarDevolucionProductos).HasMaxLength(2);
            entity.Property(x => x.EstadoDevolucionServicioFarmaceutico).HasMaxLength(20);
            entity.Property(x => x.PresentaNovedadKardex).HasMaxLength(2);
            entity.Property(x => x.PresentaNovedadRequisicion).HasMaxLength(2);
            entity.Property(x => x.PresentaNovedadAutorizacion).HasMaxLength(2);
            entity.Property(x => x.DescripcionNovedadDocumentosPaciente).HasMaxLength(2000);
            entity.Property(x => x.GestionCompletaPendiente).HasMaxLength(20).IsRequired().HasDefaultValue("Pendiente");
            entity.Property(x => x.FarmaciaEnviadoAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(x => x.FarmaciaKardexVistoAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(x => x.FarmaciaRequisicionVistoAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(x => x.RequisicionFarmaciaJson).HasColumnType("text");
            entity.Property(x => x.FarmaciaNombreRecibe).HasMaxLength(160);
            entity.Property(x => x.FarmaciaFirmaEntregaDataUrl).HasColumnType("text");
            entity.Property(x => x.FarmaciaFirmaRecibeDataUrl).HasColumnType("text");
            entity.Property(x => x.FarmaciaFechaHoraRecepcionUtc).HasColumnType("timestamp with time zone");
            entity.Property(x => x.FarmaciaFirmaActualizadaAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            ConfigureCensoDateAndTimeColumns(entity);
            entity.HasIndex(x => x.FechaIngreso);
            entity.HasIndex(x => x.FarmaciaEnviadoAtUtc);
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<CensoAdjunto>(entity =>
        {
            entity.ToTable("censo_adjuntos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.FileData).IsRequired();
            entity.Property(x => x.UploadedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.CensoRecord)
                .WithMany(x => x.Adjuntos)
                .HasForeignKey(x => x.CensoRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.CensoRecordId);
        });

        modelBuilder.Entity<Medicamento>(entity =>
        {
            entity.ToTable("Medicamentos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nombre).HasMaxLength(300).IsRequired();
            entity.Property(x => x.NormalizedNombre).HasMaxLength(300).IsRequired();
            entity.Property(x => x.PresentacionRequisicion).HasMaxLength(120);
            entity.Property(x => x.ConcentracionMiligramos).HasMaxLength(50);
            entity.Property(x => x.Jeringa).HasMaxLength(50);
            entity.Property(x => x.SolucionParaDilucion).HasMaxLength(120);
            entity.Property(x => x.DilucionRecomendada).HasMaxLength(50);
            entity.Property(x => x.VehiculoReconstitucion).HasMaxLength(120);
            entity.Property(x => x.TiempoEstabilidad).HasMaxLength(120);
            entity.Property(x => x.TiempoInfusionMinutos).HasMaxLength(80);
            entity.Property(x => x.BombaInfusion).HasMaxLength(10);
            entity.Property(x => x.MarcacionRiesgo).HasMaxLength(50);
            entity.Property(x => x.Flebozantes).HasMaxLength(10);
            entity.Property(x => x.EquipoFotosensible).HasMaxLength(10);
            entity.Property(x => x.CadenaFrio).HasMaxLength(10);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.NormalizedNombre).IsUnique();
        });

        modelBuilder.Entity<NursingAssistant>(entity =>
        {
            entity.ToTable("NursingAssistants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<OpsAssistant>(entity =>
        {
            entity.ToTable("OpsAssistants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<AppRole>().HasData(
            new AppRole { Id = 1, Name = "Administrador", NormalizedName = "ADMINISTRADOR", Description = "Administracion total del sistema" },
            new AppRole { Id = 2, Name = "Auditor", NormalizedName = "AUDITOR", Description = "Consulta de reportes y auditoria" },
            new AppRole { Id = 3, Name = "Colaborador", NormalizedName = "COLABORADOR", Description = "Acceso basico a modulos internos" }
        );

        modelBuilder.Entity<AppPermission>().HasData(
            new AppPermission { Id = 1, Code = "USERS_READ", Description = "Consultar usuarios" },
            new AppPermission { Id = 2, Code = "USERS_WRITE", Description = "Crear, editar y desactivar usuarios" },
            new AppPermission { Id = 3, Code = "AUDIT_READ", Description = "Consultar bitacora de auditoria" }
        );

        modelBuilder.Entity<AppRolePermission>().HasData(
            new AppRolePermission { RoleId = 1, PermissionId = 1 },
            new AppRolePermission { RoleId = 1, PermissionId = 2 },
            new AppRolePermission { RoleId = 1, PermissionId = 3 },
            new AppRolePermission { RoleId = 2, PermissionId = 3 },
            new AppRolePermission { RoleId = 3, PermissionId = 1 }
        );
    }

    private static void ConfigureCensoDateAndTimeColumns(EntityTypeBuilder<CensoRecord> entity)
    {
        entity.Property(x => x.FechaIngreso).HasColumnType("date");
        entity.Property(x => x.FechaRespuesta).HasColumnType("date");
        entity.Property(x => x.FechaNacimiento).HasColumnType("date");
        entity.Property(x => x.FechaInicioTratamiento).HasColumnType("date");
        entity.Property(x => x.FechaFinTratamiento).HasColumnType("date");
        entity.Property(x => x.FechaPromesaInicioTto).HasColumnType("date");
        entity.Property(x => x.FechaUltimoCambioSonda).HasColumnType("date");
        entity.Property(x => x.FechaProximoCambioSonda).HasColumnType("date");
        entity.Property(x => x.FechaUltimaCuracionPicc).HasColumnType("date");
        entity.Property(x => x.FechaAlta).HasColumnType("date");
        entity.Property(x => x.FechaPrimerSeguimiento24Horas).HasColumnType("date");
        entity.Property(x => x.FechaSegundoSeguimiento48Horas).HasColumnType("date");
        entity.Property(x => x.FechaTercerSeguimiento72Horas).HasColumnType("date");
        entity.Property(x => x.FechaRegistroReporteRehospitalizacion).HasColumnType("date");
        entity.Property(x => x.FechaRehospitalizacion).HasColumnType("date");
        entity.Property(x => x.FechaPrimerSeguimientoRehospitalizacion).HasColumnType("date");
        entity.Property(x => x.FechaSegundoSeguimientoRehospitalizacion).HasColumnType("date");
        entity.Property(x => x.FechaTercerSeguimientoRehospitalizacion).HasColumnType("date");
        entity.Property(x => x.FechaAltaHospitalizacion).HasColumnType("date");
        entity.Property(x => x.FechaNovedadDevolucionProductos).HasColumnType("date");
        entity.Property(x => x.FechaMaximaDevolucionProductos).HasColumnType("date");
        entity.Property(x => x.FechaReporteNovedadDocumentos).HasColumnType("date");
        entity.Property(x => x.FechaGestionFarmacia).HasColumnType("date");

        entity.Property(x => x.HoraIngreso).HasColumnType("time without time zone");
        entity.Property(x => x.HoraRespuesta).HasColumnType("time without time zone");
        entity.Property(x => x.HoraReporteNovedadDocumentos).HasColumnType("time without time zone");
        entity.Property(x => x.HoraGestionSolucionNovedadDocumentos).HasColumnType("time without time zone");
        entity.Property(x => x.HoraGestionFarmacia).HasColumnType("time without time zone");
    }
}
