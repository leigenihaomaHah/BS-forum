using System.Text.RegularExpressions;
using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class CommunityService
{
    public const int InviteRewardCoins = 20;
    public const int InviteRewardPoints = 10;
    public const int InviteeBonusCoins = 5;

    private static readonly (string Code, string Name, string Desc)[] BadgeDefs =
    [
        ("signin_7", "签到达人", "连续签到 7 天"),
        ("signin_30", "签到满贯", "连续签到 30 天"),
        ("essence_author", "精品作者", "帖子被设为精品"),
        ("social_10", "人气新星", "获得 10 个粉丝"),
        ("shopper", "商场常客", "在积分商城消费过"),
    ];

    private readonly AppDbContext _db;
    private readonly RewardService _rewards;
    private readonly LevelService _levels;
    private readonly NotificationService _notifications;

    public CommunityService(AppDbContext db, RewardService rewards, LevelService levels, NotificationService notifications)
    {
        _db = db;
        _rewards = rewards;
        _levels = levels;
        _notifications = notifications;
    }

    public static string NewInviteCode(int userId)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var suffix = new char[4];
        for (var i = 0; i < 4; i++) suffix[i] = chars[Random.Shared.Next(chars.Length)];
        return $"U{userId}{new string(suffix)}";
    }

    public async Task ApplyInviteOnRegisterAsync(User newbie, string? inviteCode)
    {
        if (string.IsNullOrWhiteSpace(inviteCode)) return;
        var code = inviteCode.Trim().ToUpperInvariant();
        var inviter = await _db.Users.FirstOrDefaultAsync(u => u.InviteCode == code);
        if (inviter == null || inviter.Id == newbie.Id) return;

        newbie.InvitedByUserId = inviter.Id;
        newbie.Coins += InviteeBonusCoins;
        _db.CoinLedgers.Add(new CoinLedger
        {
            UserId = newbie.Id, Delta = InviteeBonusCoins, Reason = "invitee_bonus", RefType = "user", RefId = inviter.Id
        });

        inviter.Coins += InviteRewardCoins;
        inviter.LotteryTickets += 1;
        await _rewards.TryAwardPointsAsync(inviter, InviteRewardPoints, "invite_reward", "user", newbie.Id);
        _db.CoinLedgers.Add(new CoinLedger
        {
            UserId = inviter.Id, Delta = InviteRewardCoins, Reason = "invite_reward", RefType = "user", RefId = newbie.Id
        });
    }

    public async Task<InviteInfoDto> GetInviteInfoAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId) ?? throw new InvalidOperationException();
        if (string.IsNullOrEmpty(user.InviteCode))
        {
            user.InviteCode = NewInviteCode(user.Id);
            await _db.SaveChangesAsync();
        }
        var count = await _db.Users.CountAsync(u => u.InvitedByUserId == userId);
        return new InviteInfoDto(user.InviteCode, $"/register?invite={user.InviteCode}", count, InviteRewardCoins, InviteRewardPoints);
    }

    public async Task<(FollowResultDto? Result, string? Error)> ToggleFollowAsync(int followerId, int followeeId)
    {
        if (followerId == followeeId) return (null, "不能关注自己");
        var followee = await _db.Users.FindAsync(followeeId);
        if (followee == null) return (null, "用户不存在");

        var existing = await _db.UserFollows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId);
        if (existing != null)
        {
            _db.UserFollows.Remove(existing);
            await _db.SaveChangesAsync();
            return (new FollowResultDto(false, "已取消关注"), null);
        }

        _db.UserFollows.Add(new UserFollow { FollowerId = followerId, FolloweeId = followeeId });
        await _db.SaveChangesAsync();
        var follower = await _db.Users.FindAsync(followerId);
        if (follower != null)
            await _notifications.AddFollowNotificationAsync(followeeId, followerId, follower.Nickname);
        await _db.SaveChangesAsync();
        await TryAwardBadgeAsync(followeeId, "social_10", async () =>
            await _db.UserFollows.CountAsync(f => f.FolloweeId == followeeId) >= 10);
        return (new FollowResultDto(true, "关注成功"), null);
    }

    public async Task<PagedResult<FeedItemDto>> GetFeedAsync(int userId, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var followeeIds = await _db.UserFollows.Where(f => f.FollowerId == userId).Select(f => f.FolloweeId).ToListAsync();
        if (followeeIds.Count == 0)
            return new PagedResult<FeedItemDto>([], 0, page, pageSize);

        var blocked = await GetBlockedUserIdsAsync(userId);
        var query = _db.Threads
            .Include(t => t.Forum).Include(t => t.Author)
            .Where(t => followeeIds.Contains(t.AuthorId) && !t.IsHidden && !t.PendingReview && !blocked.Contains(t.AuthorId));

        var total = await query.CountAsync();
        var now = ChinaTime.Now;
        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Title,
                ForumName = t.Forum.Name,
                t.Author.Nickname,
                t.AuthorId,
                t.CreatedAt,
                t.Author.Avatar,
                t.Author.Level,
                t.Author.IsVip,
                t.Author.VipUntil,
                t.Author.AvatarFrame,
                t.ReplyCount,
                t.Views,
                t.LikeCount,
                t.IsEssence,
                t.IsPinned,
                t.Type,
            })
            .ToListAsync();

        var items = rows.Select(t => new FeedItemDto(
            t.Id,
            t.Title,
            t.ForumName,
            t.Nickname,
            t.AuthorId,
            t.CreatedAt,
            "thread",
            t.Avatar,
            t.Level,
            t.IsVip && (t.VipUntil == null || t.VipUntil > now),
            t.AvatarFrame,
            t.ReplyCount,
            t.Views,
            t.LikeCount,
            t.IsEssence,
            t.IsPinned,
            t.Type
        )).ToList();

        return new PagedResult<FeedItemDto>(items, total, page, pageSize);
    }

    public async Task AttachTagsAsync(int threadId, List<string>? tagNames)
    {
        if (tagNames == null || tagNames.Count == 0) return;
        var names = tagNames
            .Select(t => t.Trim())
            .Where(t => t.Length is >= 1 and <= 12)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        foreach (var name in names)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name);
            if (tag == null)
            {
                tag = new Tag { Name = name };
                _db.Tags.Add(tag);
                await _db.SaveChangesAsync();
            }
            if (!await _db.ThreadTags.AnyAsync(tt => tt.ThreadId == threadId && tt.TagId == tag.Id))
                _db.ThreadTags.Add(new ThreadTag { ThreadId = threadId, TagId = tag.Id });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<string>> GetThreadTagsAsync(int threadId) =>
        await _db.ThreadTags.Where(tt => tt.ThreadId == threadId).Select(tt => tt.Tag.Name).ToListAsync();

    public async Task CreatePollAsync(int threadId, List<string>? options)
    {
        if (options == null || options.Count < 2) return;
        var i = 0;
        foreach (var text in options.Select(o => o.Trim()).Where(o => o.Length > 0).Take(6))
        {
            _db.PollOptions.Add(new PollOption { ThreadId = threadId, Text = text, SortOrder = i++ });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<PollDto?> GetPollAsync(int threadId, int? userId)
    {
        var options = await _db.PollOptions.Where(o => o.ThreadId == threadId).OrderBy(o => o.SortOrder).ToListAsync();
        if (options.Count == 0) return null;
        int? my = null;
        if (userId.HasValue)
        {
            var vote = await _db.PollVotes.FirstOrDefaultAsync(v => v.ThreadId == threadId && v.UserId == userId.Value);
            my = vote?.OptionId;
        }
        return new PollDto(
            options.Select(o => new PollOptionDto(o.Id, o.Text, o.VoteCount, o.SortOrder)).ToList(),
            my,
            options.Sum(o => o.VoteCount));
    }

    public async Task<(PollDto? Result, string? Error)> VotePollAsync(int userId, int threadId, int optionId)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null || thread.IsHidden) return (null, "帖子不存在");
        if (await _db.PollVotes.AnyAsync(v => v.ThreadId == threadId && v.UserId == userId))
            return (null, "你已经投过票了");
        var opt = await _db.PollOptions.FirstOrDefaultAsync(o => o.Id == optionId && o.ThreadId == threadId);
        if (opt == null) return (null, "选项不存在");
        opt.VoteCount += 1;
        _db.PollVotes.Add(new PollVote { ThreadId = threadId, OptionId = optionId, UserId = userId });
        await _db.SaveChangesAsync();
        return (await GetPollAsync(threadId, userId), null);
    }

    public async Task<List<ShopItemDto>> GetShopAsync() =>
        await _db.ShopItems.Where(s => s.Enabled).OrderBy(s => s.SortOrder)
            .Select(s => new ShopItemDto(s.Id, s.Sku, s.Name, s.Description, s.Currency, s.Price, s.ItemType, s.Meta, s.Enabled, s.SortOrder))
            .ToListAsync();

    public async Task<List<ShopItemDto>> AdminListShopAsync() =>
        await _db.ShopItems.OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new ShopItemDto(s.Id, s.Sku, s.Name, s.Description, s.Currency, s.Price, s.ItemType, s.Meta, s.Enabled, s.SortOrder))
            .ToListAsync();

    public async Task<(ShopItemDto? Result, string? Error)> AdminSaveShopAsync(int? id, SaveShopItemRequest req)
    {
        var sku = (req.Sku ?? "").Trim();
        var name = (req.Name ?? "").Trim();
        if (sku.Length < 1 || name.Length < 1) return (null, "SKU 与名称必填");
        if (req.Price < 0) return (null, "价格无效");
        var currency = (req.Currency ?? "coins").Trim().ToLowerInvariant();
        if (currency is not ("coins" or "points")) return (null, "货币仅支持 coins/points");
        var itemType = (req.ItemType ?? "").Trim();
        if (itemType.Length < 1) return (null, "商品类型必填");

        ShopItem item;
        if (id == null)
        {
            if (await _db.ShopItems.AnyAsync(s => s.Sku == sku)) return (null, "SKU 已存在");
            item = new ShopItem();
            _db.ShopItems.Add(item);
        }
        else
        {
            item = await _db.ShopItems.FindAsync(id.Value);
            if (item == null) return (null, "商品不存在");
            if (await _db.ShopItems.AnyAsync(s => s.Sku == sku && s.Id != id.Value)) return (null, "SKU 已存在");
        }

        item.Sku = sku;
        item.Name = name;
        item.Description = (req.Description ?? "").Trim();
        item.Currency = currency;
        item.Price = req.Price;
        item.ItemType = itemType;
        item.Meta = req.Meta;
        item.Enabled = req.Enabled;
        item.SortOrder = req.SortOrder;
        await _db.SaveChangesAsync();
        return (new ShopItemDto(item.Id, item.Sku, item.Name, item.Description, item.Currency, item.Price, item.ItemType, item.Meta, item.Enabled, item.SortOrder), null);
    }

    public async Task<(bool Ok, string? Error)> AdminDeleteShopAsync(int id)
    {
        var item = await _db.ShopItems.FindAsync(id);
        if (item == null) return (false, "商品不存在");
        item.Enabled = false;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(ShopBuyResultDto? Result, string? Error)> BuyAsync(int userId, int itemId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");
        ClearExpiredVip(user);
        var item = await _db.ShopItems.FirstOrDefaultAsync(s => s.Id == itemId && s.Enabled);
        if (item == null) return (null, "商品不存在");

        if (item.Currency == "coins")
        {
            if (user.Coins < item.Price) return (null, "金币不足");
            user.Coins -= item.Price;
            _db.CoinLedgers.Add(new CoinLedger { UserId = userId, Delta = -item.Price, Reason = "shop_buy", RefType = "shop", RefId = item.Id });
        }
        else
        {
            if (user.Points < item.Price) return (null, "积分不足");
            user.Points -= item.Price;
            _db.PointLedgers.Add(new PointLedger { UserId = userId, Delta = -item.Price, Reason = "shop_buy", RefType = "shop", RefId = item.Id });
            await _levels.RecalculateLevelAsync(user);
        }

        switch (item.ItemType)
        {
            case "lottery_ticket":
                var qty = int.TryParse(item.Meta, out var q) ? q : 1;
                user.LotteryTickets += qty;
                break;
            case "vip_30":
                var days = int.TryParse(item.Meta, out var d) ? d : 30;
                RechargeService.GrantVip(user, days);
                VipAccess.ApplyVipTier(user, VipAccess.TierFromVipDays(days));
                break;
            case "avatar_frame":
                user.AvatarFrame = item.Meta;
                _db.UserInventories.Add(new UserInventory { UserId = userId, ItemType = item.ItemType, Meta = item.Meta });
                break;
            case "rename_card":
                _db.UserInventories.Add(new UserInventory { UserId = userId, ItemType = item.ItemType, Meta = item.Meta, Quantity = 1 });
                break;
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        await TryAwardBadgeAsync(userId, "shopper", () => Task.FromResult(true));
        return (new ShopBuyResultDto("购买成功", user.Coins, user.Points, user.LotteryTickets, user.IsEffectivelyVip(), user.AvatarFrame), null);
    }

    public async Task<List<TaskItemDto>> GetDailyTasksAsync(int userId)
    {
        var today = ChinaTime.Today;
        var defs = new[]
        {
            ("signin", "每日签到", "完成今日签到", 1, 5, 1),
            ("reply", "回帖达人", "今日回帖 3 次", 3, 6, 1),
            ("like", "热情互动", "今日点赞 1 次", 1, 3, 0),
        };

        var list = new List<TaskItemDto>();
        foreach (var (code, title, desc, target, pts, coins) in defs)
        {
            var row = await _db.UserTaskProgresses.FirstOrDefaultAsync(t =>
                t.UserId == userId && t.TaskCode == code && t.ProgressDate == today);
            list.Add(new TaskItemDto(code, title, desc, target, row?.Progress ?? 0, row?.Claimed ?? false, pts, coins));
        }
        return list;
    }

    public async Task BumpTaskAsync(int userId, string taskCode, int delta = 1)
    {
        var today = ChinaTime.Today;
        var row = await _db.UserTaskProgresses.FirstOrDefaultAsync(t =>
            t.UserId == userId && t.TaskCode == taskCode && t.ProgressDate == today);
        if (row == null)
        {
            row = new UserTaskProgress { UserId = userId, TaskCode = taskCode, ProgressDate = today, Progress = 0 };
            _db.UserTaskProgresses.Add(row);
        }
        if (!row.Claimed) row.Progress += delta;
        await _db.SaveChangesAsync();
    }

    public async Task<(TaskItemDto? Result, string? Error)> ClaimTaskAsync(int userId, string taskCode)
    {
        var tasks = await GetDailyTasksAsync(userId);
        var task = tasks.FirstOrDefault(t => t.Code == taskCode);
        if (task == null) return (null, "任务不存在");
        if (task.Claimed) return (null, "已领取");
        if (task.Progress < task.Target) return (null, "尚未完成");

        var today = ChinaTime.Today;
        var row = await _db.UserTaskProgresses.FirstAsync(t =>
            t.UserId == userId && t.TaskCode == taskCode && t.ProgressDate == today);
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");

        row.Claimed = true;
        if (task.RewardPoints > 0)
            await _rewards.TryAwardPointsAsync(user, task.RewardPoints, "task_claim", "task", null);
        if (task.RewardCoins > 0)
            await _rewards.AwardCoinsAsync(user, task.RewardCoins, "task_claim", "task", null);
        await _db.SaveChangesAsync();

        var parts = new List<string>();
        if (task.RewardPoints > 0) parts.Add($"+{task.RewardPoints} 积分");
        if (task.RewardCoins > 0) parts.Add($"+{task.RewardCoins} 金币");
        var rewardText = parts.Count > 0 ? string.Join("、", parts) : "奖励";
        await _notifications.AddSystemNotificationAsync(userId, $"任务「{task.Title}」奖励已领取：{rewardText}");
        await _db.SaveChangesAsync();
        return (task with { Claimed = true }, null);
    }

    public async Task<List<BadgeDto>> GetBadgesAsync(int userId)
    {
        var earned = await _db.UserBadges.Where(b => b.UserId == userId).ToDictionaryAsync(b => b.BadgeCode, b => b.EarnedAt);
        return BadgeDefs.Select(d => new BadgeDto(d.Code, d.Name, d.Desc, earned.GetValueOrDefault(d.Code))).ToList();
    }

    public async Task TryAwardBadgeAsync(int userId, string code, Func<Task<bool>> cond)
    {
        if (await _db.UserBadges.AnyAsync(b => b.UserId == userId && b.BadgeCode == code)) return;
        if (!await cond()) return;
        _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeCode = code });
        await _db.SaveChangesAsync();
        var def = BadgeDefs.FirstOrDefault(d => d.Code == code);
        var name = def.Name ?? code;
        await _notifications.AddSystemNotificationAsync(userId, $"恭喜获得徽章「{name}」");
        await _db.SaveChangesAsync();
    }

    public async Task OnEssenceAwardedAsync(int authorId) =>
        await TryAwardBadgeAsync(authorId, "essence_author", () => Task.FromResult(true));

    public async Task<(PagedResult<TagThreadItemDto>? Result, string? Error)> GetThreadsByTagAsync(string tagName, int page, int pageSize)
    {
        var name = (tagName ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return (null, "标签不能为空");
        if (name.Length > 20) name = name[..20];

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name);
        if (tag == null) return (new PagedResult<TagThreadItemDto>([], 0, page, pageSize), null);

        var q = _db.Threads
            .Include(t => t.Author)
            .Include(t => t.Forum)
            .Where(t => !t.IsHidden && !t.PendingReview && t.ThreadTags.Any(tt => tt.TagId == tag.Id));

        var total = await q.CountAsync();
        var threads = await q
            .OrderByDescending(t => t.LastReplyAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<TagThreadItemDto>();
        foreach (var t in threads)
        {
            var ln = await _levels.GetLevelNameAsync(t.Author.Level);
            items.Add(new TagThreadItemDto(
                t.Id, t.Title, t.ForumId, t.Forum.Name, t.Views, t.ReplyCount,
                t.CreatedAt, t.LastReplyAt, t.Author.Nickname, t.Author.Level, ln, t.IsEssence));
        }

        return (new PagedResult<TagThreadItemDto>(items, total, page, pageSize), null);
    }
    public async Task OnSignInAsync(int userId, int consecutiveDays)
    {
        await BumpTaskAsync(userId, "signin");
        if (consecutiveDays >= 7)
            await TryAwardBadgeAsync(userId, "signin_7", () => Task.FromResult(true));
        if (consecutiveDays >= 30)
            await TryAwardBadgeAsync(userId, "signin_30", () => Task.FromResult(true));
    }

    public async Task<(bool Ok, string? Error)> CreateReportAsync(int reporterId, ReportRequest req)
    {
        var reason = (req.Reason ?? "").Trim();
        if (reason.Length < 2) return (false, "请填写举报原因");
        var type = (req.TargetType ?? "thread").Trim().ToLowerInvariant();
        if (type is not ("thread" or "post" or "user")) return (false, "无效目标类型");

        _db.Reports.Add(new Report
        {
            ReporterId = reporterId,
            TargetType = type,
            TargetId = req.TargetId,
            Reason = reason.Length > 200 ? reason[..200] : reason,
            Status = "pending"
        });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<PagedResult<ReportItemDto>> GetReportsAsync(int page, int pageSize, string? status)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Reports.Include(r => r.Reporter).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(r => r.Status == status.Trim().ToLowerInvariant());
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var threadIds = rows.Where(r => r.TargetType == "thread").Select(r => r.TargetId).Distinct().ToList();
        var postIds = rows.Where(r => r.TargetType == "post").Select(r => r.TargetId).Distinct().ToList();
        var userIds = rows.Where(r => r.TargetType == "user").Select(r => r.TargetId).Distinct().ToList();

        var threads = await _db.Threads.Include(t => t.Author).Where(t => threadIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id);
        var posts = await _db.Posts.Include(p => p.Thread).Include(p => p.Author)
            .Where(p => postIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var users = await _db.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var items = rows.Select(r =>
        {
            string? title = null;
            int? threadId = null;
            string? targetNick = null;
            if (r.TargetType == "thread" && threads.TryGetValue(r.TargetId, out var t))
            {
                title = t.Title;
                threadId = t.Id;
                targetNick = t.Author?.Nickname;
            }
            else if (r.TargetType == "post" && posts.TryGetValue(r.TargetId, out var p))
            {
                title = p.Thread?.Title;
                threadId = p.ThreadId;
                targetNick = p.Author?.Nickname;
            }
            else if (r.TargetType == "user" && users.TryGetValue(r.TargetId, out var u))
            {
                targetNick = u.Nickname;
                title = u.Nickname;
            }
            return new ReportItemDto(
                r.Id, r.TargetType, r.TargetId, r.Reason, r.Status,
                r.Reporter.Nickname, r.CreatedAt, r.HandleNote,
                title, threadId, targetNick);
        }).ToList();

        return new PagedResult<ReportItemDto>(items, total, page, pageSize);
    }

    public async Task<(bool Ok, string? Error)> HandleReportAsync(int adminId, int reportId, HandleReportRequest req, AdminService admin)
    {
        var report = await _db.Reports.FindAsync(reportId);
        if (report == null) return (false, "举报不存在");
        var action = (req.Action ?? "").Trim().ToLowerInvariant();

        if (action == "hide_thread" && report.TargetType == "thread")
            await admin.SetThreadHiddenAsync(adminId, report.TargetId, true, $"举报处理#{reportId}");
        else if (action == "hide_post" && report.TargetType == "post")
        {
            var post = await _db.Posts.Include(p => p.Thread).ThenInclude(t => t.Forum)
                .FirstOrDefaultAsync(p => p.Id == report.TargetId);
            if (post != null && !post.IsDeleted && post.Floor > 1)
            {
                post.IsDeleted = true;
                post.DeletedAt = ChinaTime.Now;
                post.Content = "";
                post.ImagesJson = null;
                post.Thread.ReplyCount = Math.Max(0, post.Thread.ReplyCount - 1);
                if (post.Thread.Forum != null)
                    post.Thread.Forum.PostCount = Math.Max(0, post.Thread.Forum.PostCount - 1);
            }
        }
        else if (action == "mute_user" && report.TargetType == "user")
            await admin.MuteUserAsync(adminId, report.TargetId, 7, $"举报处理#{reportId}");
        else if (action == "mute_user" && report.TargetType == "thread")
        {
            var thread = await _db.Threads.FindAsync(report.TargetId);
            if (thread != null)
                await admin.MuteUserAsync(adminId, thread.AuthorId, 7, $"举报处理#{reportId}");
        }

        report.Status = action == "reject" ? "rejected" : "resolved";
        report.HandledByAdminId = adminId;
        report.HandleNote = req.Note;
        report.HandledAt = ChinaTime.Now;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> AddModeratorAsync(int forumId, int userId)
    {
        if (await _db.Forums.FindAsync(forumId) == null) return (false, "版块不存在");
        if (await _db.Users.FindAsync(userId) == null) return (false, "用户不存在");
        if (await _db.ForumModerators.AnyAsync(m => m.ForumId == forumId && m.UserId == userId))
            return (false, "已是版主");
        _db.ForumModerators.Add(new ForumModerator { ForumId = forumId, UserId = userId });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RemoveModeratorAsync(int forumId, int userId)
    {
        var m = await _db.ForumModerators.FirstOrDefaultAsync(x => x.ForumId == forumId && x.UserId == userId);
        if (m == null) return (false, "不是版主");
        _db.ForumModerators.Remove(m);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<ModeratorDto>> ListModeratorsAsync() =>
        await _db.ForumModerators.Include(m => m.Forum).Include(m => m.User)
            .OrderBy(m => m.ForumId)
            .Select(m => new ModeratorDto(m.ForumId, m.Forum.Name, m.UserId, m.User.Nickname))
            .ToListAsync();

    public Task<bool> IsForumModeratorAsync(int userId, int forumId) =>
        _db.ForumModerators.AnyAsync(m => m.UserId == userId && m.ForumId == forumId);

    public async Task<(bool Ok, string? Error)> BlockUserAsync(int userId, int blockedUserId)
    {
        if (userId == blockedUserId) return (false, "不能屏蔽自己");
        var exists = await _db.Users.AnyAsync(u => u.Id == blockedUserId);
        if (!exists) return (false, "用户不存在");
        if (await _db.UserBlocks.AnyAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId))
            return (true, null); // already blocked, no-op
        _db.UserBlocks.Add(new UserBlock { UserId = userId, BlockedUserId = blockedUserId, CreatedAt = ChinaTime.Now });
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task UnblockUserAsync(int userId, int blockedUserId)
    {
        var block = await _db.UserBlocks.FirstOrDefaultAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId);
        if (block != null) { _db.UserBlocks.Remove(block); await _db.SaveChangesAsync(); }
    }

    public async Task<List<int>> GetBlockedUserIdsAsync(int userId) =>
        await _db.UserBlocks.Where(b => b.UserId == userId).Select(b => b.BlockedUserId).ToListAsync();

    public async Task<List<UserDto>> GetBlockedUsersAsync(int userId)
    {
        var blockedIds = await _db.UserBlocks.Where(b => b.UserId == userId).Select(b => b.BlockedUserId).ToListAsync();
        if (blockedIds.Count == 0) return new List<UserDto>();
        var users = await _db.Users.Where(u => blockedIds.Contains(u.Id)).ToListAsync();
        var result = new List<UserDto>();
        foreach (var u in users)
        {
            var levelName = await _levels.GetLevelNameAsync(u.Level);
            result.Add(new UserDto(u.Id, u.Username, u.Nickname, u.Avatar, u.Points, u.Coins, u.Level, levelName,
                u.ConsecutiveSignInDays, false, u.IsAdmin, u.IsAdmin ? "admin" : "user",
                u.IsEffectivelyMuted(), u.MutedUntil, u.InviteCode ?? "", u.IsVip, u.VipUntil, u.VipTier,
                VipAccess.TierLabel(u.VipTier), u.LotteryTickets, u.AvatarFrame));
        }
        return result;
    }

    public static List<string> ExtractMentions(string content)
    {
        var matches = Regex.Matches(content, @"@([A-Za-z0-9_\u4e00-\u9fa5]{1,20})");
        return matches.Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();
    }

    public static void ClearExpiredVip(User user) => VipAccess.ClearExpired(user);
}
