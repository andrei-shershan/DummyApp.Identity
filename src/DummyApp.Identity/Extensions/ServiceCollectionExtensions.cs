using Azure.Identity;
using DummyApp.Identity.Configuration;
using DummyApp.Identity.Data;
using DummyApp.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Text;

namespace DummyApp.Identity.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddAzureKeyVaultIfConfigured(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            var keyVaultUrl = builder.Configuration[$"{nameof(AppSettings.KeyVault)}:{nameof(KeyVaultOptions.Url)}"];
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
                var credential = string.IsNullOrEmpty(clientId)
                    ? new ManagedIdentityCredential()
                    : new ManagedIdentityCredential(clientId);

                builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), credential);
            }
        }

        return builder;
    }

    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration);

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var jwtKey = string.IsNullOrEmpty(settings.Jwt.Key)
                ? Guid.NewGuid().ToString()
                : settings.Jwt.Key;

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            return new JwtSettings(settings.Jwt.Issuer, settings.Jwt.Audience);
        });

        return services;
    }

    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseSettings = configuration
            .GetSection($"{nameof(AppSettings.Infrastructure)}:{nameof(InfrastructureOptions.Databases)}:{nameof(DatabasesOptions.Identity)}")
            .Get<IdentityDatabaseOptions>() ?? new IdentityDatabaseOptions();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (databaseSettings.UseInMemory)
            {
                options.UseInMemoryDatabase("DevDb");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(databaseSettings.ConnectionString))
                {
                    throw new InvalidOperationException("Database connection string is required when Infrastructure:Databases:Identity:UseInMemory is false.");
                }

                options.UseMySQL(databaseSettings.ConnectionString);
            }

            options.UseOpenIddict();
        });

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
        })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "bff.cookie";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.LoginPath = "/account/login";
        });

        return services;
    }

    public static IServiceCollection AddOpenIddictWithSettings(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var identityServer = configuration.GetSection(nameof(AppSettings.IdentityServer)).Get<IdentityServerOptions>() ?? new IdentityServerOptions();
        var audiences = identityServer.Audiences.Length > 0
            ? identityServer.Audiences
            : new[] { "DummyApp.StorageService" };

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<AppDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                options.SetEndSessionEndpointUris("/connect/logout");
                options.SetIssuer(new Uri(identityServer.Authority));

                options.AllowAuthorizationCodeFlow()
                       .RequireProofKeyForCodeExchange();
                options.AllowClientCredentialsFlow();
                options.AllowRefreshTokenFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    OpenIddictConstants.Scopes.OpenId,
                    "storage.read",
                    "storage.write",
                    "identity.admin");

                options.RegisterAudiences(audiences);
                options.AddEphemeralEncryptionKey();

                if (environment.IsDevelopment())
                {
                    options.AddDevelopmentSigningCertificate();
                }
                else
                {
                    options.AddEphemeralSigningKey();
                }

                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }
}
