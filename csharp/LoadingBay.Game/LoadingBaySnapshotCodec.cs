using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Rusty.Engine.Persistence;

namespace LoadingBay.Game;

/// <summary>Minimal Vector3 converter so the save DTO graph stays fully source-generated.</summary>
internal sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0, y = 0, z = 0;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected an XYZ object for Vector3.");
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return new Vector3(x, y, z);
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected a Vector3 component name.");
            string? name = reader.GetString();
            if (!reader.Read()) throw new JsonException("Expected a Vector3 component value.");
            float value = (float)reader.GetDouble();
            switch (name)
            {
                case "X": x = value; break;
                case "Y": y = value; break;
                case "Z": z = value; break;
                default: throw new JsonException($"Unknown Vector3 component '{name}'.");
            }
        }
        throw new JsonException("Unterminated Vector3 object.");
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }
}

[JsonSerializable(typeof(LoadingBaySnapshot))]
internal partial class LoadingBaySaveJsonContext : JsonSerializerContext
{
}

/// <summary>One current JSON game-state codec over the Engine product store. No schema numbers, no compatibility readers.</summary>
internal static class LoadingBaySnapshotCodec
{
    internal static JsonProductStateCodec<LoadingBaySnapshot> Create() => new(CreateTypeInfo());

    private static JsonTypeInfo<LoadingBaySnapshot> CreateTypeInfo()
    {
        JsonSerializerOptions options = new(LoadingBaySaveJsonContext.Default.Options);
        options.Converters.Add(new Vector3JsonConverter());
        return (JsonTypeInfo<LoadingBaySnapshot>)options.GetTypeInfo(typeof(LoadingBaySnapshot));
    }
}
