using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ForumApi.Services;

public class CaptchaService
{
    private static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
    private readonly ConcurrentDictionary<string, CaptchaEntry> _store = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private record CaptchaEntry(string Code, DateTime ExpiresAt);

    public (string Id, string ImageBase64) Create()
    {
        Cleanup();
        var code = new string(Enumerable.Range(0, 4).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
        var id = Guid.NewGuid().ToString("N");
        _store[id] = new CaptchaEntry(code, ChinaTime.Now.Add(Ttl));
        return (id, RenderSvgBase64(code));
    }

    public bool Validate(string? id, string? code)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code))
            return false;
        if (!_store.TryRemove(id.Trim(), out var entry))
            return false;
        if (entry.ExpiresAt < ChinaTime.Now)
            return false;
        return string.Equals(entry.Code, code.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void Cleanup()
    {
        var now = ChinaTime.Now;
        foreach (var kv in _store)
        {
            if (kv.Value.ExpiresAt < now)
                _store.TryRemove(kv.Key, out _);
        }
    }

    private static string RenderSvgBase64(string code)
    {
        var sb = new StringBuilder();
        sb.Append("""<svg xmlns="http://www.w3.org/2000/svg" width="140" height="44" viewBox="0 0 140 44">""");
        sb.Append("""<rect width="140" height="44" rx="6" fill="#f1f5f9"/>""");

        for (var i = 0; i < 5; i++)
        {
            var x1 = RandomNumberGenerator.GetInt32(0, 140);
            var y1 = RandomNumberGenerator.GetInt32(0, 44);
            var x2 = RandomNumberGenerator.GetInt32(0, 140);
            var y2 = RandomNumberGenerator.GetInt32(0, 44);
            var colors = new[] { "#cbd5e1", "#94a3b8", "#a5f3fc", "#fde68a" };
            var c = colors[RandomNumberGenerator.GetInt32(colors.Length)];
            sb.Append($"""<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{c}" stroke-width="1.2"/>""");
        }

        for (var i = 0; i < 18; i++)
        {
            var cx = RandomNumberGenerator.GetInt32(4, 136);
            var cy = RandomNumberGenerator.GetInt32(4, 40);
            sb.Append($"""<circle cx="{cx}" cy="{cy}" r="1.2" fill="#94a3b8" opacity="0.55"/>""");
        }

        for (var i = 0; i < code.Length; i++)
        {
            var x = 18 + i * 28 + RandomNumberGenerator.GetInt32(-2, 3);
            var y = 28 + RandomNumberGenerator.GetInt32(-3, 4);
            var rot = RandomNumberGenerator.GetInt32(-18, 19);
            var fill = i % 2 == 0 ? "#0f766e" : "#1e293b";
            sb.Append($"""<text x="{x}" y="{y}" fill="{fill}" font-size="22" font-family="Consolas,monospace" font-weight="700" transform="rotate({rot} {x} {y})">{code[i]}</text>""");
        }

        sb.Append("</svg>");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
    }
}

public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 32;

    private static readonly Regex HasLetter = new(@"[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex HasDigit = new(@"\d", RegexOptions.Compiled);

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return $"密码长度需为 {MinLength}-{MaxLength} 位";
        if (password.Length < MinLength || password.Length > MaxLength)
            return $"密码长度需为 {MinLength}-{MaxLength} 位";
        if (password.Any(char.IsWhiteSpace))
            return "密码不能包含空格";
        if (!HasLetter.IsMatch(password))
            return "密码需包含字母";
        if (!HasDigit.IsMatch(password))
            return "密码需包含数字";
        return null;
    }
}
