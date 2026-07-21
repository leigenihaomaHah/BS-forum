using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/lottery")]
public class LotteryController : ControllerBase
{
    private readonly LotteryService _lottery;

    public LotteryController(LotteryService lottery) => _lottery = lottery;

    [HttpGet("config")]
    [AllowAnonymous]
    public ActionResult<LotteryConfigDto> Config() => Ok(_lottery.GetConfig());

    [HttpGet("recent")]
    [AllowAnonymous]
    public async Task<ActionResult<List<LotteryRecentItemDto>>> Recent([FromQuery] int take = 20)
        => Ok(await _lottery.GetRecentAsync(take));

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<LotteryStatusDto>> Status()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var status = await _lottery.GetStatusAsync(uid.Value);
        return status == null ? NotFound() : Ok(status);
    }

    [HttpPost("spin")]
    [Authorize]
    public async Task<ActionResult<LotterySpinResultDto>> Spin()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _lottery.SpinAsync(uid.Value);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }
}
