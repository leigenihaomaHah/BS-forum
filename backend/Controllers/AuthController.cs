using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly NotificationService _notifications;

    public MeController(AuthService auth, NotificationService notifications)
    {
        _auth = auth;
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var me = await _auth.GetMeAsync(uid.Value);
        return me == null ? NotFound() : Ok(me);
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

    [HttpPut("notifications/read-all")]
    public async Task<ActionResult<ApiMessage>> MarkAllNotificationsRead([FromQuery] string? type = null)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var n = await _notifications.MarkAllReadAsync(uid.Value, type);
        return Ok(new ApiMessage($"已标记 {n} 条为已读"));
    }
}

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ThreadService _threads;

    public UsersController(AuthService auth, ThreadService threads)
    {
        _auth = auth;
        _threads = threads;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(int id)
    {
        var viewer = JwtHelper.GetUserId(User);
        var profile = await _auth.GetProfileAsync(id, viewer);
        return profile == null ? NotFound() : Ok(profile);
    }

    [HttpGet("{id:int}/activity")]
    public async Task<ActionResult<List<ActivityItemDto>>> GetActivity(int id)
    {
        if (await _auth.GetProfileAsync(id) == null) return NotFound(new ApiMessage("用户不存在"));
        return Ok(await _auth.GetActivityAsync(id));
    }

    [HttpGet("{id:int}/purchases")]
    public async Task<ActionResult<List<PurchaseHistoryDto>>> GetPurchases(int id)
    {
        if (await _auth.GetProfileAsync(id) == null) return NotFound(new ApiMessage("用户不存在"));
        return Ok(await _threads.GetPurchasesAsync(id));
    }

    [HttpGet("{id:int}/favorites")]
    public async Task<ActionResult<List<FavoriteItemDto>>> GetFavorites(int id)
    {
        if (await _auth.GetProfileAsync(id) == null) return NotFound(new ApiMessage("用户不存在"));
        return Ok(await _threads.GetFavoritesAsync(id));
    }
}
