namespace ForumApi.Models;

public class ThreadLike
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;

    public ForumThread Thread { get; set; } = null!;
    public User User { get; set; } = null!;
}
