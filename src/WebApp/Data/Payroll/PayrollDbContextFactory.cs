using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace IvaFacilitador.Data.Payroll
{
    public class PayrollDbContextFactory : IDesignTimeDbContextFactory<PayrollDbContext>
    {
        public PayrollDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var cfg = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cs = cfg.GetConnectionString("PayrollConnection")
                     ?? cfg.GetConnectionString("DefaultConnection")
                     ?? "Data Source=" + Path.Combine(basePath, "IvaFacilitador_Payroll.db"); // Fallback SQLite

            var builder = new DbContextOptionsBuilder<PayrollDbContext>();

            var csLower = cs.Trim().ToLowerInvariant();
            if (csLower.Contains(".db") || csLower.StartsWith("data source="))
                builder.UseSqlite(cs);
            else
                builder.UseSqlServer(cs);

            return new PayrollDbContext(builder.Options);
        }
    }
}
