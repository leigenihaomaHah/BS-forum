namespace ForumApi.Models;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = "reply";
    public int ThreadId { get; set; }
    public string ThreadTitle { get; set; } = string.Empty;
    public int FromUserId { get; set; }
    public string FromNickname { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool Read { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;
    public int PostId { get; set; }
    public int Floor { get; set; }

    public User User { get; set; } = null!;
}
