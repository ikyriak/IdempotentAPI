using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdempotentAPI.Helpers;

/// <summary>
/// A <see cref="JsonConverter{T}"/> for <see cref="object"/>-typed values that preserves the
/// runtime type of the serialized value so it can be restored on deserialization. It emulates
/// the Newtonsoft.Json <c>TypeNameHandling.All</c>/<c>TypeNameHandling.Auto</c> behavior:
/// <list type="bullet">
///     <item>JSON primitives (string, boolean, numbers) are written as-is and read back as <see cref="string"/>, <see cref="bool"/>, <see cref="long"/> or <see cref="double"/>.</item>
///     <item>Any other value is written as <c>{"$type":"&lt;AssemblyQualifiedName&gt;","$value":&lt;value&gt;}</c> and deserialized to that exact type.</item>
///     <item>Objects/arrays without type information are read as <c>Dictionary&lt;string, object?&gt;</c>/<c>List&lt;object?&gt;</c>.</item>
/// </list>
/// </summary>
public sealed class TypedObjectJsonConverter : JsonConverter<object>
{
    private const string TypePropertyName = "$type";
    private const string ValuePropertyName = "$value";

    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(object);
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number:
                return reader.TryGetInt64(out long longValue) ? longValue : reader.GetDouble();
            case JsonTokenType.StartArray:
                return JsonSerializer.Deserialize<List<object?>>(ref reader, options);
            case JsonTokenType.StartObject:
                return ReadObject(ref reader, options);
            default:
                throw new JsonException($"Unexpected token '{reader.TokenType}' when deserializing an object value.");
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Type runtimeType = value.GetType();

        if (IsJsonPrimitive(runtimeType))
        {
            JsonSerializer.Serialize(writer, value, runtimeType, options);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString(TypePropertyName, runtimeType.AssemblyQualifiedName);
        writer.WritePropertyName(ValuePropertyName);
        JsonSerializer.Serialize(writer, value, runtimeType, options);
        writer.WriteEndObject();
    }

    private static object? ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty(TypePropertyName, out JsonElement typeElement)
            || typeElement.ValueKind != JsonValueKind.String)
        {
            return root.Deserialize<Dictionary<string, object?>>(options);
        }

        string? typeName = typeElement.GetString();
        Type? type = typeName is null ? null : Type.GetType(typeName, throwOnError: false);
        if (type is null)
        {
            throw new JsonException($"Cannot resolve the type '{typeName}' of the serialized object value.");
        }

        if (!root.TryGetProperty(ValuePropertyName, out JsonElement valueElement))
        {
            throw new JsonException($"The '{ValuePropertyName}' property is missing for the serialized object of type '{typeName}'.");
        }

        return valueElement.Deserialize(type, options);
    }

    private static bool IsJsonPrimitive(Type type)
    {
        return type == typeof(string)
            || type == typeof(bool)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);
    }
}
