namespace OpenBrew.Api.Contracts;

/// <summary>
/// Generic response model for brewery list results.
/// </summary>
public sealed record BreweryResponse(
    string Name,
    string City,
    string? Phone,
    double? DistanceKm);