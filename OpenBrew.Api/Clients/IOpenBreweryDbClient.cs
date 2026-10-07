namespace OpenBrew.Api.Clients;

public interface IOpenBreweryDbClient
{
    Task<IReadOnlyList<OpenBreweryDbBrewery>> GetBreweriesAsync(CancellationToken cancellationToken);
}
