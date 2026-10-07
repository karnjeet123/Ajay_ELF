using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OpenBrew.Api.Data;

public sealed class BreweryDatabaseInitializer(
    OpenBreweryDbContext dbContext,
    IWebHostEnvironment environment,
    ILogger<BreweryDatabaseInitializer> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (await dbContext.Breweries.AnyAsync(cancellationToken))
        {
            logger.LogInformation("SQLite brewery database already contains data; seed import skipped.");
            return;
        }

        var seedPath = Path.Combine(environment.ContentRootPath, "Data", "Seed", "breweries.json");
        await using var stream = File.OpenRead(seedPath);
        var seedBreweries = await JsonSerializer.DeserializeAsync<List<BreweryEntity>>(
            stream,
            SerializerOptions,
            cancellationToken) ?? [];

        var openBreweries = seedBreweries
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Where(x => !string.Equals(x.BreweryType, "closed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        await dbContext.Breweries.AddRangeAsync(openBreweries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} open breweries into SQLite.", openBreweries.Count);
    }
}
