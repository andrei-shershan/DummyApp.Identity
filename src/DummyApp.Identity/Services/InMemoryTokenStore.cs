using Microsoft.Extensions.Caching.Memory;

namespace DummyApp.Identity.Services;

public class InMemoryTokenStore : ITokenStore
{
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _options = new() { SlidingExpiration = TimeSpan.FromMinutes(60) };

    public InMemoryTokenStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task SaveAsync(string userId, TokenResponse tokens)
    {
        _cache.Set(GetKey(userId), tokens, _options);
        return Task.CompletedTask;
    }

    public Task<TokenResponse?> GetAsync(string userId)
    {
        _cache.TryGetValue(GetKey(userId), out TokenResponse? tokens);
        return Task.FromResult(tokens);
    }

    public Task RemoveAsync(string userId)
    {
        _cache.Remove(GetKey(userId));
        return Task.CompletedTask;
    }

    private static string GetKey(string userId) => $"tokens:{userId}";
}
