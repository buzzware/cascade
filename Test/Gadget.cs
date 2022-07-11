// ReSharper disable ParameterHidesMember

using Cascade;
using SQLite;

namespace Test {
	public class Gadget : CascadeModel {
		
		[PrimaryKey]
		public string id { get; set; }
		public double? weight { get; set; }
		public double? power { get; set; }
		public long updatedAtMs { get; set; }

		public override object CascadeId() {
			return id;
		}

		public Gadget() {
		}

		public Gadget withChanges(
			double? weight = null,
			double? power = null,
			long? updatedAtMs = null
		) {
			var result = new Gadget() {id = this.id};
			result.weight = weight ?? this.weight;
			result.power = power ?? this.power;
			result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
			return result;
		}
	}
}
