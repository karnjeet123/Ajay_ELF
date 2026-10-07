using OpenBrew.Api.Models;

namespace OpenBrew.Api.Services;

public interface IBreweryMemoryCache
{
    bool TryGet(out IReadOnlyList<Brewery> breweries);

    void Set(IReadOnlyList<Brewery> breweries);
}
