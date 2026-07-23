namespace ForumApi.Models;

public class ForumThread
{
    public int Id { get; set; }
    public int ForumId { get; set; }
    public int AuthorId { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>public | private | coin</summary>
    public string Type { get; set; } = "public";
    public int CoinPrice { get; set; }
    public int Views { get; set; }
    public int ReplyCount { get; set; }
    public int LikeCount { get; set; }
    public bool IsHidden { get; set; }
    /// <summary>发帖审核：待审帖对普通用户不可见。</summary>
    public bool PendingReview { get; set; }
    public bool RepliesLocked { get; set; }
    public bool IsPinned { get; set; }
    public bool IsEssence { get; set; }
    /// <summary>Whether author already received essence reward (no re-award).</summary>
    public bool EssenceAwarded { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;
    public DateTime LastReplyAt { get; set; } = ChinaTime.Now;

    public Forum Forum { get; set; } = null!;
    public User Author { get; set; } = null!;
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<ThreadLike> Likes { get; set; } = new List<ThreadLike>();
    public ICollection<ThreadTag> ThreadTags { get; set; } = new List<ThreadTag>();
}
