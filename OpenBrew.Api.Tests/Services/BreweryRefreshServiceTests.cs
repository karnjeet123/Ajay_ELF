using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenBrew.Api.Clients;
using OpenBrew.Api.Data;
using OpenBrew.Api.Models;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Tests.Services;

public sealed class BreweryRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_ReplacesSqliteAndPublishesOpenBreweriesToMemory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Breweries.Add(new BreweryEntity
        {
            Name = "Stale Brewery",
            City = "Old City",
            BreweryType = "micro"
        });
        await dbContext.SaveChangesAsync();
        var source = new Mock<IOpenBreweryDbClient>();
        source.Setup(x => x.GetBreweriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OpenBreweryDbBrewery { Name = "Fresh Brewery", City = "Austin", Phone = "123", Latitude = 30.2, Longitude = -97.7, BreweryType = "micro" },
                new OpenBreweryDbBrewery { Name = "Closed Brewery", City = "Austin", BreweryType = "closed" },
                new OpenBreweryDbBrewery { Name = null, City = "Nowhere", BreweryType = "micro" }
            ]);
        var memoryCache = new Mock<IBreweryMemoryCache>();
        var service = new BreweryRefreshService(
            source.Object,
            dbContext,
            memoryCache.Object,
            NullLogger<BreweryRefreshService>.Instance);

        await service.RefreshAsync(CancellationToken.None);

        var entity = Assert.Single(await dbContext.Breweries.AsNoTracking().ToListAsync());
        Assert.Equal("Fresh Brewery", entity.Name);
        memoryCache.Verify(x => x.Set(It.Is<IReadOnlyList<Brewery>>(
            items => items.Count == 1 && items[0].Name == "Fresh Brewery")), Times.Once);
        source.Verify(x => x.GetBreweriesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_WhenSourceFails_PreservesSqliteAndMemory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Breweries.Add(new BreweryEntity
        {
            Name = "Persisted Brewery",
            City = "Denver",
            BreweryType = "regional"
        });
        await dbContext.SaveChangesAsync();
        var source = new Mock<IOpenBreweryDbClient>();
        source.Setup(x => x.GetBreweriesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Source unavailable"));
        var memoryCache = new Mock<IBreweryMemoryCache>();
        var service = new BreweryRefreshService(
            source.Object,
            dbContext,
            memoryCache.Object,
            NullLogger<BreweryRefreshService>.Instance);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.RefreshAsync(CancellationToken.None));

        var entity = Assert.Single(await dbContext.Breweries.AsNoTracking().ToListAsync());
        Assert.Equal("Persisted Brewery", entity.Name);
        memoryCache.Verify(x => x.Set(It.IsAny<IReadOnlyList<Brewery>>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenSourceIsEmpty_PreservesSqliteAndMemory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Breweries.Add(new BreweryEntity
        {
            Name = "Persisted Brewery",
            City = "Denver",
            BreweryType = "regional"
        });
        await dbContext.SaveChangesAsync();
        var source = new Mock<IOpenBreweryDbClient>();
        source.Setup(x => x.GetBreweriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var memoryCache = new Mock<IBreweryMemoryCache>();
        var service = new BreweryRefreshService(
            source.Object,
            dbContext,
            memoryCache.Object,
            NullLogger<BreweryRefreshService>.Instance);

        await service.RefreshAsync(CancellationToken.None);

        Assert.Single(await dbContext.Breweries.AsNoTracking().ToListAsync());
        memoryCache.Verify(x => x.Set(It.IsAny<IReadOnlyList<Brewery>>()), Times.Never);
    }

    private static OpenBreweryDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<OpenBreweryDbContext>()
            .UseSqlite(connection)
            .Options;
        return new OpenBreweryDbContext(options);
    }
}
