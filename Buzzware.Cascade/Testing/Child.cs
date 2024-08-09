namespace Buzzware.Cascade.Testing {
	public class Child : SuperModel {
   
		[Cascade.CascadeId]
		public string id {
			get => GetProperty(ref _id);
			set => SetProperty(ref _id, value);
		}
		private string _id;

		public int? parentId {
			get => GetProperty(ref _parentId);
			set => SetProperty(ref _parentId, value);
		}
		private int? _parentId;
   
		[Cascade.BelongsTo(idProperty: "parentId")]
		public Parent? Parent {
			get => GetProperty(ref _parent);
			set => SetProperty(ref _parent, value);
		}
		private Parent? _parent;
   
		public double? weight {
			get => GetProperty(ref _weight);
			set => SetProperty(ref _weight, value);
		}
		private double? _weight;

		public double? power {
			get => GetProperty(ref _power);
			set => SetProperty(ref _power, value);
		}
		private double? _power;

		public int age {
			get => GetProperty(ref _age);
			set => SetProperty(ref _age, value);
		}
		private int _age;

		public long updatedAtMs {
			get => GetProperty(ref _updatedAtMs);
			set => SetProperty(ref _updatedAtMs, value);
		}
		private long _updatedAtMs;

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
