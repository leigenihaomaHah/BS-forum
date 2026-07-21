using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private readonly SearchService _search;
    private readonly LevelService _levels;

    public SearchController(SearchService search, LevelService levels)
    {
        _search = search;
        _levels = levels;
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchResultDto>> Search(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? forumId = null,
        [FromQuery] string? type = null)
        => Ok(await _search.SearchAsync(q, page, pageSize, forumId, type, JwtHelper.GetUserId(User)));

    [HttpGet("levels")]
    public async Task<ActionResult<List<LevelRuleDto>>> Levels()
        => Ok(await _levels.GetRuleDtosAsync());
}
