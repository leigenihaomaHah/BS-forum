namespace ForumApi.Models;

public class LotterySpin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CostCoins { get; set; }
    public int PrizeCoins { get; set; }
    public int PrizePoints { get; set; }
    public string PrizeLabel { get; set; } = string.Empty;
    public bool IsFree { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;

    public User User { get; set; } = null!;
}
