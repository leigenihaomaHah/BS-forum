using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class LevelService
{
    private static readonly Dictionary<int, List<string>> Benefits = new()
    {
        [1] = ["浏览帖子", "每日签到", "发表回复"],
        [2] = ["浏览帖子", "每日签到", "发表新帖", "发表回复"],
        [3] = ["+ 以上所有", "金色标识起始"],
        [4] = ["+ 以上所有", "更高热度权重"],
        [5] = ["+ 以上所有", "金牌标识"],
        [6] = ["+ 以上所有", "管理后台入口（管理员）"],
    };

    private readonly AppDbContext _db;
    private List<LevelRule>? _cache;

    public LevelService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<LevelRule>> GetRulesAsync()
    {
        _cache ??= await _db.LevelRules.OrderBy(r => r.Level).ToListAsync();
        return _cache;
    }

    public async Task<List<LevelRuleDto>> GetRuleDtosAsync()
    {
        var rules = await GetRulesAsync();
        return rules.Select(r => new LevelRuleDto(
            r.Level, r.Name, r.MinPoints,
            Benefits.GetValueOrDefault(r.Level) ?? ["浏览帖子"])).ToList();
    }

    public async Task<string> GetLevelNameAsync(int level)
    {
        var rules = await GetRulesAsync();
        return rules.FirstOrDefault(r => r.Level == level)?.Name ?? $"Lv.{level}";
    }

    public async Task RecalculateLevelAsync(User user)
    {
        var rules = await GetRulesAsync();
        var matched = rules.Where(r => user.Points >= r.MinPoints).OrderByDescending(r => r.Level).FirstOrDefault();
        if (matched != null)
            user.Level = matched.Level;
    }

    public async Task<int> GetNextLevelPointsAsync(int currentLevel, int points)
    {
        var rules = await GetRulesAsync();
        var next = rules.Where(r => r.Level > currentLevel).OrderBy(r => r.Level).FirstOrDefault();
        return next?.MinPoints ?? points;
    }
}
