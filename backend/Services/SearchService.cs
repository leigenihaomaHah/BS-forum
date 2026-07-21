using ForumApi.Data;
using ForumApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class SearchService
{
    private readonly AppDbContext _db;
    private readonly CommunityService _community;

    public SearchService(AppDbContext db, CommunityService community)
    {
        _db = db;
        _community = community;
    }

    public async Task<SearchResultDto> SearchAsync(
        string? q, int page, int pageSize,
        int? forumId = null, string? type = null, int? viewerId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var keyword = (q ?? string.Empty).Trim();
        if (keyword.Length == 0)
            return new SearchResultDto([], 0);

        var typeFilter = (type ?? "").Trim().ToLowerInvariant();

        var query = _db.Threads
            .Include(t => t.Author)
            .Include(t => t.Forum)
            .Include(t => t.Posts)
            .Where(t => !t.IsHidden && !t.PendingReview)
            .Where(t => t.Type != "private")
            .Where(t =>
                t.Title.Contains(keyword) ||
                (t.Type != "coin" && t.Posts.Any(p => !p.IsDeleted && p.Content.Contains(keyword))));

        if (forumId.HasValue)
            query = query.Where(t => t.ForumId == forumId.Value);

        if (!string.IsNullOrEmpty(typeFilter))
            query = query.Where(t => t.Type == typeFilter);
        else
            query = query.Where(t => t.Type == "public" || t.Type == "coin" || t.Type == "poll" || string.IsNullOrEmpty(t.Type));

        if (viewerId.HasValue)
        {
            var blocked = await _community.GetBlockedUserIdsAsync(viewerId.Value);
            if (blocked.Count > 0)
                query = query.Where(t => !blocked.Contains(t.AuthorId));
        }

        var total = await query.CountAsync();
        var threads = await query
            .OrderByDescending(t => t.LastReplyAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = threads.Select(t =>
        {
            string snippet;
            if (t.Type == "coin")
            {
                snippet = t.Title;
            }
            else
            {
                var hit = t.Posts
                    .Where(p => !p.IsDeleted && p.Content.Contains(keyword))
                    .OrderBy(p => p.Floor)
                    .FirstOrDefault();
                snippet = hit?.Content ?? t.Title;
            }
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
