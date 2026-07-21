using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class RechargeService
{
    private readonly AppDbContext _db;

    public RechargeService(AppDbContext db) => _db = db;

    public static string DurationLabel(int? vipDays) =>
        vipDays switch
        {
            null or <= 0 => "永久会员",
            30 => "一个月会员",
            90 => "一个季度会员",
            365 => "一年会员",
            _ => $"{vipDays} 天会员"
        };

    public async Task<List<RechargePackageDto>> GetPackagesAsync(bool enabledOnly = true)
    {
        var q = _db.RechargePackages.AsQueryable();
        if (enabledOnly) q = q.Where(p => p.Enabled);
        var list = await q.OrderBy(p => p.SortOrder).ToListAsync();
        return list.Select(ToPackageDto).ToList();
    }

    public async Task<List<RechargeOrderDto>> GetMyOrdersAsync(int userId, int take = 20)
    {
        take = Math.Clamp(take, 1, 50);
        var rows = await _db.RechargeOrders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync();
        return rows.Select(o => ToOrderDto(o)).ToList();
    }

    public async Task<(RechargeOrderDto? Result, string? Error)> CreateOrderAsync(int userId, CreateRechargeOrderRequest req)
    {
        var pkg = await _db.RechargePackages.FirstOrDefaultAsync(p => p.Id == req.PackageId && p.Enabled);
        if (pkg == null) return (null, "套餐不存在或已下架");

        // 同套餐未完成申请复用，避免刷单
        var pending = await _db.RechargeOrders.FirstOrDefaultAsync(o =>
            o.UserId == userId && o.PackageId == pkg.Id && o.Status == "pending" && o.Channel == "manual");
        if (pending != null)
        {
            pending.Remark = string.IsNullOrWhiteSpace(req.Remark) ? pending.Remark : req.Remark.Trim();
            await _db.SaveChangesAsync();
            return (ToOrderDto(pending), null);
        }

        var order = new RechargeOrder
        {
            UserId = userId,
            PackageId = pkg.Id,
            PackageName = pkg.Name,
            PriceYuan = pkg.PriceYuan,
            VipDays = pkg.VipDays,
            BonusCoins = pkg.BonusCoins,
            Status = "pending",
            Channel = "manual",
            Remark = string.IsNullOrWhiteSpace(req.Remark) ? null : req.Remark.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        _db.RechargeOrders.Add(order);
        await _db.SaveChangesAsync();
        return (ToOrderDto(order), null);
    }

    public async Task<(RechargeResultDto? Result, string? Error)> RedeemCardAsync(int userId, string? code)
    {
        var normalized = NormalizeCardCode(code);
        if (normalized.Length != 64) return (null, "卡密无效，请输入完整 64 位卡密");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var card = await _db.RechargeCards.Include(c => c.Package)
                .FirstOrDefaultAsync(c => c.Code == normalized);
            if (card == null) return (null, "卡密不存在");
            if (!card.Package.Enabled) return (null, "该套餐已下架");

            var usedBy = card.UsedByUserId is > 0 ? card.UsedByUserId : null;
            var isMarkedUsed = card.UsedAt != null || usedBy != null;

            if (isMarkedUsed)
            {
                // 本人已成功兑换过 → 幂等返回成功（避免重复点击误报「已使用」）
                var paidMine = await _db.RechargeOrders.AsNoTracking()
                    .FirstOrDefaultAsync(o =>
                        o.CardCode == card.Code && o.UserId == userId && o.Status == "paid");
                if (paidMine != null)
                {
                    var me = await _db.Users.FindAsync(userId);
                    await tx.CommitAsync();
                    return (new RechargeResultDto(
                        "该卡密已兑换成功，会员已到账",
                        ToOrderDto(paidMine, me),
                        me?.IsEffectivelyVip() ?? false,
                        me?.VipUntil,
                        me?.Coins ?? 0), null);
                }

                // 卡密被标成已用，但没有成功订单 → 卡住的半成品，允许重试
                var paidAny = await _db.RechargeOrders.AsNoTracking()
                    .AnyAsync(o => o.CardCode == card.Code && o.Status == "paid");
                if (!paidAny)
                {
                    card.UsedByUserId = null;
                    card.UsedAt = null;
                    var orphans = await _db.RechargeOrders
                        .Where(o => o.CardCode == card.Code && o.Status != "paid")
                        .ToListAsync();
                    foreach (var o in orphans) o.Status = "cancelled";
                    isMarkedUsed = false;
                }
                else if (usedBy != userId)
                {
                    string who = "其他用户";
                    if (usedBy.HasValue)
                    {
                        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == usedBy.Value);
                        if (u != null) who = $"{u.Nickname}({u.Username})";
                    }
                    var when = card.UsedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "";
                    await tx.RollbackAsync();
                    return (null, $"卡密已被使用（{who}{(string.IsNullOrEmpty(when) ? "" : " · " + when)}）。请使用未使用的新卡密。");
                }
            }

            if (isMarkedUsed)
            {
                await tx.RollbackAsync();
                return (null, "卡密已被使用");
            }

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                await tx.RollbackAsync();
                return (null, "用户不存在");
            }

            var order = new RechargeOrder
            {
                UserId = userId,
                PackageId = card.PackageId,
                PackageName = card.Package.Name,
                PriceYuan = card.Package.PriceYuan,
                VipDays = card.Package.VipDays,
                BonusCoins = card.Package.BonusCoins,
                Status = "pending",
                Channel = "card",
                CardCode = card.Code,
                CreatedAt = DateTime.UtcNow
            };
            _db.RechargeOrders.Add(order);

            AutoRedeemCardOntoOrder(card, order, user, adminId: null);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (new RechargeResultDto(
                "兑换成功，会员已到账",
                ToOrderDto(order),
                user.IsEffectivelyVip(),
                user.VipUntil,
                user.Coins), null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(RechargeResultDto? Result, string? Error)> ConfirmOrderAsync(int adminId, int orderId, string? note)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = await _db.RechargeOrders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return (null, "申请不存在");
            if (order.Status == "paid") return (null, "该申请已到账");
            if (order.Status == "cancelled") return (null, "申请已取消");

            var user = await _db.Users.FindAsync(order.UserId);
            if (user == null) return (null, "用户不存在");

            if (!string.IsNullOrWhiteSpace(note))
                order.Remark = string.IsNullOrWhiteSpace(order.Remark) ? note.Trim() : $"{order.Remark} | {note.Trim()}";

            // 1) 生成专属卡密  2) 立刻按卡密兑换逻辑开通会员（无需用户再手动兑换）
            var code = GenCardCode();
            var card = new RechargeCard
            {
                Code = code,
                PackageId = order.PackageId,
                CreatedAt = DateTime.UtcNow
            };
            _db.RechargeCards.Add(card);

            order.CardCode = code;
            order.Channel = "manual";
            AutoRedeemCardOntoOrder(card, order, user, adminId);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (new RechargeResultDto(
                "已生成卡密并自动兑换，会员已到账",
                ToOrderDto(order, user),
                user.IsEffectivelyVip(),
                user.VipUntil,
                user.Coins,
                code), null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>将卡密兑换到指定申请上：开通会员 + 标记卡密已用。</summary>
    private void AutoRedeemCardOntoOrder(RechargeCard card, RechargeOrder order, User user, int? adminId)
    {
        FulfillOrder(order, user, adminId);
        card.UsedByUserId = user.Id;
        card.UsedAt = DateTime.UtcNow;
    }

    public async Task<(bool Ok, string? Error)> CancelOrderAsync(int userId, int orderId, bool isAdmin)
    {
        var order = await _db.RechargeOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return (false, "订单不存在");
        if (!isAdmin && order.UserId != userId) return (false, "无权操作");
        if (order.Status != "pending") return (false, "仅待支付订单可取消");
        order.Status = "cancelled";
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<PagedResult<RechargeOrderDto>> AdminOrdersAsync(int page, int pageSize, string? status)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.RechargeOrders.Include(o => o.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(o => o.Status == status.Trim().ToLowerInvariant());
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var items = rows.Select(o => ToOrderDto(o, o.User)).ToList();
        return new PagedResult<RechargeOrderDto>(items, total, page, pageSize);
    }

    public async Task<(List<RechargeCardDto>? Result, string? Error)> GenerateCardsAsync(int packageId, int count)
    {
        count = Math.Clamp(count, 1, 100);
        var pkg = await _db.RechargePackages.FindAsync(packageId);
        if (pkg == null) return (null, "套餐不存在");

        var created = new List<RechargeCard>();
        for (var i = 0; i < count; i++)
        {
            var card = new RechargeCard
            {
                Code = GenCardCode(),
                PackageId = packageId,
                CreatedAt = DateTime.UtcNow
            };
            _db.RechargeCards.Add(card);
            created.Add(card);
        }
        await _db.SaveChangesAsync();
        return (created.Select(c => new RechargeCardDto(
            c.Id, c.Code, pkg.Id, pkg.Name, false, null, null, null, c.CreatedAt)).ToList(), null);
    }

    public async Task<PagedResult<RechargeCardDto>> AdminCardsAsync(int page, int pageSize, int? packageId, bool? used)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.RechargeCards.Include(c => c.Package).AsQueryable();
        if (packageId.HasValue) q = q.Where(c => c.PackageId == packageId.Value);
        if (used == true) q = q.Where(c => c.UsedAt != null || (c.UsedByUserId != null && c.UsedByUserId > 0));
        if (used == false) q = q.Where(c => c.UsedAt == null && (c.UsedByUserId == null || c.UsedByUserId == 0));
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var userIds = rows.Where(c => c.UsedByUserId.HasValue).Select(c => c.UsedByUserId!.Value).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nickname);

        var items = rows.Select(c => new RechargeCardDto(
            c.Id, c.Code, c.PackageId, c.Package.Name,
            c.UsedAt != null || c.UsedByUserId is > 0,
            c.UsedByUserId is > 0 ? c.UsedByUserId : null,
            c.UsedByUserId is > 0 && users.TryGetValue(c.UsedByUserId.Value, out var n) ? n : null,
            c.UsedAt, c.CreatedAt)).ToList();
        return new PagedResult<RechargeCardDto>(items, total, page, pageSize);
    }

    /// <summary>授予 VIP：永久覆盖；时长在现有有效期上叠加；已永久则保持永久。</summary>
    public static void GrantVip(User user, int? vipDays)
    {
        var wasPermanent = user.IsEffectivelyVip() && !user.VipUntil.HasValue;
        user.IsVip = true;
        var permanent = vipDays == null || vipDays <= 0;
        if (permanent)
        {
            user.VipUntil = null;
            return;
        }
        if (wasPermanent)
            return;

        var baseTime = user.VipUntil.HasValue && user.VipUntil.Value > DateTime.UtcNow
            ? user.VipUntil.Value
            : DateTime.UtcNow;
        user.VipUntil = baseTime.AddDays(vipDays!.Value);
    }

    private void FulfillOrder(RechargeOrder order, User user, int? adminId)
    {
        if (order.Status == "paid") return;

        GrantVip(user, order.VipDays);
        VipAccess.ApplyVipTier(user, VipAccess.TierFromVipDays(order.VipDays));

        if (order.BonusCoins > 0)
        {
            user.Coins += order.BonusCoins;
            _db.CoinLedgers.Add(new CoinLedger
            {
                UserId = user.Id,
                Delta = order.BonusCoins,
                Reason = "recharge",
                RefType = "recharge_order",
                RefId = order.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        order.Status = "paid";
        order.PaidAt = DateTime.UtcNow;
        order.ConfirmedByAdminId = adminId;
    }

    private static RechargePackageDto ToPackageDto(RechargePackage p) => new(
        p.Id, p.Code, p.Name, p.Description, p.PriceYuan, p.VipDays,
        p.VipDays == null || p.VipDays <= 0, p.BonusCoins, DurationLabel(p.VipDays));

    private static RechargeOrderDto ToOrderDto(RechargeOrder o, User? user = null) => new(
        o.Id, o.PackageId, o.PackageName, o.PriceYuan, o.VipDays,
        o.VipDays == null || o.VipDays <= 0, o.BonusCoins, o.Status, o.Channel, o.Remark,
        o.CreatedAt, o.PaidAt, DurationLabel(o.VipDays),
        user?.Id ?? o.UserId, user?.Username, user?.Nickname, o.CardCode);

    private static string NormalizeCardCode(string? code) =>
        (code ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

    private static string GenCardCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;
        var buf = new char[64];
        for (var i = 0; i < buf.Length; i++) buf[i] = chars[rng.Next(chars.Length)];
        return new string(buf);
    }
}
