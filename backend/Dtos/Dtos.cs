namespace ForumApi.Dtos;

public record RegisterRequest(
    string Username,
    string Password,
    string? Nickname,
    string? InviteCode = null,
    string? CaptchaId = null,
    string? CaptchaCode = null);

public record CaptchaDto(string CaptchaId, string ImageBase64);
public record LoginRequest(string Username, string Password, string? CaptchaId = null, string? CaptchaCode = null);
public record ResetPasswordRequest(string Username, string Nickname, string NewPassword, string? CaptchaId = null, string? CaptchaCode = null);
public record UpdateSettingsRequest(
    string? Nickname,
    string? Password,
    string? Avatar,
    bool? ShowPurchases = null,
    bool? ShowFavorites = null,
    string? Email = null,
    bool? NotifyReply = null,
    bool? NotifyMention = null);

public record AuthResponse(
    string Token,
    UserDto User);

public record UserDto(
    int Id,
    string Username,
    string Nickname,
    string? Avatar,
    int Points,
    int Coins,
    int Level,
    string LevelName,
    int ConsecutiveSignInDays,
    bool SignedInToday,
    bool IsAdmin,
    string Role,
    bool IsMuted,
    DateTime? MutedUntil,
    string InviteCode,
    bool IsVip,
    DateTime? VipUntil,
    int VipTier,
    string VipTierName,
    int LotteryTickets,
    string? AvatarFrame,
    bool ShowPurchases = false,
    bool ShowFavorites = false,
    string? Email = null,
    bool NotifyReply = true,
    bool NotifyMention = true);

public record UserProfileDto(
    int Id,
    string Username,
    string Nickname,
    string? Avatar,
    int Points,
    int Coins,
    int Level,
    string LevelName,
    int NextLevelPoints,
    int ConsecutiveDays,
    DateTime CreatedAt,
    bool IsVip,
    string? AvatarFrame,
    int VipTier,
    string VipTierName,
    List<string> Badges,
    int FollowerCount,
    int FollowingCount,
    bool FollowedByMe);

public record LevelRuleDto(
    int Id,
    int Level,
    string Name,
    int MinPoints,
    List<string> Benefits);

public record CategoryDto(
    int Id,
    string Name,
    string? Icon,
    int SortOrder,
    List<ForumSummaryDto> Forums);

public record ForumSummaryDto(
    int Id,
    string Name,
    string? Description,
    string? Icon,
    bool FullWidth,
    int ThreadCount,
    int PostCount,
    int TodayThreadCount,
    LatestThreadDto? LatestThread,
    int MinVipTier = 0,
    string AccessLabel = "所有人",
    bool Locked = false);

public record LatestThreadDto(
    int Id,
    string Title,
    DateTime CreatedAt,
    string AuthorNickname,
    int AuthorLevel,
    string AuthorLevelName);

public record ThreadListItemDto(
    int Id,
    string Title,
    string Type,
    int Views,
    int ReplyCount,
    int LikeCount,
    DateTime CreatedAt,
    DateTime LastReplyAt,
    string AuthorNickname,
    int AuthorLevel,
    string AuthorLevelName,
    bool IsPinned,
    bool IsEssence);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record ThreadDetailDto(
    int Id,
    int ForumId,
    string ForumName,
    string Title,
    string Type,
    int CoinPrice,
    int Views,
    int ReplyCount,
    int LikeCount,
    DateTime CreatedAt,
    bool LikedByMe,
    bool Favorited,
    bool Restricted,
    bool Purchased,
    bool RepliesLocked,
    bool IsPinned,
    bool IsEssence,
    List<string> Tags,
    AuthorBriefDto Author,
    List<PostDto> Posts,
    PollDto? Poll = null,
    bool CanModerate = false,
    bool CanEdit = false);

public record AuthorBriefDto(
    int Id,
    string Nickname,
    int Level,
    string LevelName,
    int Points,
    bool IsVip = false,
    string? AvatarFrame = null);

