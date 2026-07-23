using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly CaptchaService _captcha;

    public AuthController(AuthService auth, CaptchaService captcha)
    {
        _auth = auth;
        _captcha = captcha;
    }

    [HttpGet("captcha")]
    public ActionResult<CaptchaDto> Captcha()
    {
        var (id, image) = _captcha.Create();
        return Ok(new CaptchaDto(id, image));
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req)
    {
        var (result, error) = await _auth.RegisterAsync(req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req)
    {
        var (result, error) = await _auth.LoginAsync(req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiMessage>> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var (ok, error) = await _auth.ResetPasswordAsync(req);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("密码已重置，请使用新密码登录"));
    }
}

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ThreadService _threads;
    private readonly NotificationService _notifications;
    private readonly CommunityService _community;

    public MeController(AuthService auth, ThreadService threads, NotificationService notifications, CommunityService community)
    {
        _auth = auth;
        _threads = threads;
        _notifications = notifications;
        _community = community;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var me = await _auth.GetMeAsync(uid.Value);
        if (me != null)
            await _community.TryAwardProgressBadgesAsync(uid.Value);
        return me == null ? NotFound() : Ok(me);
    }

    [HttpGet("reports")]
    public async Task<ActionResult<PagedResult<MyReportItemDto>>> MyReports(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _community.GetMyReportsAsync(uid.Value, page, pageSize));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<UserDto>> UpdateSettings([FromBody] UpdateSettingsRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _auth.UpdateSettingsAsync(uid.Value, req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpGet("sign-in-status")]
    public async Task<ActionResult<SignInStatusDto>> SignInStatus()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var status = await _auth.GetSignInStatusAsync(uid.Value);
        return status == null ? NotFound() : Ok(status);
    }

    [HttpPost("sign-in")]
    public async Task<ActionResult<SignInResultDto>> SignIn()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _auth.SignInAsync(uid.Value);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<List<NotificationDto>>> Notifications([FromQuery] string? type = null)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _notifications.ListAsync(uid.Value, type));
    }

    [HttpGet("notifications/summary")]
    public async Task<ActionResult<NotificationSummaryDto>> NotificationSummary()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _notifications.GetSummaryAsync(uid.Value));
    }

    [HttpPut("notifications/{id:int}/read")]
    public async Task<ActionResult<ApiMessage>> MarkNotificationRead(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _notifications.MarkReadAsync(uid.Value, id);
        if (!ok) return NotFound(new ApiMessage(error!));
        return Ok(new ApiMessage("已标记为已读"));
    }

    [HttpGet("threads")]
    public async Task<ActionResult<PagedResult<ThreadListItemDto>>> MyThreads([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _threads.GetMyThreadsAsync(uid.Value, page, pageSize));
    }

    [HttpPut("notifications/read-all")]
    public async Task<ActionResult<ApiMessage>> MarkAllNotificationsRead([FromQuery] string? type = null)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var n = await _notifications.MarkAllReadAsync(uid.Value, type);
        return Ok(new ApiMessage($"已标记 {n} 条为已读"));
    }

    // ── Favorite folders ──

    [HttpGet("favorite-folders")]
    public async Task<ActionResult<List<FavoriteFolderDto>>> FavoriteFolders()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _threads.GetFavoriteFoldersAsync(uid.Value));
    }

    [HttpPost("favorite-folders")]
    public async Task<ActionResult<FavoriteFolderDto>> CreateFavoriteFolder([FromBody] CreateFavoriteFolderRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.CreateFavoriteFolderAsync(uid.Value, req.Name);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPut("favorite-folders/{id:int}")]
    public async Task<ActionResult<FavoriteFolderDto>> UpdateFavoriteFolder(int id, [FromBody] UpdateFavoriteFolderRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.UpdateFavoriteFolderAsync(uid.Value, id, req.Name);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpDelete("favorite-folders/{id:int}")]
    public async Task<ActionResult<ApiMessage>> DeleteFavoriteFolder(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _threads.DeleteFavoriteFolderAsync(uid.Value, id);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpPut("favorites/{favoriteId:int}/move")]
    public async Task<ActionResult<ApiMessage>> MoveFavorite(int favoriteId, [FromBody] MoveFavoriteRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _threads.MoveFavoriteAsync(uid.Value, favoriteId, req.FolderId);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已移动"));
    }

    [HttpGet("favorites")]
    public async Task<ActionResult<List<FavoriteItemDto>>> MyFavorites([FromQuery] int? folderId = null)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _threads.GetFavoritesAsync(uid.Value, folderId));
    }
}

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ThreadService _threads;
    private readonly LevelService _levels;
    private readonly AppDbContext _db;

    public UsersController(AuthService auth, ThreadService threads, LevelService levels, AppDbContext db)
    {
        _auth = auth;
        _threads = threads;
        _levels = levels;
        _db = db;
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<UserDto>>> SearchUsers([FromQuery] string q, [FromQuery] int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(new List<UserDto>());
        var users = await _db.Users
            .Where(u => u.Username.Contains(q) || u.Nickname.Contains(q))
            .OrderBy(u => u.Username)
            .Take(limit)
            .ToListAsync();
        var result = new List<UserDto>();
        foreach (var u in users)
        {
            var levelName = await _levels.GetLevelNameAsync(u.Level);
            var role = u.IsAdmin ? "admin" : "user";
            CommunityService.ClearExpiredVip(u);
            result.Add(new UserDto(
                u.Id, u.Username, u.Nickname, u.Avatar, u.Points, u.Coins, u.Level, levelName,
                u.ConsecutiveSignInDays, false, u.IsAdmin, role,
                u.IsEffectivelyMuted(), u.MutedUntil, u.InviteCode ?? "", u.IsVip, u.VipUntil, u.VipTier,
                VipAccess.TierLabel(u.VipTier), u.LotteryTickets, u.AvatarFrame));
        }
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(int id)
    {
        var viewer = JwtHelper.GetUserId(User);
        var profile = await _auth.GetProfileAsync(id, viewer);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet("{id:int}/activity")]
    public async Task<ActionResult<PagedResult<ActivityItemDto>>> GetActivity(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? type = null)
    {
        if (await _auth.GetProfileAsync(id) == null) return NotFound(new ApiMessage("用户不存在"));
        return Ok(await _auth.GetActivityAsync(id, page, pageSize, type));
    }

    [Authorize]
    [HttpGet("{id:int}/purchases")]
    public async Task<ActionResult<PagedResult<PurchaseHistoryDto>>> GetPurchases(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var uid = JwtHelper.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var target = await _db.Users.FindAsync(id);
        if (target == null) return NotFound(new ApiMessage("用户不存在"));
        if (uid != id && !isAdmin && !target.ShowPurchases)
            return StatusCode(403, new ApiMessage("对方未公开该内容"));
        return Ok(await _threads.GetPurchasesAsync(id, page, pageSize));
    }

    [Authorize]
    [HttpGet("{id:int}/favorites")]
    public async Task<ActionResult<PagedResult<FavoriteItemDto>>> GetFavorites(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? folderId = null)
    {
        var uid = JwtHelper.GetUserId(User);
        var isAdmin = User.IsInRole("Admin");
        var target = await _db.Users.FindAsync(id);
        if (target == null) return NotFound(new ApiMessage("用户不存在"));
        if (uid != id && !isAdmin && !target.ShowFavorites)
            return StatusCode(403, new ApiMessage("对方未公开该内容"));
        return Ok(await _threads.GetFavoritesAsync(id, folderId, page, pageSize));
    }
}
