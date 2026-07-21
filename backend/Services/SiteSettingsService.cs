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
        if (_cache != null && DateTime.UtcNow - _cacheAt < CacheTtl)
            return new Dictionary<string, string>(_cache);

        var dict = await _db.SiteSettings.AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value);
        _cache = dict;
        _cacheAt = DateTime.UtcNow;
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
