using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly ThreadService _threads;

    public PostsController(ThreadService threads) => _threads = threads;

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PostDto>> Update(int id, [FromBody] UpdatePostRequest req)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var (result, error) = await _threads.UpdatePostAsync(uid.Value, id, req);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiMessage>> Delete(int id)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();
        var isAdmin = User.IsInRole("Admin");
        var (message, error) = await _threads.DeletePostAsync(uid.Value, id, isAdmin);
        if (error != null) return BadRequest(new ApiMessage(error));
        return Ok(new ApiMessage(message!));
    }
}
