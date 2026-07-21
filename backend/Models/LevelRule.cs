namespace ForumApi.Models;

public class LevelRule
{
    public int Id { get; set; }
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinPoints { get; set; }
}
