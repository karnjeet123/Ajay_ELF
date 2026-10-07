using OpenBrew.Api.Contracts;

namespace OpenBrew.Api.Services;

public interface IBreweryService
{
    Task<IReadOnlyList<BreweryResponse>> GetBreweriesAsync(BreweryQueryRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> AutocompleteAsync(AutocompleteQueryRequest request, CancellationToken cancellationToken);
}