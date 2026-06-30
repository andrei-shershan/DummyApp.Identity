using DummyApp.Identity.Models;

namespace DummyApp.Identity.Services;

public interface IInviteService
{
    Task SaveInviteTokenAsync(string email, string token, CancellationToken cancellationToken);
    Task<Invite?> GetInviteByTokenAsync(string token, CancellationToken cancellationToken);
    Task RemoveInviteAsync(string token, CancellationToken cancellationToken);
}
