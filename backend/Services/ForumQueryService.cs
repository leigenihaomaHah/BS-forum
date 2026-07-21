using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class ForumQueryService
{
    private readonly AppDbContext _db;
    private readonly LevelService _levels;

    public ForumQueryService(AppDbContext db, LevelService levels)
    {
        _db = db;
        _levels = levels;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(int? viewerUserId = null)
    {
        User? viewer = null;
        if (viewerUserId.HasValue)
            viewer = await _db.Users.FindAsync(viewerUserId.Value);

        var today = DateTime.UtcNow.Date;
        var categories = await _db.Categories
            .Include(c => c.Forums)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var forumIds = categories.SelectMany(c => c.Forums).Select(f => f.Id).ToList();

        var todayCounts = await _db.Threads
            .Where(t => forumIds.Contains(t.ForumId) && t.CreatedAt >= today && !t.IsHidden)
            .GroupBy(t => t.ForumId)
            .Select(g => new { ForumId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ForumId, x => x.Count);

        var latestIds = await _db.Threads
            .Where(t => forumIds.Contains(t.ForumId) && !t.IsHidden)
            .GroupBy(t => t.ForumId)
            .Select(g => g.OrderByDescending(t => t.LastReplyAt).Select(t => t.Id).FirstOrDefault())
            .ToListAsync();

        var latestThreads = await _db.Threads
            .Include(t => t.Author)
            .Where(t => latestIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.ForumId);

        var result = new List<CategoryDto>();
        foreach (var cat in categories)
        {
            var forums = new List<ForumSummaryDto>();
            foreach (var f in cat.Forums.OrderBy(x => x.SortOrder))
            {
                var locked = !VipAccess.CanAccessForum(viewer, f.MinVipTier);
                LatestThreadDto? latest = null;
                if (!locked && latestThreads.TryGetValue(f.Id, out var lt))
                {
                    var ln = await _levels.GetLevelNameAsync(lt.Author.Level);
                    latest = new LatestThreadDto(lt.Id, lt.Title, lt.LastReplyAt, lt.Author.Nickname, lt.Author.Level, ln);
                }

                todayCounts.TryGetValue(f.Id, out var todayCount);
                forums.Add(ToSummary(f, todayCount, latest, locked));
            }

            result.Add(new CategoryDto(cat.Id, cat.Name, cat.Icon, cat.SortOrder, forums));
        }

        return result;
    }

    public async Task<(ForumSummaryDto? Forum, string? Error)> GetForumAsync(int forumId, int? viewerUserId = null)
    {
        var f = await _db.Forums.FindAsync(forumId);
        if (f == null) return (null, null);

        User? viewer = null;
        if (viewerUserId.HasValue)
            viewer = await _db.Users.FindAsync(viewerUserId.Value);

        if (!VipAccess.CanAccessForum(viewer, f.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(f.MinVipTier));

        var today = DateTime.UtcNow.Date;
        var todayCount = await _db.Threads.CountAsync(t => t.ForumId == forumId && t.CreatedAt >= today && !t.IsHidden);
        var lt = await _db.Threads.Include(t => t.Author)
            .Where(t => t.ForumId == forumId && !t.IsHidden)
            .OrderByDescending(t => t.LastReplyAt)
            .FirstOrDefaultAsync();
        LatestThreadDto? latest = null;
        if (lt != null)
        {
            var ln = await _levels.GetLevelNameAsync(lt.Author.Level);
            latest = new LatestThreadDto(lt.Id, lt.Title, lt.LastReplyAt, lt.Author.Nickname, lt.Author.Level, ln);
        }
        return (ToSummary(f, todayCount, latest, locked: false), null);
    }

    public async Task<(bool Ok, string? Error)> EnsureForumAccessAsync(int forumId, int? viewerUserId)
    {
        var f = await _db.Forums.FindAsync(forumId);
        if (f == null) return (false, "版块不存在");
        User? viewer = null;
        if (viewerUserId.HasValue)
            viewer = await _db.Users.FindAsync(viewerUserId.Value);
        if (!VipAccess.CanAccessForum(viewer, f.MinVipTier))
            return (false, VipAccess.AccessDeniedMessage(f.MinVipTier));
        return (true, null);
    }

    private static ForumSummaryDto ToSummary(Forum f, int todayCount, LatestThreadDto? latest, bool locked)
    {
        var label = f.MinVipTier <= 0 ? "所有人" : VipAccess.TierLabel(f.MinVipTier) + "可见";
        return new ForumSummaryDto(
            f.Id, f.Name, f.Description, f.Icon, f.FullWidth,
            f.ThreadCount, f.PostCount, todayCount, latest,
            f.MinVipTier, label, locked);
    }
}
