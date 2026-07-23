using System.Text.Json;
using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class RetentionService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly AppDbContext _db;

    public RetentionService(AppDbContext db) => _db = db;

    // ----- Drafts -----
    public async Task<List<DraftListItemDto>> ListDraftsAsync(int userId)
    {
        return await _db.ThreadDrafts.Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DraftListItemDto(d.Id, d.ForumId, d.Title, d.Type, d.UpdatedAt))
            .ToListAsync();
    }

    public async Task<DraftDto?> GetDraftAsync(int userId, int draftId)
    {
        var d = await _db.ThreadDrafts.FirstOrDefaultAsync(x => x.Id == draftId && x.UserId == userId);
        return d == null ? null : ToDraftDto(d);
    }

    public async Task<DraftDto?> GetDraftByForumAsync(int userId, int forumId)
    {
        var d = await _db.ThreadDrafts
            .Where(x => x.UserId == userId && x.ForumId == forumId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync();
        return d == null ? null : ToDraftDto(d);
    }

    public async Task<DraftDto> SaveDraftAsync(int userId, SaveDraftRequest req)
    {
        ThreadDraft draft;
        if (req.Id is > 0)
        {
            draft = await _db.ThreadDrafts.FirstOrDefaultAsync(d => d.Id == req.Id && d.UserId == userId)
                    ?? throw new InvalidOperationException("草稿不存在");
        }
        else
        {
            draft = await _db.ThreadDrafts.FirstOrDefaultAsync(d => d.UserId == userId && d.ForumId == req.ForumId)
                    ?? new ThreadDraft { UserId = userId, ForumId = req.ForumId };
            if (draft.Id == 0) _db.ThreadDrafts.Add(draft);
        }

        draft.Title = (req.Title ?? "").Trim();
        if (draft.Title.Length > 100) draft.Title = draft.Title[..100];
        draft.Content = req.Content ?? "";
        draft.Type = string.IsNullOrWhiteSpace(req.Type) ? "public" : req.Type.Trim().ToLowerInvariant();
        draft.CoinPrice = Math.Max(0, req.CoinPrice);
        draft.TagsJson = req.Tags == null ? null : JsonSerializer.Serialize(req.Tags.Take(3), JsonOpts);
        draft.PollOptionsJson = req.PollOptions == null ? null : JsonSerializer.Serialize(req.PollOptions.Take(6), JsonOpts);
        draft.ImagesJson = req.Images == null ? null : JsonSerializer.Serialize(req.Images.Take(8), JsonOpts);
        draft.UpdatedAt = ChinaTime.Now;
        await _db.SaveChangesAsync();
        return ToDraftDto(draft);
    }

    public async Task<(bool Ok, string? Error)> DeleteDraftAsync(int userId, int draftId)
    {
        var d = await _db.ThreadDrafts.FirstOrDefaultAsync(x => x.Id == draftId && x.UserId == userId);
        if (d == null) return (false, "草稿不存在");
        _db.ThreadDrafts.Remove(d);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task DeleteDraftByForumAsync(int userId, int forumId)
    {
        var list = await _db.ThreadDrafts.Where(d => d.UserId == userId && d.ForumId == forumId).ToListAsync();
        if (list.Count == 0) return;
        _db.ThreadDrafts.RemoveRange(list);
        await _db.SaveChangesAsync();
    }

    private static DraftDto ToDraftDto(ThreadDraft d) => new(
        d.Id, d.ForumId, d.Title, d.Content, d.Type, d.CoinPrice,
        DeserializeList(d.TagsJson), DeserializeList(d.PollOptionsJson), DeserializeList(d.ImagesJson),
        d.UpdatedAt);

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? []; }
        catch { return []; }
    }

    // ----- Browse history -----
    public async Task RecordHistoryAsync(int userId, int threadId)
    {
        var row = await _db.BrowseHistories.FirstOrDefaultAsync(h => h.UserId == userId && h.ThreadId == threadId);
        if (row == null)
        {
            _db.BrowseHistories.Add(new BrowseHistory { UserId = userId, ThreadId = threadId, ViewedAt = ChinaTime.Now });
        }
        else
        {
            row.ViewedAt = ChinaTime.Now;
        }

        // keep at most 100
        var old = await _db.BrowseHistories.Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ViewedAt).Skip(100).ToListAsync();
        if (old.Count > 0) _db.BrowseHistories.RemoveRange(old);
        await _db.SaveChangesAsync();
    }

    public async Task<List<HistoryItemDto>> ListHistoryAsync(int userId, int take = 30)
    {
        take = Math.Clamp(take, 1, 100);
        return await _db.BrowseHistories
            .Include(h => h.Thread).ThenInclude(t => t.Forum)
            .Include(h => h.Thread).ThenInclude(t => t.Author)
            .Where(h => h.UserId == userId && !h.Thread.IsHidden)
            .OrderByDescending(h => h.ViewedAt)
            .Take(take)
            .Select(h => new HistoryItemDto(
                h.ThreadId, h.Thread.Title, h.Thread.ForumId, h.Thread.Forum.Name,
                h.Thread.Author.Nickname, h.Thread.ReplyCount, h.ViewedAt))
            .ToListAsync();
    }

    public async Task ClearHistoryAsync(int userId)
    {
        var rows = await _db.BrowseHistories.Where(h => h.UserId == userId).ToListAsync();
        _db.BrowseHistories.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    // ----- Forum subscriptions -----
    public async Task<(SubscribeResultDto? Result, string? Error)> ToggleSubscribeAsync(int userId, int forumId)
    {
        var forum = await _db.Forums.FindAsync(forumId);
        if (forum == null) return (null, "版块不存在");

        var existing = await _db.ForumSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.ForumId == forumId);
        if (existing != null)
        {
            _db.ForumSubscriptions.Remove(existing);
            await _db.SaveChangesAsync();
            return (new SubscribeResultDto(false, "已取消订阅"), null);
        }

        _db.ForumSubscriptions.Add(new ForumSubscription
        {
            UserId = userId,
            ForumId = forumId,
            LastReadAt = ChinaTime.Now,
            CreatedAt = ChinaTime.Now
        });
        await _db.SaveChangesAsync();
        return (new SubscribeResultDto(true, "订阅成功"), null);
    }

    public async Task MarkForumReadAsync(int userId, int forumId)
    {
        var sub = await _db.ForumSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.ForumId == forumId);
        if (sub == null) return;
        sub.LastReadAt = ChinaTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task<List<SubscriptionItemDto>> ListSubscriptionsAsync(int userId)
    {
        var subs = await _db.ForumSubscriptions.Include(s => s.Forum)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var result = new List<SubscriptionItemDto>();
        foreach (var s in subs)
        {
            var unread = await _db.Threads.CountAsync(t =>
                t.ForumId == s.ForumId && !t.IsHidden && t.CreatedAt > s.LastReadAt && t.AuthorId != userId);
            result.Add(new SubscriptionItemDto(s.ForumId, s.Forum.Name, s.Forum.Icon, s.LastReadAt, unread, s.CreatedAt));
        }
        return result;
    }

    public async Task<bool> IsSubscribedAsync(int userId, int forumId) =>
        await _db.ForumSubscriptions.AnyAsync(s => s.UserId == userId && s.ForumId == forumId);

    public async Task NotifyForumSubscribersAsync(int forumId, int authorId, string authorNickname, int threadId, string title)
    {
        var subs = await _db.ForumSubscriptions
            .Where(s => s.ForumId == forumId && s.UserId != authorId)
            .Select(s => s.UserId)
            .ToListAsync();
        if (subs.Count == 0) return;

        foreach (var uid in subs.Take(200))
        {
            _db.Notifications.Add(new Notification
            {
                UserId = uid,
                Type = "forum",
                ThreadId = threadId,
                ThreadTitle = title,
                FromUserId = authorId,
                FromNickname = authorNickname,
                Content = "你订阅的版块有新帖",
                CreatedAt = ChinaTime.Now
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<UnreadThreadDto>> GetSubscriptionUnreadAsync(int userId, int take = 15)
    {
        take = Math.Clamp(take, 1, 50);
        var subs = await _db.ForumSubscriptions.Where(s => s.UserId == userId).ToListAsync();
        if (subs.Count == 0) return [];

        var forumIds = subs.Select(s => s.ForumId).ToList();
        var lastRead = subs.ToDictionary(s => s.ForumId, s => s.LastReadAt);

        var threads = await _db.Threads.Include(t => t.Forum).Include(t => t.Author)
            .Where(t => forumIds.Contains(t.ForumId) && !t.IsHidden && t.AuthorId != userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(80)
            .ToListAsync();

        return threads
            .Where(t => t.CreatedAt > lastRead.GetValueOrDefault(t.ForumId))
            .Take(take)
            .Select(t => new UnreadThreadDto(t.Id, t.Title, t.ForumId, t.Forum.Name, t.Author.Nickname, t.CreatedAt))
            .ToList();
    }
}
