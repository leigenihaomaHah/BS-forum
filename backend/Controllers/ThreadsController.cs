using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/threads")]
public class ThreadsController : ControllerBase
{
    private readonly ThreadService _threads;
    private readonly AdminService _admin;

    public ThreadsController(ThreadService threads, AdminService admin)
    {
        _threads = threads;
        _admin = admin;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ThreadDetailDto>> Get(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        var (result, error) = await _threads.GetThreadAsync(id, uid);
        if (error != null)
        {
            if (error.Contains("会员")) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error));
            return NotFound(new ApiMessage(error));
        }
        return Ok(result);
    }

    [HttpGet("{id:int}/posts")]
    public async Task<ActionResult<PagedResult<PostDto>>> GetPosts(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var uid = JwtHelper.GetUserId(User);
        return Ok(await _threads.GetPostsAsync(id, uid, page, pageSize));
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ThreadDetailDto>> Create([FromBody] CreateThreadRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.CreateThreadAsync(uid.Value, req);
        if (error != null)
        {
            if (error.Contains("会员")) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error));
            return BadRequest(new ApiMessage(error));
        }
        return Ok(result);
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ThreadDetailDto>> Update(int id, [FromBody] UpdateThreadRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.UpdateThreadAsync(uid.Value, id, req, User.IsInRole("Admin"));
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/replies")]
    public async Task<ActionResult<PostDto>> Reply(int id, [FromBody] CreateReplyRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.ReplyAsync(uid.Value, id, req);
        if (error != null)
        {
            if (error.Contains("会员")) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error));
            return BadRequest(new ApiMessage(error));
        }
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/purchase")]
    public async Task<ActionResult<PurchaseResultDto>> Purchase(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.PurchaseAsync(uid.Value, id);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/favorite")]
    public async Task<ActionResult<FavoriteResultDto>> Favorite(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.ToggleFavoriteAsync(uid.Value, id);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/tip")]
    public async Task<ActionResult<TipResultDto>> Tip(int id, [FromBody] TipRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.TipAsync(uid.Value, id, req.Amount);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/like")]
    public async Task<ActionResult<ApiMessage>> Like(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (message, error) = await _threads.LikeAsync(uid.Value, id);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(new ApiMessage(message!));
    }

    [Authorize]
    [HttpPost("{id:int}/vote")]
    public async Task<ActionResult<PollDto>> Vote(int id, [FromBody] VotePollRequest req, [FromServices] CommunityService community)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await community.VotePollAsync(uid.Value, id, req.OptionId);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [Authorize]
    [HttpPost("{id:int}/mod/pin")]
    public Task<IActionResult> ModPin(int id, [FromBody] ModerationActionRequest? req)
        => RunModAsync(id, uid => _admin.SetThreadPinnedAsync(uid, id, true, req?.Reason));

    [Authorize]
    [HttpPost("{id:int}/mod/unpin")]
    public Task<IActionResult> ModUnpin(int id, [FromBody] ModerationActionRequest? req)
        => RunModAsync(id, uid => _admin.SetThreadPinnedAsync(uid, id, false, req?.Reason));

    [Authorize]
    [HttpPost("{id:int}/mod/lock-replies")]
    public Task<IActionResult> ModLock(int id, [FromBody] ModerationActionRequest? req)
        => RunModAsync(id, uid => _admin.SetRepliesLockedAsync(uid, id, true, req?.Reason));

    [Authorize]
    [HttpPost("{id:int}/mod/unlock-replies")]
    public Task<IActionResult> ModUnlock(int id, [FromBody] ModerationActionRequest? req)
        => RunModAsync(id, uid => _admin.SetRepliesLockedAsync(uid, id, false, req?.Reason));

    [Authorize]
    [HttpPost("{id:int}/mod/hide")]
    public Task<IActionResult> ModHide(int id, [FromBody] ModerationActionRequest? req)
        => RunModAsync(id, uid => _admin.SetThreadHiddenAsync(uid, id, true, req?.Reason));

    [Authorize]
    [HttpPost("{id:int}/mod/essence")]
    public async Task<IActionResult> ModEssence(int id, [FromBody] ModerationActionRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _threads.EnsureCanModerateAsync(uid.Value, id);
        if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error!));
        var (ok2, error2, message) = await _admin.SetThreadEssenceAsync(uid.Value, id, true, req?.Reason);
        if (!ok2) return BadRequest(new ApiMessage(error2!));
        return Ok(new ApiMessage(message ?? "已设为精品"));
    }

    [Authorize]
    [HttpPost("{id:int}/mod/unessence")]
    public async Task<IActionResult> ModUnessence(int id, [FromBody] ModerationActionRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _threads.EnsureCanModerateAsync(uid.Value, id);
        if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error!));
        var (ok2, error2, message) = await _admin.SetThreadEssenceAsync(uid.Value, id, false, req?.Reason);
        if (!ok2) return BadRequest(new ApiMessage(error2!));
        return Ok(new ApiMessage(message ?? "已取消精品"));
    }

    [Authorize]
    [HttpPost("{id:int}/mod/move")]
    public async Task<IActionResult> ModMove(int id, [FromBody] MoveThreadRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _threads.EnsureCanModerateAsync(uid.Value, id);
        if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error!));
        var (ok2, error2) = await _admin.MoveThreadAsync(uid.Value, id, req.ForumId);
        if (!ok2) return BadRequest(new ApiMessage(error2!));
        return Ok(new ApiMessage("已移动版块"));
    }

    private async Task<IActionResult> RunModAsync(int threadId, Func<int, Task<(bool Ok, string? Error)>> action)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _threads.EnsureCanModerateAsync(uid.Value, threadId);
        if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(error!));
        var (ok2, error2) = await action(uid.Value);
        if (!ok2) return BadRequest(new ApiMessage(error2!));
        return Ok(new ApiMessage("操作成功"));
    }
}
