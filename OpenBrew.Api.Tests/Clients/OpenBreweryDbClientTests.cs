using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OpenBrew.Api.Clients;
using OpenBrew.Api.Options;

namespace OpenBrew.Api.Tests.Clients;

public sealed class OpenBreweryDbClientTests
{
    [Fact]
    public async Task GetBreweriesAsync_AggregatesPagesAndStopsOnPartialPage()
    {
        var handler = new QueueHttpMessageHandler(
            """[{"name":"One","latitude":1.5,"longitude":2.5},{"name":"Two"}]""",
            """[{"name":"Three","latitude":3.5,"longitude":4.5}]""");
        var client = CreateClient(handler, perPage: 2, maxPages: 5);

        var result = await client.GetBreweriesAsync(CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.EndsWith("/v1/breweries?per_page=2&page=1", handler.RequestUris[0].PathAndQuery);
        Assert.EndsWith("/v1/breweries?per_page=2&page=2", handler.RequestUris[1].PathAndQuery);
        Assert.Equal(3.5, result[2].Latitude);
    }

    [Fact]
    public async Task GetBreweriesAsync_StopsAtMaximumPageCount()
    {
        var handler = new QueueHttpMessageHandler(
            """[{"name":"One"}]""",
            """[{"name":"Two"}]""",
            """[{"name":"Unexpected"}]""");
        var client = CreateClient(handler, perPage: 1, maxPages: 2);

        var result = await client.GetBreweriesAsync(CancellationToken.None);

        Assert.Equal(["One", "Two"], result.Select(x => x.Name));
        Assert.Equal(2, handler.RequestUris.Count);
    }

    private static OpenBreweryDbClient CreateClient(HttpMessageHandler handler, int perPage, int maxPages)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/v1/")
        };
        var options = Microsoft.Extensions.Options.Options.Create(new OpenBreweryOptions
        {
            PerPage = perPage,
            MaxPagesToFetch = maxPages
        });
        return new OpenBreweryDbClient(httpClient, options, NullLogger<OpenBreweryDbClient>.Instance);
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            });
        }
    }
}
