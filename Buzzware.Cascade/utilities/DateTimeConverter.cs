using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buzzware.Cascade {
	
	public class DateTimeJsonConverter : JsonConverter<DateTime> {
		
		/// <summary>
		/// Always deserialize DateTime as local time because DateTimes are local by default 
		/// </summary>
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			return DateTime.Parse(reader.GetString()!).ToLocalTime();
		}

		/// <summary>
		///	serialize DateTimes as UTC with Z suffix. Add milliseconds if not 0 
		/// </summary>
		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) {
			var format = value.Millisecond==0 ? "yyyy-MM-ddTHH:mm:ssZ" : "yyyy-MM-dd HH:mm:ss.fffZ";
			var s = value.ToUniversalTime().ToString(format);
			writer.WriteStringValue(s);
		}
	}
}
