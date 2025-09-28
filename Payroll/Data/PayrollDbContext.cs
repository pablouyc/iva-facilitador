using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Payroll.Models;

namespace IvaFacilitador.Payroll.Data
{
    public class PayrollDbContext : DbContext
    {
        public PayrollDbContext(DbContextOptions<PayrollDbContext> options) : base(options) { }

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<PayItem> PayItems => Set<PayItem>();
        public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
        public DbSet<PayEvent> PayEvents => Set<PayEvent>();
        public DbSet<PayRunItem> PayRunItems => Set<PayRunItem>();
        public DbSet<PayrollQboToken> PayrollQboTokens => Set<PayrollQboToken>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Company>().HasIndex(x => x.Name);
            b.Entity<Employee>().HasIndex(x => new { x.CompanyId, x.NationalId }).IsUnique();
            b.Entity<PayEvent>().HasIndex(x => new { x.CompanyId, x.Date });
            b.Entity<PayItem>().HasIndex(x => x.Code).IsUnique();
        }
    }
}
