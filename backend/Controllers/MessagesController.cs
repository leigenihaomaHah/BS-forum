using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messages;
    private readonly RateLimitService _rate;

    public MessagesController(MessageService messages, RateLimitService rate)
    {
        _messages = messages;
        _rate = rate;
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<List<ConversationDto>>> Conversations()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(await _messages.ListConversationsAsync(uid.Value));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> UnreadCount()
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        return Ok(new { count = await _messages.UnreadCountAsync(uid.Value) });
    }

    [HttpGet("with/{peerId:int}")]
    public async Task<ActionResult<List<PrivateMessageDto>>> Thread(int peerId)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (items, error) = await _messages.GetThreadAsync(uid.Value, peerId);
        if (error != null) return NotFound(new ApiMessage(error));
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<PrivateMessageDto>> Send([FromBody] SendMessageRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var limited = _rate.CheckOrNull($"pm:{uid}", 30, TimeSpan.FromMinutes(1), "私信发送过快，请稍后再试");
        if (limited != null) return BadRequest(new ApiMessage(limited));

        var (result, error) = await _messages.SendAsync(uid.Value, req.ReceiverId, req.Content);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }
}
