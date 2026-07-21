using ForumApi.Data;
using ForumApi.Dtos;
using ForumApi.Helpers;
using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Services;

public class ThreadService
{
    public const string TypePublic = "public";
    public const string TypePrivate = "private";
    public const string TypeCoin = "coin";
    public const string TypePoll = "poll";
    public const int ReplyCooldownSeconds = 15;
    public const int MinReplyLength = 5;

    private readonly AppDbContext _db;
    private readonly LevelService _levels;
    private readonly RewardService _rewards;
    private readonly NotificationService _notifications;
    private readonly CommunityService _community;
    private readonly RetentionService _retention;

    public ThreadService(AppDbContext db, LevelService levels, RewardService rewards, NotificationService notifications, CommunityService community, RetentionService retention)
    {
        _db = db;
        _levels = levels;
        _rewards = rewards;
        _notifications = notifications;
        _community = community;
        _retention = retention;
    }

    public async Task<PagedResult<ThreadListItemDto>> GetThreadsAsync(int forumId, int page, int pageSize, string sort = "latest")
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Threads.Include(t => t.Author)
            .Where(t => t.ForumId == forumId && !t.IsHidden);

        if (sort == "essence")
            query = query.Where(t => t.IsEssence);

        var total = await query.CountAsync();

        IQueryable<ForumThread> ordered = sort switch
        {
            "newest" => query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.CreatedAt),
            "hot" => query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.ReplyCount * 30 + t.Views + t.LikeCount * 50),
            "replies" => query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.ReplyCount),
            _ => query.OrderByDescending(t => t.IsPinned).ThenByDescending(t => t.LastReplyAt),
        };
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = new List<ThreadListItemDto>();
        foreach (var t in items)
        {
            var ln = await _levels.GetLevelNameAsync(t.Author.Level);
            list.Add(new ThreadListItemDto(
                t.Id, t.Title, NormalizeType(t.Type), t.Views, t.ReplyCount, t.LikeCount,
                t.CreatedAt, t.LastReplyAt, t.Author.Nickname, t.Author.Level, ln, t.IsPinned, t.IsEssence));
        }

        return new PagedResult<ThreadListItemDto>(list, total, page, pageSize);
    }

    public async Task<(ThreadDetailDto? Result, string? Error)> GetThreadAsync(int threadId, int? currentUserId)
    {
        var thread = await _db.Threads
            .Include(t => t.Author)
            .Include(t => t.Forum)
            .FirstOrDefaultAsync(t => t.Id == threadId);

        if (thread == null) return (null, "帖子不存在");
        if (thread.IsHidden) return (null, "帖子不存在");

        User? viewer = null;
        if (currentUserId.HasValue)
            viewer = await _db.Users.FindAsync(currentUserId.Value);
        if (!VipAccess.CanAccessForum(viewer, thread.Forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(thread.Forum.MinVipTier));

        thread.Views += 1;
        await _db.SaveChangesAsync();
        if (currentUserId.HasValue)
            await _retention.RecordHistoryAsync(currentUserId.Value, threadId);

        var type = NormalizeType(thread.Type);
        var purchased = false;
        if (currentUserId.HasValue && type == TypeCoin)
            purchased = await HasPurchasedAsync(thread.Id, currentUserId.Value);

        var canAccess = await CanAccessAsync(thread, currentUserId, purchased);
        var authorLevel = await _levels.GetLevelNameAsync(thread.Author.Level);
        var authorDto = await MapAuthorAsync(thread.Author);
        var tags = await _community.GetThreadTagsAsync(thread.Id);
        var poll = await _community.GetPollAsync(thread.Id, currentUserId);

        var liked = false;
        var favorited = false;
        var canModerate = false;
        if (currentUserId.HasValue)
        {
            liked = await _db.ThreadLikes.AnyAsync(l => l.ThreadId == threadId && l.UserId == currentUserId.Value);
            favorited = await _db.ThreadFavorites.AnyAsync(f => f.ThreadId == threadId && f.UserId == currentUserId.Value);
            var me = await _db.Users.FindAsync(currentUserId.Value);
            canModerate = me != null && (me.IsAdmin || await _community.IsForumModeratorAsync(currentUserId.Value, thread.ForumId));
        }
        var canEdit = currentUserId.HasValue && (thread.AuthorId == currentUserId.Value || canModerate);

        return (new ThreadDetailDto(
            thread.Id, thread.ForumId, thread.Forum.Name, thread.Title, type, thread.CoinPrice,
            thread.Views, thread.ReplyCount, thread.LikeCount, thread.CreatedAt,
            liked, favorited, Restricted: !canAccess, Purchased: purchased || thread.AuthorId == currentUserId,
            thread.RepliesLocked, thread.IsPinned, thread.IsEssence, tags, authorDto, [], poll,
            canModerate, canEdit), null);
    }

    public async Task<PagedResult<PostDto>> GetPostsAsync(int threadId, int? currentUserId, int page, int pageSize)
    {
        var thread = await _db.Threads.Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null || thread.IsHidden) return new PagedResult<PostDto>([], 0, page, pageSize);

        User? viewer = null;
        if (currentUserId.HasValue)
            viewer = await _db.Users.FindAsync(currentUserId.Value);
        if (!VipAccess.CanAccessForum(viewer, thread.Forum.MinVipTier))
            return new PagedResult<PostDto>([], 0, page, pageSize);

        var type = NormalizeType(thread.Type);
        var purchased = false;
        if (currentUserId.HasValue && type == TypeCoin)
            purchased = await HasPurchasedAsync(thread.Id, currentUserId.Value);
        var canAccess = await CanAccessAsync(thread, currentUserId, purchased);

        var query = _db.Posts.Include(p => p.Author)
            .Where(p => p.ThreadId == threadId)
            .OrderBy(p => p.Floor);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var hideContent = !canAccess && type == TypeCoin && !purchased;
        var byId = items.ToDictionary(p => p.Id);

        var list = new List<PostDto>();
        foreach (var p in items)
            list.Add(await MapPostDtoAsync(p, hideContent, byId));

        return new PagedResult<PostDto>(list, total, page, pageSize);
    }

    private async Task<AuthorBriefDto> MapAuthorAsync(User u)
    {
        var ln = await _levels.GetLevelNameAsync(u.Level);
        return new AuthorBriefDto(u.Id, u.Nickname, u.Level, ln, u.Points, u.IsEffectivelyVip(), u.AvatarFrame);
    }

    private async Task<PostDto> MapPostDtoAsync(Post p, bool hideContent, Dictionary<int, Post>? byId = null)
    {
        var author = await MapAuthorAsync(p.Author);
        int? rf = null;
        string? rn = null;
        string? rc = null;
        if (p.ReplyToPostId is int rid && byId != null && byId.TryGetValue(rid, out var parent))
        {
            rf = parent.Floor;
            rn = parent.Author.Nickname;
            rc = parent.Content.Length > 80 ? parent.Content[..80] + "…" : parent.Content;
        }
        else if (p.ReplyToPostId is int rid2)
        {
            var quoted = await _db.Posts.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == rid2);
            if (quoted != null)
            {
                rf = quoted.Floor;
                rn = quoted.Author.Nickname;
                rc = quoted.Content.Length > 80 ? quoted.Content[..80] + "…" : quoted.Content;
            }
        }

        return new PostDto(
            p.Id, p.Floor, hideContent ? "" : p.Content, p.CreatedAt, author,
            hideContent ? [] : PostImageHelper.Deserialize(p.ImagesJson),
            Hidden: hideContent, p.ReplyToPostId, rf, rn, rc);
    }

    public async Task<(ThreadDetailDto? Result, string? Error)> CreateThreadAsync(int userId, CreateThreadRequest req)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");
        if (user.Level < 2)
            return (null, "需达到正式会员（Lv.2，积分≥50）才能发帖，请先签到或回帖攒积分");
        if (user.IsEffectivelyMuted())
            return (null, "账号已被禁言，暂时无法发帖");

        var title = req.Title.Trim();
        var content = req.Content?.Trim() ?? "";
        if (title.Length < 2 || title.Length > 100)
            return (null, "标题长度需为 2-100 个字符");

        var type = NormalizeType(req.Type);
        var coinPrice = 0;
        if (type == TypeCoin)
        {
            coinPrice = req.CoinPrice;
            if (coinPrice < 1) return (null, "金币帖请设置至少 1 金币的价格");
            if (coinPrice > 9999) return (null, "金币价格过高");
        }
        if (type == TypePoll)
        {
            var opts = (req.PollOptions ?? []).Select(o => o.Trim()).Where(o => o.Length > 0).Take(6).ToList();
            if (opts.Count < 2) return (null, "投票帖至少需要 2 个选项");
        }

        var (images, imgError) = PostImageHelper.Normalize(req.Images);
        if (imgError != null) return (null, imgError);
        if (content.Length < 2 && images!.Count == 0)
            return (null, "内容不能为空");

        var forum = await _db.Forums.FindAsync(req.ForumId);
        if (forum == null) return (null, "版块不存在");
        if (!VipAccess.CanAccessForum(user, forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(forum.MinVipTier));

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var now = DateTime.UtcNow;
            var thread = new ForumThread
            {
                ForumId = forum.Id,
                AuthorId = user.Id,
                Title = title,
                Type = type,
                CoinPrice = coinPrice,
                CreatedAt = now,
                LastReplyAt = now
            };
            _db.Threads.Add(thread);
            await _db.SaveChangesAsync();

            var post = new Post
            {
                ThreadId = thread.Id,
                AuthorId = user.Id,
                Content = content.Length >= 2 ? content : "[图片]",
                ImagesJson = PostImageHelper.Serialize(images),
                Floor = 1,
                CreatedAt = now
            };
            _db.Posts.Add(post);

            forum.ThreadCount += 1;
            forum.PostCount += 1;

            await _rewards.TryAwardPointsAsync(user, 10, RewardService.ReasonCreateThread, "thread", thread.Id, dailyLimit: 5);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _community.AttachTagsAsync(thread.Id, req.Tags);
            if (type == TypePoll)
                await _community.CreatePollAsync(thread.Id, req.PollOptions);

            await _retention.NotifyForumSubscribersAsync(forum.Id, user.Id, user.Nickname, thread.Id, thread.Title);
            await _retention.DeleteDraftByForumAsync(userId, forum.Id);

            foreach (var name in CommunityService.ExtractMentions(content))
            {
                var mentioned = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == name || u.Username == name);
                if (mentioned != null)
                    await _notifications.AddMentionNotificationAsync(mentioned.Id, user.Id, user.Nickname, thread.Id, thread.Title);
            }
            await _db.SaveChangesAsync();

            return await GetThreadAsync(thread.Id, userId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(PostDto? Result, string? Error)> ReplyAsync(int userId, int threadId, CreateReplyRequest req)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");
        if (user.IsEffectivelyMuted())
            return (null, "账号已被禁言，暂时无法回帖");

        var content = req.Content?.Trim() ?? "";
        var (images, imgError) = PostImageHelper.Normalize(req.Images);
        if (imgError != null) return (null, imgError);
        if (images!.Count == 0 && content.Length < MinReplyLength)
            return (null, $"回帖内容至少 {MinReplyLength} 个字");

        var thread = await _db.Threads.Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (null, "帖子不存在");
        if (thread.IsHidden) return (null, "帖子不存在");
        if (thread.RepliesLocked) return (null, "本帖已禁止回复");
        if (!VipAccess.CanAccessForum(user, thread.Forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(thread.Forum.MinVipTier));

        var accessError = await EnsureCanInteractAsync(thread, userId);
        if (accessError != null) return (null, accessError);

        if (!user.IsAdmin)
        {
            var lastAt = await _db.Posts
                .Where(p => p.AuthorId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => (DateTime?)p.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastAt.HasValue)
            {
                var elapsed = (DateTime.UtcNow - lastAt.Value).TotalSeconds;
                var wait = (int)Math.Ceiling(ReplyCooldownSeconds - elapsed);
                if (wait > 0)
                    return (null, $"回帖太快了，请 {wait} 秒后再试");
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            Post? quote = null;
            if (req.ReplyToPostId is int qid)
            {
                quote = await _db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == qid && p.ThreadId == threadId);
                if (quote == null) return (null, "引用的楼层不存在");
            }

            var floor = thread.ReplyCount + 2;
            var post = new Post
            {
                ThreadId = thread.Id,
                AuthorId = user.Id,
                Content = content.Length >= 1 ? content : "[图片]",
                ImagesJson = PostImageHelper.Serialize(images),
                Floor = floor,
                ReplyToPostId = quote?.Id,
                CreatedAt = DateTime.UtcNow
            };
            _db.Posts.Add(post);
            thread.ReplyCount += 1;
            thread.LastReplyAt = DateTime.UtcNow;
            thread.Forum.PostCount += 1;

            // 阶梯奖励：当日回帖次数越多，单次收益递减，防刷币同时保持活跃动力
            var todayReplyCount = await _db.PointLedgers
                .CountAsync(p => p.UserId == user.Id && p.Reason == RewardService.ReasonReply && p.CreatedAt >= DateTime.UtcNow.Date);

            var pts = 0;
            var cns = 0;

            if (todayReplyCount < 10)      // 第 1-10 次：满额
            {
                pts = 2;
                cns = 2;
            }
            else if (todayReplyCount < 20) // 第 11-20 次：金币减半
            {
                pts = 2;
                cns = 1;
            }
            else if (todayReplyCount < 30) // 第 21-30 次：仅象征积分
            {
                pts = 1;
            }

            if (pts > 0)
            {
                user.Points += pts;
                _db.PointLedgers.Add(new PointLedger
                {
                    UserId = user.Id, Delta = pts, Reason = RewardService.ReasonReply,
                    RefType = "thread", RefId = thread.Id
                });
                await _levels.RecalculateLevelAsync(user);
            }

            if (cns > 0)
            {
                user.Coins += cns;
                _db.CoinLedgers.Add(new CoinLedger
                {
                    UserId = user.Id, Delta = cns, Reason = RewardService.ReasonReply,
                    RefType = "thread", RefId = thread.Id
                });
            }

            await _notifications.AddReplyNotificationAsync(
                thread.AuthorId, user.Id, user.Nickname, thread.Id, thread.Title,
                content.Length >= 1 ? content : "[图片]");

            if (quote != null && quote.AuthorId != user.Id && quote.AuthorId != thread.AuthorId)
            {
                await _notifications.AddReplyNotificationAsync(
                    quote.AuthorId, user.Id, user.Nickname, thread.Id, thread.Title,
                    $"引用了你的 {quote.Floor} 楼");
            }

            foreach (var name in CommunityService.ExtractMentions(content))
            {
                var mentioned = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == name || u.Username == name);
                if (mentioned != null)
                    await _notifications.AddMentionNotificationAsync(mentioned.Id, user.Id, user.Nickname, thread.Id, thread.Title);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            await _community.BumpTaskAsync(userId, "reply");

            post.Author = user;
            return (await MapPostDtoAsync(post, false), null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(PostDto? Result, string? Error)> UpdatePostAsync(int userId, int postId, UpdatePostRequest req)
    {
        var post = await _db.Posts.Include(p => p.Author).FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null) return (null, "回复不存在");
        if (post.AuthorId != userId) return (null, "只能编辑自己的回复");

        var content = req.Content?.Trim() ?? "";
        var (images, imgError) = PostImageHelper.Normalize(req.Images ?? PostImageHelper.Deserialize(post.ImagesJson));
        if (imgError != null) return (null, imgError);
        if (post.Floor > 1 && images!.Count == 0 && content.Length < MinReplyLength)
            return (null, $"回帖内容至少 {MinReplyLength} 个字");
        if (content.Length < 1 && images!.Count == 0)
            return (null, "内容不能为空");

        post.Content = content.Length >= 1 ? content : "[图片]";
        if (req.Images != null)
            post.ImagesJson = PostImageHelper.Serialize(images);
        await _db.SaveChangesAsync();

        var ln = await _levels.GetLevelNameAsync(post.Author.Level);
        return (new PostDto(post.Id, post.Floor, post.Content, post.CreatedAt,
            new AuthorBriefDto(post.Author.Id, post.Author.Nickname, post.Author.Level, ln, post.Author.Points),
            PostImageHelper.Deserialize(post.ImagesJson)), null);
    }

    public async Task<(ThreadDetailDto? Result, string? Error)> UpdateThreadAsync(int userId, int threadId, UpdateThreadRequest req, bool isAdmin)
    {
        var thread = await _db.Threads.Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null || thread.IsHidden) return (null, "帖子不存在");

        var canMod = isAdmin || await _community.IsForumModeratorAsync(userId, thread.ForumId);
        if (thread.AuthorId != userId && !canMod) return (null, "无权编辑本帖");

        var title = (req.Title ?? "").Trim();
        if (title.Length is < 1 or > 100) return (null, "标题长度需 1–100 字");

        var content = req.Content?.Trim() ?? "";
        var first = await _db.Posts.FirstOrDefaultAsync(p => p.ThreadId == threadId && p.Floor == 1);
        if (first == null) return (null, "主楼不存在");

        var (images, imgError) = PostImageHelper.Normalize(req.Images ?? PostImageHelper.Deserialize(first.ImagesJson));
        if (imgError != null) return (null, imgError);
        if (content.Length < 1 && images!.Count == 0) return (null, "内容不能为空");

        thread.Title = title;
        first.Content = content.Length >= 1 ? content : "[图片]";
        if (req.Images != null)
            first.ImagesJson = PostImageHelper.Serialize(images);
        await _db.SaveChangesAsync();

        return await GetThreadAsync(threadId, userId);
    }

    public async Task<(bool Ok, string? Error)> EnsureCanModerateAsync(int userId, int threadId)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (false, "帖子不存在");
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "用户不存在");
        if (user.IsAdmin) return (true, null);
        if (await _community.IsForumModeratorAsync(userId, thread.ForumId)) return (true, null);
        return (false, "需要版主或管理员权限");
    }

    public async Task<(string? Message, string? Error)> DeletePostAsync(int userId, int postId, bool isAdmin)
    {
        var post = await _db.Posts.Include(p => p.Thread).ThenInclude(t => t.Forum)
            .FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null) return (null, "回复不存在");
        if (post.AuthorId != userId && !isAdmin) return (null, "无权删除");
        if (post.Floor == 1) return (null, "主楼不能单独删除，请删除整帖");

        post.Thread.ReplyCount = Math.Max(0, post.Thread.ReplyCount - 1);
        post.Thread.Forum.PostCount = Math.Max(0, post.Thread.Forum.PostCount - 1);
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        return ("已删除", null);
    }

    public async Task<(string? Message, string? Error)> LikeAsync(int userId, int threadId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");

        var thread = await _db.Threads.Include(t => t.Author).Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (null, "帖子不存在");
        if (thread.IsHidden) return (null, "帖子不存在");
        if (thread.RepliesLocked) return (null, "本帖已禁止回复");
        if (!VipAccess.CanAccessForum(user, thread.Forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(thread.Forum.MinVipTier));

        var accessError = await EnsureCanInteractAsync(thread, userId);
        if (accessError != null) return (null, accessError);

        var existing = await _db.ThreadLikes.FirstOrDefaultAsync(l => l.ThreadId == threadId && l.UserId == userId);
        if (existing != null)
            return (null, "已经点过赞了");

        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.ThreadLikes.Add(new ThreadLike { ThreadId = threadId, UserId = userId });
        thread.LikeCount += 1;

        if (thread.AuthorId != userId)
            await _rewards.TryAwardPointsAsync(thread.Author, 1, RewardService.ReasonLiked, "thread", threadId, dailyLimit: 30);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        await _community.BumpTaskAsync(userId, "like");
        return ("点赞成功", null);
    }

    public async Task<(PurchaseResultDto? Result, string? Error)> PurchaseAsync(int userId, int threadId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");

        var thread = await _db.Threads.Include(t => t.Author).Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (null, "帖子不存在");
        if (!VipAccess.CanAccessForum(user, thread.Forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(thread.Forum.MinVipTier));
        if (NormalizeType(thread.Type) != TypeCoin) return (null, "此帖子无需购买");
        if (thread.AuthorId == userId) return (null, "无需购买自己的帖子");
        if (await HasPurchasedAsync(threadId, userId)) return (null, "已经购买过了");
        if (thread.CoinPrice < 1) return (null, "帖子价格无效");
        if (user.Coins < thread.CoinPrice) return (null, $"金币不足，需要 {thread.CoinPrice} 金币");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            user.Coins -= thread.CoinPrice;
            thread.Author.Coins += thread.CoinPrice;

            _db.CoinLedgers.Add(new CoinLedger
            {
                UserId = user.Id,
                Delta = -thread.CoinPrice,
                Reason = "purchase_thread",
                RefType = "thread",
                RefId = thread.Id
            });
            _db.CoinLedgers.Add(new CoinLedger
            {
                UserId = thread.AuthorId,
                Delta = thread.CoinPrice,
                Reason = "sell_thread",
                RefType = "thread",
                RefId = thread.Id
            });
            _db.ThreadPurchases.Add(new ThreadPurchase
            {
                ThreadId = thread.Id,
                UserId = user.Id,
                CoinPrice = thread.CoinPrice,
                PurchasedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (new PurchaseResultDto("购买成功", user.Coins), null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<List<PurchaseHistoryDto>> GetPurchasesAsync(int userId)
    {
        return await _db.ThreadPurchases
            .Include(p => p.Thread).ThenInclude(t => t.Forum)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PurchasedAt)
            .Select(p => new PurchaseHistoryDto(
                p.ThreadId, p.Thread.Title, p.Thread.Forum.Name, p.CoinPrice, p.PurchasedAt))
            .ToListAsync();
    }

    public async Task<(FavoriteResultDto? Result, string? Error)> ToggleFavoriteAsync(int userId, int threadId)
    {
        var thread = await _db.Threads.FindAsync(threadId);
        if (thread == null) return (null, "帖子不存在");

        var existing = await _db.ThreadFavorites.FirstOrDefaultAsync(f => f.ThreadId == threadId && f.UserId == userId);
        if (existing != null)
        {
            _db.ThreadFavorites.Remove(existing);
            await _db.SaveChangesAsync();
            return (new FavoriteResultDto(false, "已取消收藏"), null);
        }

        _db.ThreadFavorites.Add(new ThreadFavorite
        {
            ThreadId = threadId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return (new FavoriteResultDto(true, "已收藏"), null);
    }

    public async Task<List<FavoriteItemDto>> GetFavoritesAsync(int userId, int? folderId = null)
    {
        var query = _db.ThreadFavorites
            .Include(f => f.Thread).ThenInclude(t => t.Forum)
            .Where(f => f.UserId == userId && !f.Thread.IsHidden);

        if (folderId.HasValue)
        {
            if (folderId.Value == -1)
                query = query.Where(f => f.FolderId == null);
            else
                query = query.Where(f => f.FolderId == folderId.Value);
        }

        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FavoriteItemDto(
                f.Id, f.Thread.Id, f.Thread.Title, f.Thread.Forum.Name, f.Thread.ReplyCount, f.CreatedAt, f.FolderId))
            .ToListAsync();
    }

    public async Task<List<FavoriteFolderDto>> GetFavoriteFoldersAsync(int userId)
    {
        return await _db.FavoriteFolders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .Select(f => new FavoriteFolderDto(
                f.Id, f.Name, f.SortOrder,
                f.Favorites.Count,
                f.CreatedAt))
            .ToListAsync();
    }

    public async Task<(FavoriteFolderDto? Result, string? Error)> CreateFavoriteFolderAsync(int userId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 20)
            return (null, "分类名称不能为空且不超过 20 字");

        var maxOrder = await _db.FavoriteFolders
            .Where(f => f.UserId == userId)
            .MaxAsync(f => (int?)f.SortOrder) ?? 0;

        var folder = new FavoriteFolder
        {
            UserId = userId,
            Name = name.Trim(),
            SortOrder = maxOrder + 1
        };
        _db.FavoriteFolders.Add(folder);
        await _db.SaveChangesAsync();

        return (new FavoriteFolderDto(folder.Id, folder.Name, folder.SortOrder, 0, folder.CreatedAt), null);
    }

    public async Task<(FavoriteFolderDto? Result, string? Error)> UpdateFavoriteFolderAsync(int userId, int folderId, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 20)
            return (null, "分类名称不能为空且不超过 20 字");

        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId);
        if (folder == null) return (null, "分类不存在");

        folder.Name = name.Trim();
        await _db.SaveChangesAsync();

        var count = await _db.ThreadFavorites.CountAsync(f => f.FolderId == folderId);
        return (new FavoriteFolderDto(folder.Id, folder.Name, folder.SortOrder, count, folder.CreatedAt), null);
    }

    public async Task<(bool Success, string? Error)> DeleteFavoriteFolderAsync(int userId, int folderId)
    {
        var folder = await _db.FavoriteFolders.FirstOrDefaultAsync(f => f.Id == folderId && f.UserId == userId);
        if (folder == null) return (false, "分类不存在");

        // Unlink favorites in this folder
        var favorites = await _db.ThreadFavorites.Where(f => f.FolderId == folderId).ToListAsync();
        foreach (var f in favorites) f.FolderId = null;

        _db.FavoriteFolders.Remove(folder);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> MoveFavoriteAsync(int userId, int favoriteId, int? folderId)
    {
        var fav = await _db.ThreadFavorites.FirstOrDefaultAsync(f => f.Id == favoriteId && f.UserId == userId);
        if (fav == null) return (false, "收藏不存在");

        if (folderId.HasValue)
        {
            var folder = await _db.FavoriteFolders.AnyAsync(f => f.Id == folderId.Value && f.UserId == userId);
            if (!folder) return (false, "分类不存在");
        }

        fav.FolderId = folderId;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<PagedResult<ThreadListItemDto>> GetMyThreadsAsync(int userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Threads.Include(t => t.Author)
            .Where(t => t.AuthorId == userId && !t.IsHidden);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var list = new List<ThreadListItemDto>();
        foreach (var t in items)
        {
            var ln = await _levels.GetLevelNameAsync(t.Author.Level);
            list.Add(new ThreadListItemDto(
                t.Id, t.Title, NormalizeType(t.Type), t.Views, t.ReplyCount, t.LikeCount,
                t.CreatedAt, t.LastReplyAt, t.Author.Nickname, t.Author.Level, ln, t.IsPinned, t.IsEssence));
        }
        return new PagedResult<ThreadListItemDto>(list, total, page, pageSize);
    }

    public async Task<(TipResultDto? Result, string? Error)> TipAsync(int userId, int threadId, int amount)
    {
        if (amount < 1) return (null, "请输入有效的金币数量");
        if (amount > 9999) return (null, "单次打赏过多");

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");
        if (user.IsEffectivelyMuted())
            return (null, "账号已被禁言，暂时无法打赏");

        var thread = await _db.Threads.Include(t => t.Author).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (null, "帖子不存在");
        if (thread.IsHidden) return (null, "帖子不存在");
        if (thread.RepliesLocked) return (null, "本帖已禁止回复");
        if (thread.AuthorId == userId) return (null, "不能打赏自己的帖子");

        var accessError = await EnsureCanInteractAsync(thread, userId);
        if (accessError != null) return (null, accessError);

        if (user.Coins < amount) return (null, $"金币不足，需要 {amount} 金币");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            user.Coins -= amount;
            thread.Author.Coins += amount;
            _db.CoinLedgers.Add(new CoinLedger
            {
                UserId = user.Id,
                Delta = -amount,
                Reason = "tip_thread",
                RefType = "thread",
                RefId = thread.Id
            });
            _db.CoinLedgers.Add(new CoinLedger
            {
                UserId = thread.AuthorId,
                Delta = amount,
                Reason = "receive_tip",
                RefType = "thread",
                RefId = thread.Id
            });
            await _notifications.AddTipNotificationAsync(
                thread.AuthorId, user.Id, user.Nickname, thread.Id, thread.Title, amount);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (new TipResultDto($"打赏成功，送出 {amount} 金币", user.Coins), null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<string?> EnsureCanInteractAsync(ForumThread thread, int userId)
    {
        var type = NormalizeType(thread.Type);
        if (type == TypePublic || thread.AuthorId == userId) return null;
        if (type == TypePrivate) return "此帖子仅作者可见，无法操作";
        if (type == TypeCoin)
        {
            if (await HasPurchasedAsync(thread.Id, userId)) return null;
            return "请先购买后才能操作";
        }
        return null;
    }

    private Task<bool> CanAccessAsync(ForumThread thread, int? userId, bool purchased)
    {
        var type = NormalizeType(thread.Type);
        if (type == TypePublic || type == TypePoll) return Task.FromResult(true);
        if (userId.HasValue && thread.AuthorId == userId.Value) return Task.FromResult(true);
        if (type == TypeCoin && purchased) return Task.FromResult(true);
        return Task.FromResult(false);
    }

    private Task<bool> HasPurchasedAsync(int threadId, int userId)
        => _db.ThreadPurchases.AnyAsync(p => p.ThreadId == threadId && p.UserId == userId);

    private static string NormalizeType(string? type)
    {
        var t = (type ?? TypePublic).Trim().ToLowerInvariant();
        return t is TypePrivate or TypeCoin or TypePoll ? t : TypePublic;
    }
}
