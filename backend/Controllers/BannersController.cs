using ForumApi.Dtos;
using ForumApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/banners")]
public class BannersController : ControllerBase
{
    private readonly AdminService _admin;

    public BannersController(AdminService admin) => _admin = admin;

    [HttpGet]
    public async Task<ActionResult<List<HomeBannerDto>>> List()
        => Ok(await _admin.ListBannersAsync(enabledOnly: true));
}
