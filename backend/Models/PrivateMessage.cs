namespace ForumApi.Models;

public class PrivateMessage
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;

    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;
}
