namespace ForumApi.Models;

public class SensitiveWord
{
    public int Id { get; set; }
    public string Word { get; set; } = string.Empty;
    /// <summary>sensitive | ad</summary>
    public string Category { get; set; } = "sensitive";
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;
}
