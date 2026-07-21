using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class MessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(PrivateMessageDto? Result, string? Error)> SendAsync(int senderId, int receiverId, string content)
    {
        content = (content ?? "").Trim();
        if (content.Length is < 1 or > 2000)
            return (null, "私信内容需 1–2000 字");
        if (senderId == receiverId)
            return (null, "不能给自己发私信");

        var receiver = await _db.Users.FindAsync(receiverId);
        if (receiver == null) return (null, "用户不存在");

        var blocked = await _db.UserBlocks.AnyAsync(b =>
            (b.UserId == receiverId && b.BlockedUserId == senderId) ||
            (b.UserId == senderId && b.BlockedUserId == receiverId));
        if (blocked) return (null, "无法发送：双方存在屏蔽关系");

        var msg = new PrivateMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            IsRead = false
        };
        _db.PrivateMessages.Add(msg);
        await _db.SaveChangesAsync();

        var sender = await _db.Users.FindAsync(senderId);
        return (ToDto(msg, sender?.Nickname ?? "", receiver.Nickname), null);
    }

    public async Task<List<ConversationDto>> ListConversationsAsync(int userId)
    {
        var msgs = await _db.PrivateMessages
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(500)
            .ToListAsync();

        var peerIds = msgs.Select(m => m.SenderId == userId ? m.ReceiverId : m.SenderId).Distinct().ToList();
        var users = await _db.Users.Where(u => peerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = new List<ConversationDto>();
        var seen = new HashSet<int>();
        foreach (var m in msgs)
        {
            var peerId = m.SenderId == userId ? m.ReceiverId : m.SenderId;
            if (!seen.Add(peerId)) continue;
            users.TryGetValue(peerId, out var peer);
            var unread = msgs.Count(x => x.SenderId == peerId && x.ReceiverId == userId && !x.IsRead);
            result.Add(new ConversationDto(
                peerId,
                peer?.Nickname ?? "用户",
                peer?.Avatar,
                m.Content.Length > 60 ? m.Content[..60] + "…" : m.Content,
                m.CreatedAt,
                unread));
        }
        return result;
    }

    public async Task<(List<PrivateMessageDto> Items, string? Error)> GetThreadAsync(int userId, int peerId)
    {
        if (await _db.Users.FindAsync(peerId) == null)
            return ([], "用户不存在");

        var items = await _db.PrivateMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m =>
                (m.SenderId == userId && m.ReceiverId == peerId) ||
                (m.SenderId == peerId && m.ReceiverId == userId))
            .OrderBy(m => m.CreatedAt)
            .Take(200)
            .ToListAsync();

        var unread = items.Where(m => m.ReceiverId == userId && !m.IsRead).ToList();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync();

        return (items.Select(m => ToDto(m, m.Sender.Nickname, m.Receiver.Nickname)).ToList(), null);
    }

    public async Task<int> UnreadCountAsync(int userId) =>
        await _db.PrivateMessages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);

    private static PrivateMessageDto ToDto(PrivateMessage m, string senderNick, string receiverNick) =>
        new(m.Id, m.SenderId, senderNick, m.ReceiverId, receiverNick, m.Content, m.IsRead, m.CreatedAt);
}
