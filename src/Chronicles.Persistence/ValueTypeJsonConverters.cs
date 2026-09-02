using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Persistence;

internal sealed class AgentIdJsonConverter : JsonConverter<AgentId>
{
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value))
        {
            throw new JsonException("Agent ID는 정수여야 합니다.");
        }

        return new AgentId(value);
    }

    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

internal sealed class SimulationTickJsonConverter : JsonConverter<SimulationTick>
{
    public override SimulationTick Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var value))
        {
            throw new JsonException("Simulation tick은 정수여야 합니다.");
        }

        return new SimulationTick(value);
    }

    public override void Write(Utf8JsonWriter writer, SimulationTick value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}

/// <summary>
/// 체크포인트의 인물별 최근 로그를 속성명 없는 고정 tuple로 기록한다.
/// 기존 object-array도 계속 읽어 schema 25~31 저장 호환을 유지한다.
/// </summary>
internal sealed class PersonalLogEntryArrayJsonConverter :
    JsonConverter<IReadOnlyList<PersonalLogEntry>>
{
    public override IReadOnlyList<PersonalLogEntry> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        CompactArrayJsonReader.Require(ref reader, JsonTokenType.StartArray, "개인 로그 배열");
        List<PersonalLogEntry> entries = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                entries.Add(JsonSerializer.Deserialize<PersonalLogEntry>(ref reader, options));
                continue;
            }
            CompactArrayJsonReader.Require(ref reader, JsonTokenType.StartArray, "개인 로그 tuple");
            entries.Add(new PersonalLogEntry(
                CompactArrayJsonReader.Int64(ref reader),
                new SimulationTick(CompactArrayJsonReader.Int64(ref reader)),
                CompactArrayJsonReader.Int32(ref reader),
                (PersonalLogKind)CompactArrayJsonReader.Byte(ref reader),
                CompactArrayJsonReader.NullableEnum<AgentActivity>(ref reader),
                CompactArrayJsonReader.NullableEnum<WorldEventKind>(ref reader),
                CompactArrayJsonReader.NullableEnum<ChronicleEventKind>(ref reader),
                CompactArrayJsonReader.Boolean(ref reader),
                CompactArrayJsonReader.NullableAgentId(ref reader),
                CompactArrayJsonReader.Int64(ref reader)));
            CompactArrayJsonReader.EndTuple(ref reader, "개인 로그 tuple");
        }
        CompactArrayJsonReader.Require(ref reader, JsonTokenType.EndArray, "개인 로그 배열");
        return entries.ToArray();
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<PersonalLogEntry> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var entry in value)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(entry.Sequence);
            writer.WriteNumberValue(entry.Tick.Value);
            writer.WriteNumberValue(entry.LifeNumber);
            writer.WriteNumberValue((byte)entry.Kind);
            WriteNullableEnum(writer, entry.Activity);
            WriteNullableEnum(writer, entry.WorldEventKind);
            WriteNullableEnum(writer, entry.ChronicleEventKind);
            writer.WriteNumberValue(entry.IsPrimaryActor ? 1 : 0);
            writer.WriteNumberValue(entry.RelatedAgent?.Value ?? -1);
            writer.WriteNumberValue(entry.Value);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }

    private static void WriteNullableEnum<T>(Utf8JsonWriter writer, T? value)
        where T : struct, Enum =>
        writer.WriteNumberValue(value.HasValue
            ? Convert.ToByte(value.Value, CultureInfo.InvariantCulture)
            : -1);
}

