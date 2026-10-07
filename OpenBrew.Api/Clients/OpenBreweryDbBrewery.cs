using System.Text.Json.Serialization;

namespace OpenBrew.Api.Clients;

public sealed class OpenBreweryDbBrewery
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("brewery_type")]
    public string? BreweryType { get; init; }
}
