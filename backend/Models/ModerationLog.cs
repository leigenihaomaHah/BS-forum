namespace ForumApi.Models;

public class ModerationLog
{
    public int Id { get; set; }
    public int AdminId { get; set; }
    public string TargetType { get; set; } = string.Empty; // thread | user
    public int TargetId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Admin { get; set; } = null!;
}
