using DummyApp.Identity.Configuration;
using DummyApp.Identity.Constants;
using DummyApp.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DummyApp.Identity.Services;

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
        RoleNames.Customer,
        RoleNames.Creator
    };

    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppSettings _appSettings;
    private readonly ILogger<IdentitySeedService> _logger;

    public IdentitySeedService(
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IOptions<AppSettings> appSettings,
        ILogger<IdentitySeedService> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _appSettings = appSettings.Value;
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

        var defaultUsers = _appSettings.DefaultUsers;
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
                    EmailConfirmed = true,
                    FirstName = userSeed.FirstName,
                    LastName = userSeed.LastName
                };

                var createResult = await _userManager.CreateAsync(user, userSeed.Password);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create default user {Email}: {Errors}", email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    continue;
                }

                _logger.LogInformation("Created default user {Email}", email);
            }
            else
            {
                var shouldUpdate = false;
                if (!string.Equals(user.FirstName, userSeed.FirstName, StringComparison.Ordinal))
                {
                    user.FirstName = userSeed.FirstName;
                    shouldUpdate = true;
                }

                if (!string.Equals(user.LastName, userSeed.LastName, StringComparison.Ordinal))
                {
                    user.LastName = userSeed.LastName;
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        _logger.LogError("Failed to update default user profile for {Email}: {Errors}", email, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                        continue;
                    }

                    _logger.LogInformation("Updated default user profile for {Email}", email);
                }
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
