namespace OpenBrew.Api.Models;

public sealed record Brewery(
    string Name,
    string City,
    string? Phone,
    double? Latitude,
    double? Longitude,
    string BreweryType);