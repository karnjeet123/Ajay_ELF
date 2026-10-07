using System.ComponentModel.DataAnnotations;

namespace OpenBrew.Api.Contracts;

public sealed record BreweryQueryRequest
{
    /// <summary>
    /// Case-insensitive partial search value applied to brewery names and cities.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Optional city filter.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// Sort field: name, city, or distance.
    /// </summary>
    public string SortBy { get; init; } = "name";

    /// <summary>
    /// Sort direction: asc or desc.
    /// </summary>
    public string SortDirection { get; init; } = "asc";

    /// <summary>
    /// Latitude used for distance-based sorting.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude used for distance-based sorting.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// One-based page number.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of records per page.
    /// </summary>
    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}