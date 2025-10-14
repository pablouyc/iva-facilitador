using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Domain.Payroll;

namespace IvaFacilitador.Data.Payroll
{
    public class PayrollDbContext : DbContext
    {
        public PayrollDbContext(DbContextOptions<PayrollDbContext> options) : base(options) {}

        public DbSet<Empresa> Empresas => Set<Empresa>();
        public DbSet<Sector> Sectores => Set<Sector>();
        public DbSet<CuentaContable> CuentasContables => Set<CuentaContable>();
        public DbSet<Feriado> Feriados => Set<Feriado>();
        public DbSet<Periodo> Periodos => Set<Periodo>();
        
    public DbSet<PayrollQboToken> PayrollQboTokens => Set<PayrollQboToken>();
        public DbSet<PeriodoColaborador> PeriodoColaboradores => Set<PeriodoColaborador>();
        public DbSet<Evento> Eventos => Set<Evento>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            
        // PayrollQboToken
        b.Entity<PayrollQboToken>(e =>
        {
            e.HasIndex(x => new { x.EmpresaId, x.RealmId }).IsUnique();
            e.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        });// Empresa
            b.Entity<Empresa>(e =>
            {
                e.HasIndex(x => x.RealmId);
                e.HasIndex(x => x.Cedula).IsUnique();
            });

            // Sector
            b.Entity<Sector>(e =>
            {
                e.HasIndex(x => new { x.EmpresaId, x.Nombre }).IsUnique();
                e.HasOne(x => x.Empresa).WithMany(x => x.Sectores).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            });

            // CuentaContable: única por (Empresa, Sector?, Tipo)
            b.Entity<CuentaContable>(e =>
            {
                e.HasIndex(x => new { x.EmpresaId, x.SectorId, x.Tipo }).IsUnique();
                e.Property(x => x.QboAccountId).IsRequired();
                e.HasOne(x => x.Empresa).WithMany(x => x.Cuentas).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Sector).WithMany(x => x.Cuentas).HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Restrict);
            });

            // Feriado
            b.Entity<Feriado>(e =>
            {
                e.HasIndex(x => new { x.EmpresaId, x.Fecha }).IsUnique();
                e.HasOne(x => x.Empresa).WithMany(x => x.Feriados).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            });

            // Periodo
            b.Entity<Periodo>(e =>
            {
                e.HasIndex(x => new { x.EmpresaId, x.FechaInicio, x.FechaFin });
                e.HasOne(x => x.Empresa).WithMany(x => x.Periodos).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
            });

            // Colaborador
            b.Entity<Colaborador>(e =>
            {
                e.HasIndex(x => new { x.EmpresaId, x.Cedula }).IsUnique();
                e.HasOne(x => x.Empresa).WithMany(x => x.Colaboradores).HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Sector).WithMany(x => x.Colaboradores).HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.SetNull);
            });

            // PeriodoColaborador: único por (Periodo, Colaborador)
            b.Entity<PeriodoColaborador>(e =>
            {
                e.HasIndex(x => new { x.PeriodoId, x.ColaboradorId }).IsUnique();
                e.HasOne(x => x.Periodo).WithMany(x => x.PeriodoColaboradores).HasForeignKey(x => x.PeriodoId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Colaborador).WithMany(x => x.PeriodoColaboradores).HasForeignKey(x => x.ColaboradorId).OnDelete(DeleteBehavior.Cascade);
            });

            // Evento: uno por día/tipo por PeriodoColaborador (ajustable)
            b.Entity<Evento>().HasIndex(x => new { x.PeriodoColaboradorId, x.Fecha, x.Tipo }).IsUnique();
            b.Entity<Evento>(e =>
            {
                e.HasOne(x => x.PeriodoColaborador).WithMany(x => x.Eventos).HasForeignKey(x => x.PeriodoColaboradorId).OnDelete(DeleteBehavior.Cascade);
            });
        }

        public override int SaveChanges()
        {
            Stamp();
            return base.SaveChanges();
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Stamp();
            return await base.SaveChangesAsync(cancellationToken);
        }
        private void Stamp()
        {
            var entries = ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
            var now = DateTime.UtcNow;
            foreach (var entry in entries)
            {
                var created = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CreatedAtUtc");
                var updated = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAtUtc");
                if (created != null && entry.State == EntityState.Added) created.CurrentValue = now;
                if (updated != null) updated.CurrentValue = now;
            }
        }
    }
}

