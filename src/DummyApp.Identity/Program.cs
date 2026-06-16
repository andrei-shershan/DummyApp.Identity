using DummyApp.Identity.Configuration;
using DummyApp.Identity.Extensions;
using DummyApp.Identity.Services;

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

builder.Services.AddAuthorization();
builder.Services.AddScoped<IIdentitySeedService, IdentitySeedService>();

var app = builder.Build();

app.InitializeDatabaseAndSeed();

app.UseDeveloperExceptionPage();
if (app.Environment.IsDevelopment())
{
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