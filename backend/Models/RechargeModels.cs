namespace ForumApi.Models;

/// <summary>会员充值套餐：月/季/年/永久。</summary>
public class RechargePackage
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>标价（元）。</summary>
    public decimal PriceYuan { get; set; }
    /// <summary>VIP 天数；null 表示永久。</summary>
    public int? VipDays { get; set; }
    /// <summary>到账附赠金币。</summary>
    public int BonusCoins { get; set; }
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
}

public class RechargeOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public decimal PriceYuan { get; set; }
    public int? VipDays { get; set; }
    public int BonusCoins { get; set; }
    /// <summary>pending | paid | cancelled</summary>
    public string Status { get; set; } = "pending";
    /// <summary>manual | card</summary>
    public string Channel { get; set; } = "manual";
    public string? CardCode { get; set; }
    public string? Remark { get; set; }
    public int? ConfirmedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;
    public DateTime? PaidAt { get; set; }

    public User User { get; set; } = null!;
    public RechargePackage Package { get; set; } = null!;
}

public class RechargeCard
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int PackageId { get; set; }
    public int? UsedByUserId { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = ChinaTime.Now;

    public RechargePackage Package { get; set; } = null!;
}
