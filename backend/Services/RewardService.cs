using ForumApi.Data;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class RewardService
{
    private readonly AppDbContext _db;
    private readonly LevelService _levels;
    private readonly SiteSettingsService _settings;

    public const string ReasonCreateThread = "create_thread";
    public const string ReasonReply = "reply";
    public const string ReasonSignIn = "sign_in";
    public const string ReasonLiked = "thread_liked";

    public RewardService(AppDbContext db, LevelService levels, SiteSettingsService settings)
    {
        _db = db;
        _levels = levels;
        _settings = settings;
    }

    public async Task<bool> TryAwardPointsAsync(User user, int delta, string reason, string? refType = null, int? refId = null, int dailyLimit = 0)
    {
        if (delta > 0)
            delta = await _settings.ApplyPointsEventAsync(delta);

        if (dailyLimit > 0)
        {
            var today = ChinaTime.Today;
            var count = await _db.PointLedgers.CountAsync(p =>
                p.UserId == user.Id &&
                p.Reason == reason &&
                p.CreatedAt >= today);
            if (count >= dailyLimit)
                return false;
        }

        if (delta == 0) return false;

        user.Points += delta;
        _db.PointLedgers.Add(new PointLedger
        {
            UserId = user.Id,
            Delta = delta,
            Reason = reason,
            RefType = refType,
            RefId = refId
        });
        await _levels.RecalculateLevelAsync(user);
        return true;
    }

    public async Task AwardCoinsAsync(User user, int delta, string reason, string? refType = null, int? refId = null)
    {
        user.Coins += delta;
        _db.CoinLedgers.Add(new CoinLedger
        {
            UserId = user.Id,
            Delta = delta,
            Reason = reason,
            RefType = refType,
            RefId = refId
        });
        await Task.CompletedTask;
    }
}