public record PostDto(
    int Id,
    int Floor,
    string Content,
    DateTime CreatedAt,
    AuthorBriefDto Author,
    List<string> Images,
    bool Hidden = false,
    int? ReplyToPostId = null,
    int? ReplyToFloor = null,
    string? ReplyToNickname = null,
    string? ReplyToContent = null,
    DateTime? EditedAt = null,
    bool Deleted = false);

public record CreateThreadRequest(
    int ForumId,
    string Title,
    string Content,
    List<string>? Images = null,
    string? Type = null,
    int CoinPrice = 0,
    List<string>? Tags = null,
    List<string>? PollOptions = null);
public record CreateReplyRequest(string Content, List<string>? Images = null, int? ReplyToPostId = null);

public record PollDto(List<PollOptionDto> Options, int? MyOptionId, int TotalVotes);
public record PollOptionDto(int Id, string Text, int VoteCount, int SortOrder);
public record VotePollRequest(int OptionId);
public record UpdatePostRequest(string Content, List<string>? Images = null);
public record UpdateThreadRequest(string Title, string Content, List<string>? Images = null);
public record ModerationActionRequest(string? Reason);

public record PurchaseResultDto(string Message, int Coins);
public record TipRequest(int Amount);
public record TipResultDto(string Message, int Coins);
public record FavoriteResultDto(bool Favorited, string Message);

public record FavoriteItemDto(
    int Id,
    int ThreadId,
    string Title,
    string ForumName,
    int ReplyCount,
    DateTime CreatedAt,
    int? FolderId = null);

public record FavoriteFolderDto(
    int Id,
    string Name,
    int SortOrder,
    int Count,
    DateTime CreatedAt);

public record CreateFavoriteFolderRequest(string Name);
public record UpdateFavoriteFolderRequest(string Name);
public record MoveFavoriteRequest(int? FolderId);

public record PurchaseHistoryDto(
    int ThreadId,
    string ThreadTitle,
    string ForumName,
    int CoinPrice,
    DateTime PurchasedAt);

public record HotThreadDto(
    int Id,
    string Title,
    int ForumId,
    string ForumName,
    int Views,
    int ReplyCount,
    int LikeCount,
    double Heat,
    DateTime CreatedAt,
    string AuthorNickname,
    int AuthorLevel,
    string AuthorLevelName,
    string Type,
    bool IsEssence);

public record EssenceThreadDto(
    int Id,
    string Title,
    int ForumId,
    string ForumName,
    int Views,
    int ReplyCount,
    DateTime CreatedAt,
    string AuthorNickname,
    int AuthorLevel,
    string AuthorLevelName);

public record SignInMilestoneDto(int Days, int PointsBonus, int CoinsBonus, string Label, int? DaysLeft = null);

public record SignInResultDto(
    int PointsAwarded,
    int CoinsAwarded,
    int ConsecutiveDays,
    int TotalDays,
    SignInMilestoneDto? MilestoneBonus,
    string? Badge,
    UserDto User);

public record SignInStatusDto(
    bool TodaySignedIn,
    int ConsecutiveDays,
    int TotalDays,
    List<string> ThisMonth,
    SignInRewardsDto TodayRewards,
    SignInMilestoneDto? NextMilestone);

public record SignInRewardsDto(int Points, int Coins);

public record SearchHitDto(
    int Id,
    string Title,
    string Type,
    int ForumId,
    string ForumName,
    string AuthorNickname,
    int AuthorLevel,
    int ReplyCount,
    int Views,
    int LikeCount,
    DateTime CreatedAt,
    string Snippet);

public record SearchResultDto(List<SearchHitDto> Items, int Total);

public record SearchQuery(
    string? Q = null,
    int? ForumId = null,
    int? AuthorId = null,
    string? Type = null,
    DateTime? From = null,
    DateTime? To = null);

public record NotificationDto(
    int Id,
    string Type,
    int ThreadId,
    string ThreadTitle,
    int FromUserId,
    string FromNickname,
    string Content,
    bool Read,
    DateTime CreatedAt,
    int PostId = 0,
    int Floor = 0);

public record NotificationSummaryDto(
    int TotalUnread,
    int ReplyUnread,
    int MentionUnread,
    int TipUnread,
    int ForumUnread,
    int FollowUnread,
    int SystemUnread);

