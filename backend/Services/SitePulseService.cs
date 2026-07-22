using ForumApi.Data;
using ForumApi.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ForumApi.Services;

public class SitePulseService
{
    const string CacheKey = "site_pulse_v1";
    static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);

    readonly AppDbContext _db;
    readonly IMemoryCache _cache;

    public SitePulseService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<SitePulseDto> GetAsync() =>
        _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await ComputeAsync();
        })!;

    async Task<SitePulseDto> ComputeAsync()
    {
        var tz = ResolveChinaTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayLocal = nowLocal.Date;
        var tomorrowLocal = todayLocal.AddDays(1);
        var yesterdayLocal = todayLocal.AddDays(-1);

        var todayStart = TimeZoneInfo.ConvertTimeToUtc(todayLocal, tz);
        var tomorrowStart = TimeZoneInfo.ConvertTimeToUtc(tomorrowLocal, tz);
        var yesterdayStart = TimeZoneInfo.ConvertTimeToUtc(yesterdayLocal, tz);

        var visibleThreads = _db.Threads.Where(t => !t.IsHidden && !t.PendingReview);

        var todayThreads = await visibleThreads.CountAsync(t => t.CreatedAt >= todayStart && t.CreatedAt < tomorrowStart);
        var yesterdayThreads = await visibleThreads.CountAsync(t => t.CreatedAt >= yesterdayStart && t.CreatedAt < todayStart);

        var visibleReplies = _db.Posts.Where(p =>
            p.Floor > 1 &&
            !p.IsDeleted &&
            !p.Thread.IsHidden &&
            !p.Thread.PendingReview);

        var todayReplies = await visibleReplies.CountAsync(p => p.CreatedAt >= todayStart && p.CreatedAt < tomorrowStart);
        var yesterdayReplies = await visibleReplies.CountAsync(p => p.CreatedAt >= yesterdayStart && p.CreatedAt < todayStart);

        return new SitePulseDto(todayThreads, yesterdayThreads, todayReplies, yesterdayReplies, "Asia/Shanghai");
    }

    static TimeZoneInfo ResolveChinaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "China Standard Time" : "Asia/Shanghai");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Shanghai", TimeSpan.FromHours(8), "CST", "CST");
        }
    }
}
