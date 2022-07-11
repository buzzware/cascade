using Cascade;
using SQLite;

namespace Test {
	public class Thing : CascadeModel {
		
		[PrimaryKey, AutoIncrement]
		public long id { get; set; }
		public string? colour { get; set; }
		public string? Size { get; set; }
		public long updatedAtMs { get; set; }

		public override object CascadeId() {
			return id;
		}

		public Thing() {
		}
		
		public Thing withChanges(
			string? colour = null,
			string? size = null,
			long? updatedAtMs = null
		) {
			var result = new Thing() {id = this.id};
			result.colour = colour ?? this.colour;
			result.Size = size ?? this.Size;
			result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
			return result;
		}
	}
}
