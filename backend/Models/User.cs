namespace ForumApi.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public int Points { get; set; }
    public int Coins { get; set; }
    public int Level { get; set; } = 1;
    public DateTime? LastSignInDate { get; set; }
    /// <summary>最近活跃（登录/发帖/回帖等），用于沉默用户运营。</summary>
    public DateTime? LastActiveAt { get; set; }
    public int ConsecutiveSignInDays { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;
    public bool IsAdmin { get; set; }
    public bool IsMuted { get; set; }
    public DateTime? MutedUntil { get; set; }
    public string? MuteReason { get; set; }
    public string InviteCode { get; set; } = string.Empty;
    public int? InvitedByUserId { get; set; }
    public bool IsVip { get; set; }
    public DateTime? VipUntil { get; set; }
    /// <summary>会员档位：0无 1月 2季 3年 4永久。以有效 VIP 为准读取。</summary>
    public int VipTier { get; set; }
    public int LotteryTickets { get; set; }
    public string? AvatarFrame { get; set; }
    /// <summary>他人是否可见购买记录。</summary>
    public bool ShowPurchases { get; set; }
    /// <summary>他人是否可见收藏。</summary>
    public bool ShowFavorites { get; set; }
    public string? Email { get; set; }
    /// <summary>被回帖通知（站内）。</summary>
    public bool NotifyReply { get; set; } = true;
    /// <summary>被 @ 通知。</summary>
    public bool NotifyMention { get; set; } = true;
    /// <summary>帖子楼层签名档，最多 200 字。</summary>
    public string? Signature { get; set; }
    /// <summary>主题：light | dark | system</summary>
    public string ThemePreference { get; set; } = "light";

    public ICollection<ForumThread> Threads { get; set; } = new List<ForumThread>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();

    public bool IsEffectivelyVip()
    {
        if (!IsVip) return false;
        if (VipUntil.HasValue && VipUntil.Value <= ChinaTime.Now) return false;
        return true;
    }

    /// <summary>True if muted and not expired.</summary>
    public bool IsEffectivelyMuted()
    {
        if (!IsMuted) return false;
        if (MutedUntil.HasValue && MutedUntil.Value <= ChinaTime.Now) return false;
        return true;
    }
}
