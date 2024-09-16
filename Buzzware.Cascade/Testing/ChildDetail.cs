namespace Buzzware.Cascade.Testing {

	/// <summary>
	/// A Child model for demonstrating SuperModel and associations (with Parent). Not a human child.
	/// </summary>
	public class ChildDetail : SuperModel {

		/// <summary>
		/// The unique identifier
		/// </summary>
		[Cascade.CascadeId]
		public string id {
			get => GetProperty(ref _id);
			set => SetProperty(ref _id, value);
		}
		private string _id;

		public string? childId {
			get => GetProperty(ref _childId);
			set => SetProperty(ref _childId, value);
		}
		private string? _childId;
   
		[Cascade.BelongsTo(idProperty: "childId")]
		public Child? Child {
			get => GetProperty(ref _child);
			set => SetProperty(ref _child, value);
		}
		private Child? _child;
		
		public string description {
			get => GetProperty(ref _description);
			set => SetProperty(ref _description, value);
		}
		private string _description;
	}
}
