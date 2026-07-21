using System.Text;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AdminService _admin;
    private readonly ImageMigrationService _migrator;

    public AdminController(AdminService admin, ImageMigrationService migrator)
    {
        _admin = admin;
        _migrator = migrator;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> Stats() => Ok(await _admin.GetStatsAsync());

    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserItemDto>>> Users(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] bool? muted = null)
        => Ok(await _admin.GetUsersAsync(page, pageSize, search, muted));

    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateAdminUserRequest req)
    {
        var (result, error) = await _admin.UpdateUserAsync(id, req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.DeleteUserAsync(id, uid.Value);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpPut("users/{id:int}/role")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _admin.UpdateRoleAsync(id, uid.Value, req.Role);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("users/{id:int}/mute")]
    public async Task<IActionResult> MuteUser(int id, [FromBody] MuteUserRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.MuteUserAsync(uid.Value, id, req?.Days, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已禁言"));
    }

    [HttpPost("users/{id:int}/unmute")]
    public async Task<IActionResult> UnmuteUser(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.UnmuteUserAsync(uid.Value, id, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已解除禁言"));
    }

    [HttpGet("threads")]
    public async Task<ActionResult<PagedResult<AdminThreadItemDto>>> Threads(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null, [FromQuery] string? status = null)
        => Ok(await _admin.GetThreadsAsync(page, pageSize, search, status));

    [HttpDelete("threads/{id:int}")]
    public async Task<IActionResult> DeleteThread(int id)
    {
        var (ok, error) = await _admin.DeleteThreadAsync(id);
        if (!ok) return NotFound(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpPost("threads/{id:int}/hide")]
    public async Task<IActionResult> HideThread(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.SetThreadHiddenAsync(uid.Value, id, true, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已拉黑"));
    }

    [HttpPost("threads/{id:int}/unhide")]
    public async Task<IActionResult> UnhideThread(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.SetThreadHiddenAsync(uid.Value, id, false, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已取消拉黑"));
    }

    [HttpPost("threads/{id:int}/lock-replies")]
    public async Task<IActionResult> LockReplies(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.SetRepliesLockedAsync(uid.Value, id, true, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已禁止回复"));
    }

    [HttpPost("threads/{id:int}/unlock-replies")]
    public async Task<IActionResult> UnlockReplies(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.SetRepliesLockedAsync(uid.Value, id, false, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已解除禁回"));
    }

    [HttpPost("threads/{id:int}/pin")]
    public async Task<IActionResult> PinThread(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.SetThreadPinnedAsync(uid.Value, id, true, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已置顶"));
    }

    [HttpPost("threads/{id:int}/unpin")]
    public async Task<IActionResult> UnpinThread(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _admin.SetThreadPinnedAsync(uid.Value, id, false, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已取消置顶"));
    }

    [HttpPost("threads/{id:int}/essence")]
    public async Task<IActionResult> EssenceThread(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error, message) = await _admin.SetThreadEssenceAsync(uid.Value, id, true, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage(message ?? "已设为精品"));
    }

    [HttpPost("threads/{id:int}/unessence")]
    public async Task<IActionResult> UnessenceThread(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error, message) = await _admin.SetThreadEssenceAsync(uid.Value, id, false, req?.Reason);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage(message ?? "已取消精品"));
    }

    [HttpGet("moderation-logs")]
    public async Task<ActionResult<PagedResult<ModerationLogDto>>> ModerationLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? targetType = null)
        => Ok(await _admin.GetModerationLogsAsync(page, pageSize, targetType));

    [HttpGet("categories")]
    public async Task<ActionResult<List<AdminCategoryDto>>> Categories()
        => Ok(await _admin.GetCategoriesAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest? req)
    {
        try
        {
            return Ok(await _admin.CreateCategoryAsync(req ?? new CreateCategoryRequest(null, null)));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiMessage($"创建分类失败: {ex.InnerException?.Message ?? ex.Message}"));
        }
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest? req)
    {
        var (result, error) = await _admin.UpdateCategoryAsync(id, req ?? new UpdateCategoryRequest(null, null));
        if (error != null) return NotFound(new ApiMessage(error));
        return Ok(result);
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var (ok, error) = await _admin.DeleteCategoryAsync(id);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpPost("forums")]
    public async Task<IActionResult> CreateForum([FromBody] CreateForumRequest? req)
    {
        if (req == null) return BadRequest(new ApiMessage("请求体无效"));
        try
        {
            var (result, error) = await _admin.CreateForumAsync(req);
            if (error != null) return BadRequest(new ApiMessage(error));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiMessage($"创建版块失败: {ex.InnerException?.Message ?? ex.Message}"));
        }
    }

    [HttpPut("forums/{id:int}")]
    public async Task<IActionResult> UpdateForum(int id, [FromBody] UpdateForumRequest req)
    {
        var (result, error) = await _admin.UpdateForumAsync(id, req);
        if (error != null) return NotFound(new ApiMessage(error));
        return Ok(result);
    }

    [HttpDelete("forums/{id:int}")]
    public async Task<IActionResult> DeleteForum(int id)
    {
        var (ok, error) = await _admin.DeleteForumAsync(id);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpGet("signin/stats")]
    public async Task<ActionResult<AdminSignInStatsDto>> SignInStats()
        => Ok(await _admin.GetSignInStatsAsync());

    [HttpGet("banners")]
    public async Task<ActionResult<List<HomeBannerDto>>> Banners()
        => Ok(await _admin.ListBannersAsync());

    [HttpPost("banners")]
    public async Task<ActionResult<HomeBannerDto>> CreateBanner([FromBody] SaveHomeBannerRequest req)
    {
        var (result, error) = await _admin.CreateBannerAsync(req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPut("banners/{id:int}")]
    public async Task<ActionResult<HomeBannerDto>> UpdateBanner(int id, [FromBody] SaveHomeBannerRequest req)
    {
        var (result, error) = await _admin.UpdateBannerAsync(id, req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpDelete("banners/{id:int}")]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var (ok, error) = await _admin.DeleteBannerAsync(id);
        if (!ok) return NotFound(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpPost("migrate-images")]
    public async Task<ActionResult<MigrationResult>> MigrateImages()
    {
        var result = await _migrator.MigrateAsync();
        return Ok(result);
    }

    [HttpPut("levels/{id:int}")]
    public async Task<IActionResult> UpdateLevel(int id, [FromBody] UpdateLevelRequest req)
    {
        var (result, error) = await _admin.UpdateLevelAsync(id, req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    // ── Tags ─────────────────────────────────────────────
    [HttpGet("tags")]
    public async Task<ActionResult<List<AdminTagDto>>> Tags() => Ok(await _admin.GetTagsAsync());

    [HttpPut("tags/{id:int}")]
    public async Task<IActionResult> RenameTag(int id, [FromBody] RenameTagRequest req)
    {
        var (ok, error) = await _admin.RenameTagAsync(id, req.Name);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已重命名"));
    }

    [HttpDelete("tags/{id:int}")]
    public async Task<IActionResult> DeleteTag(int id)
    {
        var (ok, error) = await _admin.DeleteTagAsync(id);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    // ── Posts (Reply Management) ─────────────────────────
    [HttpGet("posts")]
    public async Task<ActionResult<PagedResult<AdminPostDto>>> Posts(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
        => Ok(await _admin.GetPostsAsync(page, pageSize, search));

    [HttpDelete("posts/{id:int}")]
    public async Task<IActionResult> DeletePost(int id)
    {
        var (ok, error) = await _admin.DeletePostAsync(id);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    // ── Batch Operations ─────────────────────────────────
    [HttpPost("threads/batch")]
    public async Task<IActionResult> BatchThreadAction([FromBody] BatchThreadActionRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var results = new List<string>();
        foreach (var id in req.Ids)
        {
            try
            {
                switch (req.Action)
                {
                    case "hide":
                        var (hOk, hErr) = await _admin.SetThreadHiddenAsync(uid.Value, id, true, "批量操作");
                        if (hOk) results.Add($"#{id}: 已拉黑"); else results.Add($"#{id}: {hErr}");
                        break;
                    case "unhide":
                        var (uOk, uErr) = await _admin.SetThreadHiddenAsync(uid.Value, id, false, "批量操作");
                        if (uOk) results.Add($"#{id}: 已取消拉黑"); else results.Add($"#{id}: {uErr}");
                        break;
                    case "pin":
                        var (pOk, pErr) = await _admin.SetThreadPinnedAsync(uid.Value, id, true, "批量操作");
                        if (pOk) results.Add($"#{id}: 已置顶"); else results.Add($"#{id}: {pErr}");
                        break;
                    case "delete":
                        var (dOk, dErr) = await _admin.DeleteThreadAsync(id);
                        if (dOk) results.Add($"#{id}: 已删除"); else results.Add($"#{id}: {dErr}");
                        break;
                    default:
                        results.Add($"#{id}: 未知操作");
                        break;
                }
            }
            catch (Exception ex) { results.Add($"#{id}: {ex.Message}"); }
        }
        return Ok(new { results });
    }

    // ── Notification Broadcast ─────────────────────────
    [HttpPost("notifications/broadcast")]
    public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastNotificationRequest req)
    {
        var (ok, error) = await _admin.BroadcastNotificationAsync(req.Content, req.UserId);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("通知已发送"));
    }

    // ── Ledger ─────────────────────────────────────────
    [HttpGet("ledger")]
    public async Task<ActionResult<PagedResult<LedgerEntryDto>>> Ledger(
        [FromQuery] int? userId, [FromQuery] string? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _admin.GetLedgerAsync(userId, type, page, pageSize));

    // ── Invite Codes ───────────────────────────────────
    [HttpGet("invites")]
    public async Task<ActionResult<List<AdminInviteDto>>> Invites()
        => Ok(await _admin.GetInviteCodesAsync());

    [HttpPost("invites/{userId:int}/regenerate")]
    public async Task<IActionResult> RegenerateInvite(int userId)
    {
        try
        {
            var code = await _admin.RegenerateInviteCodeAsync(userId);
            return Ok(new ApiMessage($"已重新生成：{code}"));
        }
        catch (Exception ex) { return BadRequest(new ApiMessage(ex.Message)); }
    }

    // ── Site Settings ──────────────────────────────────
    [HttpGet("settings")]
    public async Task<ActionResult<Dictionary<string, string>>> Settings()
        => Ok(await _admin.GetSettingsAsync());

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] AdminUpdateSettingsRequest req)
    {
        await _admin.UpdateSettingsAsync(req.Settings);
        return Ok(new ApiMessage("设置已保存"));
    }

    // ── Data Export ────────────────────────────────────
    [HttpGet("export/users")]
    public async Task<IActionResult> ExportUsers()
    {
        var csv = await _admin.ExportUsersCsvAsync();
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"users_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("export/threads")]
    public async Task<IActionResult> ExportThreads()
    {
        var csv = await _admin.ExportThreadsCsvAsync();
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"threads_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
