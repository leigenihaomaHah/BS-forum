using System.Text.RegularExpressions;
using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class AuthService
{
    private static readonly SignInMilestoneDto[] Milestones =
    [
        new(7, 5, 0, "连续 7 天"),
        new(14, 10, 2, "连续 14 天"),
        new(21, 15, 3, "连续 21 天"),
        new(30, 30, 5, "连续 30 天（满贯）"),
    ];

    private readonly AppDbContext _db;
    private readonly JwtHelper _jwt;
    private readonly LevelService _levels;
    private readonly CommunityService _community;
    private readonly CaptchaService _captcha;
    private readonly SiteSettingsService _settings;
    private readonly RateLimitService _rate;

    public AuthService(
        AppDbContext db, JwtHelper jwt, LevelService levels, CommunityService community,
        CaptchaService captcha, SiteSettingsService settings, RateLimitService rate)
    {
        _db = db;
        _jwt = jwt;
        _levels = levels;
        _community = community;
        _captcha = captcha;
        _settings = settings;
        _rate = rate;
    }

    public async Task<(AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest req)
    {
        if (!await _settings.GetBoolAsync("allow_register", true))
            return (null, "当前暂停注册");

        var username = (req.Username ?? "").Trim();
        if (username.Length < 3 || username.Length > 20)
            return (null, "用户名长度需为 3-20 个字符");
        if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            return (null, "用户名仅支持字母、数字和下划线");

        var pwdError = PasswordRules.Validate(req.Password);
        if (pwdError != null)
            return (null, pwdError);

        if (!_captcha.Validate(req.CaptchaId, req.CaptchaCode))
            return (null, "验证码错误或已过期，请刷新后重试");

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (null, "用户名已存在");

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? username : req.Nickname.Trim(),
            Points = await _settings.GetIntAsync("default_points", 0),
            Coins = await _settings.GetIntAsync("default_coins", 10),
            Level = 1,
            InviteCode = ""
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        user.InviteCode = CommunityService.NewInviteCode(user.Id);
        await _community.ApplyInviteOnRegisterAsync(user, req.InviteCode);
        await _db.SaveChangesAsync();

        var token = _jwt.CreateToken(user);
        return (new AuthResponse(token, await ToUserDtoAsync(user)), null);
    }

    public async Task<(AuthResponse? Result, string? Error)> LoginAsync(LoginRequest req)
    {
        var username = (req.Username ?? "").Trim();
        var key = $"login:{username.ToLower()}";
        if (!_rate.TryAcquire(key, 15, TimeSpan.FromMinutes(15)))
            return (null, "登录尝试过多，请稍后再试");

        if (!string.IsNullOrWhiteSpace(req.CaptchaId) || !string.IsNullOrWhiteSpace(req.CaptchaCode))
        {
            if (!_captcha.Validate(req.CaptchaId, req.CaptchaCode))
                return (null, "验证码错误或已过期，请刷新后重试");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return (null, "用户名或密码错误");

        var token = _jwt.CreateToken(user);
        return (new AuthResponse(token, await ToUserDtoAsync(user)), null);
    }

    public Task<(bool Success, string? Error)> ResetPasswordAsync(ResetPasswordRequest req)
        => Task.FromResult<(bool, string?)>((false, "出于安全考虑，已关闭昵称找回密码。请联系管理员在后台重置密码。"));

    public async Task<UserDto?> GetMeAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        return user == null ? null : await ToUserDtoAsync(user);
    }

    public async Task<(UserDto? Result, string? Error)> UpdateSettingsAsync(int userId, UpdateSettingsRequest req)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");

        if (req.Nickname != null)
        {
            var nick = req.Nickname.Trim();
            if (nick.Length is < 1 or > 20)
                return (null, "昵称长度需为 1-20 个字符");
            if (!string.Equals(nick, user.Nickname, StringComparison.Ordinal))
            {
                var card = await _db.UserInventories
                    .FirstOrDefaultAsync(i => i.UserId == userId && i.ItemType == "rename_card" && i.Quantity > 0);
                if (card == null)
                    return (null, "修改昵称需要改名卡，请先在商城兑换");
                card.Quantity -= 1;
                if (card.Quantity <= 0)
                    _db.UserInventories.Remove(card);
            }
            user.Nickname = nick;
        }

        if (!string.IsNullOrWhiteSpace(req.Password))
        {
            var pwdError = PasswordRules.Validate(req.Password);
            if (pwdError != null)
                return (null, pwdError);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }

        if (req.Avatar != null)
        {
            if (req.Avatar.Length > 800_000)
                return (null, "头像过大，请压缩后重试");
            user.Avatar = string.IsNullOrWhiteSpace(req.Avatar) ? null : req.Avatar;
        }

        if (req.ShowPurchases.HasValue) user.ShowPurchases = req.ShowPurchases.Value;
        if (req.ShowFavorites.HasValue) user.ShowFavorites = req.ShowFavorites.Value;
        if (req.NotifyReply.HasValue) user.NotifyReply = req.NotifyReply.Value;
        if (req.NotifyMention.HasValue) user.NotifyMention = req.NotifyMention.Value;
        if (req.Email != null)
        {
            var email = req.Email.Trim();
            if (email.Length == 0)
                user.Email = null;
            else if (email.Length > 80 || !email.Contains('@') || email.Contains(' '))
                return (null, "邮箱格式无效");
            else
                user.Email = email;
        }

        await _db.SaveChangesAsync();
        return (await ToUserDtoAsync(user), null);
    }

    public async Task<SignInStatusDto?> GetSignInStatusAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;

        var today = ChinaTime.Today;
        var todaySignedIn = user.LastSignInDate?.Date == today;
        var totalDays = await _db.SignInRecords.CountAsync(r => r.UserId == userId);

        var year = today.Year;
        var month = today.Month;
        var thisMonth = await _db.SignInRecords
            .Where(r => r.UserId == userId && r.SignInDate.Year == year && r.SignInDate.Month == month)
            .OrderBy(r => r.SignInDate)
            .Select(r => r.SignInDate.ToString("yyyy-MM-dd"))
            .ToListAsync();

        var previewDays = todaySignedIn
            ? user.ConsecutiveSignInDays
            : (user.LastSignInDate?.Date == today.AddDays(-1) ? user.ConsecutiveSignInDays + 1 : 1);
        var (points, coins) = CalcSignInRewards(previewDays);

        SignInMilestoneDto? next = null;
        foreach (var m in Milestones)
        {
            if (user.ConsecutiveSignInDays < m.Days)
            {
                next = m with { DaysLeft = m.Days - user.ConsecutiveSignInDays };
                break;
            }
        }

        return new SignInStatusDto(
            todaySignedIn,
            user.ConsecutiveSignInDays,
            totalDays,
            thisMonth,
            new SignInRewardsDto(points, coins),
            next);
    }

    public async Task<(SignInResultDto? Result, string? Error)> SignInAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");

        var today = ChinaTime.Today;
        if (user.LastSignInDate?.Date == today)
            return (null, "今日已签到");

        var yesterday = today.AddDays(-1);
        if (user.LastSignInDate?.Date == yesterday)
            user.ConsecutiveSignInDays += 1;
        else
            user.ConsecutiveSignInDays = 1;

        var (points, coins) = CalcSignInRewards(user.ConsecutiveSignInDays);

        SignInMilestoneDto? milestone = Milestones.FirstOrDefault(m => m.Days == user.ConsecutiveSignInDays);
        if (milestone != null)
        {
            points += milestone.PointsBonus;
            coins += milestone.CoinsBonus;
        }

        user.LastSignInDate = ChinaTime.Now;
        user.Points += points;
        user.Coins += coins;

        _db.PointLedgers.Add(new PointLedger
        {
            UserId = user.Id,
            Delta = points,
            Reason = RewardService.ReasonSignIn
        });
        _db.CoinLedgers.Add(new CoinLedger
        {
            UserId = user.Id,
            Delta = coins,
            Reason = RewardService.ReasonSignIn
        });
        _db.SignInRecords.Add(new SignInRecord
        {
            UserId = user.Id,
            SignInDate = today,
            PointsAwarded = points,
            CoinsAwarded = coins
        });

        await _levels.RecalculateLevelAsync(user);
        await _db.SaveChangesAsync();
        await _community.OnSignInAsync(user.Id, user.ConsecutiveSignInDays);

        var totalDays = await _db.SignInRecords.CountAsync(r => r.UserId == userId);
        var badge = user.ConsecutiveSignInDays >= 30 ? "🏅 签到满贯" : null;

        return (new SignInResultDto(
            points, coins, user.ConsecutiveSignInDays, totalDays,
            milestone, badge, await ToUserDtoAsync(user)), null);
    }

    public async Task<UserDto> ToUserDtoAsync(User user)
    {
        if (user.IsMuted && user.MutedUntil.HasValue && user.MutedUntil.Value <= ChinaTime.Now)
        {
            user.IsMuted = false;
            user.MutedUntil = null;
            user.MuteReason = null;
            await _db.SaveChangesAsync();
        }

        var levelName = await _levels.GetLevelNameAsync(user.Level);
        var signedInToday = user.LastSignInDate?.Date == ChinaTime.Today;
        var role = user.IsAdmin ? "admin" : "user";
        var muted = user.IsEffectivelyMuted();
        CommunityService.ClearExpiredVip(user);
        if (string.IsNullOrEmpty(user.InviteCode))
        {
            user.InviteCode = CommunityService.NewInviteCode(user.Id);
            await _db.SaveChangesAsync();
        }
        return new UserDto(
            user.Id, user.Username, user.Nickname, user.Avatar,
            user.Points, user.Coins, user.Level, levelName,
            user.ConsecutiveSignInDays, signedInToday, user.IsAdmin, role,
            muted, muted ? user.MutedUntil : null,
            user.InviteCode, user.IsEffectivelyVip(), user.VipUntil,
            VipAccess.EffectiveTier(user), VipAccess.TierLabel(VipAccess.EffectiveTier(user)),
            user.LotteryTickets, user.AvatarFrame,
            user.ShowPurchases, user.ShowFavorites, user.Email,
            user.NotifyReply, user.NotifyMention);
    }

    public async Task<UserProfileDto?> GetProfileAsync(int userId, int? viewerId = null)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;
        CommunityService.ClearExpiredVip(user);
        var levelName = await _levels.GetLevelNameAsync(user.Level);
        var next = await _levels.GetNextLevelPointsAsync(user.Level, user.Points);
        var badges = await _db.UserBadges.Where(b => b.UserId == userId).Select(b => b.BadgeCode).ToListAsync();
        var followers = await _db.UserFollows.CountAsync(f => f.FolloweeId == userId);
        var following = await _db.UserFollows.CountAsync(f => f.FollowerId == userId);
        var followed = viewerId.HasValue && await _db.UserFollows.AnyAsync(f => f.FollowerId == viewerId && f.FolloweeId == userId);
        return new UserProfileDto(
            user.Id, user.Username, user.Nickname, user.Avatar,
            user.Points, user.Coins, user.Level, levelName, next,
            user.ConsecutiveSignInDays, user.CreatedAt,
            user.IsEffectivelyVip(), user.AvatarFrame,
            VipAccess.EffectiveTier(user), VipAccess.TierLabel(VipAccess.EffectiveTier(user)),
            badges, followers, following, followed);
    }

    public async Task<PagedResult<ActivityItemDto>> GetActivityAsync(
        int userId, int page = 1, int pageSize = 10, string? type = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var kind = (type ?? "").Trim().ToLowerInvariant();

        var query = _db.Posts
            .Include(p => p.Author)
            .Include(p => p.Thread).ThenInclude(t => t.Forum)
            .Include(p => p.Thread).ThenInclude(t => t.Author)
            .Include(p => p.ReplyToPost!).ThenInclude(r => r.Author)
            .Where(p => p.AuthorId == userId && !p.IsDeleted && !p.Thread.IsHidden && !p.Thread.PendingReview);

        if (kind == "reply")
            query = query.Where(p => p.Floor > 1);
        else if (kind is "thread" or "post")
            query = query.Where(p => p.Floor == 1);

        var total = await query.CountAsync();
        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = posts.Select(p =>
        {
            var content = p.Content ?? "";
            if (content.Length > 200) content = content[..200] + "…";
            string? replyTo = null;
            if (p.Floor > 1)
            {
                replyTo = p.ReplyToPost?.Author?.Nickname;
                if (string.IsNullOrWhiteSpace(replyTo))
                    replyTo = p.Thread.Author?.Nickname ?? "楼主";
            }
            return new ActivityItemDto(
                p.Floor == 1 ? "thread" : "reply",
                p.ThreadId,
                p.Thread.Title,
                p.Thread.Forum.Name,
                content,
                p.CreatedAt,
                p.Id,
                p.Floor,
                replyTo,
                p.Author.Nickname,
                p.Author.Avatar);
        }).ToList();

        return new PagedResult<ActivityItemDto>(items, total, page, pageSize);
    }

    private static (int Points, int Coins) CalcSignInRewards(int consecutiveDays)
    {
        var bonus = Math.Min(Math.Max(consecutiveDays - 1, 0), 5);
        return (5 + bonus, 2 + Math.Min(bonus / 2, 3));
    }
}
