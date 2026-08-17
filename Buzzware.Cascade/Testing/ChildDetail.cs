namespace Buzzware.Cascade.Testing {

	/// <summary>
	/// A sample detail model belonging to a Child, for demonstrating SuperModel and the HasOne/BelongsTo associations.
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

		/// <summary>
		/// The id of the Child this detail belongs to.
		/// </summary>
		public string? childId {
			get => GetProperty(ref _childId);
			set => SetProperty(ref _childId, value);
		}
		private string? _childId;
   
		/// <summary>
		/// The Child association model property, resolved from childId.
		/// </summary>
		[Cascade.BelongsTo(idProperty: "childId")]
		public Child? Child {
			get => GetProperty(ref _child);
			set => SetProperty(ref _child, value);
		}
		private Child? _child;
		
		/// <summary>
		/// The description attribute of the detail.
		/// </summary>
		public string description {
			get => GetProperty(ref _description);
			set => SetProperty(ref _description, value);
		}
		private string _description;
	}
}
