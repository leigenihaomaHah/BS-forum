using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForumApi.Helpers;

/// <summary>
/// 业务时间一律按北京时间读写。输出带 +08:00，避免被当成 UTC。
/// </summary>
public sealed class ChinaDateTimeJsonConverter : JsonConverter<DateTime>
{
    const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return default;
        var dt = DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return ChinaTime.SpecifyAsChina(dt);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var china = ChinaTime.SpecifyAsChina(value);
        // 强制按 +08:00 偏移写出（Unspecified 视为北京墙钟）
        var offset = new DateTimeOffset(DateTime.SpecifyKind(china, DateTimeKind.Unspecified), ChinaTime.Zone.BaseUtcOffset);
        writer.WriteStringValue(offset.ToString(Format, CultureInfo.InvariantCulture));
    }
}

public sealed class ChinaNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private static readonly ChinaDateTimeJsonConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        return Inner.Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        Inner.Write(writer, value.Value, options);
    }
}
