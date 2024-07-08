using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade {

    public class DictionaryJsonConverter : JsonConverter<Dictionary<string, object?>> {
        public override Dictionary<string, object?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            return (ReadValue(ref reader) as Dictionary<string, object?>)!;
        }

        private object ReadValue(ref Utf8JsonReader reader) {
            switch (reader.TokenType) {
                case JsonTokenType.Number:
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
                    var obj = new Dictionary<string, object?>();
                    while (reader.Read()) {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            return obj;

                        var key = reader.GetString();
                        reader.Read();
                        obj[key] = ReadValue(ref reader);
                    }

                    return obj;
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read()) {
                        if (reader.TokenType == JsonTokenType.EndArray)
                            return list;
                        list.Add(ReadValue(ref reader));
                    }

                    return list;
                case JsonTokenType.Null:
                    return null;
                default:
                    var jsonDocument = JsonDocument.ParseValue(ref reader);
                    return jsonDocument.RootElement.Clone();
            }
        }

        // var jsonString = JsonSerializer.Serialize(value, options);
        // writer.WriteStringValue(jsonString);


        public override void Write(Utf8JsonWriter writer, Dictionary<string, object?> value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, object?> kvp in value) {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value, options);
            }

            writer.WriteEndObject();
        }
        
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
                case Dictionary<string, object?> dictionaryValue:
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
                    //throw new JsonException("Unexpected value type: " + value.GetType());
                    break;
            }
        }
    }
}
