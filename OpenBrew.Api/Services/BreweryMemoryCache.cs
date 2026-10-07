using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenBrew.Api.Models;
using OpenBrew.Api.Options;

namespace OpenBrew.Api.Services;

public sealed class BreweryMemoryCache(
    IMemoryCache memoryCache,
    IOptions<OpenBreweryOptions> options) : IBreweryMemoryCache
{
    private const string CacheKey = "breweries:open";
    private readonly TimeSpan _duration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes);

    public bool TryGet(out IReadOnlyList<Brewery> breweries)
    {
        if (memoryCache.TryGetValue(CacheKey, out IReadOnlyList<Brewery>? cached) && cached is not null)
        {
            breweries = cached;
            return true;
        }

        breweries = Array.Empty<Brewery>();
        return false;
    }

    public void Set(IReadOnlyList<Brewery> breweries)
    {
        memoryCache.Set(CacheKey, breweries, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _duration
        });
    }
}
