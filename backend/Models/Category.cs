namespace ForumApi.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsCollapsedDefault { get; set; }

    public ICollection<Forum> Forums { get; set; } = new List<Forum>();
}