/// <summary>
/// 영구 연대기는 tuple과 생별 이름 변경점만 저장한다. 같은 이름을 항목마다 반복하지 않는다.
/// </summary>
internal sealed class LifeChronicleEntryArrayJsonConverter :
    JsonConverter<IReadOnlyList<LifeChronicleEntry>>
{
    public override IReadOnlyList<LifeChronicleEntry> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        CompactArrayJsonReader.Require(ref reader, JsonTokenType.StartArray, "영구 연대기 배열");
        List<LifeChronicleEntry> entries = [];
        string? previousName = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var legacy = JsonSerializer.Deserialize<LifeChronicleEntry>(ref reader, options);
                previousName = legacy.PersonName;
                entries.Add(legacy);
                continue;
            }
            CompactArrayJsonReader.Require(ref reader, JsonTokenType.StartArray, "영구 연대기 tuple");
            var sequence = CompactArrayJsonReader.Int64(ref reader);
            var tick = CompactArrayJsonReader.Int64(ref reader);
            var lifeNumber = CompactArrayJsonReader.Int32(ref reader);
            var ageYears = CompactArrayJsonReader.Int64(ref reader);
            var personName = CompactArrayJsonReader.StringOrPrevious(ref reader, previousName);
            previousName = personName;
            entries.Add(new LifeChronicleEntry(
                sequence,
                new SimulationTick(tick),
                lifeNumber,
                ageYears,
                personName,
                (ChronicleEventKind)CompactArrayJsonReader.Byte(ref reader),
                (RealmStage)CompactArrayJsonReader.Byte(ref reader),
                (MinorStage)CompactArrayJsonReader.Byte(ref reader),
                CompactArrayJsonReader.Int32(ref reader)));
            CompactArrayJsonReader.EndTuple(ref reader, "영구 연대기 tuple");
        }
        CompactArrayJsonReader.Require(ref reader, JsonTokenType.EndArray, "영구 연대기 배열");
        return entries.ToArray();
    }

    public override void Write(
        Utf8JsonWriter writer,
        IReadOnlyList<LifeChronicleEntry> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        string? previousName = null;
        foreach (var entry in value)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(entry.Sequence);
            writer.WriteNumberValue(entry.Tick.Value);
            writer.WriteNumberValue(entry.LifeNumber);
            writer.WriteNumberValue(entry.AgeYears);
            if (string.Equals(previousName, entry.PersonName, StringComparison.Ordinal))
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(entry.PersonName);
                previousName = entry.PersonName;
            }
            writer.WriteNumberValue((byte)entry.Kind);
            writer.WriteNumberValue((byte)entry.RealmStage);
            writer.WriteNumberValue((byte)entry.MinorStage);
            writer.WriteNumberValue(entry.Value);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}

internal static class CompactArrayJsonReader
{
    public static long Int64(ref Utf8JsonReader reader)
    {
        Read(ref reader, "정수");
        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var value)
            ? value
            : throw new JsonException("압축 저장 필드가 64비트 정수가 아닙니다.");
    }

    public static int Int32(ref Utf8JsonReader reader)
    {
        Read(ref reader, "정수");
        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value)
            ? value
            : throw new JsonException("압축 저장 필드가 32비트 정수가 아닙니다.");
    }

    public static byte Byte(ref Utf8JsonReader reader)
    {
        var value = Int32(ref reader);
        return value is >= byte.MinValue and <= byte.MaxValue
            ? (byte)value
            : throw new JsonException("압축 저장 enum이 byte 범위를 벗어났습니다.");
    }

    public static T? NullableEnum<T>(ref Utf8JsonReader reader)
        where T : struct, Enum
    {
        var value = Int32(ref reader);
        if (value == -1)
        {
            return null;
        }
        if (value is < byte.MinValue or > byte.MaxValue
            || !Enum.IsDefined(typeof(T), (byte)value))
        {
            throw new JsonException($"압축 저장 {typeof(T).Name} 값이 잘못되었습니다.");
        }
        return (T)Enum.ToObject(typeof(T), (byte)value);
    }

    public static bool Boolean(ref Utf8JsonReader reader)
    {
        Read(ref reader, "논리값");
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number when reader.TryGetInt32(out var value) && value is 0 or 1 => value == 1,
            _ => throw new JsonException("압축 저장 논리값이 잘못되었습니다."),
        };
    }

    public static AgentId? NullableAgentId(ref Utf8JsonReader reader)
    {
        var value = Int32(ref reader);
        return value < 0 ? null : new AgentId(value);
    }

    public static string StringOrPrevious(ref Utf8JsonReader reader, string? previous)
    {
        Read(ref reader, "문자열");
        if (reader.TokenType == JsonTokenType.Null)
        {
            return previous ?? throw new JsonException("첫 영구 연대기 이름은 생략할 수 없습니다.");
        }
        if (reader.TokenType != JsonTokenType.String
            || string.IsNullOrWhiteSpace(reader.GetString()))
        {
            throw new JsonException("영구 연대기 인물 이름이 비어 있습니다.");
        }
        return reader.GetString()!;
    }

    public static void EndTuple(ref Utf8JsonReader reader, string subject)
    {
        Read(ref reader, subject);
        Require(ref reader, JsonTokenType.EndArray, subject);
    }

    public static void Require(ref Utf8JsonReader reader, JsonTokenType expected, string subject)
    {
        if (reader.TokenType != expected)
        {
            throw new JsonException($"{subject} 형식이 잘못되었습니다.");
        }
    }

    private static void Read(ref Utf8JsonReader reader, string subject)
    {
        if (!reader.Read())
        {
            throw new JsonException($"{subject} 데이터가 중간에 끝났습니다.");
        }
    }
}
