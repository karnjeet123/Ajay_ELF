namespace OpenBrew.Api.Data;

public sealed class BreweryEntity
{
    public int Id { get; set; }

    public required string Name { get; init; }

    public required string City { get; init; }

    public string? Phone { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public required string BreweryType { get; init; }
}
