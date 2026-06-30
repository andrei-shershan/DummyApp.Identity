namespace DummyApp.Identity.Configuration;

public sealed class AppSettings
{
    public JwtOptions Jwt { get; init; } = new();
    public FeatureFlagsOptions FeatureFlags { get; init; } = new();
    public InfrastructureOptions Infrastructure { get; init; } = new();
    public ServicesOptions Services { get; init; } = new();
    public IdentityServerOptions IdentityServer { get; init; } = new();
    public ReverseProxyOptions ReverseProxy { get; init; } = new();
    public KeyVaultOptions KeyVault { get; init; } = new();
    public List<DefaultUserSeed> DefaultUsers { get; init; } = new();
}

public sealed class JwtOptions
{
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = "DummyApp.Identity";
    public string Audience { get; init; } = "DummyApp.Client";
}

public sealed class FeatureFlagsOptions
{
    public bool DefaultRolesSeed { get; init; }
    public bool DefaultUsersSeed { get; init; }
}

public sealed class InfrastructureOptions
{
    public DatabasesOptions Databases { get; init; } = new();
}

public sealed class DatabasesOptions
{
    public IdentityDatabaseOptions Identity { get; init; } = new();
}

public sealed class IdentityDatabaseOptions
{
    public bool UseInMemory { get; init; } = true;
    public string ConnectionString { get; init; } = string.Empty;
}

public sealed class ServicesOptions
{
    public ServiceOptions Identity { get; init; } = new();
    public ServiceOptions BFF { get; init; } = new();
    public ServiceOptions Frontend { get; init; } = new();
}

public sealed class ServiceOptions
{
    public string BaseUrl { get; init; } = string.Empty;
}

public sealed class IdentityServerOptions
{
    public string Authority { get; init; } = "https://identity.dummy.localhost";
    public string[] Audiences { get; init; } = Array.Empty<string>();
    public OidcClientsOptions OidcClients { get; init; } = new();
}

public sealed class OidcClientsOptions
{
    public OidcClientOptions BFF { get; init; } = new();
    public OidcClientOptions ApiGateway { get; init; } = new();
}

public sealed class OidcClientOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string PostLogoutRedirectUri { get; init; } = string.Empty;
}

public sealed class ReverseProxyOptions
{
    public bool TrustAllProxies { get; init; }
}

public sealed class KeyVaultOptions
{
    public string Url { get; init; } = string.Empty;
}

public sealed class DefaultUserSeed
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
}
