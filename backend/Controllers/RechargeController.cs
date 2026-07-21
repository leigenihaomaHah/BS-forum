using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/recharge")]
public class RechargeController : ControllerBase
{
    private readonly RechargeService _recharge;

    public RechargeController(RechargeService recharge) => _recharge = recharge;

    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<ActionResult<List<RechargePackageDto>>> Packages()
        => Ok(await _recharge.GetPackagesAsync());

    [HttpGet("orders/mine")]
    [Authorize]
    public async Task<ActionResult<List<RechargeOrderDto>>> MyOrders([FromQuery] int take = 20)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _recharge.GetMyOrdersAsync(uid.Value, take));
    }

    [HttpPost("orders")]
    [Authorize]
    public async Task<IActionResult> CreateOrder([FromBody] CreateRechargeOrderRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        if (req == null) return BadRequest(new ApiMessage("请求体无效"));
        var (result, error) = await _recharge.CreateOrderAsync(uid.Value, req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("orders/{id:int}/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _recharge.CancelOrderAsync(uid.Value, id, isAdmin: false);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已取消"));
    }

    [HttpPost("redeem")]
    [Authorize]
    public async Task<IActionResult> Redeem([FromBody] RedeemRechargeCardRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _recharge.RedeemCardAsync(uid.Value, req?.Code);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }
}

[ApiController]
[Route("api/admin/recharge")]
[Authorize(Roles = "Admin")]
public class AdminRechargeController : ControllerBase
{
    private readonly RechargeService _recharge;

    public AdminRechargeController(RechargeService recharge) => _recharge = recharge;

    [HttpGet("packages")]
    public async Task<ActionResult<List<RechargePackageDto>>> Packages()
        => Ok(await _recharge.GetPackagesAsync(enabledOnly: false));

    [HttpGet("orders")]
    public async Task<ActionResult<PagedResult<RechargeOrderDto>>> Orders(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
        => Ok(await _recharge.AdminOrdersAsync(page, pageSize, status));

    [HttpPost("orders/{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ModerationReasonRequest? req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _recharge.ConfirmOrderAsync(uid.Value, id, req?.Reason);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpPost("orders/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (ok, error) = await _recharge.CancelOrderAsync(uid.Value, id, isAdmin: true);
        if (!ok) return BadRequest(new ApiMessage(error!));
        return Ok(new ApiMessage("已取消"));
    }
}
