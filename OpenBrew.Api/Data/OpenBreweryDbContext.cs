using Microsoft.EntityFrameworkCore;

namespace OpenBrew.Api.Data;

public sealed class OpenBreweryDbContext(DbContextOptions<OpenBreweryDbContext> options)
    : DbContext(options)
{
    public DbSet<BreweryEntity> Breweries => Set<BreweryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var brewery = modelBuilder.Entity<BreweryEntity>();
        brewery.ToTable("Breweries");
        brewery.HasKey(x => x.Id);
        brewery.Property(x => x.Name).HasMaxLength(250).IsRequired();
        brewery.Property(x => x.City).HasMaxLength(150).IsRequired();
        brewery.Property(x => x.Phone).HasMaxLength(50);
        brewery.Property(x => x.BreweryType).HasMaxLength(50).IsRequired();
        brewery.HasIndex(x => x.Name);
        brewery.HasIndex(x => x.City);
    }
}
