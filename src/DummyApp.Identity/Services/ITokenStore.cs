namespace DummyApp.Identity.Services;

public record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public interface ITokenStore
{
    Task SaveAsync(string userId, TokenResponse tokens);
    Task<TokenResponse?> GetAsync(string userId);
    Task RemoveAsync(string userId);
}
