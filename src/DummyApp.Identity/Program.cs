using DummyApp.Identity.Configuration;
using DummyApp.Identity.Extensions;
using DummyApp.Identity.Services;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddAzureKeyVaultIfConfigured();

builder.Services.AddApplicationOptions(builder.Configuration);
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddIdentityServices();
builder.Services.AddOpenIddictWithSettings(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddControllersWithViews();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOrIdentityService", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") ||
            context.User.HasClaim(claim =>
                claim.Type == OpenIddictConstants.Claims.Scope &&
                claim.Value == "identity.admin")));
});

builder.Services.AddScoped<IIdentitySeedService, IdentitySeedService>();
builder.Services.AddScoped<IInviteService, InviteService>();

var app = builder.Build();

app.InitializeDatabaseAndSeed();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}

app.UseConfiguredForwardedHeaders();
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