using ForumApi.Data;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

/// <summary>读取站点设置；短时内存缓存，避免每次查库。</summary>
public class SiteSettingsService
{
    private readonly AppDbContext _db;
    private static Dictionary<string, string>? _cache;
    private static DateTime _cacheAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public SiteSettingsService(AppDbContext db) => _db = db;

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        if (_cache != null && ChinaTime.Now - _cacheAt < CacheTtl)
            return new Dictionary<string, string>(_cache);

        var dict = await _db.SiteSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        _cache = dict;
        _cacheAt = ChinaTime.Now;
        return new Dictionary<string, string>(dict);
    }

    public static void Invalidate()
    {
        _cache = null;
        _cacheAt = DateTime.MinValue;
    }

    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        var all = await GetAllAsync();
        return all.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
    {
        var v = await GetAsync(key, defaultValue ? "1" : "0");
        return v is "1" or "true" or "True" or "yes";
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0)
    {
        var v = await GetAsync(key, defaultValue.ToString());
        return int.TryParse(v, out var n) ? n : defaultValue;
    }

    /// <summary>活动时间窗内的积分倍数；未开启或未到窗口则返回 1。</summary>
    public async Task<(bool Active, int Multiplier, string Name)> GetPointsEventAsync()
    {
        if (!await GetBoolAsync("points_event_enabled"))
            return (false, 1, "");
        var mult = Math.Clamp(await GetIntAsync("points_event_multiplier", 2), 1, 10);
        var name = await GetAsync("points_event_name", "限时双倍积分");
        var startRaw = await GetAsync("points_event_start");
        var endRaw = await GetAsync("points_event_end");
        var now = ChinaTime.Now;
        if (!string.IsNullOrWhiteSpace(startRaw) &&
            DateTime.TryParse(startRaw, out var start) && now < start)
            return (false, 1, name);
        if (!string.IsNullOrWhiteSpace(endRaw) &&
            DateTime.TryParse(endRaw, out var end) && now > end)
            return (false, 1, name);
        return (mult > 1, mult, name);
    }

    public async Task<int> ApplyPointsEventAsync(int points)
    {
        if (points <= 0) return points;
        var (active, mult, _) = await GetPointsEventAsync();
        return active && mult > 1 ? points * mult : points;
    }

    public async Task EnsureDefaultsAsync()
    {
        var defaults = new Dictionary<string, string>
        {
            ["site_name"] = "BS Forum",
            ["site_description"] = "BS 综合社区",
            ["allow_register"] = "1",
            ["require_review"] = "0",
            ["max_replies_per_day"] = "50",
            ["max_file_size_mb"] = "10",
            ["default_points"] = "0",
            ["default_coins"] = "10",
            ["points_per_thread"] = "10",
            ["points_per_reply"] = "2",
            ["points_per_sign_in"] = "5",
            ["coins_per_sign_in"] = "2",
            ["coins_per_thread"] = "0",
            ["coins_per_reply"] = "1",
            ["lottery_cost_coins"] = "5",
            ["lottery_daily_limit"] = "10",
            ["lottery_pity"] = "10",
            ["review_exempt_min_level"] = "4",
            ["sensitive_hit_action"] = "mask_review",
            ["points_event_enabled"] = "0",
            ["points_event_multiplier"] = "2",
            ["points_event_start"] = "",
            ["points_event_end"] = "",
            ["points_event_name"] = "限时双倍积分",
            ["paid_pin_enabled"] = "1",
            ["paid_pin_cost_coins"] = "20",
            ["paid_pin_hours"] = "24",
        };
        var existing = await _db.SiteSettings.Select(s => s.Key).ToListAsync();
        var missing = defaults.Where(kv => !existing.Contains(kv.Key)).ToList();
        if (missing.Count == 0) return;
        foreach (var (k, v) in missing)
            _db.SiteSettings.Add(new SiteSetting { Key = k, Value = v });
        await _db.SaveChangesAsync();
        Invalidate();
    }
}
