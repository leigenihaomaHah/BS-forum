namespace ForumApi.Models;

public class FavoriteFolder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public List<ThreadFavorite> Favorites { get; set; } = new();
}
