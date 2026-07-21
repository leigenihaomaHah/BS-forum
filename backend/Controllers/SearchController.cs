using ForumApi.Dtos;
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
    public async Task<ActionResult<SearchResultDto>> Search([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _search.SearchAsync(q, page, pageSize));

    [HttpGet("levels")]
    public async Task<ActionResult<List<LevelRuleDto>>> Levels()
        => Ok(await _levels.GetRuleDtosAsync());
}
