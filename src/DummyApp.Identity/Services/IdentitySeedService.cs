using DummyApp.Identity.Constants;
using DummyApp.Identity.Models;
using Microsoft.AspNetCore.Identity;

namespace DummyApp.Identity.Services;

public sealed record FeatureFlags
{
    public bool DefaultRolesSeed { get; init; }
    public bool DefaultUsersSeed { get; init; }
}

public sealed record DefaultUserSeed
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}

public interface IIdentitySeedService
{
    Task SeedRolesAsync();
    Task SeedUsersAsync();
}

public sealed class IdentitySeedService : IIdentitySeedService
{
    private static readonly string[] RoleNamesToSeed =
    {
        RoleNames.Admin,
        RoleNames.Moderator,
        RoleNames.User
    };

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySeedService> _logger;

    public IdentitySeedService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger<IdentitySeedService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedRolesAsync()
    {
        _logger.LogInformation("Default role seeding started.");

        foreach (var roleName in RoleNamesToSeed)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to seed role {RoleName}: {Errors}", roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                continue;
            }

            _logger.LogInformation("Seeded role {RoleName}", roleName);
        }
    }

    public async Task SeedUsersAsync()
    {
        _logger.LogInformation("Default user seeding started.");

        var defaultUsers = _configuration.GetSection("DefaultUsers").Get<List<DefaultUserSeed>>() ?? new List<DefaultUserSeed>();
        if (!defaultUsers.Any())
        {
            _logger.LogInformation("DefaultUsers section is empty; no users to seed.");
            return;
        }

        foreach (var userSeed in defaultUsers)
        {
            if (string.IsNullOrWhiteSpace(userSeed.Email)
                || string.IsNullOrWhiteSpace(userSeed.Password)
                || string.IsNullOrWhiteSpace(userSeed.Role))
            {
                _logger.LogWarning("Skipping invalid default user entry. Email, Password and Role are required.");
                continue;
            }

            var email = userSeed.Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var createResult = await _userManager.CreateAsync(user, userSeed.Password);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create default user {Email}: {Errors}", email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    continue;
                }

                _logger.LogInformation("Created default user {Email}", email);
            }

            if (!await _roleManager.RoleExistsAsync(userSeed.Role))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(userSeed.Role));
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to create role {RoleName} for default user {Email}: {Errors}", userSeed.Role, email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    continue;
                }

                _logger.LogInformation("Created missing role {RoleName} for default user seeding", userSeed.Role);
            }

            if (!await _userManager.IsInRoleAsync(user, userSeed.Role))
            {
                var addToRoleResult = await _userManager.AddToRoleAsync(user, userSeed.Role);
                if (!addToRoleResult.Succeeded)
                {
                    _logger.LogError("Failed to add user {Email} to role {RoleName}: {Errors}", email, userSeed.Role, string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
                    continue;
                }

                _logger.LogInformation("Added default user {Email} to role {RoleName}", email, userSeed.Role);
            }
        }
    }
}
