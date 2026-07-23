using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForumApi.Controllers;

[ApiController]
[Route("api/upload")]
public class UploadController : ControllerBase
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly SiteSettingsService _settings;
    private readonly RateLimitService _rate;

    public UploadController(SiteSettingsService settings, RateLimitService rate)
    {
        _settings = settings;
        _rate = rate;
    }

    [Authorize]
    [HttpPost]
    [RequestSizeLimit(80 * 1024 * 1024)]
    public async Task<ActionResult<UploadResultDto>> Upload([FromForm] List<IFormFile> files)
    {
        var uid = JwtHelper.GetUserId(User);
        if (uid == null) return Unauthorized();

        if (!_rate.TryAcquire($"upload:{uid}", 30, TimeSpan.FromHours(1)))
            return BadRequest(new ApiMessage("上传过于频繁，请稍后再试"));

        if (files == null || files.Count == 0)
            return BadRequest(new ApiMessage("请选择文件"));
        if (files.Count > 8)
            return BadRequest(new ApiMessage("一次最多上传 8 张图片"));

        var maxMb = await _settings.GetIntAsync("max_file_size_mb", 10);
        if (maxMb < 1) maxMb = 10;
        var maxBytes = (long)maxMb * 1024 * 1024;

        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(basePath);

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length > maxBytes)
                return BadRequest(new ApiMessage($"文件 \"{file.FileName}\" 超过 {maxMb}MB 限制"));
            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExt.Contains(ext))
                return BadRequest(new ApiMessage($"文件 \"{file.FileName}\" 格式不支持，仅支持 JPG/PNG/GIF/WebP"));

            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (!IsAllowedImageMagic(bytes.AsSpan(0, Math.Min(12, bytes.Length))))
                return BadRequest(new ApiMessage($"文件 \"{file.FileName}\" 内容校验失败，请上传真实图片"));

            var name = $"{ChinaTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(basePath, name);
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            urls.Add($"/uploads/{name}");
        }

        return Ok(new UploadResultDto(urls));
    }

    private static bool IsAllowedImageMagic(ReadOnlySpan<byte> header)
    {
        if (header.Length < 3) return false;
        // JPEG FF D8
        if (header[0] == 0xFF && header[1] == 0xD8) return true;
        // PNG 89 50 4E 47
        if (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return true;
        // GIF 47 49 46
        if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46) return true;
        // WEBP RIFF....WEBP
        if (header.Length >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return true;
        return false;
    }
}
