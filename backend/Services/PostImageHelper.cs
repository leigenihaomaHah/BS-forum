using System.Text.Json;

namespace ForumApi.Services;

public static class PostImageHelper
{
    private const int MaxImages = 8;
    private const int MaxSingleLength = 2_000_000; // ~1.5MB binary after base64

    public static (List<string>? Images, string? Error) Normalize(IReadOnlyList<string>? images)
    {
        if (images == null || images.Count == 0)
            return ([], null);

        if (images.Count > MaxImages)
            return (null, $"最多上传 {MaxImages} 张图片");

        var result = new List<string>(images.Count);
        foreach (var raw in images)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var img = raw.Trim();
            if (!img.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                && !img.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !img.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return (null, "图片格式无效，请上传 JPG/PNG/GIF");
            if (img.Length > MaxSingleLength)
                return (null, "单张图片过大，请压缩后重试");
            result.Add(img);
        }

        return (result, null);
    }

    public static string? Serialize(List<string>? images)
    {
        if (images == null || images.Count == 0) return null;
        return JsonSerializer.Serialize(images);
    }

    public static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
