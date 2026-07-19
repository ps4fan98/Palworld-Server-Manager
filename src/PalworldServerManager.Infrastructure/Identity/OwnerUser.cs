using Microsoft.AspNetCore.Identity;

namespace PalworldServerManager.Infrastructure.Identity;

public sealed class OwnerUser : IdentityUser
{
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSuccessfulLoginAtUtc { get; set; }

    public bool IsEnabled { get; set; } = true;
}
