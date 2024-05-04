// ReSharper disable ParameterHidesMember




namespace Buzzware.Cascade.Testing {
	public class Child : SuperModel {
		
		[Cascade.CascadeId]
		public string id { get; set; }
		public int? parentId { get; set; }
		
		[Cascade.BelongsTo(idProperty: "parentId")]
		public Parent? Parent { get; set; }
		
		public double? weight { get; set; }
		public double? power { get; set; }
		public int age { get; set; }
		public long updatedAtMs { get; set; }

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
