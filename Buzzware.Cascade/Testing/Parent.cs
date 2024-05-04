using System.Collections.Generic;

namespace Buzzware.Cascade.Testing {
	public class Parent : SuperModel {
		
		[Cascade.CascadeId]
		public long id { get; set; }
		
		[Cascade.HasMany(foreignIdProperty: "parentId")]
		public IEnumerable<Child>? Children { get; set; }
		
		public string? colour { get; set; }
		public string? Size { get; set; }
		public long updatedAtMs { get; set; }

		public Parent() {
		}
		
		public Parent withChanges(
			string? colour = null,
			string? size = null,
			long? updatedAtMs = null
		) {
			var result = new Parent() {id = this.id};
			result.colour = colour ?? this.colour;
			result.Size = size ?? this.Size;
			result.updatedAtMs = updatedAtMs ?? this.updatedAtMs;
			return result;
		}
	}
}
