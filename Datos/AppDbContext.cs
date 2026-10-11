using Microsoft.EntityFrameworkCore;

namespace ServicioRegistroCrm.Datos;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ResultadoRegistro> ResultadosRegistro => Set<ResultadoRegistro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResultadoRegistro>(e =>
        {
            e.ToTable("ResultadosRegistro");
            e.HasKey(x => x.Id);
            e.Property(x => x.NumeroIdentificacion).HasMaxLength(30);
            e.Property(x => x.Etapa).HasMaxLength(20);
            e.HasIndex(x => x.NumeroIdentificacion);
        });
    }
}
