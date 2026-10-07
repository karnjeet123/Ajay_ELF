using Microsoft.Extensions.Options;
using OpenBrew.Api.Options;

namespace OpenBrew.Api.Services;

public sealed class BreweryRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<OpenBreweryOptions> options,
    ILogger<BreweryRefreshBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshSafelyAsync(stoppingToken);
        }
    }

    private async Task RefreshSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var refreshService = scope.ServiceProvider.GetRequiredService<IBreweryRefreshService>();
            await refreshService.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Brewery refresh failed; existing SQLite data will continue to be served.");
        }
    }
}
