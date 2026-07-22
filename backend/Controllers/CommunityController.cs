using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api")]
public class CommunityController : ControllerBase
{
    private readonly CommunityService _community;
    private readonly AdminService _admin;

    public CommunityController(CommunityService community, AdminService admin)
    {
        _community = community;
        _admin = admin;
    }

    [HttpGet("invite")]
    [Authorize]
    public async Task<ActionResult<InviteInfoDto>> Invite()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _community.GetInviteInfoAsync(uid.Value));
    }

    [HttpPost("users/{id:int}/follow")]
    [Authorize]
    public async Task<ActionResult<FollowResultDto>> Follow(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _community.ToggleFollowAsync(uid.Value, id);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("users/{id:int}/block")]
    [Authorize]
    public async Task<IActionResult> BlockUser(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _community.BlockUserAsync(uid.Value, id);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已屏蔽"));
    }

    [HttpDelete("users/{id:int}/block")]
    [Authorize]
    public async Task<IActionResult> UnblockUser(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        await _community.UnblockUserAsync(uid.Value, id);
        return Ok(new ApiMessage("已取消屏蔽"));
    }

    [HttpGet("users/blocked")]
    [Authorize]
    public async Task<ActionResult<List<UserDto>>> BlockedUsers()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _community.GetBlockedUsersAsync(uid.Value));
    }

    [HttpGet("feed")]
    [Authorize]
    public async Task<ActionResult<PagedResult<FeedItemDto>>> Feed(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] int? take = null)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        if (take.HasValue && !Request.Query.ContainsKey("page"))
            return Ok(await _community.GetFeedAsync(uid.Value, 1, take.Value));
        return Ok(await _community.GetFeedAsync(uid.Value, page, pageSize));
    }

    [HttpGet("shop")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ShopItemDto>>> Shop() => Ok(await _community.GetShopAsync());

    [HttpPost("shop/{id:int}/buy")]
    [Authorize]
    public async Task<ActionResult<ShopBuyResultDto>> Buy(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _community.BuyAsync(uid.Value, id);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpGet("tasks")]
    [Authorize]
    public async Task<ActionResult<List<TaskItemDto>>> Tasks()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _community.GetDailyTasksAsync(uid.Value));
    }

    [HttpPost("tasks/{code}/claim")]
    [Authorize]
    public async Task<ActionResult<TaskItemDto>> ClaimTask(string code)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _community.ClaimTaskAsync(uid.Value, code);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpGet("users/{id:int}/badges")]
    [AllowAnonymous]
    public async Task<ActionResult<List<BadgeDto>>> Badges(int id) => Ok(await _community.GetBadgesAsync(id));

    [HttpGet("tags/{name}")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<TagThreadItemDto>>> ThreadsByTag(
        string name, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (result, error) = await _community.GetThreadsByTagAsync(Uri.UnescapeDataString(name), page, pageSize);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("reports")]
    [Authorize]
    public async Task<IActionResult> Report([FromBody] ReportRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _community.CreateReportAsync(uid.Value, req);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("举报已提交"));
    }

    [HttpGet("admin/reports")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<ReportItemDto>>> AdminReports(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = "pending")
        => Ok(await _community.GetReportsAsync(page, pageSize, status));

    [HttpPost("admin/reports/{id:int}/handle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> HandleReport(int id, [FromBody] HandleReportRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _community.HandleReportAsync(uid.Value, id, req, _admin);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已处理"));
    }

    [HttpGet("admin/moderators")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ModeratorDto>>> Moderators()
        => Ok(await _community.ListModeratorsAsync());

    [HttpPost("admin/moderators")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddModerator([FromBody] SetModeratorRequest req)
    {
        var (ok, error) = await _community.AddModeratorAsync(req.ForumId, req.UserId);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已添加版主"));
    }

    [HttpDelete("admin/moderators")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveModerator([FromQuery] int forumId, [FromQuery] int userId)
    {
        var (ok, error) = await _community.RemoveModeratorAsync(forumId, userId);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已移除版主"));
    }
}
