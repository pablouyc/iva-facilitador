using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Areas.Payroll.ModelosPayroll;

namespace IvaFacilitador.Areas.Payroll.BaseDatosPayroll
{
    public class PayrollDbContext : DbContext
    {
    public DbSet<PayrollQboToken> PayrollQboTokens { get; set; }

        public PayrollDbContext(DbContextOptions<PayrollDbContext> options) : base(options) { }

        public DbSet<Company>   Companies   => Set<Company>();
        public DbSet<Employee>  Employees   => Set<Employee>();
        public DbSet<PayItem>   PayItems    => Set<PayItem>();
        public DbSet<PayEvent>  PayEvents   => Set<PayEvent>();
        public DbSet<PayPeriod> PayPeriods  => Set<PayPeriod>();
        public DbSet<PayRunItem> PayRunItems => Set<PayRunItem>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Companies
            b.Entity<Company>()
              .HasIndex(c => c.Name);

            // Employees
            b.Entity<Employee>()
              .HasIndex(e => new { e.CompanyId, e.NationalId })
              .IsUnique();

            // PayItems
            b.Entity<PayItem>()
              .HasIndex(i => i.Code)
              .IsUnique();

            // PayEvents
            b.Entity<PayEvent>()
              .HasOne(e => e.Company).WithMany(c => c.PayEvents).HasForeignKey(e => e.CompanyId);
            b.Entity<PayEvent>()
              .HasIndex(e => new { e.CompanyId, e.Date });
            b.Entity<PayEvent>()
              .HasIndex(e => e.EmployeeId);
            b.Entity<PayEvent>()
              .HasIndex(e => e.ItemId);

            // PayPeriods
            b.Entity<PayPeriod>()
              .HasOne(p => p.Company).WithMany(c => c.PayPeriods).HasForeignKey(p => p.CompanyId);
            b.Entity<PayPeriod>()
              .HasIndex(p => p.CompanyId);

            // PayRunItems
            b.Entity<PayRunItem>()
              .HasOne(r => r.Period).WithMany().HasForeignKey(r => r.PeriodId);
            b.Entity<PayRunItem>()
              .HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId);
            b.Entity<PayRunItem>()
              .HasOne(r => r.Item).WithMany().HasForeignKey(r => r.ItemId);
            b.Entity<PayRunItem>()
              .HasIndex(r => r.EmployeeId);
            b.Entity<PayRunItem>()
              .HasIndex(r => r.ItemId);
            b.Entity<PayRunItem>()
              .HasIndex(r => r.PeriodId);
        }
    }
}



