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
    [CustomValidation(typeof(BreweryQueryRequest), nameof(ValidateLatitude))]
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude used for distance-based sorting.
    /// </summary>
    [CustomValidation(typeof(BreweryQueryRequest), nameof(ValidateLongitude))]
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

    public static ValidationResult? ValidateLatitude(double? value) =>
        ValidateCoordinate(value, -90, 90, nameof(Latitude));

    public static ValidationResult? ValidateLongitude(double? value) =>
        ValidateCoordinate(value, -180, 180, nameof(Longitude));

    private static ValidationResult? ValidateCoordinate(
        double? value,
        double minimum,
        double maximum,
        string propertyName)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (!double.IsFinite(value.Value))
        {
            return new ValidationResult($"{propertyName} must be a finite number.");
        }

        return value.Value < minimum || value.Value > maximum
            ? new ValidationResult($"{propertyName} must be between {minimum} and {maximum}.")
            : ValidationResult.Success;
    }
}