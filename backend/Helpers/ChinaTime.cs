namespace ForumApi.Helpers;

/// <summary>全站业务时钟：统一使用北京时间（东八区），不使用 UTC 墙钟。</summary>
public static class ChinaTime
{
    public static readonly TimeZoneInfo Zone = Resolve();

    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateTime Today => Now.Date;

    public static DateTime SpecifyAsChina(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, Zone),
            DateTimeKind.Local => TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), Zone),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
        };

    static TimeZoneInfo Resolve()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "China Standard Time" : "Asia/Shanghai");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Shanghai", TimeSpan.FromHours(8), "CST", "CST");
        }
    }
}
