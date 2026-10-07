using Microsoft.EntityFrameworkCore;
using OpenBrew.Api.Clients;
using OpenBrew.Api.Data;
using OpenBrew.Api.Models;

namespace OpenBrew.Api.Services;

public sealed class BreweryRefreshService(
    IOpenBreweryDbClient sourceClient,
    OpenBreweryDbContext dbContext,
    IBreweryMemoryCache memoryCache,
    ILogger<BreweryRefreshService> logger) : IBreweryRefreshService
{
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var sourceBreweries = await sourceClient.GetBreweriesAsync(cancellationToken);
        var breweries = sourceBreweries
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Where(x => !string.Equals(x.BreweryType, "closed", StringComparison.OrdinalIgnoreCase))
            .Select(x => new Brewery(
                x.Name!,
                x.City ?? string.Empty,
                string.IsNullOrWhiteSpace(x.Phone) ? null : x.Phone,
                x.Latitude,
                x.Longitude,
                x.BreweryType ?? "unknown"))
            .ToList();

        if (breweries.Count == 0)
        {
            logger.LogWarning("Source refresh returned no open breweries; existing SQLite data was retained.");
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Breweries.ExecuteDeleteAsync(cancellationToken);
        await dbContext.Breweries.AddRangeAsync(
            breweries.Select(x => new BreweryEntity
            {
                Name = x.Name,
                City = x.City,
                Phone = x.Phone,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                BreweryType = x.BreweryType
            }),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        memoryCache.Set(breweries);
        logger.LogInformation(
            "Refreshed {Count} breweries in SQLite and memory for 10 minutes.",
            breweries.Count);
    }
}
