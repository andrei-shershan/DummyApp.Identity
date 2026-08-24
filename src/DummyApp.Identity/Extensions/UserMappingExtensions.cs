using DummyApp.Identity.DtoModels;
using DummyApp.Identity.Models;

namespace DummyApp.Identity.Extensions;

public static class UserMappingExtensions
{
    public static UserDto ToDto(this ApplicationUser user, IEnumerable<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            AvatarSmallUrl = user.AvatarSmallUrl,
            Roles = roles,
            IsActive = user.IsActive
        };
    }
}
