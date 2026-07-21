namespace ForumApi.Models;

public class UserFollow
{
    public int Id { get; set; }
    public int FollowerId { get; set; }
    public int FolloweeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User Follower { get; set; } = null!;
    public User Followee { get; set; } = null!;
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<ThreadTag> ThreadTags { get; set; } = new List<ThreadTag>();
}


public class UserBlock
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BlockedUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ThreadTag
{
    public int ThreadId { get; set; }
    public int TagId { get; set; }
    public ForumThread Thread { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public class ShopItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "coins"; // coins | points
    public int Price { get; set; }
    public string ItemType { get; set; } = string.Empty; // lottery_ticket | vip_30 | avatar_frame | rename_card
    public string? Meta { get; set; }
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UserInventory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public string? Meta { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

public class UserBadge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BadgeCode { get; set; } = string.Empty;
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}

public class UserTaskProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public DateTime ProgressDate { get; set; }
    public int Progress { get; set; }
    public bool Claimed { get; set; }
    public User User { get; set; } = null!;
}

public class Report
{
    public int Id { get; set; }
    public int ReporterId { get; set; }
    public string TargetType { get; set; } = "thread"; // thread | post | user
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending | resolved | rejected
    public int? HandledByAdminId { get; set; }
    public string? HandleNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
    public User Reporter { get; set; } = null!;
}

public class ForumModerator
{
    public int Id { get; set; }
    public int ForumId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Forum Forum { get; set; } = null!;
    public User User { get; set; } = null!;
}

public class PollOption
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public int SortOrder { get; set; }
    public ForumThread Thread { get; set; } = null!;
}

public class PollVote
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int OptionId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