public record DraftListItemDto(int Id, int ForumId, string Title, string Type, DateTime UpdatedAt);
public record DraftDto(
    int Id, int ForumId, string Title, string Content, string Type, int CoinPrice,
    List<string> Tags, List<string> PollOptions, List<string> Images, DateTime UpdatedAt);
public record SaveDraftRequest(
    int? Id, int ForumId, string? Title, string? Content, string? Type, int CoinPrice = 0,
    List<string>? Tags = null, List<string>? PollOptions = null, List<string>? Images = null);

public record HistoryItemDto(
    int ThreadId, string Title, int ForumId, string ForumName,
    string AuthorNickname, int ReplyCount, DateTime ViewedAt);

public record SubscribeResultDto(bool Subscribed, string Message);
public record SubscriptionItemDto(
    int ForumId, string ForumName, string? Icon, DateTime LastReadAt, int UnreadCount, DateTime CreatedAt);
public record UnreadThreadDto(
    int ThreadId, string Title, int ForumId, string ForumName, string AuthorNickname, DateTime CreatedAt);

public record ActivityItemDto(
    string Type,
    int ThreadId,
    string ThreadTitle,
    string ForumName,
    string? Content,
    DateTime CreatedAt);

public record ApiMessage(string Message);

// --- Admin ---

public record AdminStatsDto(
    int TotalUsers,
    int TotalThreads,
    int TotalPosts,
    int TotalForums,
    int TodaySignIns,
    int TodayRegistrations,
    int TodayActive,
    int TodayThreads,
    int TodayReplies,
    int TodayActiveUsers,
    int PendingReports,
    int HiddenThreads,
    int MutedUsers,
    int LockedThreads,
    int EssenceCount,
    int PinnedCount,
    int TodayCoinDelta,
    int TodayLotterySpins,
    int TodayLotteryOutCoins,
    int TodayLotteryCostCoins,
    int TodayShopOrders,
    int VipUsers,
    int LotteryTicketStock,
    double SignInAvg7d,
    List<AdminRecentUserDto> RecentUsers,
    List<AdminHotThreadDto> HotThreads,
    List<AdminDayCountDto> WeeklyActivity,
    List<AdminDayCountDto> DailyRegistrations,
    List<AdminDayCountDto> DailyActive,
    List<AdminDayCountDto> DailyNewThreads,
    List<AdminForumHeatDto> ForumHeat,
    List<AdminTodoReportDto> TodoReports,
    List<AdminTodoThreadDto> TodoHidden,
    List<AdminTodoUserDto> TodoMuted,
    List<AdminTodoThreadDto> TodoLocked,
    List<ModerationLogDto> RecentModLogs);

public record AdminForumHeatDto(
    int ForumId, string Name, int ThreadCount, int TodayThreads, int SubscriberCount);

public record AdminTodoReportDto(
    int Id, string TargetType, int TargetId, string Reason, string ReporterNickname, DateTime CreatedAt);

public record AdminTodoThreadDto(
    int Id, string Title, string ForumName, string AuthorNickname, DateTime CreatedAt);

public record AdminTodoUserDto(
    int Id, string Username, string Nickname, DateTime? MutedUntil, string? MuteReason);

public record AdminRecentUserDto(int Id, string Username, string Nickname, int Level, string LevelName, DateTime CreatedAt);
public record AdminHotThreadDto(int Id, string Title, string ForumName, int Views, int ReplyCount);
public record AdminDayCountDto(string Date, string Day, int Count);

public record AdminUserItemDto(
    int Id,
    string Username,
    string Nickname,
    string? Avatar,
    int Level,
    string LevelName,
    int Points,
    int Coins,
    int ConsecutiveDays,
    int TotalDays,
    int ThreadCount,
    int ReplyCount,
    DateTime CreatedAt,
    string Role,
    bool IsAdmin,
    bool IsMuted,
    DateTime? MutedUntil,
    string? MuteReason,
    bool IsVip = false,
    DateTime? VipUntil = null);

