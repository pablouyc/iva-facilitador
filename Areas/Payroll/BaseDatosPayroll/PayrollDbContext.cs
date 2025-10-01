using Microsoft.EntityFrameworkCore;

namespace IvaFacilitador.Areas.Payroll.BaseDatosPayroll
{
    public class PayrollDbContext : DbContext
    {
        public PayrollDbContext(DbContextOptions<PayrollDbContext> options) : base(options) {}

        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<PayItem> PayItems => Set<PayItem>();
        public DbSet<PayEvent> PayEvents => Set<PayEvent>();
        public DbSet<PayPeriod> PayPeriods => Set<PayPeriod>();
        public DbSet<PayRunItem> PayRunItems => Set<PayRunItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Company>()
                .HasIndex(x => x.Name);

            modelBuilder.Entity<Employee>()
                .HasIndex(x => new { x.CompanyId, x.NationalId })
                .IsUnique();

            modelBuilder.Entity<PayItem>()
                .HasIndex(x => x.Code)
                .IsUnique();

            modelBuilder.Entity<PayEvent>()
                .HasIndex(x => new { x.CompanyId, x.Date });

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Company)
                .WithMany(c => c.Employees)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayEvent>()
                .HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayEvent>()
                .HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PayEvent>()
                .HasOne(e => e.Item)
                .WithMany()
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PayPeriod>()
                .HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayRunItem>()
                .HasOne(r => r.Period)
                .WithMany()
                .HasForeignKey(r => r.PeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayRunItem>()
                .HasOne(r => r.Employee)
                .WithMany()
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayRunItem>()
                .HasOne(r => r.Item)
                .WithMany()
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? TaxId { get; set; }
        public string? QboId { get; set; }
        public string? PayPolicy { get; set; }
        public string? ObligationsNotes { get; set; }

        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }

    public class Employee
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public string NationalId { get; set; } = ""; // cédula
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateTime JoinDate { get; set; }
        public string? Email { get; set; }
        public string Status { get; set; } = "Activo";
        public decimal? BaseSalary { get; set; }
    }

    public class PayItem
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "earning"; // earning|deduction|employer
        public bool IsRecurring { get; set; } = false;
    }

    public class PayEvent
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public int ItemId { get; set; }
        public PayItem? Item { get; set; }

        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string? Note { get; set; }
    }

    public class PayPeriod
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Draft"; // Draft|Closed
    }

    public class PayRunItem
    {
        public int Id { get; set; }
        public int PeriodId { get; set; }
        public PayPeriod? Period { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }
        public int ItemId { get; set; }
        public PayItem? Item { get; set; }
        public decimal Amount { get; set; }
    }
}
