namespace ForumApi.Models;

public class PointLedger
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? RefType { get; set; }
    public int? RefId { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;

    public User User { get; set; } = null!;
}
