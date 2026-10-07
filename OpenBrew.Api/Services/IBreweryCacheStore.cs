using OpenBrew.Api.Models;

namespace OpenBrew.Api.Services;

public interface IBreweryCacheStore
{
    Task<IReadOnlyList<Brewery>> GetOpenBreweriesAsync(CancellationToken cancellationToken);
}