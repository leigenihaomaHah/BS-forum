using System.Text.Json;
using ForumApi.Data;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class ImageMigrationService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private static readonly HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public ImageMigrationService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<MigrationResult> MigrateAsync()
    {
        var basePath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
        Directory.CreateDirectory(basePath);

        var totalMigrated = 0;
        var totalErrors = 0;

        var posts = await _db.Posts
            .Where(p => p.ImagesJson != null && p.ImagesJson != "[]")
            .ToListAsync();

        foreach (var post in posts)
        {
            try
            {
                var migrated = MigratePostImages(post, basePath);
                if (migrated > 0)
                {
                    totalMigrated += migrated;
                    _db.Posts.Update(post);
                }
            }
            catch
            {
                totalErrors++;
            }
        }

        if (totalMigrated > 0)
            await _db.SaveChangesAsync();

        return new MigrationResult(totalMigrated, totalErrors);
    }

    private int MigratePostImages(Post post, string basePath)
    {
        List<string> images;
        try
        {
            images = JsonSerializer.Deserialize<List<string>>(post.ImagesJson!) ?? [];
        }
        catch
        {
            return 0;
        }

        if (images.Count == 0) return 0;

        var migrated = 0;
        for (var i = 0; i < images.Count; i++)
        {
            var img = images[i];
            if (!img.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                continue;

            var url = SaveDataUrl(img, basePath);
            if (url != null)
            {
                images[i] = url;
                migrated++;
            }
        }

        if (migrated > 0)
        {
            post.ImagesJson = JsonSerializer.Serialize(images);
            return migrated;
        }

        return 0;
    }

    private static string? SaveDataUrl(string dataUrl, string basePath)
    {
        try
        {
            var commaIdx = dataUrl.IndexOf(',');
            if (commaIdx < 0) return null;

            var meta = dataUrl[..commaIdx];
            var b64 = dataUrl[(commaIdx + 1)..];

            var mimeMatch = System.Text.RegularExpressions.Regex.Match(meta, @"image/(\w+)");
            var ext = mimeMatch.Success ? "." + mimeMatch.Groups[1].Value : ".jpg";
            if (ext == ".jpeg") ext = ".jpg";
            if (!_allowed.Contains(ext)) ext = ".jpg";

            var name = $"{ChinaTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(basePath, name);

            var bytes = Convert.FromBase64String(b64);
            System.IO.File.WriteAllBytes(path, bytes);

            return $"/uploads/{name}";
        }
        catch
        {
            return null;
        }
    }
}

public record MigrationResult(int Migrated, int Errors);
