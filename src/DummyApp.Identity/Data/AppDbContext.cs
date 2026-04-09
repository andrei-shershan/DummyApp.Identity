namespace DummyApp.Identity.Data;

using DummyApp.Identity.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // MySql.EntityFrameworkCore generates varchar(400) for OpenIddict Subject columns.
        // The composite index on OpenIddictTokens (ApplicationId+Status+Subject+Type) exceeds
        // MySQL's 3072-byte key limit with utf8mb4 (4 bytes/char): 255+50+400+150 = 855 × 4 = 3420.
        // Limit Subject to 300 so the index fits: 255+50+300+150 = 755 × 4 = 3020 bytes.
        builder.Entity("OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken")
            .Property<string>("Subject")
            .HasMaxLength(300);
    }
}
