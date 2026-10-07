using Microsoft.EntityFrameworkCore;
using OpenBrew.Api.Data;
using OpenBrew.Api.Models;

namespace OpenBrew.Api.Services;

public sealed class SqliteBreweryStore(
    OpenBreweryDbContext dbContext,
    IBreweryMemoryCache memoryCache) : IBreweryCacheStore
{
    public async Task<IReadOnlyList<Brewery>> GetOpenBreweriesAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGet(out var cachedBreweries))
        {
            return cachedBreweries;
        }

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
            return breweries;
    }
}
