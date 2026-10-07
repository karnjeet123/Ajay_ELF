using OpenBrew.Api.Contracts;
using OpenBrew.Api.Models;

namespace OpenBrew.Api.Services;

public sealed class BreweryService : IBreweryService
{
    private readonly IBreweryCacheStore _breweryCacheStore;

    public BreweryService(IBreweryCacheStore breweryCacheStore)
    {
        _breweryCacheStore = breweryCacheStore;
    }

    public async Task<IReadOnlyList<BreweryResponse>> GetBreweriesAsync(BreweryQueryRequest request, CancellationToken cancellationToken)
    {
        var breweries = await _breweryCacheStore.GetOpenBreweriesAsync(cancellationToken);

        var filtered = breweries.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            filtered = filtered.Where(x => x.City.Contains(request.City, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            filtered = filtered.Where(x =>
                x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.City.Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        }

        var sortDirection = ParseSortDirection(request.SortDirection);
        var sortBy = ParseSortBy(request.SortBy);

        var origin = request.Latitude.HasValue && request.Longitude.HasValue
            ? (request.Latitude.Value, request.Longitude.Value)
            : ((double, double)?)null;

        if (sortBy == SortBy.Distance && origin is null)
        {
            throw new ArgumentException("Latitude and longitude are required when sorting by distance.");
        }

        var ordered = sortBy switch
        {
            SortBy.Name => sortDirection == SortDirection.Asc
                ? filtered.OrderBy(x => x.Name)
                : filtered.OrderByDescending(x => x.Name),
            SortBy.City => sortDirection == SortDirection.Asc
                ? filtered.OrderBy(x => x.City)
                : filtered.OrderByDescending(x => x.City),
            SortBy.Distance => sortDirection == SortDirection.Asc
                ? filtered.OrderBy(x => GetDistanceOrMax(x, origin!.Value))
                : filtered.OrderByDescending(x => GetDistanceOrMax(x, origin!.Value)),
            _ => filtered.OrderBy(x => x.Name)
        };

        var paged = ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return paged.Select(x => new BreweryResponse(
                x.Name,
                x.City,
                x.Phone,
                origin is null || x.Latitude is null || x.Longitude is null
                    ? null
                    : Math.Round(CalculateDistanceKm(origin.Value.Item1, origin.Value.Item2, x.Latitude.Value, x.Longitude.Value), 2)))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(AutocompleteQueryRequest request, CancellationToken cancellationToken)
    {
        var breweries = await _breweryCacheStore.GetOpenBreweriesAsync(cancellationToken);

        return breweries
            .Where(x => x.Name.StartsWith(request.Term, StringComparison.OrdinalIgnoreCase) ||
                        x.Name.Contains(request.Term, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Take(request.Limit)
            .ToList();
    }

    private static SortBy ParseSortBy(string sortBy)
    {
        return sortBy.Trim().ToLowerInvariant() switch
        {
            "name" => SortBy.Name,
            "city" => SortBy.City,
            "distance" => SortBy.Distance,
            _ => throw new ArgumentException("Invalid sortBy value. Allowed values: name, city, distance.")
        };
    }

    private static SortDirection ParseSortDirection(string sortDirection)
    {
        return sortDirection.Trim().ToLowerInvariant() switch
        {
            "asc" => SortDirection.Asc,
            "desc" => SortDirection.Desc,
            _ => throw new ArgumentException("Invalid sortDirection value. Allowed values: asc, desc.")
        };
    }

    private static double GetDistanceOrMax(Brewery brewery, (double Latitude, double Longitude) origin)
    {
        return brewery.Latitude is null || brewery.Longitude is null
            ? double.MaxValue
            : CalculateDistanceKm(origin.Latitude, origin.Longitude, brewery.Latitude.Value, brewery.Longitude.Value);
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private enum SortBy
    {
        Name,
        City,
        Distance
    }

    private enum SortDirection
    {
        Asc,
        Desc
    }
}