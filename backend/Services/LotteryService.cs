using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class LotteryService
{
    public const int CostCoins = 5;
    public const int DailyLimit = 10;
    public const int PityThreshold = 10;

    // 权重合计 100，便于看概率；含金币 + 积分
    private static readonly LotteryPrizeDto[] Prizes =
    [
        new("miss", "谢谢参与", 0, 0, 18, "#94a3b8"),
        new("p5", "+5 积分", 0, 5, 14, "#a5b4fc"),
        new("c3", "+3 金币", 3, 0, 12, "#7dd3fc"),
        new("p10", "+10 积分", 0, 10, 12, "#c4b5fd"),
        new("c5", "+5 金币", 5, 0, 10, "#6ee7b7"),
        new("p20", "+20 积分", 0, 20, 9, "#818cf8"),
        new("c8", "+8 金币", 8, 0, 8, "#fde047"),
        new("p30", "+30 积分", 0, 30, 7, "#6366f1"),
        new("c15", "+15 金币", 15, 0, 5, "#fdba74"),
        new("c50", "+50 金币", 50, 0, 3, "#fda4af"),
        new("p80", "+80 积分", 0, 80, 1, "#4f46e5"),
        new("jackpot", "大奖 +30金+50分", 30, 50, 1, "#f43f5e"),
    ];

    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    private readonly LevelService _levels;

    public LotteryService(AppDbContext db, NotificationService notifications, LevelService levels)
    {
        _db = db;
        _notifications = notifications;
        _levels = levels;
    }

    public LotteryConfigDto GetConfig() =>
        new(CostCoins, DailyLimit, PityThreshold, Prizes.ToList());

    public async Task<LotteryStatusDto?> GetStatusAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;
        CommunityService.ClearExpiredVip(user);

        var (spinsToday, freeUsed) = await GetTodayStatsAsync(userId);
        var dailyLimit = user.IsEffectivelyVip() ? DailyLimit + 5 : DailyLimit;
        return new LotteryStatusDto(
            user.Coins,
            user.Points,
            spinsToday,
            dailyLimit,
            !freeUsed,
            Math.Max(0, dailyLimit - spinsToday),
            CostCoins,
            user.IsEffectivelyMuted(),
            user.LotteryTickets,
            user.IsEffectivelyVip(),
            freeUsed && user.LotteryTickets > 0);
    }

    public async Task<(LotterySpinResultDto? Result, string? Error)> SpinAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");
        if (user.IsEffectivelyMuted()) return (null, "账号已被禁言，暂时无法抽奖");

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            user = await _db.Users.FirstAsync(u => u.Id == userId);
            CommunityService.ClearExpiredVip(user);

            var dailyLimit = user.IsEffectivelyVip() ? DailyLimit + 5 : DailyLimit;
            var spinsToday = await _db.LotterySpins
                .CountAsync(s => s.UserId == userId && s.CreatedAt >= todayStart && s.CreatedAt < todayEnd);
            if (spinsToday >= dailyLimit)
                return (null, $"今日抽奖次数已达上限（{dailyLimit} 次）");

            var freeUsed = await _db.LotterySpins
                .AnyAsync(s => s.UserId == userId && s.IsFree && s.CreatedAt >= todayStart && s.CreatedAt < todayEnd);
            var isFree = !freeUsed;
            var usedTicket = false;
            var cost = 0;
            if (!isFree)
            {
                if (user.LotteryTickets > 0)
                {
                    user.LotteryTickets -= 1;
                    usedTicket = true;
                    cost = 0;
                }
                else
                {
                    cost = CostCoins;
                    if (user.Coins < CostCoins)
                        return (null, $"金币不足，需要 {CostCoins} 金币（或购买转盘券）");
                    user.Coins -= cost;
                }
            }

            var prize = await PickPrizeAsync(userId);
            if (prize.Coins > 0) user.Coins += prize.Coins;
            if (prize.Points > 0) user.Points += prize.Points;

            var spin = new LotterySpin
            {
                UserId = userId,
                CostCoins = cost,
                PrizeCoins = prize.Coins,
                PrizePoints = prize.Points,
                PrizeLabel = prize.Label,
                IsFree = isFree,
                CreatedAt = DateTime.UtcNow,
            };
            _db.LotterySpins.Add(spin);
            await _db.SaveChangesAsync();

            if (cost > 0)
            {
                _db.CoinLedgers.Add(new CoinLedger
                {
                    UserId = userId,
                    Delta = -cost,
                    Reason = "lottery_cost",
                    RefType = "lottery",
                    RefId = spin.Id,
                });
            }

            if (prize.Coins > 0)
            {
                _db.CoinLedgers.Add(new CoinLedger
                {
                    UserId = userId,
                    Delta = prize.Coins,
                    Reason = "lottery_win",
                    RefType = "lottery",
                    RefId = spin.Id,
                });
            }

            if (prize.Points > 0)
            {
                _db.PointLedgers.Add(new PointLedger
                {
                    UserId = userId,
                    Delta = prize.Points,
                    Reason = "lottery_win",
                    RefType = "lottery",
                    RefId = spin.Id,
                });
                await _levels.RecalculateLevelAsync(user);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            if (prize.Coins >= 15 || prize.Points >= 30 || prize.Id == "jackpot")
            {
                await _notifications.AddSystemNotificationAsync(userId, $"幸运转盘中奖：{prize.Label}！");
                await _db.SaveChangesAsync();
            }

            spinsToday += 1;
            var result = new LotterySpinResultDto(
                spin.Id, prize.Id, prize.Label, prize.Coins, prize.Points, cost, isFree,
                user.Coins, user.Points, spinsToday, false, Math.Max(0, dailyLimit - spinsToday),
                user.LotteryTickets, usedTicket);
            return (result, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<LotteryRecentItemDto>> GetRecentAsync(int take = 20)
    {
        take = Math.Clamp(take, 1, 50);
        return await _db.LotterySpins
            .Include(s => s.User)
            .Where(s => s.PrizeCoins > 0 || s.PrizePoints > 0)
            .OrderByDescending(s => s.CreatedAt)
            .Take(take)
            .Select(s => new LotteryRecentItemDto(
                s.User.Nickname, s.PrizeLabel, s.PrizeCoins, s.PrizePoints, s.CreatedAt))
            .ToListAsync();
    }

    private async Task<(int SpinsToday, bool FreeUsed)> GetTodayStatsAsync(int userId)
    {
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);
        var todaySpins = await _db.LotterySpins
            .Where(s => s.UserId == userId && s.CreatedAt >= todayStart && s.CreatedAt < todayEnd)
            .Select(s => new { s.IsFree })
            .ToListAsync();
        return (todaySpins.Count, todaySpins.Any(s => s.IsFree));
    }

    private async Task<LotteryPrizeDto> PickPrizeAsync(int userId)
    {
        var recent = await _db.LotterySpins
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Id)
            .Take(PityThreshold)
            .Select(s => new { s.PrizeCoins, s.PrizePoints })
            .ToListAsync();

        if (recent.Count >= PityThreshold && recent.All(r => r.PrizeCoins == 0 && r.PrizePoints == 0))
        {
            var guaranteed = Prizes.Where(p => p.Coins >= 5 || p.Points >= 10).ToArray();
            return guaranteed[Random.Shared.Next(guaranteed.Length)];
        }

        var total = Prizes.Sum(p => p.Weight);
        var roll = Random.Shared.Next(total);
        var acc = 0;
        foreach (var p in Prizes)
        {
            acc += p.Weight;
            if (roll < acc) return p;
        }
        return Prizes[0];
    }
}
