using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenBrew.Api.Contracts;
using OpenBrew.Api.Models;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Tests.Services;

public sealed class BreweryServiceTests
{
    private static readonly IReadOnlyList<Brewery> Breweries =
    [
        new("Alpha Brewing", "Portland", "111", 45.5152, -122.6784, "micro"),
        new("Beta Beer", "Seattle", "222", 47.6062, -122.3321, "brewpub"),
        new("Gamma Ales", "Portland", null, 45.5200, -122.6700, "regional"),
        new("alpha brewing", "Boston", "333", null, null, "micro")
    ];

    [Fact]
    public async Task GetBreweriesAsync_FiltersSearchAndCityCaseInsensitively()
    {
        var service = CreateService(Breweries);
        var request = new BreweryQueryRequest
        {
            City = "PORT",
            Search = "ales",
            SortBy = "name"
        };

        var result = await service.GetBreweriesAsync(request, CancellationToken.None);

        var brewery = Assert.Single(result);
        Assert.Equal("Gamma Ales", brewery.Name);
        Assert.Equal("Portland", brewery.City);
    }

    [Theory]
    [InlineData("name", "asc", "alpha brewing", "Gamma Ales")]
    [InlineData("name", "desc", "Gamma Ales", "alpha brewing")]
    [InlineData("city", "asc", "alpha brewing", "Beta Beer")]
    [InlineData("city", "desc", "Beta Beer", "alpha brewing")]
    public async Task GetBreweriesAsync_SortsByRequestedField(
        string sortBy,
        string direction,
        string expectedFirst,
        string expectedLast)
    {
        var service = CreateService(Breweries);

        var result = await service.GetBreweriesAsync(
            new BreweryQueryRequest { SortBy = sortBy, SortDirection = direction },
            CancellationToken.None);

        Assert.Equal(expectedFirst, result.First().Name);
        Assert.Equal(expectedLast, result.Last().Name);
    }

    [Fact]
    public async Task GetBreweriesAsync_AppliesPaginationAfterSorting()
    {
        var service = CreateService(Breweries);

        var result = await service.GetBreweriesAsync(
            new BreweryQueryRequest { SortBy = "name", Page = 2, PageSize = 2 },
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(["Beta Beer", "Gamma Ales"], result.Select(x => x.Name));
    }

    [Fact]
    public async Task GetBreweriesAsync_SortsByDistanceAndReturnsRoundedDistance()
    {
        var service = CreateService(Breweries);

        var result = await service.GetBreweriesAsync(
            new BreweryQueryRequest
            {
                SortBy = "distance",
                Latitude = 45.5152,
                Longitude = -122.6784
            },
            CancellationToken.None);

        Assert.Equal("Alpha Brewing", result[0].Name);
        Assert.Equal(0, result[0].DistanceKm);
        Assert.Null(result[^1].DistanceKm);
    }

    [Theory]
    [InlineData("distance", "asc", "Latitude and longitude are required")]
    [InlineData("rating", "asc", "Invalid sortBy value")]
    [InlineData("name", "sideways", "Invalid sortDirection value")]
    public async Task GetBreweriesAsync_InvalidSortingThrowsArgumentException(
        string sortBy,
        string direction,
        string expectedMessage)
    {
        var service = CreateService(Breweries);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetBreweriesAsync(
            new BreweryQueryRequest { SortBy = sortBy, SortDirection = direction },
            CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task AutocompleteAsync_IsCaseInsensitiveDistinctOrderedAndLimited()
    {
        var service = CreateService(Breweries);

        var result = await service.AutocompleteAsync(
            new AutocompleteQueryRequest { Term = "A", Limit = 2 },
            CancellationToken.None);

        Assert.Equal(["Alpha Brewing", "Beta Beer"], result);
    }

    private static BreweryService CreateService(IReadOnlyList<Brewery> breweries)
    {
        var cache = new Mock<IBreweryCacheStore>();
        cache.Setup(x => x.GetOpenBreweriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(breweries);
        return new BreweryService(cache.Object, NullLogger<BreweryService>.Instance);
    }
}
