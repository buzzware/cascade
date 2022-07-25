// ReSharper disable ParameterHidesMember

using Cascade;
using SQLite;

namespace Cascade {
	public class Child : CascadeModel {
		
		[Cascade.CascadeId]
		[SQLite.PrimaryKey]
		public string id { get; set; }
		public int? parentId { get; set; }
		
		[SQLite.Ignore]
		[Cascade.BelongsTo(idProperty: "parentId")]
		public Parent? Parent { get; set; }
		
		public double? weight { get; set; }
		public double? power { get; set; }
		public int age { get; set; }
		public long updatedAtMs { get; set; }

		public override object CascadeId() {
			return id;
		}

		public Child() {
		}

		public Child withChanges(
			double? weight = null,
			double? power = null,
			long? updatedAtMs = null
		) {
			var result = new Child() {id = this.id};
			result.weight = weight ?? this.weight;
			result.power = power ?? this.power;
			result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
			return result;
		}
	}
}
