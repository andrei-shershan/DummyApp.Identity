namespace DummyApp.Identity.Models;

using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? AvatarUrl { get; set; }
    public string? AvatarSmallUrl { get; set; }
}
