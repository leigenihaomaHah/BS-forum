using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class RetentionController : ControllerBase
{
    private readonly RetentionService _retention;

    public RetentionController(RetentionService retention) => _retention = retention;

    [HttpGet("drafts")]
    public async Task<ActionResult<List<DraftListItemDto>>> Drafts()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _retention.ListDraftsAsync(uid.Value));
    }

    [HttpGet("drafts/{id:int}")]
    public async Task<ActionResult<DraftDto>> GetDraft(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var d = await _retention.GetDraftAsync(uid.Value, id);
        return d == null ? NotFound(new ApiMessage("草稿不存在")) : Ok(d);
    }

    [HttpGet("drafts/forum/{forumId:int}")]
    public async Task<ActionResult<DraftDto>> GetDraftByForum(int forumId)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var d = await _retention.GetDraftByForumAsync(uid.Value, forumId);
        return d == null ? NotFound() : Ok(d);
    }

    [HttpPost("drafts")]
    public async Task<ActionResult<DraftDto>> SaveDraft([FromBody] SaveDraftRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        try { return Ok(await _retention.SaveDraftAsync(uid.Value, req)); }
        catch (Exception ex) { return BadRequest(new ApiMessage(ex.Message)); }
    }

    [HttpDelete("drafts/{id:int}")]
    public async Task<IActionResult> DeleteDraft(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _retention.DeleteDraftAsync(uid.Value, id);
        if (!ok) return NotFound(new ApiMessage(error!));
        return Ok(new ApiMessage("已删除"));
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<HistoryItemDto>>> History([FromQuery] int take = 30)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _retention.ListHistoryAsync(uid.Value, take));
    }

    [HttpDelete("history")]
    public async Task<IActionResult> ClearHistory()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        await _retention.ClearHistoryAsync(uid.Value);
        return Ok(new ApiMessage("已清空"));
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<List<SubscriptionItemDto>>> Subscriptions()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _retention.ListSubscriptionsAsync(uid.Value));
    }

    [HttpGet("subscriptions/unread")]
    public async Task<ActionResult<List<UnreadThreadDto>>> SubscriptionUnread([FromQuery] int take = 15)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _retention.GetSubscriptionUnreadAsync(uid.Value, take));
    }
}

[ApiController]
[Route("api/forums")]
public class ForumSubscribeController : ControllerBase
{
    private readonly RetentionService _retention;

    public ForumSubscribeController(RetentionService retention) => _retention = retention;

    [Authorize]
    [HttpPost("{id:int}/subscribe")]
    public async Task<ActionResult<SubscribeResultDto>> Toggle(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _retention.ToggleSubscribeAsync(uid.Value, id);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        await _retention.MarkForumReadAsync(uid.Value, id);
        return Ok(new ApiMessage("已标记已读"));
    }

    [Authorize]
    [HttpGet("{id:int}/subscribed")]
    public async Task<ActionResult<object>> IsSubscribed(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(new { subscribed = await _retention.IsSubscribedAsync(uid.Value, id) });
    }
}
