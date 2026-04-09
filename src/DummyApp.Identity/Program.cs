using DummyApp.Identity.Data;
using DummyApp.Identity.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Add MVC with views for login UI
builder.Services.AddControllersWithViews();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Jwt settings (dev). In production, override via configuration/secrets.
var jwtKey = builder.Configuration["Jwt:Key"] ?? Guid.NewGuid().ToString();
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DummyApp.Identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DummyApp.Client";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services.AddSingleton(signingKey);
builder.Services.AddSingleton(new JwtSettings(jwtIssuer, jwtAudience));

var databaseSection = builder.Configuration.GetSection("Database");
var useInMemoryDb = databaseSection.GetValue<bool?>("UseInMemory") ?? true;
var connectionString = databaseSection.GetValue<string>("ConnectionString");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useInMemoryDb)
    {
        options.UseInMemoryDatabase("DevDb");
    }
    else
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is required when Database:UseInMemory is false.");
        }

        options.UseMySQL(connectionString);
    }

    // Register the entity sets needed by OpenIddict.
    options.UseOpenIddict();
});

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Cookie settings (BFF cookie)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "bff.cookie";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/account/login";
});

// OpenIddict configuration
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        // Authorization and token endpoints
        options.SetAuthorizationEndpointUris("/connect/authorize");
        options.SetTokenEndpointUris("/connect/token");
        options.SetEndSessionEndpointUris("/connect/logout");
        options.SetIssuer(new Uri("https://identity.dummy.localhost"));

        // Authorization Code flow with PKCE (recommended for SPAs)
        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        // Client Credentials flow for backend-to-backend
        options.AllowClientCredentialsFlow();

        // Refresh tokens
        options.AllowRefreshTokenFlow();

        // Register the scopes
        options.RegisterScopes(
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.OfflineAccess,
            OpenIddictConstants.Scopes.OpenId,
            "storage.read",
            "storage.write");

        // Register the audiences so OpenIddict allows them in tokens.
        options.RegisterAudiences("DummyApp.StorageService");

        // JWT access tokens are the default in this OpenIddict version.
        // UseReferenceAccessTokens() would be needed for opaque reference tokens.

        // Register an encryption key for OpenIddict itself, but do not encrypt access tokens.
        options.AddEphemeralEncryptionKey();
        options.AddDevelopmentSigningCertificate();
        options.DisableAccessTokenEncryption();

        // Register the ASP.NET Core host and enable endpoint passthrough to allow custom controller handling
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

// Authorization
builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure database is created (InMemory) and seed OpenIddict client if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Step 1: Wait until the DB is reachable.
    // Retry ONLY on connection errors — not on migration or seeding errors.
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
                startupLogger.LogWarning(ex, "Database not ready, retrying in 5 s ({Retries} attempts left)", retries);
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        // Step 2: Apply pending migrations exactly once.
        // Database.Migrate() is idempotent: it checks __EFMigrationsHistory
        // and skips already-applied migrations. Safe to call on every restart.
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    // Seed a confidential BFF client (confidential client performs server-side code exchange).
    var manager = scope.ServiceProvider.GetRequiredService<OpenIddict.Abstractions.IOpenIddictApplicationManager>();
    var clientId = "bff-client";
    var existing = manager.FindByClientIdAsync(clientId).GetAwaiter().GetResult();
    if (existing == null)
    {
        // Hardcoded secret for dev. Matches BFF appsettings.json "ClientSecret": "secret".
        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = "secret",
            DisplayName = "BFF (confidential) client",
        };

        // Redirect URI used by the BFF callback handler after the auth code is issued.
        descriptor.RedirectUris.Add(new Uri("https://bff.dummy.localhost/signin-oidc"));
        descriptor.PostLogoutRedirectUris.Add(new Uri("https://bff.dummy.localhost/signout-callback-oidc"));

        // Permissions required for the authorization code + PKCE flow and refresh tokens
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        // Scope permissions: openid is auto-allowed; profile and offline_access must be explicit.
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Email);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Profile);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);

        manager.CreateAsync(descriptor).GetAwaiter().GetResult();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        logger.LogInformation("Created OpenIddict client {ClientId}", clientId);
    }

    // Seed test user: test@test.com / !test123
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string testEmail = "test@test.com";
    const string testPassword = "!test123";
    var testUser = userManager.FindByEmailAsync(testEmail).GetAwaiter().GetResult();
    if (testUser == null)
    {
        testUser = new ApplicationUser
        {
            UserName = testEmail,
            Email = testEmail,
            EmailConfirmed = true
        };
        var createResult = userManager.CreateAsync(testUser, testPassword).GetAwaiter().GetResult();
        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed test user: {errors}");
        }

        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        seedLogger.LogInformation("Seeded test user {Email}", testEmail);
    }

    // Seed a client credentials client for backend-to-backend authentication
    var storageClientId = "storage-client";
    var existingStorageClient = manager.FindByClientIdAsync(storageClientId).GetAwaiter().GetResult();
    if (existingStorageClient == null)
    {
        var storageDescriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
        {
            ClientId = storageClientId,
            ClientSecret = "storage-secret",
            DisplayName = "Storage service client",
        };

        storageDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        storageDescriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        storageDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "storage.read");
        storageDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "storage.write");

        manager.CreateAsync(storageDescriptor).GetAwaiter().GetResult();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        logger.LogInformation("Created OpenIddict client {ClientId} with secret: {Secret}", storageClientId, "storage-secret");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
if (builder.Configuration.GetValue<bool>("ReverseProxy:TrustAllProxies"))
{
    // Dev only: trust all proxies inside the Docker network (Traefik).
    // Do NOT enable in production.
    forwardedOptions.KnownNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();
}
app.UseForwardedHeaders(forwardedOptions);
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

record JwtSettings(string Issuer, string Audience);
