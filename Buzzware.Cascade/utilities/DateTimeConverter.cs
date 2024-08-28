using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Buzzware.Cascade {
	
	/// <summary>
	/// Default Json converter for DateTime and DateTime? typed properties
	/// </summary>
	public class DateTimeJsonConverter : JsonConverter<DateTime?> {
		
		/// <summary>
		///	1. If the timezone is specified, decode the date and time using it
		/// 2. When no timezone is specified, assume the local timezone
		/// 3. Always return a DateTime with kind == Local so will be converted to the local time
		/// </summary>
		public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
			string? dateString = reader.GetString();
			if (string.IsNullOrEmpty(dateString))
				return typeToConvert == typeof(DateTime?) ? null : default(DateTime);

			try {
				DateTime parsedDate = DateTime.Parse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

				switch (parsedDate.Kind) {
					case DateTimeKind.Unspecified: parsedDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Local); break;
					case DateTimeKind.Utc: parsedDate = parsedDate.ToLocalTime();	break;
					case DateTimeKind.Local: break;
				}
				
				return parsedDate;
			} catch {
				return typeToConvert == typeof(DateTime?) ? null : default(DateTime);
			}
		}

		/// <summary>
		///	Always write the date and time with Z (UTC) timezone
		/// Milliseconds are only written if they are not 0 
		/// </summary>
		public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options) {
			if (value.HasValue) {
				var utcTime = value.Value.ToUniversalTime();
				var format = utcTime.Millisecond == 0 ? "yyyy-MM-ddTHH:mm:ssZ" : "yyyy-MM-ddTHH:mm:ss.fffZ";
				writer.WriteStringValue(utcTime.ToString(format));
			} else {
				writer.WriteNullValue();
			}
		}
	}
}
