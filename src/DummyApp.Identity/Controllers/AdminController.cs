using DummyApp.Identity.Models;
using DummyApp.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace DummyApp.Identity.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Policy = "AdminOrIdentityService")]
public sealed class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IInviteService _inviteService;

    public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IInviteService inviteService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _inviteService = inviteService;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = _userManager.Users.ToList();
        var result = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = roles,
                IsActive = user.IsActive
            });
        }

        return Ok(result);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = _roleManager.Roles.ToList();
        var result = roles.Select(role => new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty
        });

        return Ok(await Task.FromResult(result));
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest("Email and token are required.");
        }

        await _inviteService.SaveInviteTokenAsync(request.Email.Trim(), request.Token.Trim(), CancellationToken.None);
        return Ok();
    }

    [HttpPut("users/{id}/active")]
    public async Task<IActionResult> UpdateUserActiveState([FromRoute] string id, [FromBody] UpdateUserActiveStateRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(updateResult.Errors.Select(e => e.Description));
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles,
            IsActive = user.IsActive
        });
    }

    public sealed record UpdateUserActiveStateRequest(bool IsActive);

    public sealed record InviteRequest(string Email, string Token);

    public sealed class UserDto
    {
        public string Id { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public bool IsActive { get; init; } = true;
        public IEnumerable<string> Roles { get; init; } = Array.Empty<string>();
    }

    public sealed class RoleDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