public record UpdateAdminUserRequest(
    string? Nickname,
    int? Points,
    int? Coins,
    string? Password,
    bool? IsVip = null,
    int? VipDays = null,
    int? VipTier = null);
public record UpdateRoleRequest(string Role);
public record ModerationReasonRequest(string? Reason);
public record MuteUserRequest(int? Days, string? Reason);

public record AdminThreadItemDto(
    int Id,
    string Title,
    string ForumName,
    string AuthorNickname,
    int AuthorLevel,
    int ReplyCount,
    int Views,
    int LikeCount,
    DateTime CreatedAt,
    bool IsHidden,
    bool RepliesLocked,
    bool IsPinned,
    bool IsEssence,
    int ForumId = 0,
    bool PendingReview = false);

public record MoveThreadRequest(int ForumId);

public record ModerationLogDto(
    int Id,
    int AdminId,
    string AdminNickname,
    string TargetType,
    int TargetId,
    string Action,
    string? Reason,
    DateTime CreatedAt);

public record AdminCategoryDto(
    int Id,
    string Name,
    string? Icon,
    List<AdminForumDto> Forums);

public record AdminForumDto(
    int Id,
    string Name,
    string? Icon,
    string? Description,
    int ThreadCount,
    int PostCount,
    int MinVipTier = 0,
    string AccessLabel = "所有人");

public record CreateCategoryRequest(string? Name, string? Icon);
public record UpdateCategoryRequest(string? Name, string? Icon);
public record CreateForumRequest(int CategoryId, string? Name, string? Icon, string? Description, int MinVipTier = 0);
public record UpdateForumRequest(string? Name, string? Icon, string? Description, int? MinVipTier = null);

public record AdminSignInStatsDto(
    int TodayCount,
    Dictionary<string, int> ConsecutiveDist,
    List<AdminSignInTopUserDto> TopUsers);

public record AdminSignInTopUserDto(
    int UserId,
    string Username,
    string Nickname,
    int ConsecutiveDays,
    int TotalDays);

public record LotteryPrizeDto(
    string Id,
    string Label,
    int Coins,
    int Points,
    int Weight,
    string Color);

public record LotteryConfigDto(
    int CostCoins,
    int DailyLimit,
    int PityThreshold,
    List<LotteryPrizeDto> Prizes);

public record LotteryStatusDto(
    int Coins,
    int Points,
    int SpinsToday,
    int DailyLimit,
    bool FreeAvailable,
    int RemainingSpins,
    int CostCoins,
    bool IsMuted,
    int LotteryTickets,
    bool IsVip,
    bool UseTicketNext);

public record LotterySpinResultDto(
    int SpinId,
    string PrizeId,
    string PrizeLabel,
    int PrizeCoins,
    int PrizePoints,
    int CostCoins,
    bool IsFree,
    int Coins,
    int Points,
    int SpinsToday,
    bool FreeAvailable,
    int RemainingSpins,
    int LotteryTickets,
    bool UsedTicket);

public record LotteryRecentItemDto(
    string Nickname,
    string PrizeLabel,
    int PrizeCoins,
    int PrizePoints,
    DateTime CreatedAt);

public record InviteInfoDto(string InviteCode, string InviteUrlPath, int InviteCount, int RewardCoins, int RewardPoints);
public record FollowResultDto(bool Following, string Message);
public record FeedItemDto(int ThreadId, string Title, string ForumName, string AuthorNickname, int AuthorId, DateTime CreatedAt, string Kind);
public record TagThreadItemDto(
    int Id, string Title, int ForumId, string ForumName, int Views, int ReplyCount,
    DateTime CreatedAt, DateTime LastReplyAt, string AuthorNickname, int AuthorLevel,
    string AuthorLevelName, bool IsEssence);
