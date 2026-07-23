using System.Globalization;
using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class AdminService
{
    public const int EssencePointsReward = 20;
    public const int EssenceCoinsReward = 5;

    private readonly AppDbContext _db;
    private readonly LevelService _levels;
    private readonly RewardService _rewards;
    private readonly CommunityService _community;
    private readonly NotificationService _notifications;
    private readonly JwtHelper _jwt;
    private readonly AuthService _auth;

    public AdminService(
        AppDbContext db,
        LevelService levels,
        RewardService rewards,
        CommunityService community,
        NotificationService notifications,
        JwtHelper jwt,
        AuthService auth)
    {
        _db = db;
        _levels = levels;
        _rewards = rewards;
        _community = community;
        _notifications = notifications;
        _jwt = jwt;
        _auth = auth;
    }

    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var today = ChinaTime.Today;
        var tomorrow = today.AddDays(1);
        var now = ChinaTime.Now;

        var totalUsers = await _db.Users.CountAsync();
        var totalThreads = await _db.Threads.CountAsync();
        var totalPosts = await _db.Posts.CountAsync();
        var totalForums = await _db.Forums.CountAsync();

        var todaySignIns = await _db.SignInRecords.CountAsync(r => r.SignInDate == today);
        var todayRegistrations = await _db.Users.CountAsync(u => u.CreatedAt >= today && u.CreatedAt < tomorrow);
        var todayThreads = await _db.Threads.CountAsync(t => t.CreatedAt >= today && t.CreatedAt < tomorrow);
        var todayReplies = await _db.Posts.CountAsync(p => p.Floor > 1 && p.CreatedAt >= today && p.CreatedAt < tomorrow);

        var yesterday = today.AddDays(-1);
        var yesterdaySignIns = await _db.SignInRecords.CountAsync(r => r.SignInDate == yesterday);
        var yesterdayRegistrations = await _db.Users.CountAsync(u => u.CreatedAt >= yesterday && u.CreatedAt < today);
        var yesterdayThreads = await _db.Threads.CountAsync(t => t.CreatedAt >= yesterday && t.CreatedAt < today);
        var yesterdayReplies = await _db.Posts.CountAsync(p => p.Floor > 1 && p.CreatedAt >= yesterday && p.CreatedAt < today);
        var ySignIds = await _db.SignInRecords.Where(r => r.SignInDate == yesterday).Select(r => r.UserId).ToListAsync();
        var yThreadAuthors = await _db.Threads.Where(t => t.CreatedAt >= yesterday && t.CreatedAt < today).Select(t => t.AuthorId).ToListAsync();
        var yPostAuthors = await _db.Posts.Where(p => p.CreatedAt >= yesterday && p.CreatedAt < today).Select(p => p.AuthorId).ToListAsync();
        var yesterdayActiveUsers = ySignIds.Concat(yThreadAuthors).Concat(yPostAuthors).Distinct().Count();

        var signInUserIds = await _db.SignInRecords.Where(r => r.SignInDate == today).Select(r => r.UserId).ToListAsync();
        var threadAuthorIds = await _db.Threads.Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow).Select(t => t.AuthorId).ToListAsync();
        var postAuthorIds = await _db.Posts.Where(p => p.CreatedAt >= today && p.CreatedAt < tomorrow).Select(p => p.AuthorId).ToListAsync();
        var todayActiveUsers = signInUserIds.Concat(threadAuthorIds).Concat(postAuthorIds).Distinct().Count();
        var todayActive = todayActiveUsers;

        var pendingReports = await _db.Reports.CountAsync(r => r.Status == "pending");
        var pendingReviewThreads = await _db.Threads.CountAsync(t => t.PendingReview && !t.IsHidden);
        var hiddenThreads = await _db.Threads.CountAsync(t => t.IsHidden);
        var mutedUsers = await _db.Users.CountAsync(u => u.IsMuted && (u.MutedUntil == null || u.MutedUntil > now));
        var lockedThreads = await _db.Threads.CountAsync(t => t.RepliesLocked);
        var essenceCount = await _db.Threads.CountAsync(t => t.IsEssence && !t.IsHidden);
        var pinnedCount = await _db.Threads.CountAsync(t => t.IsPinned && !t.IsHidden);

        var todayCoinDelta = await _db.CoinLedgers
            .Where(c => c.CreatedAt >= today && c.CreatedAt < tomorrow)
            .SumAsync(c => (int?)c.Delta) ?? 0;
        var todayLotterySpins = await _db.LotterySpins.CountAsync(s => s.CreatedAt >= today && s.CreatedAt < tomorrow);
        var todayLotteryOutCoins = await _db.LotterySpins
            .Where(s => s.CreatedAt >= today && s.CreatedAt < tomorrow)
            .SumAsync(s => (int?)s.PrizeCoins) ?? 0;
        var todayLotteryCostCoins = await _db.LotterySpins
            .Where(s => s.CreatedAt >= today && s.CreatedAt < tomorrow)
            .SumAsync(s => (int?)s.CostCoins) ?? 0;
        var todayShopOrders = await _db.CoinLedgers.CountAsync(c =>
            c.Reason == "shop_buy" && c.CreatedAt >= today && c.CreatedAt < tomorrow)
            + await _db.PointLedgers.CountAsync(p =>
                p.Reason == "shop_buy" && p.CreatedAt >= today && p.CreatedAt < tomorrow);
        var vipUsers = await _db.Users.CountAsync(u => u.IsVip && (u.VipUntil == null || u.VipUntil > now));
        var lotteryTicketStock = await _db.Users.SumAsync(u => (int?)u.LotteryTickets) ?? 0;

        var recentUsersRaw = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .ToListAsync();
        var recentUsers = new List<AdminRecentUserDto>();
        foreach (var u in recentUsersRaw)
        {
            var ln = await _levels.GetLevelNameAsync(u.Level);
            recentUsers.Add(new AdminRecentUserDto(u.Id, u.Username, u.Nickname, u.Level, ln, u.CreatedAt));
        }

        var hotThreads = await _db.Threads
            .Include(t => t.Forum)
            .Where(t => !t.IsHidden)
            .OrderByDescending(t => t.Views)
            .Take(5)
            .Select(t => new AdminHotThreadDto(t.Id, t.Title, t.Forum.Name, t.Views, t.ReplyCount))
            .ToListAsync();

        var weeklyActivity = new List<AdminDayCountDto>();
        var dailyRegistrations = new List<AdminDayCountDto>();
        var dailyActive = new List<AdminDayCountDto>();
        var dailyNewThreads = new List<AdminDayCountDto>();
        var culture = CultureInfo.GetCultureInfo("zh-CN");
        var signInSum7 = 0;

        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var dateStr = d.ToString("yyyy-MM-dd");
            var dayLabel = d.ToString("ddd", culture);
            var signCount = await _db.SignInRecords.CountAsync(r => r.SignInDate == d);
            var regCount = await _db.Users.CountAsync(u => u.CreatedAt >= d && u.CreatedAt < d.AddDays(1));
            var newThreadCount = await _db.Threads.CountAsync(t => t.CreatedAt >= d && t.CreatedAt < d.AddDays(1));
            var daySignIds = await _db.SignInRecords.Where(r => r.SignInDate == d).Select(r => r.UserId).ToListAsync();
            var dayThreadAuthors = await _db.Threads.Where(t => t.CreatedAt >= d && t.CreatedAt < d.AddDays(1)).Select(t => t.AuthorId).ToListAsync();
            var dayPostAuthors = await _db.Posts.Where(p => p.CreatedAt >= d && p.CreatedAt < d.AddDays(1)).Select(p => p.AuthorId).ToListAsync();
            var dayActive = daySignIds.Concat(dayThreadAuthors).Concat(dayPostAuthors).Distinct().Count();
            signInSum7 += signCount;
            weeklyActivity.Add(new AdminDayCountDto(dateStr, dayLabel, signCount));
            dailyRegistrations.Add(new AdminDayCountDto(dateStr, dayLabel, regCount));
            dailyActive.Add(new AdminDayCountDto(dateStr, dayLabel, dayActive));
            dailyNewThreads.Add(new AdminDayCountDto(dateStr, dayLabel, newThreadCount));
        }

        var signInAvg7d = Math.Round(signInSum7 / 7.0, 1);

        var forums = await _db.Forums.OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync();
        var threadCounts = await _db.Threads.GroupBy(t => t.ForumId)
            .Select(g => new { ForumId = g.Key, C = g.Count() }).ToDictionaryAsync(x => x.ForumId, x => x.C);
        var todayByForum = await _db.Threads
            .Where(t => t.CreatedAt >= today && t.CreatedAt < tomorrow)
            .GroupBy(t => t.ForumId)
            .Select(g => new { ForumId = g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.ForumId, x => x.C);
        var subCounts = await _db.ForumSubscriptions.GroupBy(s => s.ForumId)
            .Select(g => new { ForumId = g.Key, C = g.Count() }).ToDictionaryAsync(x => x.ForumId, x => x.C);
        var forumHeat = forums.Select(f => new AdminForumHeatDto(
            f.Id, f.Name,
            threadCounts.GetValueOrDefault(f.Id),
            todayByForum.GetValueOrDefault(f.Id),
            subCounts.GetValueOrDefault(f.Id))).ToList();

        var todoReports = await _db.Reports.Include(r => r.Reporter)
            .Where(r => r.Status == "pending")
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new AdminTodoReportDto(r.Id, r.TargetType, r.TargetId, r.Reason, r.Reporter.Nickname, r.CreatedAt))
            .ToListAsync();

        var todoHidden = await _db.Threads.Include(t => t.Forum).Include(t => t.Author)
            .Where(t => t.IsHidden)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new AdminTodoThreadDto(t.Id, t.Title, t.Forum.Name, t.Author.Nickname, t.CreatedAt))
            .ToListAsync();

        var todoMuted = await _db.Users
            .Where(u => u.IsMuted && (u.MutedUntil == null || u.MutedUntil > now))
            .OrderByDescending(u => u.Id)
            .Take(5)
            .Select(u => new AdminTodoUserDto(u.Id, u.Username, u.Nickname, u.MutedUntil, u.MuteReason))
            .ToListAsync();

        var todoLocked = await _db.Threads.Include(t => t.Forum).Include(t => t.Author)
            .Where(t => t.RepliesLocked && !t.IsHidden)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new AdminTodoThreadDto(t.Id, t.Title, t.Forum.Name, t.Author.Nickname, t.CreatedAt))
            .ToListAsync();

        var recentModLogs = await _db.ModerationLogs.Include(m => m.Admin)
            .OrderByDescending(m => m.CreatedAt)
            .Take(8)
            .Select(m => new ModerationLogDto(
                m.Id, m.AdminId, m.Admin.Nickname, m.TargetType, m.TargetId,
                m.Action, m.Reason, m.CreatedAt))
            .ToListAsync();

        return new AdminStatsDto(
            totalUsers, totalThreads, totalPosts, totalForums,
            todaySignIns, todayRegistrations, todayActive,
            todayThreads, todayReplies, todayActiveUsers,
            pendingReports, hiddenThreads, mutedUsers, lockedThreads,
            essenceCount, pinnedCount,
            todayCoinDelta, todayLotterySpins, todayLotteryOutCoins, todayLotteryCostCoins,
            todayShopOrders, vipUsers, lotteryTicketStock, signInAvg7d,
            recentUsers, hotThreads, weeklyActivity, dailyRegistrations, dailyActive, dailyNewThreads,
            forumHeat, todoReports, todoHidden, todoMuted, todoLocked, recentModLogs,
            yesterdaySignIns, yesterdayRegistrations, yesterdayThreads, yesterdayReplies,
            yesterdayActiveUsers, pendingReviewThreads);
    }

    public async Task<PagedResult<AdminUserItemDto>> GetUsersAsync(int page, int pageSize, string? search, bool? muted = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = ChinaTime.Now;
        var q = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.Username.Contains(s) || u.Nickname.Contains(s));
        }
        if (muted == true)
            q = q.Where(u => u.IsMuted && (u.MutedUntil == null || u.MutedUntil > now));

        var total = await q.CountAsync();
        var users = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var threadCounts = await _db.Threads.Where(t => ids.Contains(t.AuthorId))
            .GroupBy(t => t.AuthorId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        var replyCounts = await _db.Posts.Where(p => ids.Contains(p.AuthorId) && p.Floor > 1)
            .GroupBy(p => p.AuthorId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);
        var signTotals = await _db.SignInRecords.Where(r => ids.Contains(r.UserId))
            .GroupBy(r => r.UserId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C);

        var items = new List<AdminUserItemDto>();
        foreach (var u in users)
        {
            var ln = await _levels.GetLevelNameAsync(u.Level);
            var effectivelyMuted = u.IsEffectivelyMuted();
            items.Add(new AdminUserItemDto(
                u.Id, u.Username, u.Nickname, u.Avatar, u.Level, ln,
                u.Points, u.Coins, u.ConsecutiveSignInDays,
                signTotals.GetValueOrDefault(u.Id),
                threadCounts.GetValueOrDefault(u.Id),
                replyCounts.GetValueOrDefault(u.Id),
                u.CreatedAt,
                u.IsAdmin ? "admin" : "user",
                u.IsAdmin,
                effectivelyMuted,
                effectivelyMuted ? u.MutedUntil : null,
                effectivelyMuted ? u.MuteReason : null,
                u.IsEffectivelyVip(),
                u.IsEffectivelyVip() ? u.VipUntil : null));
        }

        return new PagedResult<AdminUserItemDto>(items, total, page, pageSize);
    }

    public async Task<(object? Result, string? Error)> UpdateUserAsync(int id, UpdateAdminUserRequest req)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return (null, "用户不存在");

        if (req.Nickname != null)
        {
            var nick = req.Nickname.Trim();
            if (nick.Length is < 1 or > 20) return (null, "昵称无效");
            user.Nickname = nick;
        }
        if (req.Points.HasValue) user.Points = Math.Max(0, req.Points.Value);
        if (req.Coins.HasValue) user.Coins = Math.Max(0, req.Coins.Value);
        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            var pwdError = PasswordRules.Validate(req.Password);
            if (pwdError != null) return (null, pwdError);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }

        if (req.IsVip == false)
        {
            user.IsVip = false;
            user.VipUntil = null;
            user.VipTier = 0;
        }
        if (req.IsVip == true || req.VipDays.HasValue)
        {
            RechargeService.GrantVip(user, req.VipDays ?? 30);
            if (req.VipTier.HasValue)
                VipAccess.ApplyVipTier(user, req.VipTier.Value);
            else if (req.VipDays.HasValue)
                VipAccess.ApplyVipTier(user, VipAccess.TierFromVipDays(req.VipDays));
        }
        else if (req.VipTier.HasValue && user.IsEffectivelyVip())
        {
            VipAccess.ApplyVipTier(user, req.VipTier.Value);
        }

        await _levels.RecalculateLevelAsync(user);
        await _db.SaveChangesAsync();
        var ln = await _levels.GetLevelNameAsync(user.Level);
        return (new
        {
            message = "更新成功",
            user = new { user.Id, user.Username, user.Nickname, user.Level, levelName = ln, user.Points, user.Coins }
        }, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteUserAsync(int id, int adminId)
    {
        if (id == adminId) return (false, "不能删除自己");
        var user = await _db.Users.FindAsync(id);
        if (user == null) return (false, "用户不存在");
        if (await _db.Threads.AnyAsync(t => t.AuthorId == id) || await _db.Posts.AnyAsync(p => p.AuthorId == id))
            return (false, "该用户仍有发帖/回复，无法删除");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(AuthResponse? Result, string? Error)> LoginAsUserAsync(int adminId, int targetUserId)
    {
        if (adminId == targetUserId) return (null, "已经是该账号，无需切换");
        var target = await _db.Users.FindAsync(targetUserId);
        if (target == null) return (null, "用户不存在");
        if (target.IsAdmin) return (null, "不能登录其他管理员账号");

        await LogAsync(adminId, "user", targetUserId, "login-as", null);
        await _db.SaveChangesAsync();

        var token = _jwt.CreateToken(target);
        return (new AuthResponse(token, await _auth.ToUserDtoAsync(target)), null);
    }

    public async Task<(object? Result, string? Error)> UpdateRoleAsync(int id, int adminId, string role)
    {
        if (id == adminId) return (null, "不能修改自己的角色");
        var user = await _db.Users.FindAsync(id);
        if (user == null) return (null, "用户不存在");

        var normalized = role.Trim().ToLowerInvariant();
        if (normalized is "admin" or "super_admin")
            user.IsAdmin = true;
        else if (normalized == "user")
            user.IsAdmin = false;
        else
            return (null, "无效的角色");

        await _db.SaveChangesAsync();
        return (new
        {
            message = "角色更新成功",
            user = new { user.Id, user.Username, role = user.IsAdmin ? "admin" : "user" }
        }, null);
    }

    public async Task<PagedResult<AdminThreadItemDto>> GetThreadsAsync(int page, int pageSize, string? search, string? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Threads.Include(t => t.Forum).Include(t => t.Author).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(t => t.Title.Contains(s) || t.Author.Nickname.Contains(s) || t.Forum.Name.Contains(s));
        }

        status = status?.Trim().ToLowerInvariant();
        if (status == "hidden") q = q.Where(t => t.IsHidden);
        else if (status is "locked" or "replies_locked") q = q.Where(t => t.RepliesLocked);
        else if (status == "pinned") q = q.Where(t => t.IsPinned);
        else if (status is "essence" or "精品") q = q.Where(t => t.IsEssence);
        else if (status is "pending" or "review") q = q.Where(t => t.PendingReview && !t.IsHidden);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AdminThreadItemDto(
                t.Id, t.Title, t.Forum.Name, t.Author.Nickname, t.Author.Level,
                t.ReplyCount, t.Views, t.LikeCount, t.CreatedAt,
                t.IsHidden, t.RepliesLocked, t.IsPinned, t.IsEssence,
                t.ForumId, t.PendingReview))
            .ToListAsync();

        return new PagedResult<AdminThreadItemDto>(items, total, page, pageSize);
    }

    public async Task<(bool Ok, string? Error)> ApproveThreadReviewAsync(int adminId, int threadId)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (false, "帖子不存在");
        if (!thread.PendingReview) return (false, "该帖不在待审状态");
        thread.PendingReview = false;
        await LogAsync(adminId, "thread", threadId, "approve_review", null);
        await _notifications.AddSystemNotificationAsync(thread.AuthorId, $"你的帖子「{thread.Title}」已通过审核", thread.Id, thread.Title);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RejectThreadReviewAsync(int adminId, int threadId, string? reason)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (false, "帖子不存在");
        if (!thread.PendingReview) return (false, "该帖不在待审状态");
        thread.PendingReview = false;
        thread.IsHidden = true;
        await LogAsync(adminId, "thread", threadId, "reject_review", reason);
        var msg = string.IsNullOrWhiteSpace(reason)
            ? $"你的帖子「{thread.Title}」未通过审核"
            : $"你的帖子「{thread.Title}」未通过审核：{reason.Trim()}";
        await _notifications.AddSystemNotificationAsync(thread.AuthorId, msg, thread.Id, thread.Title);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<BatchResultDto> BatchApproveThreadReviewAsync(int adminId, List<int> ids)
    {
        var ok = 0;
        var fail = 0;
        foreach (var id in ids.Distinct().Take(50))
        {
            var (success, _) = await ApproveThreadReviewAsync(adminId, id);
            if (success) ok++; else fail++;
        }
        return new BatchResultDto(ok, fail, $"已通过 {ok} 条" + (fail > 0 ? $"，失败 {fail} 条" : ""));
    }

    public async Task<BatchResultDto> BatchRejectThreadReviewAsync(int adminId, List<int> ids, string? reason)
    {
        var ok = 0;
        var fail = 0;
        foreach (var id in ids.Distinct().Take(50))
        {
            var (success, _) = await RejectThreadReviewAsync(adminId, id, reason);
            if (success) ok++; else fail++;
        }
        return new BatchResultDto(ok, fail, $"已驳回 {ok} 条" + (fail > 0 ? $"，失败 {fail} 条" : ""));
    }

    public async Task<(bool Ok, string? Error)> MoveThreadAsync(int adminId, int threadId, int forumId)
    {
        var thread = await _db.Threads.Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (false, "帖子不存在");
        if (thread.ForumId == forumId) return (false, "已在目标版块");
        var dest = await _db.Forums.FindAsync(forumId);
        if (dest == null) return (false, "目标版块不存在");

        var src = thread.Forum;
        var postCount = await _db.Posts.CountAsync(p => p.ThreadId == threadId && !p.IsDeleted);
        src.ThreadCount = Math.Max(0, src.ThreadCount - 1);
        src.PostCount = Math.Max(0, src.PostCount - Math.Max(1, postCount));
        dest.ThreadCount += 1;
        dest.PostCount += Math.Max(1, postCount);
        thread.ForumId = forumId;
        await LogAsync(adminId, "thread", threadId, "move", $"forum:{src.Id}->{forumId}");
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteThreadAsync(int id)
    {
        var thread = await _db.Threads.Include(t => t.Forum).Include(t => t.Posts)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (thread == null) return (false, "帖子不存在");

        thread.Forum.ThreadCount = Math.Max(0, thread.Forum.ThreadCount - 1);
        thread.Forum.PostCount = Math.Max(0, thread.Forum.PostCount - thread.Posts.Count);
        _db.Threads.Remove(thread);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<AdminCategoryDto>> GetCategoriesAsync()
    {
        var cats = await _db.Categories.Include(c => c.Forums).OrderBy(c => c.SortOrder).ToListAsync();
        return cats.Select(c => new AdminCategoryDto(
            c.Id, c.Name, c.Icon,
            c.Forums.OrderBy(f => f.SortOrder).Select(f => new AdminForumDto(
                f.Id, f.Name, f.Icon, f.Description, f.ThreadCount, f.PostCount,
                f.MinVipTier, f.MinVipTier <= 0 ? "所有人" : VipAccess.TierLabel(f.MinVipTier) + "可见")).ToList()
        )).ToList();
    }

    public async Task<AdminCategoryDto> CreateCategoryAsync(CreateCategoryRequest? req)
    {
        var maxSort = await _db.Categories.MaxAsync(c => (int?)c.SortOrder) ?? 0;
        var name = string.IsNullOrWhiteSpace(req?.Name) ? "新分类" : req!.Name!.Trim();
        if (name.Length > 64) name = name[..64];
        var icon = string.IsNullOrWhiteSpace(req?.Icon) ? "📁" : req!.Icon!.Trim();
        if (icon.Length > 16) icon = icon[..16];

        var cat = new Category
        {
            Name = name,
            Icon = icon,
            SortOrder = maxSort + 1,
            IsCollapsedDefault = false
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return new AdminCategoryDto(cat.Id, cat.Name, cat.Icon, new List<AdminForumDto>());
    }

    public async Task<(AdminCategoryDto? Result, string? Error)> UpdateCategoryAsync(int id, UpdateCategoryRequest req)
    {
        var cat = await _db.Categories.Include(c => c.Forums).FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return (null, "分类不存在");
        if (!string.IsNullOrWhiteSpace(req.Name)) cat.Name = req.Name.Trim();
        if (req.Icon != null) cat.Icon = req.Icon;
        await _db.SaveChangesAsync();
        return (new AdminCategoryDto(cat.Id, cat.Name, cat.Icon,
            cat.Forums.OrderBy(f => f.SortOrder).Select(f => new AdminForumDto(
                f.Id, f.Name, f.Icon, f.Description, f.ThreadCount, f.PostCount,
                f.MinVipTier, f.MinVipTier <= 0 ? "所有人" : VipAccess.TierLabel(f.MinVipTier) + "可见")).ToList()), null);
    }

    public async Task<(bool Ok, string? Error)> DeleteCategoryAsync(int id)
    {
        var cat = await _db.Categories.Include(c => c.Forums).FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return (false, "分类不存在");
        if (cat.Forums.Any()) return (false, "请先删除分类下的版块");
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(AdminForumDto? Result, string? Error)> CreateForumAsync(CreateForumRequest req)
    {
        var cat = await _db.Categories.FindAsync(req.CategoryId);
        if (cat == null) return (null, "分类不存在");
        var maxSort = await _db.Forums.Where(f => f.CategoryId == cat.Id).MaxAsync(f => (int?)f.SortOrder) ?? 0;
        var forum = new Forum
        {
            CategoryId = cat.Id,
            Name = string.IsNullOrWhiteSpace(req.Name) ? "新版块" : req.Name.Trim(),
            Icon = string.IsNullOrWhiteSpace(req.Icon) ? "📁" : req.Icon,
            Description = req.Description?.Trim() ?? "",
            SortOrder = maxSort + 1,
            MinVipTier = Math.Clamp(req.MinVipTier, 0, VipAccess.TierLifetime)
        };
        _db.Forums.Add(forum);
        await _db.SaveChangesAsync();
        var label = forum.MinVipTier <= 0 ? "所有人" : VipAccess.TierLabel(forum.MinVipTier) + "可见";
        return (new AdminForumDto(forum.Id, forum.Name, forum.Icon, forum.Description, 0, 0, forum.MinVipTier, label), null);
    }

    public async Task<(AdminForumDto? Result, string? Error)> UpdateForumAsync(int id, UpdateForumRequest req)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum == null) return (null, "版块不存在");
        if (!string.IsNullOrWhiteSpace(req.Name)) forum.Name = req.Name.Trim();
        if (req.Icon != null) forum.Icon = req.Icon;
        if (req.Description != null) forum.Description = req.Description;
        if (req.MinVipTier.HasValue)
            forum.MinVipTier = Math.Clamp(req.MinVipTier.Value, 0, VipAccess.TierLifetime);
        await _db.SaveChangesAsync();
        var label = forum.MinVipTier <= 0 ? "所有人" : VipAccess.TierLabel(forum.MinVipTier) + "可见";
        return (new AdminForumDto(forum.Id, forum.Name, forum.Icon, forum.Description, forum.ThreadCount, forum.PostCount, forum.MinVipTier, label), null);
    }

    public async Task<(bool Ok, string? Error)> DeleteForumAsync(int id)
    {
        var forum = await _db.Forums.FindAsync(id);
        if (forum == null) return (false, "版块不存在");
        if (await _db.Threads.AnyAsync(t => t.ForumId == id))
            return (false, "版块下仍有帖子，无法删除");
        _db.Forums.Remove(forum);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<AdminSignInStatsDto> GetSignInStatsAsync()
    {
        var today = ChinaTime.Today;
        var todayCount = await _db.SignInRecords.CountAsync(r => r.SignInDate == today);
        var users = await _db.Users.ToListAsync();
        var signTotals = await _db.SignInRecords
            .GroupBy(r => r.UserId)
            .Select(g => new { g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.C);

        var dist = new Dictionary<string, int>
        {
            ["0"] = 0, ["1-6"] = 0, ["7-13"] = 0, ["14-29"] = 0, ["30+"] = 0
        };

        var top = new List<AdminSignInTopUserDto>();
        foreach (var u in users)
        {
            var cons = u.ConsecutiveSignInDays;
            var bucket = cons >= 30 ? "30+" : cons >= 14 ? "14-29" : cons >= 7 ? "7-13" : cons >= 1 ? "1-6" : "0";
            dist[bucket] = dist.GetValueOrDefault(bucket) + 1;
            top.Add(new AdminSignInTopUserDto(u.Id, u.Username, u.Nickname, cons, signTotals.GetValueOrDefault(u.Id)));
        }

        return new AdminSignInStatsDto(
            todayCount,
            dist,
            top.OrderByDescending(t => t.TotalDays).Take(10).ToList());
    }

    public async Task<(bool Ok, string? Error)> SetThreadHiddenAsync(int adminId, int threadId, bool hidden, string? reason)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (false, "帖子不存在");
        thread.IsHidden = hidden;
        await LogAsync(adminId, "thread", threadId, hidden ? "hide" : "unhide", reason);
        await _db.SaveChangesAsync();
        if (hidden)
        {
            var content = string.IsNullOrWhiteSpace(reason)
                ? $"你的帖子「{thread.Title}」已被管理员隐藏"
                : $"你的帖子「{thread.Title}」已被隐藏：{reason.Trim()}";
            await _notifications.AddSystemNotificationAsync(thread.AuthorId, content, thread.Id, thread.Title);
            await _db.SaveChangesAsync();
        }
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetRepliesLockedAsync(int adminId, int threadId, bool locked, string? reason)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (false, "帖子不存在");
        thread.RepliesLocked = locked;
        await LogAsync(adminId, "thread", threadId, locked ? "lock_replies" : "unlock_replies", reason);
        await _db.SaveChangesAsync();
        if (locked)
        {
            var content = string.IsNullOrWhiteSpace(reason)
                ? $"你的帖子「{thread.Title}」已被禁止回复"
                : $"你的帖子「{thread.Title}」已被禁止回复：{reason.Trim()}";
            await _notifications.AddSystemNotificationAsync(thread.AuthorId, content, thread.Id, thread.Title);
            await _db.SaveChangesAsync();
        }
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> SetThreadPinnedAsync(int adminId, int threadId, bool pinned, string? reason)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (false, "帖子不存在");
        thread.IsPinned = pinned;
        thread.PinnedUntil = null; // 管理置顶为永久；取消时一并清空
        await LogAsync(adminId, "thread", threadId, pinned ? "pin" : "unpin", reason);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error, string? Message)> SetThreadEssenceAsync(int adminId, int threadId, bool essence, string? reason)
    {
        var thread = await _db.Threads.Include(t => t.Author).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (false, "帖子不存在", null);
        if (thread.IsHidden && essence) return (false, "已拉黑的帖子不能设为精品", null);

        thread.IsEssence = essence;
        string? msg = essence ? "已设为精品" : "已取消精品";

        if (essence && !thread.EssenceAwarded)
        {
            await _rewards.TryAwardPointsAsync(
                thread.Author, EssencePointsReward, "essence_award", "thread", thread.Id);
            await _rewards.AwardCoinsAsync(
                thread.Author, EssenceCoinsReward, "essence_award", "thread", thread.Id);
            thread.EssenceAwarded = true;
            msg = $"已设为精品，作者获得 +{EssencePointsReward} 积分、+{EssenceCoinsReward} 金币";
            await _community.OnEssenceAwardedAsync(thread.AuthorId);
        }

        await LogAsync(adminId, "thread", threadId, essence ? "essence" : "unessence", reason);
        await _db.SaveChangesAsync();
        if (essence)
        {
            var content = thread.EssenceAwarded && msg!.Contains("积分")
                ? $"恭喜！你的帖子「{thread.Title}」被设为精品，获得 +{EssencePointsReward} 积分、+{EssenceCoinsReward} 金币"
                : $"你的帖子「{thread.Title}」被设为精品";
            await _notifications.AddSystemNotificationAsync(thread.AuthorId, content, thread.Id, thread.Title);
            await _db.SaveChangesAsync();
        }
        return (true, null, msg);
    }

    public async Task<(bool Ok, string? Error)> MuteUserAsync(int adminId, int userId, int? days, string? reason)
    {
        if (adminId == userId) return (false, "不能禁言自己");
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "用户不存在");
        if (user.IsAdmin) return (false, "不能禁言管理员");

        user.IsMuted = true;
        user.MuteReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        user.MutedUntil = days is > 0 ? ChinaTime.Now.AddDays(days.Value) : null;
        await LogAsync(adminId, "user", userId, "mute", reason);
        await _db.SaveChangesAsync();

        var duration = days is > 0 ? $"{days} 天" : "永久";
        var muteMsg = string.IsNullOrWhiteSpace(reason)
            ? $"你的账号已被禁言（{duration}）"
            : $"你的账号已被禁言（{duration}）：{reason.Trim()}";
        await _notifications.AddSystemNotificationAsync(userId, muteMsg);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UnmuteUserAsync(int adminId, int userId, string? reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "用户不存在");
        user.IsMuted = false;
        user.MutedUntil = null;
        user.MuteReason = null;
        await LogAsync(adminId, "user", userId, "unmute", reason);
        await _db.SaveChangesAsync();
        await _notifications.AddSystemNotificationAsync(userId, "你的禁言已解除，可以正常发帖回帖了");
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<PagedResult<ModerationLogDto>> GetModerationLogsAsync(int page, int pageSize, string? targetType)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.ModerationLogs.Include(m => m.Admin).AsQueryable();
        if (!string.IsNullOrWhiteSpace(targetType))
            q = q.Where(m => m.TargetType == targetType.Trim().ToLowerInvariant());

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ModerationLogDto(
                m.Id, m.AdminId, m.Admin.Nickname, m.TargetType, m.TargetId,
                m.Action, m.Reason, m.CreatedAt))
            .ToListAsync();
        return new PagedResult<ModerationLogDto>(items, total, page, pageSize);
    }

    public async Task<List<HomeBannerDto>> ListBannersAsync(bool enabledOnly = false)
    {
        var q = _db.HomeBanners.AsQueryable();
        if (enabledOnly) q = q.Where(b => b.Enabled);
        return await q.OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new HomeBannerDto(b.Id, b.Title, b.ImageUrl, b.LinkUrl, b.SortOrder, b.Enabled, b.CreatedAt, b.UpdatedAt))
            .ToListAsync();
    }

    public async Task<(HomeBannerDto? Result, string? Error)> CreateBannerAsync(SaveHomeBannerRequest req)
    {
        var (ok, error, image, title, link) = ValidateBanner(req);
        if (!ok) return (null, error);

        var banner = new HomeBanner
        {
            Title = title!,
            ImageUrl = image!,
            LinkUrl = link,
            SortOrder = req.SortOrder,
            Enabled = req.Enabled,
            CreatedAt = ChinaTime.Now,
            UpdatedAt = ChinaTime.Now,
        };
        _db.HomeBanners.Add(banner);
        await _db.SaveChangesAsync();
        return (ToBannerDto(banner), null);
    }

    public async Task<(HomeBannerDto? Result, string? Error)> UpdateBannerAsync(int id, SaveHomeBannerRequest req)
    {
        var banner = await _db.HomeBanners.FindAsync(id);
        if (banner == null) return (null, "广告不存在");

        var (ok, error, image, title, link) = ValidateBanner(req);
        if (!ok) return (null, error);

        banner.Title = title!;
        banner.ImageUrl = image!;
        banner.LinkUrl = link;
        banner.SortOrder = req.SortOrder;
        banner.Enabled = req.Enabled;
        banner.UpdatedAt = ChinaTime.Now;
        await _db.SaveChangesAsync();
        return (ToBannerDto(banner), null);
    }

    public async Task<(bool Ok, string? Error)> DeleteBannerAsync(int id)
    {
        var banner = await _db.HomeBanners.FindAsync(id);
        if (banner == null) return (false, "广告不存在");
        _db.HomeBanners.Remove(banner);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static (bool Ok, string? Error, string? Image, string? Title, string? Link) ValidateBanner(SaveHomeBannerRequest req)
    {
        var title = (req.Title ?? "").Trim();
        if (title.Length is < 1 or > 60) return (false, "标题长度 1–60", null, null, null);

        var image = (req.ImageUrl ?? "").Trim();
        if (string.IsNullOrEmpty(image)) return (false, "请上传或填写图片", null, null, null);
        if (image.Length > 900_000) return (false, "图片过大，请压缩后上传", null, null, null);
        if (!(image.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
              || image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
              || image.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
              || image.StartsWith("/")))
            return (false, "图片地址无效", null, null, null);

        var link = string.IsNullOrWhiteSpace(req.LinkUrl) ? null : req.LinkUrl.Trim();
        if (link is { Length: > 300 }) return (false, "链接过长", null, null, null);

        return (true, null, image, title, link);
    }

    private static HomeBannerDto ToBannerDto(HomeBanner b) =>
        new(b.Id, b.Title, b.ImageUrl, b.LinkUrl, b.SortOrder, b.Enabled, b.CreatedAt, b.UpdatedAt);

    private Task LogAsync(int adminId, string targetType, int targetId, string action, string? reason)
    {
        _db.ModerationLogs.Add(new ModerationLog
        {
            AdminId = adminId,
            TargetType = targetType,
            TargetId = targetId,
            Action = action,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = ChinaTime.Now
        });
        return Task.CompletedTask;
    }

    public async Task<(LevelRuleDto? Result, string? Error)> UpdateLevelAsync(int id, UpdateLevelRequest req)
    {
        var rule = await _db.LevelRules.FindAsync(id);
        if (rule == null) return (null, "等级规则不存在");

        if (!string.IsNullOrWhiteSpace(req.Name))
            rule.Name = req.Name.Trim();
        if (req.MinPoints.HasValue)
            rule.MinPoints = req.MinPoints.Value;

        await _db.SaveChangesAsync();
        _levels.InvalidateCache();

        var rules = await _levels.GetRuleDtosAsync();
        var updated = rules.FirstOrDefault(r => r.Level == rule.Level);
        return (updated, null);
    }

    // ── Tags ──────────────────────────────────────────────

    public async Task<List<AdminTagDto>> GetTagsAsync()
    {
        var tags = await _db.Tags
            .Select(t => new { t.Id, t.Name, Count = t.ThreadTags.Count })
            .OrderByDescending(t => t.Count)
            .ToListAsync();
        return tags.Select(t => new AdminTagDto(t.Id, t.Name, t.Count)).ToList();
    }

    public async Task<(bool Ok, string? Error)> RenameTagAsync(int id, string name)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag == null) return (false, "标签不存在");
        tag.Name = name.Trim();
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteTagAsync(int id)
    {
        var tag = await _db.Tags.Include(t => t.ThreadTags).FirstOrDefaultAsync(t => t.Id == id);
        if (tag == null) return (false, "标签不存在");
        _db.ThreadTags.RemoveRange(tag.ThreadTags);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ── Posts (Reply Management) ──────────────────────────

    public async Task<PagedResult<AdminPostDto>> GetPostsAsync(int page, int pageSize, string? search)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Posts.Where(p => p.Floor > 1).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p => p.Content.Contains(s) || p.Author.Nickname.Contains(s));
        }
        var total = await q.CountAsync();
        var raw = await q.OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new { p.Id, p.ThreadId, ThreadTitle = p.Thread.Title, p.Floor, p.Content, p.AuthorId, AuthorNickname = p.Author.Nickname, AuthorLevel = p.Author.Level, p.CreatedAt })
            .ToListAsync();
        var items = raw.Select(p => new AdminPostDto(
            p.Id, p.ThreadId, p.ThreadTitle, p.Floor,
            string.IsNullOrEmpty(p.Content) ? "" : (p.Content.Length > 200 ? p.Content.Substring(0, 200) + "…" : p.Content),
            p.AuthorId, p.AuthorNickname, p.AuthorLevel, p.CreatedAt))
            .ToList();
        return new PagedResult<AdminPostDto>(items, total, page, pageSize);
    }

    public async Task<(bool Ok, string? Error)> DeletePostAsync(int id)
    {
        var post = await _db.Posts.Include(p => p.Thread).FirstOrDefaultAsync(p => p.Id == id);
        if (post == null) return (false, "回复不存在");
        if (post.Floor <= 1) return (false, "不能删除主题帖，请在帖子管理操作");
        _db.Posts.Remove(post);
        post.Thread.ReplyCount = Math.Max(0, post.Thread.ReplyCount - 1);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ── Notification Broadcast ──────────────────────────

    public async Task<(bool Ok, string? Error)> BroadcastNotificationAsync(string content, int? userId = null)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
            return (false, "通知内容不能为空且不超过 500 字");
        if (userId.HasValue)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId.Value,
                Type = "system",
                ThreadId = 0,
                ThreadTitle = "系统通知",
                FromUserId = 0,
                FromNickname = "系统",
                Content = content.Trim(),
                CreatedAt = ChinaTime.Now
            });
        }
        else
        {
            var allUserIds = await _db.Users.Where(u => !u.IsAdmin).Select(u => u.Id).ToListAsync();
            foreach (var uid in allUserIds)
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = uid,
                    Type = "system",
                    ThreadId = 0,
                    ThreadTitle = "系统通知",
                    FromUserId = 0,
                    FromNickname = "系统",
                    Content = content.Trim(),
                    CreatedAt = ChinaTime.Now
                });
            }
        }
        await _db.SaveChangesAsync();
        return (true, null);
    }

    // ── Silent users / recall ───────────────────────────

    public async Task<PagedResult<SilentUserDto>> GetSilentUsersAsync(int days, int page, int pageSize)
    {
        days = Math.Clamp(days, 3, 365);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var cutoff = ChinaTime.Now.AddDays(-days);

        // LastActiveAt ?? LastSignInDate ?? CreatedAt < cutoff
        var query = _db.Users.Where(u => !u.IsAdmin).Where(u =>
            (u.LastActiveAt ?? u.LastSignInDate ?? u.CreatedAt) < cutoff);

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.LastActiveAt ?? u.LastSignInDate ?? u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<SilentUserDto>();
        foreach (var u in users)
        {
            var last = u.LastActiveAt ?? u.LastSignInDate ?? u.CreatedAt;
            var silentDays = Math.Max(0, (int)(ChinaTime.Now - last).TotalDays);
            var ln = await _levels.GetLevelNameAsync(u.Level);
            items.Add(new SilentUserDto(
                u.Id, u.Username, u.Nickname, u.Level, ln, u.Points, u.Coins,
                last, silentDays, u.CreatedAt));
        }
        return new PagedResult<SilentUserDto>(items, total, page, pageSize);
    }

    public async Task<(BatchResultDto? Result, string? Error)> RecallUsersAsync(List<int> ids, string content)
    {
        if (ids == null || ids.Count == 0) return (null, "请选择用户");
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
            return (null, "召回内容不能为空且不超过 500 字");

        var unique = ids.Distinct().Take(200).ToList();
        var users = await _db.Users.Where(u => unique.Contains(u.Id) && !u.IsAdmin).Select(u => u.Id).ToListAsync();
        var ok = 0;
        foreach (var uid in users)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = uid,
                Type = "system",
                ThreadId = 0,
                ThreadTitle = "召回通知",
                FromUserId = 0,
                FromNickname = "系统",
                Content = content.Trim(),
                CreatedAt = ChinaTime.Now
            });
            ok++;
        }
        await _db.SaveChangesAsync();
        return (new BatchResultDto(ok, unique.Count - ok, $"已发送召回通知 {ok} 人"), null);
    }

    // ── Points/Coins Ledger ──────────────────────────────

    public async Task<PagedResult<LedgerEntryDto>> GetLedgerAsync(int? userId, string? type, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var results = new List<LedgerEntryDto>();

        if (type == "coin" || type == null)
        {
            var q = _db.CoinLedgers.AsQueryable();
            if (userId.HasValue) q = q.Where(l => l.UserId == userId.Value);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new { l.Id, l.UserId, l.Delta, l.Reason, l.RefType, l.RefId, l.CreatedAt, Nickname = l.User.Nickname })
                .ToListAsync();
            results.AddRange(items.Select(l => new LedgerEntryDto(l.Id, l.UserId, l.Nickname, l.Delta, l.Reason, "coin", l.RefType, l.RefId, l.CreatedAt)));
        }

        if (type == "point" || type == null)
        {
            var q = _db.PointLedgers.AsQueryable();
            if (userId.HasValue) q = q.Where(l => l.UserId == userId.Value);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new { l.Id, l.UserId, l.Delta, l.Reason, l.RefType, l.RefId, l.CreatedAt, Nickname = l.User.Nickname })
                .ToListAsync();
            results.AddRange(items.Select(l => new LedgerEntryDto(l.Id, l.UserId, l.Nickname, l.Delta, l.Reason, "point", l.RefType, l.RefId, l.CreatedAt)));
        }

        return new PagedResult<LedgerEntryDto>(results.OrderByDescending(r => r.CreatedAt).ToList(), results.Count, page, pageSize);
    }

    // ── Invite Code Management ───────────────────────────

    public async Task<List<AdminInviteDto>> GetInviteCodesAsync()
    {
        var users = await _db.Users
            .Where(u => !string.IsNullOrEmpty(u.InviteCode))
            .Select(u => new AdminInviteDto(u.Id, u.Nickname, u.InviteCode!, u.CreatedAt,
                _db.Users.Count(i => i.InvitedByUserId == u.Id)))
            .OrderByDescending(d => d.UsedCount)
            .ToListAsync();
        return users;
    }

    public async Task<string> RegenerateInviteCodeAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) throw new InvalidOperationException("用户不存在");
        user.InviteCode = GenerateInviteCode();
        await _db.SaveChangesAsync();
        return user.InviteCode;
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rand = Random.Shared;
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rand.Next(chars.Length)]).ToArray());
    }

    // ── Site Settings ────────────────────────────────────

    public async Task<Dictionary<string, string>> GetSettingsAsync()
    {
        return await _db.SiteSettings
            .Select(s => new { s.Key, s.Value })
            .ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    public async Task UpdateSettingsAsync(Dictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
        {
            var existing = await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key);
            if (existing != null)
                existing.Value = value;
            else
                _db.SiteSettings.Add(new SiteSetting { Key = key, Value = value });
        }
        await _db.SaveChangesAsync();
        SiteSettingsService.Invalidate();
    }

    public async Task<string> ExportUsersCsvAsync()
    {
        var users = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.Username, u.Nickname, u.Level, u.Points, u.Coins, u.IsAdmin, u.IsMuted, u.CreatedAt })
            .ToListAsync();
        var lines = new List<string> { "ID,用户名,昵称,等级,积分,金币,管理员,禁言,注册时间" };
        lines.AddRange(users.Select(u => $"{u.Id},{EscapeCsv(u.Username)},{EscapeCsv(u.Nickname)},{u.Level},{u.Points},{u.Coins},{u.IsAdmin},{u.IsMuted},{u.CreatedAt:yyyy-MM-dd HH:mm:ss}"));
        return string.Join("\n", lines);
    }

    public async Task<string> ExportThreadsCsvAsync()
    {
        var threads = await _db.Threads
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.Id, t.Title, t.Type, t.Views, t.ReplyCount, t.LikeCount, t.CreatedAt, AuthorNickname = t.Author.Nickname, ForumName = t.Forum.Name })
            .ToListAsync();
        var lines = new List<string> { "ID,标题,类型,作者,版块,回复,浏览,点赞,创建时间" };
        lines.AddRange(threads.Select(t => $"{t.Id},{EscapeCsv(t.Title)},{t.Type},{EscapeCsv(t.AuthorNickname)},{EscapeCsv(t.ForumName)},{t.ReplyCount},{t.Views},{t.LikeCount},{t.CreatedAt:yyyy-MM-dd HH:mm:ss}"));
        return string.Join("\n", lines);
    }

    private static string EscapeCsv(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
