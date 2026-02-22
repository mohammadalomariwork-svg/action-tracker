using ActionTracker.Domain.Common;

namespace ActionTracker.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CreatedByIp { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive() => !IsRevoked && !IsExpired();
}
