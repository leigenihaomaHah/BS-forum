namespace ForumApi.Models;

public class HomeBanner
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Image URL or data:image base64.</summary>
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
