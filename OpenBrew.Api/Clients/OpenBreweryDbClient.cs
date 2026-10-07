using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using OpenBrew.Api.Options;

namespace OpenBrew.Api.Clients;

public sealed class OpenBreweryDbClient(
    HttpClient httpClient,
    IOptions<OpenBreweryOptions> options,
    ILogger<OpenBreweryDbClient> logger) : IOpenBreweryDbClient
{
    private readonly OpenBreweryOptions _options = options.Value;

    public async Task<IReadOnlyList<OpenBreweryDbBrewery>> GetBreweriesAsync(
        CancellationToken cancellationToken)
    {
        var allBreweries = new List<OpenBreweryDbBrewery>();

        for (var page = 1; page <= _options.MaxPagesToFetch; page++)
        {
            var requestUri = $"breweries?per_page={_options.PerPage}&page={page}";
            logger.LogInformation("Fetching Open Brewery DB page {Page}", page);

            var breweries = await httpClient.GetFromJsonAsync<List<OpenBreweryDbBrewery>>(
                requestUri,
                cancellationToken) ?? [];

            if (breweries.Count == 0)
            {
                break;
            }

            allBreweries.AddRange(breweries);

            if (breweries.Count < _options.PerPage)
            {
                break;
            }
        }

        return allBreweries;
    }
}
