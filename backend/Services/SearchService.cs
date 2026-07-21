using ForumApi.Data;
using ForumApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class SearchService
{
    private readonly AppDbContext _db;

    public SearchService(AppDbContext db) => _db = db;

    public async Task<SearchResultDto> SearchAsync(string? q, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var keyword = (q ?? string.Empty).Trim();
        if (keyword.Length == 0)
            return new SearchResultDto([], 0);

        var query = _db.Threads
            .Include(t => t.Author)
            .Include(t => t.Forum)
            .Include(t => t.Posts)
            .Where(t => !t.IsHidden && (t.Title.Contains(keyword) || t.Posts.Any(p => p.Content.Contains(keyword))));

        var total = await query.CountAsync();
        var threads = await query
            .OrderByDescending(t => t.LastReplyAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = threads.Select(t =>
        {
            var first = t.Posts.OrderBy(p => p.Floor).FirstOrDefault();
            var snippet = first?.Content ?? "";
            if (snippet.Length > 120) snippet = snippet[..120];
            return new SearchHitDto(
                t.Id, t.Title, string.IsNullOrWhiteSpace(t.Type) ? "public" : t.Type, t.ForumId, t.Forum.Name,
                t.Author.Nickname, t.Author.Level,
                t.ReplyCount, t.Views, t.LikeCount,
                t.CreatedAt, snippet);
        }).ToList();

        return new SearchResultDto(items, total);
    }
}
