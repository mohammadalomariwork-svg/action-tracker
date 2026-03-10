using System.Text.Json;
using System.Text.Json.Serialization;

namespace ActionTracker.API.Converters;

/// <summary>
/// Ensures every DateTime is serialized with a trailing 'Z' (UTC indicator)
/// so that browser clients interpret timestamps as UTC and convert to local time.
/// On deserialization, incoming values are normalized to DateTimeKind.Utc.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        return dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}
