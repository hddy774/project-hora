using System.Text.Json;
using System.Text.Json.Serialization;
using Chronicles.Core;

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
