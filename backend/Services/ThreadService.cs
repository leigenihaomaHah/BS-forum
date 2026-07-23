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
    private readonly SiteSettingsService _settings;
    private readonly ContentFilterService _filter;

    public ThreadService(
        AppDbContext db, LevelService levels, RewardService rewards, NotificationService notifications,
        CommunityService community, RetentionService retention, SiteSettingsService settings,
        ContentFilterService filter)
    {
        _db = db;
        _levels = levels;
        _rewards = rewards;
        _notifications = notifications;
        _community = community;
        _retention = retention;
        _settings = settings;
        _filter = filter;
    }

    public async Task<PagedResult<ThreadListItemDto>> GetThreadsAsync(
        int forumId, int page, int pageSize, string sort = "latest", int? viewerId = null, string? q = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Threads.Include(t => t.Author)
            .Where(t => t.ForumId == forumId && !t.IsHidden && !t.PendingReview);

        await ClearExpiredPinsAsync(forumId);

        var now = ChinaTime.Now;

        if (viewerId.HasValue)
        {
            var blocked = await _community.GetBlockedUserIdsAsync(viewerId.Value);
            if (blocked.Count > 0)
                query = query.Where(t => !blocked.Contains(t.AuthorId));
        }

        var keyword = (q ?? string.Empty).Trim();
        if (keyword.Length > 0)
        {
            query = query.Where(t =>
                t.Title.Contains(keyword) ||
                (t.Type != TypeCoin && t.Posts.Any(p => !p.IsDeleted && p.Content.Contains(keyword))));
        }

        if (sort == "essence")
            query = query.Where(t => t.IsEssence);

        var total = await query.CountAsync();

        IQueryable<ForumThread> ordered = sort switch
        {
            "newest" => query.OrderByDescending(t => t.IsPinned && (t.PinnedUntil == null || t.PinnedUntil > now)).ThenByDescending(t => t.CreatedAt),
            "hot" => query.OrderByDescending(t => t.IsPinned && (t.PinnedUntil == null || t.PinnedUntil > now)).ThenByDescending(t => t.ReplyCount * 30 + t.Views + t.LikeCount * 50),
            "replies" => query.OrderByDescending(t => t.IsPinned && (t.PinnedUntil == null || t.PinnedUntil > now)).ThenByDescending(t => t.ReplyCount),
            _ => query.OrderByDescending(t => t.IsPinned && (t.PinnedUntil == null || t.PinnedUntil > now)).ThenByDescending(t => t.LastReplyAt),
        };
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var threadIds = items.Select(t => t.Id).ToList();
        var lastNickByThread = new Dictionary<int, string>();
        var imageThreadIds = new HashSet<int>();
        if (threadIds.Count > 0)
        {
            var replyRows = await _db.Posts.AsNoTracking()
                .Where(p => threadIds.Contains(p.ThreadId) && !p.IsDeleted)
                .Select(p => new { p.ThreadId, p.Floor, p.AuthorId, Nick = p.Author.Nickname })
                .ToListAsync();
            foreach (var g in replyRows.GroupBy(x => x.ThreadId))
            {
                var last = g.OrderByDescending(x => x.Floor).First();
                lastNickByThread[g.Key] = last.Nick;
            }

            var withImg = await _db.Posts.AsNoTracking()
                .Where(p => threadIds.Contains(p.ThreadId) && p.Floor == 1 && !p.IsDeleted
                    && p.ImagesJson != null && p.ImagesJson != "" && p.ImagesJson != "[]")
                .Select(p => p.ThreadId)
                .ToListAsync();
            imageThreadIds = withImg.ToHashSet();
        }

        var list = new List<ThreadListItemDto>();
        foreach (var t in items)
        {
            var ln = await _levels.GetLevelNameAsync(t.Author.Level);
            lastNickByThread.TryGetValue(t.Id, out var lastNick);
            list.Add(new ThreadListItemDto(
                t.Id, t.Title, NormalizeType(t.Type), t.Views, t.ReplyCount, t.LikeCount,
                t.CreatedAt, t.LastReplyAt, t.Author.Nickname, t.Author.Level, ln, t.IsEffectivelyPinned(), t.IsEssence,
                t.Author.Avatar, lastNick ?? t.Author.Nickname, imageThreadIds.Contains(t.Id)));
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

        if (thread.PendingReview)
        {
            var canSeePending = viewer != null && (viewer.IsAdmin || thread.AuthorId == viewer.Id);
            if (!canSeePending)
                return (null, "帖子审核中");
        }

        if (!VipAccess.CanAccessForum(viewer, thread.Forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(thread.Forum.MinVipTier));

        thread.Views += 1;
        await _db.SaveChangesAsync();
        if (currentUserId.HasValue)
        {
            await _retention.RecordHistoryAsync(currentUserId.Value, threadId);
            await _community.BumpTaskAsync(currentUserId.Value, "browse");
        }

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

        var tipCoins = await _db.CoinLedgers
            .Where(c => c.Reason == "receive_tip" && c.RefType == "thread" && c.RefId == threadId && c.Delta > 0)
            .SumAsync(c => (int?)c.Delta) ?? 0;
        var tipCount = await _db.CoinLedgers
            .CountAsync(c => c.Reason == "receive_tip" && c.RefType == "thread" && c.RefId == threadId && c.Delta > 0);

        return (new ThreadDetailDto(
            thread.Id, thread.ForumId, thread.Forum.Name, thread.Title, type, thread.CoinPrice,
            thread.Views, thread.ReplyCount, thread.LikeCount, thread.CreatedAt,
            liked, favorited, Restricted: !canAccess, Purchased: purchased || thread.AuthorId == currentUserId,
            thread.RepliesLocked, thread.IsEffectivelyPinned(), thread.IsEssence, tags, authorDto, [], poll,
            canModerate, canEdit, tipCoins, tipCount), null);
    }

    public async Task<PagedResult<PostDto>> GetPostsAsync(int threadId, int? currentUserId, int page, int pageSize)
    {
        var thread = await _db.Threads.Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null || thread.IsHidden) return new PagedResult<PostDto>([], 0, page, pageSize);

        User? viewer = null;
        if (currentUserId.HasValue)
            viewer = await _db.Users.FindAsync(currentUserId.Value);
        if (thread.PendingReview)
        {
            var canSee = viewer != null && (viewer.IsAdmin || thread.AuthorId == viewer.Id);
            if (!canSee) return new PagedResult<PostDto>([], 0, page, pageSize);
        }
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
        var authorIds = items.Select(p => p.AuthorId).Distinct().ToList();
        var blockedAuthors = new HashSet<int>();
        if (currentUserId.HasValue)
        {
            var blocked = await _community.GetBlockedUserIdsAsync(currentUserId.Value);
            blockedAuthors = blocked.Where(id => authorIds.Contains(id)).ToHashSet();
        }
        var postCounts = await _db.Threads
            .Where(t => authorIds.Contains(t.AuthorId) && !t.IsHidden)
            .GroupBy(t => t.AuthorId)
            .Select(g => new { AuthorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AuthorId, x => x.Count);
        var essenceCounts = await _db.Threads
            .Where(t => authorIds.Contains(t.AuthorId) && !t.IsHidden && t.IsEssence)
            .GroupBy(t => t.AuthorId)
            .Select(g => new { AuthorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AuthorId, x => x.Count);

        var list = new List<PostDto>();
        foreach (var p in items)
        {
            var dto = await MapPostDtoAsync(p, hideContent, byId, postCounts, essenceCounts);
            if (blockedAuthors.Contains(p.AuthorId) && !p.IsDeleted)
            {
                dto = dto with
                {
                    Content = "",
                    Images = [],
                    AuthorBlocked = true,
                    Hidden = true
                };
            }
            list.Add(dto);
        }

        return new PagedResult<PostDto>(list, total, page, pageSize);
    }

    private async Task<AuthorBriefDto> MapAuthorAsync(User u, int? postCount = null, int? essenceCount = null)
    {
        var ln = await _levels.GetLevelNameAsync(u.Level);
        // 侧栏「帖子」= 主题数（与后台发帖统计一致），不含回帖
        var count = postCount ?? await _db.Threads.CountAsync(t => t.AuthorId == u.Id && !t.IsHidden);
        var essence = essenceCount ?? await _db.Threads.CountAsync(t => t.AuthorId == u.Id && !t.IsHidden && t.IsEssence);
        return new AuthorBriefDto(
            u.Id, u.Nickname, u.Level, ln, u.Points, u.IsEffectivelyVip(), u.AvatarFrame,
            u.Avatar, count, u.CreatedAt, essence, u.Signature);
    }

    private async Task<PostDto> MapPostDtoAsync(
        Post p, bool hideContent, Dictionary<int, Post>? byId = null,
        Dictionary<int, int>? postCounts = null, Dictionary<int, int>? essenceCounts = null)
    {
        int? pc = null;
        if (postCounts != null && postCounts.TryGetValue(p.AuthorId, out var c))
            pc = c;
        int? ec = null;
        if (essenceCounts != null && essenceCounts.TryGetValue(p.AuthorId, out var e))
            ec = e;
        else if (essenceCounts != null)
            ec = 0;
        var author = await MapAuthorAsync(p.Author, pc, ec);
        int? rf = null;
        string? rn = null;
        string? rc = null;
        if (p.ReplyToPostId is int rid && byId != null && byId.TryGetValue(rid, out var parent))
        {
            rf = parent.Floor;
            rn = parent.Author?.Nickname ?? "";
            rc = parent.IsDeleted ? "已删除" : (parent.Content.Length > 80 ? parent.Content[..80] + "…" : parent.Content);
        }
        else if (p.ReplyToPostId is int rid2)
        {
            var quoted = await _db.Posts.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == rid2);
            if (quoted != null)
            {
                rf = quoted.Floor;
                rn = quoted.Author.Nickname;
                rc = quoted.IsDeleted ? "已删除" : (quoted.Content.Length > 80 ? quoted.Content[..80] + "…" : quoted.Content);
            }
        }

        if (p.IsDeleted)
        {
            return new PostDto(
                p.Id, p.Floor, "已删除", p.CreatedAt, author,
                [], Hidden: hideContent, p.ReplyToPostId, rf, rn, rc,
                EditedAt: p.EditedAt, Deleted: true);
        }

        return new PostDto(
            p.Id, p.Floor, hideContent ? "" : p.Content, p.CreatedAt, author,
            hideContent ? [] : PostImageHelper.Deserialize(p.ImagesJson),
            Hidden: hideContent, p.ReplyToPostId, rf, rn, rc,
            EditedAt: p.EditedAt, Deleted: false);
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

        var (filteredTitle, filteredContent, filterResult) = await _filter.FilterThreadAsync(user, title, content);
        if (filterResult.BlockError != null) return (null, filterResult.BlockError);
        title = filteredTitle;
        content = filteredContent;

        var forum = await _db.Forums.FindAsync(req.ForumId);
        if (forum == null) return (null, "版块不存在");
        if (!VipAccess.CanAccessForum(user, forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(forum.MinVipTier));

        var requireReview = await _settings.GetBoolAsync("require_review");
        var exemptLevel = await _settings.GetIntAsync("review_exempt_min_level", 4);
        var exempt = user.IsAdmin || user.Level >= exemptLevel;
        var pending = !exempt && (requireReview || filterResult.ForceReview);
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var now = ChinaTime.Now;
            var thread = new ForumThread
            {
                ForumId = forum.Id,
                AuthorId = user.Id,
                Title = title,
                Type = type,
                CoinPrice = coinPrice,
                CreatedAt = now,
                LastReplyAt = now,
                PendingReview = pending
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
            user.LastActiveAt = now;

            var threadPts = await _settings.GetIntAsync("points_per_thread", 10);
            if (threadPts > 0)
                await _rewards.TryAwardPointsAsync(user, threadPts, RewardService.ReasonCreateThread, "thread", thread.Id, dailyLimit: 5);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            await _community.AttachTagsAsync(thread.Id, req.Tags);
            if (type == TypePoll)
                await _community.CreatePollAsync(thread.Id, req.PollOptions, req.PollEndsAt, req.PollAllowMulti);

            await _retention.NotifyForumSubscribersAsync(forum.Id, user.Id, user.Nickname, thread.Id, thread.Title);
            await _retention.DeleteDraftByForumAsync(userId, forum.Id);

            foreach (var name in CommunityService.ExtractMentions(content))
            {
                var mentioned = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == name || u.Username == name);
                if (mentioned != null)
                    await _notifications.AddMentionNotificationAsync(mentioned.Id, user.Id, user.Nickname, thread.Id, thread.Title);
            }
            await _db.SaveChangesAsync();

            await _community.BumpTaskAsync(userId, "post");
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

        var (filteredContent, filterResult) = await _filter.FilterReplyAsync(user, content);
        if (filterResult.BlockError != null) return (null, filterResult.BlockError);
        content = filteredContent;

        var thread = await _db.Threads.Include(t => t.Forum).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (null, "帖子不存在");
        if (thread.IsHidden) return (null, "帖子不存在");
        if (thread.PendingReview && !user.IsAdmin && thread.AuthorId != userId)
            return (null, "帖子审核中，暂不可回复");
        if (thread.RepliesLocked) return (null, "本帖已禁止回复");
        if (!VipAccess.CanAccessForum(user, thread.Forum.MinVipTier))
            return (null, VipAccess.AccessDeniedMessage(thread.Forum.MinVipTier));

        var accessError = await EnsureCanInteractAsync(thread, userId);
        if (accessError != null) return (null, accessError);

        var maxReplies = await _settings.GetIntAsync("max_replies_per_day", 50);
        if (!user.IsAdmin && maxReplies > 0)
        {
            var todayStart = ChinaTime.Today;
            var todayReplies = await _db.Posts.CountAsync(p =>
                p.AuthorId == userId && p.Floor > 1 && p.CreatedAt >= todayStart && !p.IsDeleted);
            if (todayReplies >= maxReplies)
                return (null, $"今日回帖已达上限（{maxReplies}）");
        }

        if (!user.IsAdmin)
        {
            // 仅统计本人最近一次发帖/回帖；时间戳异常（未来时间、未标注 UTC）时不得算出数千秒等待
            var lastAt = await _db.Posts
                .Where(p => p.AuthorId == userId && !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => (DateTime?)p.CreatedAt)
                .FirstOrDefaultAsync();
            if (lastAt.HasValue)
            {
                var last = ChinaTime.SpecifyAsChina(lastAt.Value);
                var elapsed = (ChinaTime.Now - last).TotalSeconds;
                if (elapsed < 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed))
                    elapsed = ReplyCooldownSeconds; // 脏数据 / 时钟偏差：放行
                var wait = (int)Math.Ceiling(ReplyCooldownSeconds - elapsed);
                if (wait > 0)
                {
                    wait = Math.Min(wait, ReplyCooldownSeconds);
                    return (null, $"回帖太快了，请 {wait} 秒后再试");
                }
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
                CreatedAt = ChinaTime.Now
            };
            _db.Posts.Add(post);
            thread.ReplyCount += 1;
            thread.LastReplyAt = ChinaTime.Now;
            thread.Forum.PostCount += 1;
            user.LastActiveAt = ChinaTime.Now;

            // 阶梯奖励：当日回帖次数越多，单次收益递减；基数来自站点设置
            var todayReplyCount = await _db.PointLedgers
                .CountAsync(p => p.UserId == user.Id && p.Reason == RewardService.ReasonReply && p.CreatedAt >= ChinaTime.Today);

            var basePts = await _settings.GetIntAsync("points_per_reply", 2);
            var baseCns = await _settings.GetIntAsync("coins_per_reply", 1);
            var pts = 0;
            var cns = 0;

            if (todayReplyCount < 10)
            {
                pts = basePts;
                cns = Math.Max(baseCns, 1);
            }
            else if (todayReplyCount < 20)
            {
                pts = basePts;
                cns = Math.Max(baseCns / 2, 0);
            }
            else if (todayReplyCount < 30)
            {
                pts = Math.Max(basePts / 2, 1);
            }

            pts = await _settings.ApplyPointsEventAsync(pts);

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

            await _db.SaveChangesAsync(); // ensure post.Id

            await _notifications.AddReplyNotificationAsync(
                thread.AuthorId, user.Id, user.Nickname, thread.Id, thread.Title,
                content.Length >= 1 ? content : "[图片]", post.Id, post.Floor);

            if (quote != null && quote.AuthorId != user.Id && quote.AuthorId != thread.AuthorId)
            {
                await _notifications.AddReplyNotificationAsync(
                    quote.AuthorId, user.Id, user.Nickname, thread.Id, thread.Title,
                    $"引用了你的 {quote.Floor} 楼", post.Id, post.Floor);
            }

            foreach (var name in CommunityService.ExtractMentions(content))
            {
                var mentioned = await _db.Users.FirstOrDefaultAsync(u => u.Nickname == name || u.Username == name);
                if (mentioned != null)
                    await _notifications.AddMentionNotificationAsync(mentioned.Id, user.Id, user.Nickname, thread.Id, thread.Title, post.Id, post.Floor);
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
        post.EditedAt = ChinaTime.Now;
        await _db.SaveChangesAsync();

        return (await MapPostDtoAsync(post, false), null);
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
        if (post.IsDeleted) return ("已删除", null);

        post.IsDeleted = true;
        post.DeletedAt = ChinaTime.Now;
        post.Content = "";
        post.ImagesJson = null;
        post.Thread.ReplyCount = Math.Max(0, post.Thread.ReplyCount - 1);
        post.Thread.Forum.PostCount = Math.Max(0, post.Thread.Forum.PostCount - 1);
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
        await _community.TryAwardProgressBadgesAsync(thread.AuthorId);
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
                PurchasedAt = ChinaTime.Now
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
        var result = await GetPurchasesAsync(userId, 1, 1000);
        return result.Items;
    }

    public async Task<PagedResult<PurchaseHistoryDto>> GetPurchasesAsync(int userId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.ThreadPurchases
            .Include(p => p.Thread).ThenInclude(t => t.Forum)
            .Where(p => p.UserId == userId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.PurchasedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PurchaseHistoryDto(
                p.ThreadId, p.Thread.Title, p.Thread.Forum.Name, p.CoinPrice, p.PurchasedAt))
            .ToListAsync();

        return new PagedResult<PurchaseHistoryDto>(items, total, page, pageSize);
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
            CreatedAt = ChinaTime.Now
        });
        await _db.SaveChangesAsync();
        return (new FavoriteResultDto(true, "已收藏"), null);
    }

    public async Task<List<FavoriteItemDto>> GetFavoritesAsync(int userId, int? folderId = null)
    {
        var result = await GetFavoritesAsync(userId, folderId, 1, 1000);
        return result.Items;
    }

    public async Task<PagedResult<FavoriteItemDto>> GetFavoritesAsync(int userId, int? folderId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

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

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FavoriteItemDto(
                f.Id, f.Thread.Id, f.Thread.Title, f.Thread.Forum.Name, f.Thread.ReplyCount, f.CreatedAt, f.FolderId))
            .ToListAsync();

        return new PagedResult<FavoriteItemDto>(items, total, page, pageSize);
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

    public async Task<(PaidPinResultDto? Result, string? Error)> PaidPinAsync(int userId, int threadId)
    {
        if (!await _settings.GetBoolAsync("paid_pin_enabled", true))
            return (null, "付费置顶暂未开放");

        var cost = Math.Max(1, await _settings.GetIntAsync("paid_pin_cost_coins", 20));
        var hours = Math.Clamp(await _settings.GetIntAsync("paid_pin_hours", 24), 1, 168);

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (null, "用户不存在");
        if (user.IsEffectivelyMuted()) return (null, "账号已被禁言");

        var thread = await _db.Threads.FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return (null, "帖子不存在");
        if (thread.AuthorId != userId) return (null, "只能置顶自己的帖子");
        if (thread.IsHidden || thread.PendingReview) return (null, "帖子当前不可置顶");
        if (thread.IsEffectivelyPinned() && thread.PinnedUntil == null)
            return (null, "帖子已由管理置顶，无需付费");

        if (user.Coins < cost) return (null, $"金币不足，需要 {cost} 金币");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            user.Coins -= cost;
            user.LastActiveAt = ChinaTime.Now;
            var until = ChinaTime.Now.AddHours(hours);
            // 若已有付费置顶未过期，在原到期时间上续期
            if (thread.IsEffectivelyPinned() && thread.PinnedUntil.HasValue && thread.PinnedUntil > ChinaTime.Now)
                until = thread.PinnedUntil.Value.AddHours(hours);

            thread.IsPinned = true;
            thread.PinnedUntil = until;

            _db.CoinLedgers.Add(new CoinLedger
            {
                UserId = user.Id,
                Delta = -cost,
                Reason = "paid_pin",
                RefType = "thread",
                RefId = thread.Id
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (new PaidPinResultDto($"置顶成功，有效至 {until:yyyy-MM-dd HH:mm}", user.Coins, until), null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task ClearExpiredPinsAsync(int forumId)
    {
        var now = ChinaTime.Now;
        var expired = await _db.Threads
            .Where(t => t.ForumId == forumId && t.IsPinned && t.PinnedUntil != null && t.PinnedUntil <= now)
            .ToListAsync();
        if (expired.Count == 0) return;
        foreach (var t in expired)
        {
            t.IsPinned = false;
            t.PinnedUntil = null;
        }
        await _db.SaveChangesAsync();
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
