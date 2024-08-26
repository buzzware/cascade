using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade {

  /// <summary>
  /// Custom JSON converter for serializing and deserializing 
  /// ImmutableDictionary<string, object?> using System.Text.Json.
  /// </summary>
  public class ImmutableDictionaryJsonConverter : JsonConverter<ImmutableDictionary<string, object?>>
  {
    /// <summary>
    /// Reads and converts JSON to an ImmutableDictionary<string, object?>. 
    /// Handles different token types and ensures proper deserialization.
    /// </summary>
    /// <param name="reader">The reference to the JSON reader.</param>
    /// <param name="typeToConvert">The type of object to convert, used for validation.</param>
    /// <param name="options">Serializer options for custom conversions.</param>
    /// <returns>Deserialized dictionary from JSON.</returns>
    public override ImmutableDictionary<string, object?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException();

      return (ReadValue(ref reader) as ImmutableDictionary<string, object?>)!;
    }

    /// <summary>
    /// Recursively reads JSON values and returns appropriate .NET objects.
    /// </summary>
    /// <param name="reader">The reference to the JSON reader.</param>
    /// <returns>Parsed value from JSON as the most appropriate .NET type.</returns>
    private object ReadValue(ref Utf8JsonReader reader)
    {
      switch (reader.TokenType)
      {
        case JsonTokenType.Number:
          // Attempt to parse the number as an integer, a long, or a double.
          if (reader.TryGetInt32(out int intValue))
              return intValue;
          else if (reader.TryGetInt64(out long longValue))
              return longValue;
          else
              return reader.GetDouble();
        case JsonTokenType.True:
        case JsonTokenType.False:
          return reader.GetBoolean();
        case JsonTokenType.String:
          return reader.GetString();
        case JsonTokenType.StartObject:
          var obj = ImmutableDictionary<string, object?>.Empty;
          while (reader.Read())
          {
            if (reader.TokenType == JsonTokenType.EndObject)
                return obj;

            var key = reader.GetString();
            reader.Read();
            obj = obj.Add(key,ReadValue(ref reader));
          }
          return obj;
        case JsonTokenType.StartArray:
          var list = ImmutableList<object>.Empty;
          while (reader.Read())
          {
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;
            list = list.Add(ReadValue(ref reader));
          }
          return list;
        case JsonTokenType.Null:
          return null;
        default:
          // Parse and clone JSON documents for complex or unexpected types.
          var jsonDocument = JsonDocument.ParseValue(ref reader);
          return jsonDocument.RootElement.Clone();
      }
    }

    // var jsonString = JsonSerializer.Serialize(value, options);
    // writer.WriteStringValue(jsonString);

    /// <summary>
    /// Writes the specified ImmutableDictionary<string, object?> to JSON.
    /// Iterates over key-value pairs to handle serialization.
    /// </summary>
    /// <param name="writer">JSON writer to output the JSON.</param>
    /// <param name="value">The dictionary value to serialize.</param>
    /// <param name="options">Serializer options for custom conversions.</param>
    public override void Write(Utf8JsonWriter writer, ImmutableDictionary<string, object?> value, JsonSerializerOptions options) {
      writer.WriteStartObject();
      foreach (KeyValuePair<string, object?> kvp in value) {
          writer.WritePropertyName(kvp.Key);
          WriteValue(writer, kvp.Value, options);
      }
      writer.WriteEndObject();
    }

    /// <summary>
    /// Serializes different types of values within the dictionary to JSON format.
    /// Handles nulls, numbers, booleans, strings, and nested objects and arrays.
    /// </summary>
    /// <param name="writer">JSON writer to output the JSON.</param>
    /// <param name="value">The object value to serialize.</param>
    /// <param name="options">Serializer options for custom conversions.</param>
    private void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options) {
      switch (value) {
        case null:
          writer.WriteNullValue();
          break;
        case int intValue:
          writer.WriteNumberValue(intValue);
          break;
        case long longValue:
          writer.WriteNumberValue(longValue);
          break;
        case double doubleValue:
          writer.WriteNumberValue(doubleValue);
          break;
        case bool boolValue:
          writer.WriteBooleanValue(boolValue);
          break;
        case string strValue:
          writer.WriteStringValue(strValue);
          break;
        case ImmutableDictionary<string, object?> dictionaryValue:
          Write(writer, dictionaryValue, options);
          break;
        case JsonObject jsonObject:
          JsonSerializer.Serialize(writer, value, options);                    
          break;                    
        case IEnumerable listValue:
          writer.WriteStartArray();
          foreach (object obj in listValue) {
              WriteValue(writer, obj, options);
          }
          writer.WriteEndArray();
          break;
        default:
          JsonSerializer.Serialize(writer, value, options);
          // Handle unexpected types by deferring to default serialization.
          // throw new JsonException("Unexpected value type: " + value.GetType());
          break;
      }
    }
  }
}