namespace ForumApi.Models;

public class ThreadPurchase
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int UserId { get; set; }
    public int CoinPrice { get; set; }
    public DateTime PurchasedAt { get; set; } = ChinaTime.Now;

    public ForumThread Thread { get; set; } = null!;
    public User User { get; set; } = null!;
}
