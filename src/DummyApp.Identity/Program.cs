using DummyApp.Identity.Data;
using DummyApp.Identity.Models;
using DummyApp.Identity.Services;
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

// DbContext - InMemory for development. Swap to UseSqlServer later.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseInMemoryDatabase("DevDb");

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

// In-memory token store
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();

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

        // Authorization Code flow with PKCE (recommended for SPAs)
        options.AllowAuthorizationCodeFlow()
               .RequireProofKeyForCodeExchange();

        // Refresh tokens
        options.AllowRefreshTokenFlow();

        // Register the scopes
        options.RegisterScopes(OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.OfflineAccess, OpenIddictConstants.Scopes.OpenId);

        // Development encryption/signing keys (replace in production)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // Register the ASP.NET Core host and enable endpoint passthrough to allow custom controller handling
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();
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
    db.Database.EnsureCreated();

    // Seed a confidential BFF client (confidential client performs server-side code exchange).
    var manager = scope.ServiceProvider.GetRequiredService<OpenIddict.Abstractions.IOpenIddictApplicationManager>();
    var clientId = "bff-client";
    var existing = manager.FindByClientIdAsync(clientId).GetAwaiter().GetResult();
    if (existing == null)
    {
        var secret = Guid.NewGuid().ToString("N");
        var descriptor = new OpenIddict.Abstractions.OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            DisplayName = "BFF (confidential) client",
        };

        // Adjust RedirectUris to your BFF callback endpoint. Default used here:
        descriptor.RedirectUris.Add(new Uri("https://localhost:5002/signin-oidc"));

        // Permissions required for the authorization code + PKCE flow and refresh tokens
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Scopes.Email);
        descriptor.Permissions.Add(OpenIddictConstants.Scopes.Profile);
        descriptor.Permissions.Add(OpenIddictConstants.Scopes.OfflineAccess);
        descriptor.Permissions.Add(OpenIddictConstants.Scopes.OpenId);

        manager.CreateAsync(descriptor).GetAwaiter().GetResult();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        logger.LogInformation("Created OpenIddict client {ClientId} with secret: {Secret}", clientId, secret);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();

record JwtSettings(string Issuer, string Audience);
