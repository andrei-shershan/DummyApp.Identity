using DummyApp.Identity.Data;
using DummyApp.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace DummyApp.Identity.Services;

public sealed class InviteService : IInviteService
{
    private readonly AppDbContext _dbContext;

    public InviteService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveInviteTokenAsync(string email, string token, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var invite = await _dbContext.Invites.SingleOrDefaultAsync(i => i.Email == normalizedEmail, cancellationToken);
        if (invite is null)
        {
            invite = new Invite
            {
                Email = normalizedEmail,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };
            _dbContext.Invites.Add(invite);
        }
        else
        {
            invite.Token = token;
            invite.CreatedAt = DateTime.UtcNow;
            invite.ExpiresAt = DateTime.UtcNow.AddDays(7);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Invite?> GetInviteByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return await _dbContext.Invites.SingleOrDefaultAsync(i => i.Token == token.Trim(), cancellationToken);
    }

    public async Task RemoveInviteAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var invite = await _dbContext.Invites.SingleOrDefaultAsync(i => i.Token == token.Trim(), cancellationToken);
        if (invite is null)
        {
            return;
        }

        _dbContext.Invites.Remove(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
