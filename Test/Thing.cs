using Cascade;

namespace Test {
	public class Thing : CascadeModel {
		public long Id { get; set; }
		public string Colour { get; set; }
		public string Size { get; set; }
		public override string CascadeId() {
			return Id.ToString();
		}
	}
}