namespace DummyApp.Identity.Services;

public interface IInviteService
{
    Task SaveInviteTokenAsync(string email, string token, CancellationToken cancellationToken);
}
