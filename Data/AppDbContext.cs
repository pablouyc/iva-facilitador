using System;
using Microsoft.EntityFrameworkCore;
using CompanyConnection = IvaFacilitador.Models.CompanyConnection;

namespace IvaFacilitador.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<CompanyConnection> CompanyConnections => Set<CompanyConnection>();
        public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
        public DbSet<ParametrizacionEmpresa> ParametrizacionEmpresas => Set<ParametrizacionEmpresa>();
        public DbSet<QboToken> QboTokens => Set<QboToken>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompanyConnection>().HasKey(c => c.RealmId);
            modelBuilder.Entity<CompanyConnection>().HasIndex(c => c.RealmId).IsUnique();

            modelBuilder.Entity<CompanyProfile>().HasKey(p => p.RealmId);
            modelBuilder.Entity<CompanyProfile>().HasIndex(p => p.RealmId).IsUnique();

            modelBuilder.Entity<ParametrizacionEmpresa>().HasKey(p => p.RealmId);
            modelBuilder.Entity<ParametrizacionEmpresa>().HasIndex(p => p.RealmId).IsUnique();

            modelBuilder.Entity<QboToken>().HasKey(t => t.RealmId);
            modelBuilder.Entity<QboToken>().HasIndex(t => t.RealmId).IsUnique();
        }
    }

    public class CompanyProfile
    {
        public string RealmId { get; set; } = "";
        public string? Json { get; set; }
    }

    public class ParametrizacionEmpresa
    {
        public string RealmId { get; set; } = "";
        public string JsonConfig { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }

    public class QboToken
    {
        public string RealmId { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

// dotnet ef migrations add Init_Stores_To_Postgres
// dotnet ef database update
