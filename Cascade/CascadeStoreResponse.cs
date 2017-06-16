namespace Cascade {
	public class CascadeStoreResponse<M>
	{
		public bool present = false;
		public bool connected = false;
		public M value = default(M);
	}
}