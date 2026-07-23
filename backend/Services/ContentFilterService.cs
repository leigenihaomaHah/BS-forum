using System.Text.RegularExpressions;
using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class ContentFilterService
{
    private static readonly Regex UrlRegex = new(
        @"https?://|www\.|qq\.com|t\.me/|discord\.gg",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex QqGroupRegex = new(
        @"(?:QQ|qq|扣扣)\s*[=:：]?\s*\d{5,12}|\d{5,12}\s*(?:QQ群|群号)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly SiteSettingsService _settings;
    private List<SensitiveWord>? _cache;
    private DateTime _cacheAt = DateTime.MinValue;

    public ContentFilterService(AppDbContext db, SiteSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public void InvalidateCache() => _cache = null;

    private async Task<List<SensitiveWord>> LoadWordsAsync()
    {
        if (_cache != null && (ChinaTime.Now - _cacheAt).TotalSeconds < 60)
            return _cache;
        _cache = await _db.SensitiveWords.AsNoTracking().Where(w => w.Enabled).ToListAsync();
        _cacheAt = ChinaTime.Now;
        return _cache;
    }

    public async Task<(string Title, string Content, ContentFilterResult Result)> FilterThreadAsync(
        User user, string title, string content)
    {
        var (t, hitTitle) = await MaskAsync(title);
        var (c, hitBody) = await MaskAsync(content);
        var hits = hitTitle || hitBody;
        var ad = LooksLikeAd(title) || LooksLikeAd(content);
        var dup = await IsDuplicateAsync(user.Id, content);
        var newUserLink = IsNewUser(user) && ContainsLink(content + " " + title);

        var forceReview = hits || ad || newUserLink;
        string? blockError = null;
        if (dup)
            blockError = "检测到重复内容，请勿刷帖";
        var action = (await _settings.GetAsync("sensitive_hit_action")) ?? "mask_review";
        if (hits && action == "block")
            blockError ??= "内容包含敏感词，请修改后重试";

        return (t, c, new ContentFilterResult(hits, ad, forceReview, blockError));
    }

    public async Task<(string Content, ContentFilterResult Result)> FilterReplyAsync(User user, string content)
    {
        var (c, hits) = await MaskAsync(content);
        var ad = LooksLikeAd(content);
        var dup = await IsDuplicateReplyAsync(user.Id, content);
        var newUserLink = IsNewUser(user) && ContainsLink(content);

        string? blockError = null;
        if (dup) blockError = "检测到重复回复，请勿刷屏";
        var action = (await _settings.GetAsync("sensitive_hit_action")) ?? "mask_review";
        if (hits && action == "block")
            blockError ??= "内容包含敏感词，请修改后重试";
        if (newUserLink && action != "mask")
            blockError ??= "新用户暂时不能发布外链，请先提升等级";

        return (c, new ContentFilterResult(hits, ad || newUserLink, false, blockError));
    }

    public async Task<(string Text, bool Hit)> MaskAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return (text, false);
        var words = await LoadWordsAsync();
        if (words.Count == 0) return (text, false);
        var hit = false;
        var result = text;
        foreach (var w in words.OrderByDescending(x => x.Word.Length))
        {
            if (string.IsNullOrWhiteSpace(w.Word)) continue;
            if (result.Contains(w.Word, StringComparison.OrdinalIgnoreCase))
            {
                hit = true;
                result = Regex.Replace(result, Regex.Escape(w.Word), "***", RegexOptions.IgnoreCase);
            }
        }
        return (result, hit);
    }

    public static bool LooksLikeAd(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return QqGroupRegex.IsMatch(text) ||
               (UrlRegex.IsMatch(text) && (text.Contains("加群", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("代理", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("刷单", StringComparison.OrdinalIgnoreCase)));
    }

    public static bool ContainsLink(string text) => !string.IsNullOrEmpty(text) && UrlRegex.IsMatch(text);

    public static bool IsNewUser(User user)
    {
        if (user.IsAdmin) return false;
        if (user.Level < 2) return true;
        return user.CreatedAt > ChinaTime.Now.AddDays(-3);
    }

    private async Task<bool> IsDuplicateAsync(int userId, string content)
    {
        var norm = Normalize(content);
        if (norm.Length < 8) return false;
        var since = ChinaTime.Now.AddHours(-24);
        var recent = await _db.Posts
            .Where(p => p.AuthorId == userId && p.Floor == 1 && p.CreatedAt >= since && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => p.Content)
            .ToListAsync();
        return recent.Any(c => Normalize(c) == norm);
    }

    private async Task<bool> IsDuplicateReplyAsync(int userId, string content)
    {
        var norm = Normalize(content);
        if (norm.Length < 6) return false;
        var since = ChinaTime.Now.AddHours(-6);
        var recent = await _db.Posts
            .Where(p => p.AuthorId == userId && p.Floor > 1 && p.CreatedAt >= since && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Take(8)
            .Select(p => p.Content)
            .ToListAsync();
        return recent.Any(c => Normalize(c) == norm);
    }

    private static string Normalize(string s) =>
        Regex.Replace((s ?? "").Trim().ToLowerInvariant(), @"\s+", "");

    // ----- Admin CRUD -----
    public async Task<PagedResult<SensitiveWordDto>> ListAsync(int page, int pageSize, string? q)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _db.SensitiveWords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            query = query.Where(w => w.Word.Contains(s));
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(w => w.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new SensitiveWordDto(w.Id, w.Word, w.Category, w.Enabled, w.CreatedAt))
            .ToListAsync();
        return new PagedResult<SensitiveWordDto>(items, total, page, pageSize);
    }

    public async Task<(SensitiveWordDto? Result, string? Error)> CreateAsync(SaveSensitiveWordRequest req)
    {
        var word = (req.Word ?? "").Trim();
        if (word.Length is < 1 or > 40) return (null, "敏感词长度需为 1-40");
        var cat = (req.Category ?? "sensitive").Trim().ToLowerInvariant();
        if (cat is not ("sensitive" or "ad")) cat = "sensitive";
        if (await _db.SensitiveWords.AnyAsync(w => w.Word == word))
            return (null, "该词已存在");
        var row = new SensitiveWord { Word = word, Category = cat, Enabled = req.Enabled };
        _db.SensitiveWords.Add(row);
        await _db.SaveChangesAsync();
        InvalidateCache();
        return (new SensitiveWordDto(row.Id, row.Word, row.Category, row.Enabled, row.CreatedAt), null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(int id, SaveSensitiveWordRequest req)
    {
        var row = await _db.SensitiveWords.FindAsync(id);
        if (row == null) return (false, "不存在");
        var word = (req.Word ?? "").Trim();
        if (word.Length is < 1 or > 40) return (false, "敏感词长度需为 1-40");
        if (await _db.SensitiveWords.AnyAsync(w => w.Word == word && w.Id != id))
            return (false, "该词已存在");
        row.Word = word;
        row.Category = (req.Category ?? row.Category).Trim().ToLowerInvariant();
        if (row.Category is not ("sensitive" or "ad")) row.Category = "sensitive";
        row.Enabled = req.Enabled;
        await _db.SaveChangesAsync();
        InvalidateCache();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id)
    {
        var row = await _db.SensitiveWords.FindAsync(id);
        if (row == null) return (false, "不存在");
        _db.SensitiveWords.Remove(row);
        await _db.SaveChangesAsync();
        InvalidateCache();
        return (true, null);
    }

    public async Task SeedDefaultsIfEmptyAsync()
    {
        if (await _db.SensitiveWords.AnyAsync()) return;
        var seeds = new[] { "加微信", "刷单", "代练", "赌场", "色情" };
        foreach (var s in seeds)
            _db.SensitiveWords.Add(new SensitiveWord { Word = s, Category = "sensitive" });
        await _db.SaveChangesAsync();
        InvalidateCache();
    }
}

public record ContentFilterResult(bool HitSensitive, bool HitAd, bool ForceReview, string? BlockError);
