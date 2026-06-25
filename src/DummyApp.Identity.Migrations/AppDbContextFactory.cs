using DummyApp.Identity.Configuration;
using DummyApp.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace DummyApp.Identity.Migrations;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "DummyApp.Identity"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var databaseSection = configuration.GetSection($"{nameof(AppSettings.Infrastructure)}:{nameof(InfrastructureOptions.Databases)}:{nameof(DatabasesOptions.Identity)}");
        var useInMemoryDb = databaseSection.GetValue<bool?>(nameof(IdentityDatabaseOptions.UseInMemory)) ?? true;
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        if (useInMemoryDb)
        {
            optionsBuilder.UseInMemoryDatabase("DevDb");
        }
        else
        {
            var connectionString = databaseSection.GetValue<string>(nameof(IdentityDatabaseOptions.ConnectionString));
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is required when Infrastructure:Databases:Identity:UseInMemory is false.");
            }

            optionsBuilder.UseMySQL(connectionString);
        }

        optionsBuilder.UseOpenIddict();
        return new AppDbContext(optionsBuilder.Options);
    }
}
