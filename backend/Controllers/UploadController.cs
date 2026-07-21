using ForumApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private static readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxFileSize = 10 * 1024 * 1024;

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(MaxFileSize * 8)]
    public async Task<ActionResult<UploadResultDto>> Upload([FromForm] List<IFormFile> files)
    {
        var uid = Helpers.JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();

        if (files == null || files.Count == 0)
            return BadRequest(new ApiMessage("请选择文件"));
        if (files.Count > 8)
            return BadRequest(new ApiMessage("一次最多上传 8 张图片"));

        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(basePath);

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length > MaxFileSize)
                return BadRequest(new ApiMessage($"文件 \"{file.FileName}\" 超过 10MB 限制"));
            var ext = Path.GetExtension(file.FileName);
            if (!_allowed.Contains(ext))
                return BadRequest(new ApiMessage($"文件 \"{file.FileName}\" 格式不支持，仅支持 JPG/PNG/GIF/WebP"));

            var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(basePath, name);
            await using var stream = new FileStream(path, FileMode.Create);
            await file.CopyToAsync(stream);
            urls.Add($"/uploads/{name}");
        }

        return Ok(new UploadResultDto(urls));
    }
}
