using Microsoft.EntityFrameworkCore;
using OpenBrew.Api.Data;
using OpenBrew.Api.Models;

namespace OpenBrew.Api.Services;

public sealed class SqliteBreweryStore(
    OpenBreweryDbContext dbContext,
    IBreweryMemoryCache memoryCache,
    ILogger<SqliteBreweryStore> logger) : IBreweryCacheStore
{
    public async Task<IReadOnlyList<Brewery>> GetOpenBreweriesAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGet(out var cachedBreweries))
        {
            logger.LogDebug("Serving {Count} breweries from memory cache", cachedBreweries.Count);
            return cachedBreweries;
        }

        logger.LogInformation("Memory cache miss; loading breweries from SQLite");

        var breweries = await dbContext.Breweries
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new Brewery(
                x.Name,
                x.City,
                x.Phone,
                x.Latitude,
                x.Longitude,
                x.BreweryType))
            .ToListAsync(cancellationToken);

            memoryCache.Set(breweries);
            logger.LogInformation("Loaded {Count} breweries from SQLite into memory cache", breweries.Count);
            return breweries;
    }
}
