using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenBrew.Api.Data;
using OpenBrew.Api.Models;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Tests.Services;

public sealed class SqliteBreweryStoreTests
{
    [Fact]
    public async Task GetOpenBreweriesAsync_ReadsAndMapsPersistedRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Breweries.AddRange(
            new BreweryEntity
            {
                Name = "SQLite Brewing",
                City = "Denver",
                Phone = "5551234",
                Latitude = 39.7392,
                Longitude = -104.9903,
                BreweryType = "micro"
            },
            new BreweryEntity
            {
                Name = "Local Ales",
                City = "Austin",
                BreweryType = "brewpub"
            });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var memoryCache = new Mock<IBreweryMemoryCache>();
        memoryCache.Setup(x => x.TryGet(out It.Ref<IReadOnlyList<Brewery>>.IsAny)).Returns(false);
        var store = new SqliteBreweryStore(dbContext, memoryCache.Object, NullLogger<SqliteBreweryStore>.Instance);

        var result = await store.GetOpenBreweriesAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("SQLite Brewing", result[0].Name);
        Assert.Equal("Denver", result[0].City);
        Assert.Equal(39.7392, result[0].Latitude);
        Assert.Empty(dbContext.ChangeTracker.Entries());
        memoryCache.Verify(x => x.Set(It.Is<IReadOnlyList<Brewery>>(items => items.Count == 2)), Times.Once);
    }

    [Fact]
    public async Task GetOpenBreweriesAsync_WhenMemoryCacheHits_DoesNotReadSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        IReadOnlyList<Brewery> cached =
        [
            new("Cached Brewery", "Boston", null, null, null, "micro")
        ];
        var memoryCache = new Mock<IBreweryMemoryCache>();
        memoryCache.Setup(x => x.TryGet(out cached)).Returns(true);
        var store = new SqliteBreweryStore(dbContext, memoryCache.Object, NullLogger<SqliteBreweryStore>.Instance);

        var result = await store.GetOpenBreweriesAsync(CancellationToken.None);

        Assert.Same(cached, result);
        memoryCache.Verify(x => x.Set(It.IsAny<IReadOnlyList<Brewery>>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_SeedsOnlyOpenBreweriesAndDoesNotDuplicateData()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"openbrew-tests-{Guid.NewGuid():N}");
        var seedDirectory = Path.Combine(contentRoot, "Data", "Seed");
        Directory.CreateDirectory(seedDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(seedDirectory, "breweries.json"),
            """
            [
              { "name": "Seeded Brewery", "city": "Portland", "phone": "123", "latitude": 45.5, "longitude": -122.6, "breweryType": "micro" },
              { "name": "Closed Brewery", "city": "Portland", "breweryType": "closed" },
              { "name": "", "city": "Nowhere", "breweryType": "micro" }
            ]
            """);

        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var dbContext = CreateDbContext(connection);
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
            var initializer = new BreweryDatabaseInitializer(
                dbContext,
                environment.Object,
                NullLogger<BreweryDatabaseInitializer>.Instance);

            await initializer.InitializeAsync();
            await initializer.InitializeAsync();

            var brewery = Assert.Single(await dbContext.Breweries.AsNoTracking().ToListAsync());
            Assert.Equal("Seeded Brewery", brewery.Name);
            Assert.Equal("Portland", brewery.City);
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    private static OpenBreweryDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<OpenBreweryDbContext>()
            .UseSqlite(connection)
            .Options;
        return new OpenBreweryDbContext(options);
    }
}
