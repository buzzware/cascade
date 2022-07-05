using Cascade;
using SQLite;

namespace Test {
	public class Gadget : CascadeModel<string> {
		
		[PrimaryKey]
		public string Id { get; set; }
		public double? Weight { get; set; }
		public double? Power { get; set; }
		public long UpdatedAtMs { get; set; }

		public override string CascadeId() {
			return Id;
		}

		public Gadget() {
		}

		public Gadget withChanges(
			double? weight = null,
			double? power = null,
			long? updatedAtMs = null
		) {
			var result = new Gadget() {Id = this.Id};
			result.Weight = weight ?? this.Weight;
			result.Power = power ?? this.Power;
			result.UpdatedAtMs = updatedAtMs ?? this.UpdatedAtMs;
			return result;
		}
	}
}
