using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsureNotificationsTableAsync(db);
        await EnsurePostImagesColumnAsync(db);
        await EnsureThreadAccessSchemaAsync(db);
        await EnsureModerationSchemaAsync(db);
        await EnsureLotterySchemaAsync(db);
        await EnsureCommunitySchemaAsync(db);
        await EnsureRetentionSchemaAsync(db);
        await EnsureBannerSchemaAsync(db);
        await EnsureRechargeSchemaAsync(db);

        if (!await db.LevelRules.AnyAsync())
        {
            db.LevelRules.AddRange(
                new LevelRule { Level = 1, Name = "见习会员", MinPoints = 0 },
                new LevelRule { Level = 2, Name = "正式会员", MinPoints = 50 },
                new LevelRule { Level = 3, Name = "活跃会员", MinPoints = 200 },
                new LevelRule { Level = 4, Name = "资深会员", MinPoints = 800 },
                new LevelRule { Level = 5, Name = "金牌会员", MinPoints = 2000 },
                new LevelRule { Level = 6, Name = "论坛元老", MinPoints = 5000 }
            );
            await db.SaveChangesAsync();
        }

        if (await db.Users.AnyAsync())
            return;

        var admin = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Nickname = "站长",
            Points = 5200,
            Coins = 999,
            Level = 6,
            IsAdmin = true
        };
        var demo = new User
        {
            Username = "demo",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("demo123"),
            Nickname = "演示用户",
            Points = 80,
            Coins = 30,
            Level = 2
        };
        var newbie = new User
        {
            Username = "newbie",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("newbie123"),
            Nickname = "新人小白",
            Points = 12,
            Coins = 10,
            Level = 1
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        admin.InviteCode = GenInviteCode(admin.Id);

        db.Users.Add(demo);
        await db.SaveChangesAsync();
        demo.InviteCode = GenInviteCode(demo.Id);

        db.Users.Add(newbie);
        await db.SaveChangesAsync();
        newbie.InviteCode = GenInviteCode(newbie.Id);

        await db.SaveChangesAsync();

        var cat1 = new Category { Name = "综合交流", Icon = "💬", SortOrder = 1 };
        var cat2 = new Category { Name = "同城生活", Icon = "🏙️", SortOrder = 2 };
        var cat3 = new Category { Name = "兴趣爱好", Icon = "🎯", SortOrder = 3 };
        var cat4 = new Category { Name = "站务公告", Icon = "📢", SortOrder = 4 };
        db.Categories.AddRange(cat1, cat2, cat3, cat4);
        await db.SaveChangesAsync();

        var forums = new List<Forum>
        {
            new() { CategoryId = cat1.Id, Name = "灌水闲聊", Description = "轻松闲聊，分享日常", Icon = "🌊", SortOrder = 1 },
            new() { CategoryId = cat1.Id, Name = "新人报到", Description = "新朋友自我介绍", Icon = "👋", SortOrder = 2 },
            new() { CategoryId = cat1.Id, Name = "求助问答", Description = "有问必答互助区", Icon = "❓", SortOrder = 3 },
            new() { CategoryId = cat1.Id, Name = "资源分享", Description = "好东西一起看", Icon = "📦", SortOrder = 4, FullWidth = true },

            new() { CategoryId = cat2.Id, Name = "北京生活", Description = "帝都同城交流", Icon = "🅱️", SortOrder = 1 },
            new() { CategoryId = cat2.Id, Name = "上海生活", Description = "魔都同城交流", Icon = "🆂", SortOrder = 2 },
            new() { CategoryId = cat2.Id, Name = "广州生活", Description = "羊城同城交流", Icon = "🅶", SortOrder = 3 },
            new() { CategoryId = cat2.Id, Name = "深圳生活", Description = "鹏城同城交流", Icon = "🆉", SortOrder = 4 },
            new() { CategoryId = cat2.Id, Name = "杭州生活", Description = "天堂同城交流", Icon = "🅷", SortOrder = 5 },
            new() { CategoryId = cat2.Id, Name = "成都生活", Description = "蓉城同城交流", Icon = "🅲", SortOrder = 6 },

            new() { CategoryId = cat3.Id, Name = "数码科技", Description = "手机电脑数码讨论", Icon = "💻", SortOrder = 1 },
            new() { CategoryId = cat3.Id, Name = "影音娱乐", Description = "影视音乐游戏", Icon = "🎬", SortOrder = 2 },
            new() { CategoryId = cat3.Id, Name = "运动健身", Description = "跑步健身户外", Icon = "🏃", SortOrder = 3 },
            new() { CategoryId = cat3.Id, Name = "美食探店", Description = "吃货聚集地", Icon = "🍜", SortOrder = 4 },

            new() { CategoryId = cat4.Id, Name = "公告通知", Description = "官方公告", Icon = "📣", SortOrder = 1, FullWidth = true },
            new() { CategoryId = cat4.Id, Name = "建议反馈", Description = "意见与建议", Icon = "💡", SortOrder = 2 },
        };
        db.Forums.AddRange(forums);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var seedThreads = new[]
        {
            (forums[0].Id, admin.Id, "欢迎来到 BS 综合社区！", "这里是综合交流区，请遵守论坛规则，友善交流。欢迎发帖回帖赚积分升级！", 120, 8, 15),
            (forums[0].Id, demo.Id, "今天天气真不错，出来晒晒太阳", "周末好心情，有同城的朋友一起出来玩吗？", 56, 5, 3),
            (forums[1].Id, newbie.Id, "大家好，我是新人！", "刚注册，请多多关照～先回帖攒积分争取早日能发帖。", 34, 4, 2),
            (forums[2].Id, demo.Id, "求推荐一款好用的笔记软件", "Windows + 手机同步，最好免费或便宜一点。", 89, 12, 6),
            (forums[4].Id, admin.Id, "北京周末有什么好去处？", "想带家人出去转转，求推荐不排队的地方。", 200, 18, 10),
            (forums[5].Id, demo.Id, "上海咖啡店打卡合集", "整理了几家最近去过的，口味都不错。", 150, 9, 7),
            (forums[6].Id, demo.Id, "广州早茶哪家强", "老广请进，求地道早茶推荐！", 98, 11, 4),
            (forums[7].Id, admin.Id, "深圳科技园附近午餐推荐", "工作日中午不想排队，预算 30 内。", 76, 6, 2),
            (forums[10].Id, demo.Id, "2026 年性价比笔记本怎么选", "预算 5k，主要办公和轻度剪辑。", 310, 25, 20),
            (forums[11].Id, admin.Id, "最近在追的剧，有同好吗", "节奏不错，演员演技在线，剧透慎入。", 180, 14, 9),
            (forums[12].Id, demo.Id, "晨跑打卡第 30 天", "坚持就是胜利，体重掉了 3 斤！", 65, 7, 5),
            (forums[13].Id, newbie.Id, "发现一家超好吃的面馆", "汤底很鲜，面劲道，人均 25。", 44, 3, 1),
            (forums[14].Id, admin.Id, "【公告】论坛正式上线", "积分、金币、等级体系已启用。每日签到可领奖励。发帖需 Lv.2。", 500, 30, 40),
            (forums[3].Id, admin.Id, "精品资源索引（持续更新）", "本帖汇总站内优质资源链接，欢迎补充。", 220, 16, 12),
            (forums[9].Id, demo.Id, "成都火锅避坑指南", "本地人总结，别再踩雷了。", 130, 10, 8),
        };

        // Mix of today / this week / this month so hot tabs all have content
        var offsetsHours = new[] { 1, 2, 4, 8, 12, 20, 30, 48, 60, 72, 96, 120, 150, 200, 240 };
        var idx = 0;
        foreach (var (forumId, authorId, title, content, views, replies, likes) in seedThreads)
        {
            var created = now.AddHours(-(offsetsHours[idx % offsetsHours.Length]));
            idx++;
            var thread = new ForumThread
            {
                ForumId = forumId,
                AuthorId = authorId,
                Title = title,
                Views = views,
                ReplyCount = replies,
                LikeCount = likes,
                CreatedAt = created,
                LastReplyAt = created.AddHours(Random.Shared.Next(0, 6))
            };
            db.Threads.Add(thread);
            await db.SaveChangesAsync();

            db.Posts.Add(new Post
            {
                ThreadId = thread.Id,
                AuthorId = authorId,
                Content = content,
                Floor = 1,
                CreatedAt = created
            });

            for (var i = 0; i < Math.Min(replies, 3); i++)
            {
                var replyAuthor = i % 2 == 0 ? demo : admin;
                db.Posts.Add(new Post
                {
                    ThreadId = thread.Id,
                    AuthorId = replyAuthor.Id,
                    Content = $"回帖测试内容 #{i + 1}：说得很有道理，支持一下！",
                    Floor = i + 2,
                    CreatedAt = created.AddHours(i + 1)
                });
            }

            var forum = forums.First(f => f.Id == forumId);
            forum.ThreadCount += 1;
            forum.PostCount += 1 + Math.Min(replies, 3);
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureNotificationsTableAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Notifications" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "Type" TEXT NOT NULL,
                "ThreadId" INTEGER NOT NULL,
                "ThreadTitle" TEXT NOT NULL,
                "FromUserId" INTEGER NOT NULL,
                "FromNickname" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "Read" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId_CreatedAt"
            ON "Notifications" ("UserId", "CreatedAt");
            """);
    }

    private static async Task EnsurePostImagesColumnAsync(AppDbContext db)
    {
        // SQLite: add column if missing (EnsureCreated won't alter existing DB)
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Posts')";
        var hasImages = false;
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader["name"]?.ToString(), "ImagesJson", StringComparison.OrdinalIgnoreCase))
                {
                    hasImages = true;
                    break;
                }
            }
        }

        if (!hasImages)
        {
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Posts" ADD COLUMN "ImagesJson" TEXT NULL;""");
        }
    }

    private static async Task EnsureThreadAccessSchemaAsync(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Threads')";
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    cols.Add(reader["name"]?.ToString() ?? "");
            }

            if (!cols.Contains("Type"))
                await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Threads" ADD COLUMN "Type" TEXT NOT NULL DEFAULT 'public';""");
            if (!cols.Contains("CoinPrice"))
                await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "Threads" ADD COLUMN "CoinPrice" INTEGER NOT NULL DEFAULT 0;""");
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ThreadPurchases" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ThreadPurchases" PRIMARY KEY AUTOINCREMENT,
                "ThreadId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "CoinPrice" INTEGER NOT NULL,
                "PurchasedAt" TEXT NOT NULL,
                CONSTRAINT "FK_ThreadPurchases_Threads_ThreadId" FOREIGN KEY ("ThreadId") REFERENCES "Threads" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ThreadPurchases_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ThreadPurchases_ThreadId_UserId"
            ON "ThreadPurchases" ("ThreadId", "UserId");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ThreadFavorites" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ThreadFavorites" PRIMARY KEY AUTOINCREMENT,
                "ThreadId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_ThreadFavorites_Threads_ThreadId" FOREIGN KEY ("ThreadId") REFERENCES "Threads" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ThreadFavorites_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ThreadFavorites_ThreadId_UserId"
            ON "ThreadFavorites" ("ThreadId", "UserId");
            """);
    }

    private static async Task EnsureModerationSchemaAsync(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        async Task EnsureColumn(string table, string column, string sqlType)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{table}')";
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    cols.Add(reader["name"]?.ToString() ?? "");
            }
            if (!cols.Contains(column))
                await db.Database.ExecuteSqlRawAsync($"""ALTER TABLE "{table}" ADD COLUMN "{column}" {sqlType};""");
        }

        await EnsureColumn("Threads", "IsHidden", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Threads", "RepliesLocked", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Threads", "IsPinned", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Threads", "IsEssence", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Threads", "EssenceAwarded", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Users", "IsMuted", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Users", "MutedUntil", "TEXT NULL");
        await EnsureColumn("Users", "MuteReason", "TEXT NULL");
        await EnsureColumn("Categories", "IsCollapsedDefault", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Forums", "FullWidth", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Forums", "MinVipTier", "INTEGER NOT NULL DEFAULT 0");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ModerationLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ModerationLogs" PRIMARY KEY AUTOINCREMENT,
                "AdminId" INTEGER NOT NULL,
                "TargetType" TEXT NOT NULL,
                "TargetId" INTEGER NOT NULL,
                "Action" TEXT NOT NULL,
                "Reason" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_ModerationLogs_Users_AdminId" FOREIGN KEY ("AdminId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ModerationLogs_TargetType_TargetId_CreatedAt"
            ON "ModerationLogs" ("TargetType", "TargetId", "CreatedAt");
            """);
    }

    private static async Task EnsureLotterySchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "LotterySpins" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_LotterySpins" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "CostCoins" INTEGER NOT NULL,
                "PrizeCoins" INTEGER NOT NULL,
                "PrizePoints" INTEGER NOT NULL DEFAULT 0,
                "PrizeLabel" TEXT NOT NULL,
                "IsFree" INTEGER NOT NULL DEFAULT 0,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_LotterySpins_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_LotterySpins_UserId_CreatedAt"
            ON "LotterySpins" ("UserId", "CreatedAt");
            """);

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('LotterySpins')";
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                cols.Add(reader["name"]?.ToString() ?? "");
        }
        if (!cols.Contains("PrizePoints"))
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "LotterySpins" ADD COLUMN "PrizePoints" INTEGER NOT NULL DEFAULT 0;""");
    }

    private static async Task EnsureCommunitySchemaAsync(AppDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        async Task EnsureColumn(string table, string column, string sqlType)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{table}')";
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    cols.Add(reader["name"]?.ToString() ?? "");
            }
            if (!cols.Contains(column))
                await db.Database.ExecuteSqlRawAsync($"""ALTER TABLE "{table}" ADD COLUMN "{column}" {sqlType};""");
        }

        await EnsureColumn("Users", "InviteCode", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumn("Users", "InvitedByUserId", "INTEGER NULL");
        await EnsureColumn("Users", "IsVip", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Users", "VipUntil", "TEXT NULL");
        await EnsureColumn("Users", "VipTier", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Users", "LotteryTickets", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumn("Users", "AvatarFrame", "TEXT NULL");
        await EnsureColumn("Posts", "ReplyToPostId", "INTEGER NULL");

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserFollows" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserFollows" PRIMARY KEY AUTOINCREMENT,
                "FollowerId" INTEGER NOT NULL,
                "FolloweeId" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserFollows_FollowerId_FolloweeId"
            ON "UserFollows" ("FollowerId", "FolloweeId");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Tags" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Tags" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Tags_Name" ON "Tags" ("Name");
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ThreadTags" (
                "ThreadId" INTEGER NOT NULL,
                "TagId" INTEGER NOT NULL,
                CONSTRAINT "PK_ThreadTags" PRIMARY KEY ("ThreadId", "TagId")
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ShopItems" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ShopItems" PRIMARY KEY AUTOINCREMENT,
                "Sku" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "Currency" TEXT NOT NULL,
                "Price" INTEGER NOT NULL,
                "ItemType" TEXT NOT NULL,
                "Meta" TEXT NULL,
                "Enabled" INTEGER NOT NULL DEFAULT 1,
                "SortOrder" INTEGER NOT NULL DEFAULT 0
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ShopItems_Sku" ON "ShopItems" ("Sku");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserInventories" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserInventories" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "ItemType" TEXT NOT NULL,
                "Meta" TEXT NULL,
                "Quantity" INTEGER NOT NULL DEFAULT 1,
                "CreatedAt" TEXT NOT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserBadges" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserBadges" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "BadgeCode" TEXT NOT NULL,
                "EarnedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserBadges_UserId_BadgeCode"
            ON "UserBadges" ("UserId", "BadgeCode");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "UserTaskProgresses" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_UserTaskProgresses" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "TaskCode" TEXT NOT NULL,
                "ProgressDate" TEXT NOT NULL,
                "Progress" INTEGER NOT NULL DEFAULT 0,
                "Claimed" INTEGER NOT NULL DEFAULT 0
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserTaskProgresses_UserId_TaskCode_ProgressDate"
            ON "UserTaskProgresses" ("UserId", "TaskCode", "ProgressDate");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Reports" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Reports" PRIMARY KEY AUTOINCREMENT,
                "ReporterId" INTEGER NOT NULL,
                "TargetType" TEXT NOT NULL,
                "TargetId" INTEGER NOT NULL,
                "Reason" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "HandledByAdminId" INTEGER NULL,
                "HandleNote" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "HandledAt" TEXT NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ForumModerators" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ForumModerators" PRIMARY KEY AUTOINCREMENT,
                "ForumId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ForumModerators_ForumId_UserId"
            ON "ForumModerators" ("ForumId", "UserId");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PollOptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PollOptions" PRIMARY KEY AUTOINCREMENT,
                "ThreadId" INTEGER NOT NULL,
                "Text" TEXT NOT NULL,
                "VoteCount" INTEGER NOT NULL DEFAULT 0,
                "SortOrder" INTEGER NOT NULL DEFAULT 0
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PollVotes" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PollVotes" PRIMARY KEY AUTOINCREMENT,
                "ThreadId" INTEGER NOT NULL,
                "OptionId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PollVotes_ThreadId_UserId"
            ON "PollVotes" ("ThreadId", "UserId");
            """);

        // Backfill invite codes
        var users = await db.Users.Where(u => u.InviteCode == null || u.InviteCode == "").ToListAsync();
        foreach (var u in users)
        {
            u.InviteCode = GenInviteCode(u.Id);
        }
        if (users.Count > 0) await db.SaveChangesAsync();

        if (!await db.ShopItems.AnyAsync())
        {
            db.ShopItems.AddRange(
                new ShopItem { Sku = "ticket_1", Name = "转盘券 ×1", Description = "抽奖时优先消耗，免金币", Currency = "coins", Price = 8, ItemType = "lottery_ticket", Meta = "1", SortOrder = 1 },
                new ShopItem { Sku = "ticket_5", Name = "转盘券 ×5", Description = "五次免金币抽奖", Currency = "coins", Price = 35, ItemType = "lottery_ticket", Meta = "5", SortOrder = 2 },
                new ShopItem { Sku = "frame_gold", Name = "金色头像框", Description = "个人主页与发帖展示", Currency = "points", Price = 100, ItemType = "avatar_frame", Meta = "gold", SortOrder = 3 },
                new ShopItem { Sku = "frame_teal", Name = "青绿头像框", Description = "清新社区风格", Currency = "points", Price = 80, ItemType = "avatar_frame", Meta = "teal", SortOrder = 4 },
                new ShopItem { Sku = "vip_30", Name = "VIP 30 天", Description = "每日多 5 次抽奖上限 + 标识", Currency = "coins", Price = 100, ItemType = "vip_30", Meta = "30", SortOrder = 5 },
                new ShopItem { Sku = "rename", Name = "改名卡", Description = "可修改一次昵称（设置页使用）", Currency = "points", Price = 50, ItemType = "rename_card", Meta = "1", SortOrder = 6 }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureRetentionSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ThreadDrafts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ThreadDrafts" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "ForumId" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "Type" TEXT NOT NULL,
                "CoinPrice" INTEGER NOT NULL DEFAULT 0,
                "TagsJson" TEXT NULL,
                "PollOptionsJson" TEXT NULL,
                "ImagesJson" TEXT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_ThreadDrafts_UserId_ForumId"
            ON "ThreadDrafts" ("UserId", "ForumId");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "BrowseHistories" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BrowseHistories" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "ThreadId" INTEGER NOT NULL,
                "ViewedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_BrowseHistories_UserId_ThreadId"
            ON "BrowseHistories" ("UserId", "ThreadId");
            """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ForumSubscriptions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ForumSubscriptions" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "ForumId" INTEGER NOT NULL,
                "LastReadAt" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ForumSubscriptions_UserId_ForumId"
            ON "ForumSubscriptions" ("UserId", "ForumId");
            """);
    }

    private static async Task EnsureBannerSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "HomeBanners" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_HomeBanners" PRIMARY KEY AUTOINCREMENT,
                "Title" TEXT NOT NULL,
                "ImageUrl" TEXT NOT NULL,
                "LinkUrl" TEXT NULL,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "Enabled" INTEGER NOT NULL DEFAULT 1,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_HomeBanners_Enabled_SortOrder"
            ON "HomeBanners" ("Enabled", "SortOrder");
            """);

        if (!await db.HomeBanners.AnyAsync())
        {
            var now = DateTime.UtcNow;
            db.HomeBanners.AddRange(
                new HomeBanner
                {
                    Title = "欢迎来到 BS Forum",
                    ImageUrl = "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?w=1200&h=360&fit=crop",
                    LinkUrl = "/",
                    SortOrder = 1,
                    Enabled = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new HomeBanner
                {
                    Title = "每日签到领奖励",
                    ImageUrl = "https://images.unsplash.com/photo-1557683316-973673baf926?w=1200&h=360&fit=crop",
                    LinkUrl = "/sign-in",
                    SortOrder = 2,
                    Enabled = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureRechargeSchemaAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RechargePackages" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RechargePackages" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Description" TEXT NOT NULL,
                "PriceYuan" TEXT NOT NULL,
                "VipDays" INTEGER NULL,
                "BonusCoins" INTEGER NOT NULL DEFAULT 0,
                "SortOrder" INTEGER NOT NULL DEFAULT 0,
                "Enabled" INTEGER NOT NULL DEFAULT 1
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RechargePackages_Code"
            ON "RechargePackages" ("Code");
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RechargeOrders" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RechargeOrders" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL,
                "PackageId" INTEGER NOT NULL,
                "PackageName" TEXT NOT NULL,
                "PriceYuan" TEXT NOT NULL,
                "VipDays" INTEGER NULL,
                "BonusCoins" INTEGER NOT NULL DEFAULT 0,
                "Status" TEXT NOT NULL,
                "Channel" TEXT NOT NULL,
                "CardCode" TEXT NULL,
                "Remark" TEXT NULL,
                "ConfirmedByAdminId" INTEGER NULL,
                "CreatedAt" TEXT NOT NULL,
                "PaidAt" TEXT NULL,
                CONSTRAINT "FK_RechargeOrders_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_RechargeOrders_RechargePackages_PackageId" FOREIGN KEY ("PackageId") REFERENCES "RechargePackages" ("Id") ON DELETE RESTRICT
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_RechargeOrders_UserId_CreatedAt"
            ON "RechargeOrders" ("UserId", "CreatedAt");
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "RechargeCards" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RechargeCards" PRIMARY KEY AUTOINCREMENT,
                "Code" TEXT NOT NULL,
                "PackageId" INTEGER NOT NULL,
                "UsedByUserId" INTEGER NULL,
                "UsedAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_RechargeCards_RechargePackages_PackageId" FOREIGN KEY ("PackageId") REFERENCES "RechargePackages" ("Id") ON DELETE RESTRICT
            );
            """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_RechargeCards_Code"
            ON "RechargeCards" ("Code");
            """);

        if (!await db.RechargePackages.AnyAsync())
        {
            db.RechargePackages.AddRange(
                new RechargePackage
                {
                    Code = "vip_month",
                    Name = "一个月会员",
                    Description = "VIP 标识 + 每日抽奖上限 +5，有效期 30 天",
                    PriceYuan = 18m,
                    VipDays = 30,
                    BonusCoins = 30,
                    SortOrder = 1,
                    Enabled = true
                },
                new RechargePackage
                {
                    Code = "vip_quarter",
                    Name = "一个季度会员",
                    Description = "VIP 标识 + 每日抽奖上限 +5，有效期 90 天",
                    PriceYuan = 48m,
                    VipDays = 90,
                    BonusCoins = 100,
                    SortOrder = 2,
                    Enabled = true
                },
                new RechargePackage
                {
                    Code = "vip_year",
                    Name = "一年会员",
                    Description = "VIP 标识 + 每日抽奖上限 +5，有效期 365 天",
                    PriceYuan = 128m,
                    VipDays = 365,
                    BonusCoins = 300,
                    SortOrder = 3,
                    Enabled = true
                },
                new RechargePackage
                {
                    Code = "vip_lifetime",
                    Name = "永久会员",
                    Description = "一次开通，终身 VIP（含抽奖加成与标识）",
                    PriceYuan = 298m,
                    VipDays = null,
                    BonusCoins = 800,
                    SortOrder = 4,
                    Enabled = true
                }
            );
            await db.SaveChangesAsync();
        }
    }

    private static string GenInviteCode(int userId)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = Random.Shared;
        var suffix = new char[4];
        for (var i = 0; i < 4; i++) suffix[i] = chars[rng.Next(chars.Length)];
        return $"U{userId}{new string(suffix)}";
    }
}

