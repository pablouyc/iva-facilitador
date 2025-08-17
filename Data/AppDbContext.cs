using Microsoft.EntityFrameworkCore;
using IvaFacilitador.Models;

namespace IvaFacilitador.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<ConexionQbo> ConexionesQbo => Set<ConexionQbo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>()
            .HasOne(e => e.ConexionQbo)
            .WithOne()
            .HasForeignKey<ConexionQbo>(c => c.Id);
    }
}