public record ShopItemDto(int Id, string Sku, string Name, string Description, string Currency, int Price, string ItemType, string? Meta, bool Enabled = true, int SortOrder = 0);
public record SaveShopItemRequest(string Sku, string Name, string Description, string Currency, int Price, string ItemType, string? Meta = null, bool Enabled = true, int SortOrder = 0);
public record ShopBuyResultDto(string Message, int Coins, int Points, int LotteryTickets, bool IsVip, string? AvatarFrame);
public record TaskItemDto(string Code, string Title, string Description, int Target, int Progress, bool Claimed, int RewardPoints, int RewardCoins);
public record BadgeDto(string Code, string Name, string Description, DateTime? EarnedAt);
public record ReportRequest(string TargetType, int TargetId, string Reason);
public record ReportItemDto(
    int Id,
    string TargetType,
    int TargetId,
    string Reason,
    string Status,
    string ReporterNickname,
    DateTime CreatedAt,
    string? HandleNote,
    string? TargetTitle = null,
    int? ThreadId = null,
    string? TargetUserNickname = null);
public record HandleReportRequest(string Action, string? Note); // resolve | reject | hide_thread | hide_post | mute_user
public record ModeratorDto(int ForumId, string ForumName, int UserId, string Nickname);
public record SetModeratorRequest(int ForumId, int UserId);

public record PrivateMessageDto(
    int Id,
    int SenderId,
    string SenderNickname,
    int ReceiverId,
    string ReceiverNickname,
    string Content,
    bool IsRead,
    DateTime CreatedAt);
public record ConversationDto(
    int PeerId,
    string PeerNickname,
    string? PeerAvatar,
    string LastContent,
    DateTime LastAt,
    int UnreadCount);
public record SendMessageRequest(int ReceiverId, string Content);

public record HomeBannerDto(
    int Id, string Title, string ImageUrl, string? LinkUrl, int SortOrder, bool Enabled,
    DateTime CreatedAt, DateTime UpdatedAt);
public record SaveHomeBannerRequest(
    string Title, string ImageUrl, string? LinkUrl, int SortOrder = 0, bool Enabled = true);

// --- Recharge (VIP membership packages) ---
public record RechargePackageDto(
    int Id,
    string Code,
    string Name,
    string Description,
    decimal PriceYuan,
    int? VipDays,
    bool IsLifetime,
    int BonusCoins,
    string DurationLabel);

public record CreateRechargeOrderRequest(int PackageId, string? Remark);
public record RedeemRechargeCardRequest(string Code);

public record RechargeOrderDto(
    int Id,
    int PackageId,
    string PackageName,
    decimal PriceYuan,
    int? VipDays,
    bool IsLifetime,
    int BonusCoins,
    string Status,
    string Channel,
    string? Remark,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string? DurationLabel,
    int? UserId = null,
    string? Username = null,
    string? Nickname = null,
    string? CardCode = null);

public record RechargeResultDto(
    string Message,
    RechargeOrderDto Order,
    bool IsVip,
    DateTime? VipUntil,
    int Coins,
    string? CardCode = null);

public record GenerateRechargeCardsRequest(int PackageId, int Count = 10);
public record GenerateCardsRequest(int PackageId, int Count);
public record RechargeCardDto(
    int Id,
    string Code,
    int PackageId,
    string PackageName,
    bool Used,
    int? UsedByUserId,
    string? UsedByNickname,
    DateTime? UsedAt,
    DateTime CreatedAt);

public class UpdateLevelRequest
{
    public string? Name { get; set; }
    public int? MinPoints { get; set; }
}

public record AdminTagDto(int Id, string Name, int Count);
public record AdminPostDto(int Id, int ThreadId, string ThreadTitle, int Floor, string ContentPreview, int AuthorId, string AuthorNickname, int AuthorLevel, DateTime CreatedAt);
public record RenameTagRequest(string Name);
public record BatchThreadActionRequest(List<int> Ids, string Action);
public record LedgerEntryDto(int Id, int UserId, string Nickname, int Delta, string Reason, string Type, string? RefType, int? RefId, DateTime CreatedAt);
public record AdminInviteDto(int UserId, string Nickname, string Code, DateTime CreatedAt, int UsedCount);
public record BroadcastNotificationRequest(string Content, int? UserId = null);
public record AdminUpdateSettingsRequest(Dictionary<string, string> Settings);

public record UploadResultDto(List<string> Urls);
