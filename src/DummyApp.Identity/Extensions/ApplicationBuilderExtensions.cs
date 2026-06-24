using DummyApp.Identity.Configuration;
using DummyApp.Identity.Data;
using DummyApp.Identity.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace DummyApp.Identity.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication InitializeDatabaseAndSeed(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var appSettings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
        var seedService = scope.ServiceProvider.GetRequiredService<IIdentitySeedService>();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        EnsureDatabaseInitialized(db, startupLogger);

        if (appSettings.FeatureFlags.DefaultRolesSeed)
        {
            seedService.SeedRolesAsync().GetAwaiter().GetResult();
        }

        if (appSettings.FeatureFlags.DefaultUsersSeed)
        {
            seedService.SeedUsersAsync().GetAwaiter().GetResult();
        }

        EnsureOpenIddictClient(manager, appSettings.IdentityServer.OidcClients.BFF, "bff-client", "secret", "BFF (confidential) client", startupLogger);
        EnsureOpenIddictClient(manager, appSettings.IdentityServer.OidcClients.ApiGateway, "storage-client", "storage-secret", "Storage service client", startupLogger, addPermissions: descriptor =>
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "storage.read");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "storage.write");
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "identity.admin");
        });

        return app;
    }

    public static WebApplication UseConfiguredForwardedHeaders(this WebApplication app)
    {
        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        var appSettings = app.Services.GetRequiredService<IOptions<AppSettings>>().Value;
        if (appSettings.ReverseProxy.TrustAllProxies)
        {
            forwardedOptions.KnownIPNetworks.Clear();
            forwardedOptions.KnownProxies.Clear();
        }

        app.UseForwardedHeaders(forwardedOptions);
        return app;
    }

    private static void EnsureDatabaseInitialized(AppDbContext db, ILogger logger)
    {
        if (db.Database.IsRelational())
        {
            var retries = 10;
            while (true)
            {
                try
                {
                    db.Database.OpenConnection();
                    db.Database.CloseConnection();
                    break;
                }
                catch (Exception ex) when (retries-- > 0)
                {
                    logger.LogWarning(ex, "Database not ready, retrying in 10 s ({Retries} attempts left)", retries);
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }

            db.Database.Migrate();
        }
        else
        {
            db.Database.EnsureCreated();
        }
    }

    private static void EnsureOpenIddictClient(
        IOpenIddictApplicationManager manager,
        OidcClientOptions clientOptions,
        string defaultClientId,
        string defaultClientSecret,
        string defaultDisplayName,
        ILogger logger,
        Action<OpenIddictApplicationDescriptor>? addPermissions = null)
    {
        var clientId = string.IsNullOrWhiteSpace(clientOptions.ClientId) ? defaultClientId : clientOptions.ClientId;

        var resolvedSecret = string.IsNullOrWhiteSpace(clientOptions.ClientSecret) ? defaultClientSecret : clientOptions.ClientSecret;
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = resolvedSecret,
            ClientType = string.IsNullOrWhiteSpace(resolvedSecret)
                ? OpenIddictConstants.ClientTypes.Public
                : OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = defaultDisplayName,
        };

        if (!string.IsNullOrWhiteSpace(clientOptions.RedirectUri))
        {
            descriptor.RedirectUris.Add(new Uri(clientOptions.RedirectUri));
        }

        if (!string.IsNullOrWhiteSpace(clientOptions.PostLogoutRedirectUri))
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(clientOptions.PostLogoutRedirectUri));
        }

        addPermissions?.Invoke(descriptor);

        if (descriptor.Permissions.Count == 0)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Email);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);
        }

        var existing = manager.FindByClientIdAsync(clientId).GetAwaiter().GetResult();
        if (existing is null)
        {
            manager.CreateAsync(descriptor).GetAwaiter().GetResult();
            logger.LogInformation("Created OpenIddict client {ClientId}", clientId);
        }
        else
        {
            manager.UpdateAsync(existing, descriptor).GetAwaiter().GetResult();
            logger.LogInformation("Updated OpenIddict client {ClientId}", clientId);
        }
    }
}
