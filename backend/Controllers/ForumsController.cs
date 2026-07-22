using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ForumQueryService _forums;

    public CategoriesController(ForumQueryService forums) => _forums = forums;

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll()
        => Ok(await _forums.GetCategoriesAsync(JwtHelper.GetUserId(User)));
}

[ApiController]
[Route("api/forums")]
public class ForumsController : ControllerBase
{
    private readonly ForumQueryService _forums;
    private readonly ThreadService _threads;

    public ForumsController(ForumQueryService forums, ThreadService threads)
    {
        _forums = forums;
        _threads = threads;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var (forum, error) = await _forums.GetForumAsync(id, JwtHelper.GetUserId(User));
        if (error != null) return StatusCode(403, new ApiMessage(error));
        return forum == null ? NotFound() : Ok(forum);
    }

    [HttpGet("{id:int}/threads")]
    public async Task<IActionResult> GetThreads(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "latest",
        [FromQuery] string? q = null)
    {
        var (ok, error) = await _forums.EnsureForumAccessAsync(id, JwtHelper.GetUserId(User));
        if (!ok) return StatusCode(403, new ApiMessage(error!));
        return Ok(await _threads.GetThreadsAsync(id, page, pageSize, sort, JwtHelper.GetUserId(User), q));
    }
}

[ApiController]
[Route("api/hot")]
public class HotController : ControllerBase
{
    private readonly HotService _hot;

    public HotController(HotService hot) => _hot = hot;

    [HttpGet]
    public async Task<ActionResult<List<HotThreadDto>>> Get([FromQuery] string period = "day")
        => Ok(await _hot.GetHotAsync(period));
}

[ApiController]
[Route("api/essence")]
public class EssenceController : ControllerBase
{
    private readonly HotService _hot;

    public EssenceController(HotService hot) => _hot = hot;

    [HttpGet]
    public async Task<ActionResult<List<EssenceThreadDto>>> Get([FromQuery] int take = 10)
        => Ok(await _hot.GetEssenceAsync(take));
}
