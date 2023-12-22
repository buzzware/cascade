

// using SQLite;

namespace Cascade.Testing {
	
	public class Thing : SuperModel {

		// for setting proxyFor
		public Thing(Thing? proxyFor=null) : base(proxyFor) {
		}
		
		// for JSON deserialize
		public Thing() : base(null) {
		}
		
		[CascadeId]
		// [PrimaryKey]
		public int id {
			get => GetProperty(ref _id); 
			set => SetProperty(ref _id, value);
		}
		private int _id;

		public string? name {
			get => GetProperty(ref _name); 
			set => SetProperty(ref _name, value);
		}
		private string? _name;

		public string? colour {
			get => GetProperty(ref _colour);
			set => SetProperty(ref _colour, value);
		}
		private string? _colour;
	}
}
