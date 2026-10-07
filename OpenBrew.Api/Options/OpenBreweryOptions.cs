namespace OpenBrew.Api.Options;

public sealed class OpenBreweryOptions
{
    public string BaseUrl { get; init; } = "https://api.openbrewerydb.org/v1/";

    public int CacheDurationMinutes { get; init; } = 10;

    public int PerPage { get; init; } = 200;

    public int MaxPagesToFetch { get; init; } = 10;
}
