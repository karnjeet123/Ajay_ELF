using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenBrew.Api.Contracts;
using OpenBrew.Api.Models;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Tests.Integration;

public sealed class BreweriesApiTests
{
    [Fact]
    public async Task GetBreweries_ReturnsMappedOpenBreweries()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries?sortBy=name&pageSize=10");
        var breweries = await response.Content.ReadFromJsonAsync<BreweryResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(breweries);
        Assert.Equal(["Alpha Brewing", "Stone Brewing", "Zeta Ales"], breweries.Select(x => x.Name));
        Assert.DoesNotContain(breweries, x => x.Name == "Closed Brewery");
        Assert.Equal("1.0", response.Headers.GetValues("api-supported-versions").Single());
    }

    [Fact]
    public async Task Autocomplete_ReturnsOrderedSuggestions()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries/autocomplete?term=brew&limit=5");
        var suggestions = await response.Content.ReadFromJsonAsync<string[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(suggestions);
        Assert.Equal(["Alpha Brewing", "Stone Brewing"], suggestions);
    }

    [Fact]
    public async Task GetBreweries_CombinesCityAndSearchFilters()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries?city=austin&search=ales");
        var breweries = await response.Content.ReadFromJsonAsync<BreweryResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var brewery = Assert.Single(Assert.IsType<BreweryResponse[]>(breweries));
        Assert.Equal("Zeta Ales", brewery.Name);
        Assert.Equal("Austin", brewery.City);
    }

    [Fact]
    public async Task GetBreweries_AppliesSortingAndPagination()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/breweries?sortBy=name&sortDirection=asc&page=2&pageSize=1");
        var breweries = await response.Content.ReadFromJsonAsync<BreweryResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var brewery = Assert.Single(Assert.IsType<BreweryResponse[]>(breweries));
        Assert.Equal("Stone Brewing", brewery.Name);
    }

    [Fact]
    public async Task GetBreweries_SortsByDistanceAndIncludesCalculatedDistance()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/breweries?sortBy=distance&latitude=30.2&longitude=-97.7&pageSize=10");
        var breweries = await response.Content.ReadFromJsonAsync<BreweryResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(breweries);
        Assert.Equal("Alpha Brewing", breweries[0].Name);
        Assert.Equal(0, breweries[0].DistanceKm);
        Assert.All(breweries, brewery => Assert.NotNull(brewery.DistanceKm));
    }

    [Fact]
    public async Task GetBreweries_DistanceSortWithoutCoordinatesReturnsBadRequest()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries?sortBy=distance");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Latitude and longitude are required", problem?.Detail);
    }

    [Fact]
    public async Task GetBreweries_InvalidPageSizeReturnsValidationProblem()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries?pageSize=0");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("PageSize", problem.Errors.Keys);
    }

    [Fact]
    public async Task GetBreweries_MalformedPageValueReturnsValidationProblem()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries?page=not-a-number");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Page", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("/api/v1/breweries/autocomplete", "Term")]
    [InlineData("/api/v1/breweries/autocomplete?term=brew&limit=0", "Limit")]
    public async Task Autocomplete_InvalidQueryReturnsValidationProblem(string path, string property)
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains(property, problem.Errors.Keys);
    }

    [Fact]
    public async Task GetBreweries_InvalidSortReturnsBadRequestProblemDetails()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries?sortBy=rating");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Bad Request", problem?.Title);
        Assert.Contains("Invalid sortBy value", problem?.Detail);
    }

    [Fact]
    public async Task GetBreweries_UnexpectedStoreFailureReturnsSafeServerError()
    {
        await using var factory = new BreweryApiFactory(throwOnRequest: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/breweries");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Server Error", problem?.Title);
        Assert.Equal("An unexpected error occurred.", problem?.Detail);
    }

    [Fact]
    public async Task UnsupportedApiVersion_DoesNotMatchVersionedRoute()
    {
        await using var factory = new BreweryApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v2/breweries");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class BreweryApiFactory(bool throwOnRequest = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBreweryCacheStore>();
                services.AddSingleton<IBreweryCacheStore>(
                    throwOnRequest ? new ThrowingBreweryStore() : new FakeBreweryStore());
            });
        }
    }

    private sealed class FakeBreweryStore : IBreweryCacheStore
    {
        public Task<IReadOnlyList<Brewery>> GetOpenBreweriesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<Brewery> breweries =
            [
                new("Stone Brewing", "Escondido", "111", 33.1, -117.1, "micro"),
                new("Alpha Brewing", "Austin", "222", 30.2, -97.7, "brewpub"),
                new("Zeta Ales", "Austin", "333", 30.3, -97.8, "regional")
            ];
            return Task.FromResult(breweries);
        }
    }

    private sealed class ThrowingBreweryStore : IBreweryCacheStore
    {
        public Task<IReadOnlyList<Brewery>> GetOpenBreweriesAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Sensitive database failure");
        }
    }
}
