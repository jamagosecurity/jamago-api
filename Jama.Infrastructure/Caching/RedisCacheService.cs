using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Jama.Infrastructure.Caching;

public class RedisCacheService(IDistributedCache cache) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var json = await cache.GetStringAsync(key, ct);
        return json is null ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry;
        }

        await cache.SetStringAsync(key, JsonSerializer.Serialize(value, JsonOptions), options, ct);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default) =>
        cache.RemoveAsync(key, ct);
}
