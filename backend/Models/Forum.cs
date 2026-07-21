namespace ForumApi.Models;

public class Forum
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool FullWidth { get; set; }
    /// <summary>进入版块所需最低会员档：0公开 1月会员 2季会员 3年会员 4永久会员。</summary>
    public int MinVipTier { get; set; }
    public int ThreadCount { get; set; }
    public int PostCount { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<ForumThread> Threads { get; set; } = new List<ForumThread>();
}
