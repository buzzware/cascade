using System.Collections.Generic;
namespace Buzzware.Cascade.Testing {
	public class Parent : SuperModel {
        
		[Cascade.CascadeId]
		public long id {
			get => GetProperty(ref _id);
			set => SetProperty(ref _id, value);
		}
		private long _id;
        
		[Cascade.HasMany(foreignIdProperty: "parentId")]
		public IEnumerable<Child>? Children {
			get => GetProperty(ref _children);
			set => SetProperty(ref _children, value);
		}
		private IEnumerable<Child>? _children;
        
		public string? colour {
			get => GetProperty(ref _colour);
			set => SetProperty(ref _colour, value);
		}
		private string? _colour;

		public string? Size {
			get => GetProperty(ref _size);
			set => SetProperty(ref _size, value);
		}
		private string? _size;

		public long updatedAtMs {
			get => GetProperty(ref _updatedAtMs);
			set => SetProperty(ref _updatedAtMs, value);
		}
		private long _updatedAtMs;

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
