using Cascade;
using SQLite;

namespace Test {
	public class Thing : CascadeModel<long> {
		
		[PrimaryKey, AutoIncrement]
		public long Id { get; set; }
		public string? Colour { get; set; }
		public string? Size { get; set; }
		public long UpdatedAtMs { get; set; }

		public override long CascadeId() {
			return Id;
		}

		public Thing() {
		}
		
		public Thing withChanges(
			string? colour = null,
			string? size = null,
			long? updatedAtMs = null
		) {
			var result = new Thing() {Id = this.Id};
			result.Colour = colour ?? this.Colour;
			result.Size = size ?? this.Size;
			result.UpdatedAtMs = updatedAtMs ?? this.UpdatedAtMs;
			return result;
		}
	}
}
