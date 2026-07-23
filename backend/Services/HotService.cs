using ForumApi.Data;
using ForumApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class HotService
{
    private readonly AppDbContext _db;
    private readonly LevelService _levels;

    public HotService(AppDbContext db, LevelService levels)
    {
        _db = db;
        _levels = levels;
    }

    public async Task<List<HotThreadDto>> GetHotAsync(string period)
    {
        var now = ChinaTime.Now;
        var start = period.ToLowerInvariant() switch
        {
            "week" => StartOfWeek(now),
            "month" => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => ChinaTime.Today
        };

        var threads = await _db.Threads
            .Include(t => t.Author)
            .Include(t => t.Forum)
            .Where(t => t.CreatedAt >= start && !t.IsHidden && !t.PendingReview)
            .ToListAsync();

        var scored = threads.Select(t =>
        {
            var hours = Math.Max(0, (now - t.CreatedAt).TotalHours);
            var freshBonus = Math.Max(0, 48 - hours) * 0.5;
            var heat = t.ReplyCount * 3 + t.Views * 0.1 + t.LikeCount * 5 + freshBonus;
            return (t, heat);
        })
        .OrderByDescending(x => x.heat)
        .Take(20)
        .ToList();

        var result = new List<HotThreadDto>();
        foreach (var (t, heat) in scored)
        {
            var ln = await _levels.GetLevelNameAsync(t.Author.Level);
            result.Add(new HotThreadDto(
                t.Id, t.Title, t.ForumId, t.Forum.Name,
                t.Views, t.ReplyCount, t.LikeCount, Math.Round(heat, 1),
                t.CreatedAt, t.Author.Nickname, t.Author.Level, ln,
                t.Type, t.IsEssence));
        }

        return result;
    }

    public async Task<List<EssenceThreadDto>> GetEssenceAsync(int take = 10)
    {
        take = Math.Clamp(take, 1, 30);
        var threads = await _db.Threads
            .Include(t => t.Author)
            .Include(t => t.Forum)
            .Where(t => t.IsEssence && !t.IsHidden && !t.PendingReview)
            .OrderByDescending(t => t.LastReplyAt)
            .Take(take)
            .ToListAsync();

        var result = new List<EssenceThreadDto>();
        foreach (var t in threads)
        {
            var ln = await _levels.GetLevelNameAsync(t.Author.Level);
            result.Add(new EssenceThreadDto(
                t.Id, t.Title, t.ForumId, t.Forum.Name,
                t.Views, t.ReplyCount, t.CreatedAt,
                t.Author.Nickname, t.Author.Level, ln));
        }
        return result;
    }

    private static DateTime StartOfWeek(DateTime now)
    {
        var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        return now.Date.AddDays(-diff);
    }
}
