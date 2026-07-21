namespace ForumApi.Models;

public class ThreadFavorite
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int UserId { get; set; }
    public int? FolderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ForumThread Thread { get; set; } = null!;
    public User User { get; set; } = null!;
    public FavoriteFolder? Folder { get; set; }
}
