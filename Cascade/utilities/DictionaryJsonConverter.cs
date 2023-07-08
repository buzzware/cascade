using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cascade {

public class DictionaryJsonConverter : JsonConverter<Dictionary<string, object>>
{
    public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        return (ReadValue(ref reader) as Dictionary<string, object>)!;
    }

    private object ReadValue(ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
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
                var obj = new Dictionary<string, object>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return obj;

                    var key = reader.GetString();
                    reader.Read();
                    obj[key] = ReadValue(ref reader);
                }
                return obj;
            case JsonTokenType.StartArray:
                var list = new List<object>();
                while (reader.Read())
                {
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

    public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
    }

}
