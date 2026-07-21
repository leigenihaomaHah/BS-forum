namespace ForumApi.Models;

public class SignInRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime SignInDate { get; set; }
    public int PointsAwarded { get; set; }
    public int CoinsAwarded { get; set; }

    public User User { get; set; } = null!;
}
