namespace ForumApi.Models;

public class Post
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>JSON array of data-URL / URL strings.</summary>
    public string? ImagesJson { get; set; }
    public int Floor { get; set; }
    public int? ReplyToPostId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ForumThread Thread { get; set; } = null!;
    public User Author { get; set; } = null!;
    public Post? ReplyToPost { get; set; }
}
