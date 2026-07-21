using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public Task AddReplyNotificationAsync(
        int authorId, int fromUserId, string fromNickname, int threadId, string threadTitle, string content,
        int postId = 0, int floor = 0)
    {
        if (authorId == fromUserId) return Task.CompletedTask;
        var snippet = content.Length > 80 ? content[..80] + "…" : content;
        _db.Notifications.Add(new Notification
        {
            UserId = authorId,
            Type = "reply",
            ThreadId = threadId,
            ThreadTitle = threadTitle,
            FromUserId = fromUserId,
            FromNickname = fromNickname,
            Content = $"回复了你：{snippet}",
            PostId = postId,
            Floor = floor,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task AddMentionNotificationAsync(
        int userId, int fromUserId, string fromNickname, int threadId, string threadTitle,
        int postId = 0, int floor = 0)
    {
        if (userId == fromUserId) return Task.CompletedTask;
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = "mention",
            ThreadId = threadId,
            ThreadTitle = threadTitle,
            FromUserId = fromUserId,
            FromNickname = fromNickname,
            Content = "在回复中提到了你",
            PostId = postId,
            Floor = floor,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task AddTipNotificationAsync(int authorId, int fromUserId, string fromNickname, int threadId, string threadTitle, int amount)
    {
        if (authorId == fromUserId) return Task.CompletedTask;
        _db.Notifications.Add(new Notification
        {
            UserId = authorId,
            Type = "tip",
            ThreadId = threadId,
            ThreadTitle = threadTitle,
            FromUserId = fromUserId,
            FromNickname = fromNickname,
            Content = $"打赏了 {amount} 金币",
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task AddFollowNotificationAsync(int followeeId, int followerId, string followerNickname)
    {
        if (followeeId == followerId) return Task.CompletedTask;
        _db.Notifications.Add(new Notification
        {
            UserId = followeeId,
            Type = "follow",
            ThreadId = 0,
            ThreadTitle = "",
            FromUserId = followerId,
            FromNickname = followerNickname,
            Content = "关注了你",
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task AddSystemNotificationAsync(int userId, string content, int threadId = 0, string threadTitle = "")
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = "system",
            ThreadId = threadId,
            ThreadTitle = threadTitle,
            FromUserId = 0,
            FromNickname = "系统",
            Content = content,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public async Task<List<NotificationDto>> ListAsync(int userId, string? type = null, int take = 50)
    {
        take = Math.Clamp(take, 1, 100);
        var q = _db.Notifications.Where(n => n.UserId == userId);
        if (!string.IsNullOrWhiteSpace(type) && type != "all")
        {
            var t = type.Trim().ToLowerInvariant();
            if (t == "interact")
                q = q.Where(n => n.Type == "reply" || n.Type == "mention" || n.Type == "tip");
            else
                q = q.Where(n => n.Type == t);
        }

        return await q.OrderByDescending(n => n.CreatedAt).Take(take)
            .Select(n => new NotificationDto(
                n.Id, n.Type, n.ThreadId, n.ThreadTitle, n.FromUserId,
                n.FromNickname, n.Content, n.Read, n.CreatedAt, n.PostId, n.Floor))
            .ToListAsync();
    }

    public async Task<NotificationSummaryDto> GetSummaryAsync(int userId)
    {
        var rows = await _db.Notifications.Where(n => n.UserId == userId && !n.Read)
            .GroupBy(n => n.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        int C(string t) => rows.FirstOrDefault(r => r.Type == t)?.Count ?? 0;
        var reply = C("reply");
        var mention = C("mention");
        var tip = C("tip");
        var forum = C("forum");
        var follow = C("follow");
        var system = C("system");
        return new NotificationSummaryDto(
            reply + mention + tip + forum + follow + system,
            reply, mention, tip, forum, follow, system);
    }

    public async Task<(bool Ok, string? Error)> MarkReadAsync(int userId, int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n == null) return (false, "通知不存在");
        n.Read = true;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<int> MarkAllReadAsync(int userId, string? type = null)
    {
        var q = _db.Notifications.Where(n => n.UserId == userId && !n.Read);
        if (!string.IsNullOrWhiteSpace(type) && type != "all")
        {
            var t = type.Trim().ToLowerInvariant();
            if (t == "interact")
                q = q.Where(n => n.Type == "reply" || n.Type == "mention" || n.Type == "tip");
            else
                q = q.Where(n => n.Type == t);
        }
        var list = await q.ToListAsync();
        foreach (var n in list) n.Read = true;
        await _db.SaveChangesAsync();
        return list.Count;
    }
}
