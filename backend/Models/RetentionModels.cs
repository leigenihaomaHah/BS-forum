namespace ForumApi.Models;

public class ThreadDraft
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ForumId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "public";
    public int CoinPrice { get; set; }
    public string? TagsJson { get; set; }
    public string? PollOptionsJson { get; set; }
    public string? ImagesJson { get; set; }
    public DateTime UpdatedAt { get; set; } = ChinaTime.Now;
    public User User { get; set; } = null!;
}

public class BrowseHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ThreadId { get; set; }
    public DateTime ViewedAt { get; set; } = ChinaTime.Now;
    public User User { get; set; } = null!;
    public ForumThread Thread { get; set; } = null!;
}

public class ForumSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ForumId { get; set; }
    public DateTime LastReadAt { get; set; } = ChinaTime.Now;
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;
    public User User { get; set; } = null!;
    public Forum Forum { get; set; } = null!;
}
