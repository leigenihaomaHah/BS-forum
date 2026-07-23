using ForumApi.Data;
using ForumApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class LeaderboardService
{
    private readonly AppDbContext _db;
    private readonly LevelService _levels;

    public LeaderboardService(AppDbContext db, LevelService levels)
    {
        _db = db;
        _levels = levels;
    }

    public async Task<LeaderboardResultDto> GetAsync(string metric, string period, int take = 50)
    {
        take = Math.Clamp(take, 1, 100);
        metric = (metric ?? "points").Trim().ToLowerInvariant();
        period = (period ?? "all").Trim().ToLowerInvariant();

        var since = period switch
        {
            "day" => ChinaTime.Today,
            "week" => ChinaTime.Today.AddDays(-(int)ChinaTime.Today.DayOfWeek),
            "month" => new DateTime(ChinaTime.Today.Year, ChinaTime.Today.Month, 1),
            _ => (DateTime?)null
        };

        return metric switch
        {
            "coins" => await ByCoinsAsync(take),
            "essence" => await ByEssenceAsync(take),
            "likes" => await ByLikesAsync(take, since),
            "signin" => await BySignInAsync(take),
            _ => await ByPointsAsync(take),
        };
    }

    private async Task<LeaderboardResultDto> ByPointsAsync(int take)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderByDescending(u => u.Points).ThenBy(u => u.Id)
            .Take(take).ToListAsync();
        return await MapAsync("points", "all", users, u => u.Points);
    }

    private async Task<LeaderboardResultDto> ByCoinsAsync(int take)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderByDescending(u => u.Coins).ThenBy(u => u.Id)
            .Take(take).ToListAsync();
        return await MapAsync("coins", "all", users, u => u.Coins);
    }

    private async Task<LeaderboardResultDto> BySignInAsync(int take)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderByDescending(u => u.ConsecutiveSignInDays).ThenByDescending(u => u.Points)
            .Take(take).ToListAsync();
        return await MapAsync("signin", "all", users, u => u.ConsecutiveSignInDays);
    }

    private async Task<LeaderboardResultDto> ByEssenceAsync(int take)
    {
        var rows = await _db.Threads.AsNoTracking()
            .Where(t => t.IsEssence && !t.IsHidden)
            .GroupBy(t => t.AuthorId)
            .Select(g => new { AuthorId = g.Key, Score = g.Count() })
            .OrderByDescending(x => x.Score)
            .Take(take)
            .ToListAsync();
        return await MapScoreRowsAsync("essence", "all", rows.Select(r => (r.AuthorId, r.Score)).ToList());
    }

    private async Task<LeaderboardResultDto> ByLikesAsync(int take, DateTime? since)
    {
        var q = _db.Threads.AsNoTracking().Where(t => !t.IsHidden);
        if (since.HasValue) q = q.Where(t => t.CreatedAt >= since.Value);
        var rows = await q
            .GroupBy(t => t.AuthorId)
            .Select(g => new { AuthorId = g.Key, Score = g.Sum(t => t.LikeCount) })
            .OrderByDescending(x => x.Score)
            .Take(take)
            .ToListAsync();
        var period = since == null ? "all" : since.Value == ChinaTime.Today ? "day" : "period";
        return await MapScoreRowsAsync("likes", period, rows.Select(r => (r.AuthorId, r.Score)).ToList());
    }

    private async Task<LeaderboardResultDto> MapAsync(string metric, string period, List<Models.User> users, Func<Models.User, int> score)
    {
        var items = new List<LeaderboardItemDto>();
        var rank = 1;
        foreach (var u in users)
        {
            var ln = await _levels.GetLevelNameAsync(u.Level);
            items.Add(new LeaderboardItemDto(
                rank++, u.Id, u.Nickname, u.Avatar, u.Level, ln, u.IsEffectivelyVip(), score(u)));
        }
        return new LeaderboardResultDto(metric, period, items);
    }

    private async Task<LeaderboardResultDto> MapScoreRowsAsync(string metric, string period, List<(int AuthorId, int Score)> rows)
    {
        var ids = rows.Select(r => r.AuthorId).ToList();
        var users = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var items = new List<LeaderboardItemDto>();
        var rank = 1;
        foreach (var (authorId, score) in rows)
        {
            if (!users.TryGetValue(authorId, out var u)) continue;
            var ln = await _levels.GetLevelNameAsync(u.Level);
            items.Add(new LeaderboardItemDto(
                rank++, u.Id, u.Nickname, u.Avatar, u.Level, ln, u.IsEffectivelyVip(), score));
        }
        return new LeaderboardResultDto(metric, period, items);
    }
}
