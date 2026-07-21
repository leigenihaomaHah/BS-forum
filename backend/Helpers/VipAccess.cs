using ForumApi.Models;

namespace ForumApi.Helpers;

/// <summary>
/// 会员等级：0 普通，1 月卡，2 季卡，3 年卡，4 永久。
/// 版块 MinVipTier 表示进入该版块所需的最低会员等级。
/// </summary>
public static class VipAccess
{
    public const int TierGuest = 0;
    public const int TierMonth = 1;
    public const int TierQuarter = 2;
    public const int TierYear = 3;
    public const int TierLifetime = 4;

    public static string TierLabel(int tier) => tier switch
    {
        TierLifetime => "永久会员",
        TierYear => "年会员",
        TierQuarter => "季会员",
        TierMonth => "月会员",
        _ => "普通用户"
    };

    public static int TierFromPackageCode(string? code) => (code ?? "").Trim().ToLowerInvariant() switch
    {
        "vip_lifetime" => TierLifetime,
        "vip_year" => TierYear,
        "vip_quarter" => TierQuarter,
        "vip_month" => TierMonth,
        _ => TierMonth
    };

    public static int TierFromVipDays(int? vipDays)
    {
        if (vipDays == null || vipDays <= 0) return TierLifetime;
        if (vipDays >= 365) return TierYear;
        if (vipDays >= 90) return TierQuarter;
        return TierMonth;
    }

    /// <summary>当前有效会员等级；过期或未开通为 0。管理员视为永久。</summary>
    public static int EffectiveTier(User? user)
    {
        if (user == null) return TierGuest;
        if (user.IsAdmin) return TierLifetime;
        ClearExpired(user);
        if (!user.IsEffectivelyVip()) return TierGuest;
        if (!user.VipUntil.HasValue) return Math.Max(user.VipTier, TierLifetime);
        var tier = user.VipTier <= 0 ? TierMonth : user.VipTier;
        return Math.Clamp(tier, TierMonth, TierLifetime);
    }

    public static bool CanAccessForum(User? user, int minVipTier)
    {
        if (minVipTier <= TierGuest) return true;
        return EffectiveTier(user) >= minVipTier;
    }

    public static string AccessDeniedMessage(int minVipTier) =>
        $"该版块仅限「{TierLabel(minVipTier)}」及以上会员访问，请先开通会员。";

    public static void ApplyVipTier(User user, int newTier)
    {
        ClearExpired(user);
        var storedCurrent = 0;
        if (user.IsEffectivelyVip())
        {
            storedCurrent = !user.VipUntil.HasValue
                ? Math.Max(user.VipTier, TierLifetime)
                : Math.Max(user.VipTier <= 0 ? TierMonth : user.VipTier, TierMonth);
        }
        user.IsVip = true;
        user.VipTier = Math.Max(storedCurrent, Math.Clamp(newTier, TierMonth, TierLifetime));
    }

    public static void ClearExpired(User user)
    {
        if (user.IsVip && user.VipUntil.HasValue && user.VipUntil.Value <= DateTime.UtcNow)
        {
            user.IsVip = false;
            user.VipUntil = null;
            user.VipTier = 0;
        }
    }
}
