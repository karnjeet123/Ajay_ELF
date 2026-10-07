namespace OpenBrew.Api.Services;

public interface IBreweryRefreshService
{
    Task RefreshAsync(CancellationToken cancellationToken);
}
