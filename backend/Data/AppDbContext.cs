using ForumApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Forum> Forums => Set<Forum>();
    public DbSet<ForumThread> Threads => Set<ForumThread>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PointLedger> PointLedgers => Set<PointLedger>();
    public DbSet<CoinLedger> CoinLedgers => Set<CoinLedger>();
    public DbSet<LevelRule> LevelRules => Set<LevelRule>();
    public DbSet<SignInRecord> SignInRecords => Set<SignInRecord>();
    public DbSet<ThreadLike> ThreadLikes => Set<ThreadLike>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ThreadPurchase> ThreadPurchases => Set<ThreadPurchase>();
    public DbSet<ThreadFavorite> ThreadFavorites => Set<ThreadFavorite>();
    public DbSet<ModerationLog> ModerationLogs => Set<ModerationLog>();
    public DbSet<LotterySpin> LotterySpins => Set<LotterySpin>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ThreadTag> ThreadTags => Set<ThreadTag>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<UserInventory> UserInventories => Set<UserInventory>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<UserTaskProgress> UserTaskProgresses => Set<UserTaskProgress>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ForumModerator> ForumModerators => Set<ForumModerator>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<ThreadDraft> ThreadDrafts => Set<ThreadDraft>();
    public DbSet<BrowseHistory> BrowseHistories => Set<BrowseHistory>();
    public DbSet<ForumSubscription> ForumSubscriptions => Set<ForumSubscription>();
    public DbSet<HomeBanner> HomeBanners => Set<HomeBanner>();
    public DbSet<RechargePackage> RechargePackages => Set<RechargePackage>();
    public DbSet<RechargeOrder> RechargeOrders => Set<RechargeOrder>();
    public DbSet<RechargeCard> RechargeCards => Set<RechargeCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.InviteCode)
            .IsUnique();

        modelBuilder.Entity<ForumThread>()
            .ToTable("Threads");

        modelBuilder.Entity<ThreadLike>()
            .HasIndex(l => new { l.ThreadId, l.UserId })
            .IsUnique();

        modelBuilder.Entity<ThreadPurchase>()
            .HasIndex(p => new { p.ThreadId, p.UserId })
            .IsUnique();

        modelBuilder.Entity<ThreadFavorite>()
            .HasIndex(f => new { f.ThreadId, f.UserId })
            .IsUnique();

        modelBuilder.Entity<LevelRule>()
            .HasIndex(l => l.Level)
            .IsUnique();

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.CreatedAt });

        modelBuilder.Entity<ModerationLog>()
            .HasIndex(m => new { m.TargetType, m.TargetId, m.CreatedAt });

        modelBuilder.Entity<ForumThread>()
            .HasOne(t => t.Author)
            .WithMany(u => u.Threads)
            .HasForeignKey(t => t.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Author)
            .WithMany(u => u.Posts)
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Thread)
            .WithMany(t => t.Posts)
            .HasForeignKey(p => p.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ThreadPurchase>()
            .HasOne(p => p.Thread)
            .WithMany()
            .HasForeignKey(p => p.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ThreadPurchase>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ThreadFavorite>()
            .HasOne(f => f.Thread)
            .WithMany()
            .HasForeignKey(f => f.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ThreadFavorite>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ModerationLog>()
            .HasOne(m => m.Admin)
            .WithMany()
            .HasForeignKey(m => m.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LotterySpin>()
            .HasIndex(s => new { s.UserId, s.CreatedAt });

        modelBuilder.Entity<LotterySpin>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserFollow>()
            .HasIndex(f => new { f.FollowerId, f.FolloweeId })
            .IsUnique();

        modelBuilder.Entity<ThreadTag>()
            .HasKey(tt => new { tt.ThreadId, tt.TagId });

        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        modelBuilder.Entity<ShopItem>()
            .HasIndex(s => s.Sku)
            .IsUnique();

        modelBuilder.Entity<UserBadge>()
            .HasIndex(b => new { b.UserId, b.BadgeCode })
            .IsUnique();

        modelBuilder.Entity<UserTaskProgress>()
            .HasIndex(t => new { t.UserId, t.TaskCode, t.ProgressDate })
            .IsUnique();

        modelBuilder.Entity<ForumModerator>()
            .HasIndex(m => new { m.ForumId, m.UserId })
            .IsUnique();

        modelBuilder.Entity<PollVote>()
            .HasIndex(v => new { v.ThreadId, v.UserId })
            .IsUnique();

        modelBuilder.Entity<Post>()
            .HasOne(p => p.ReplyToPost)
            .WithMany()
            .HasForeignKey(p => p.ReplyToPostId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ThreadDraft>()
            .HasIndex(d => new { d.UserId, d.ForumId });

        modelBuilder.Entity<BrowseHistory>()
            .HasIndex(h => new { h.UserId, h.ThreadId })
            .IsUnique();

        modelBuilder.Entity<ForumSubscription>()
            .HasIndex(s => new { s.UserId, s.ForumId })
            .IsUnique();

        modelBuilder.Entity<HomeBanner>()
            .HasIndex(b => new { b.Enabled, b.SortOrder });

        modelBuilder.Entity<RechargePackage>()
            .HasIndex(p => p.Code)
            .IsUnique();

        modelBuilder.Entity<RechargeCard>()
            .HasIndex(c => c.Code)
            .IsUnique();

        modelBuilder.Entity<RechargeOrder>()
            .HasIndex(o => new { o.UserId, o.CreatedAt });

        modelBuilder.Entity<RechargeOrder>()
            .HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RechargeOrder>()
            .HasOne(o => o.Package)
            .WithMany()
            .HasForeignKey(o => o.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RechargeCard>()
            .HasOne(c => c.Package)
            .WithMany()
            .HasForeignKey(c => c.PackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
