using Cascade;

namespace Test {
	public class Thing : CascadeModel<long> {
		public long Id { get; set; }
		public string Colour { get; set; }
		public string Size { get; set; }
		public override long CascadeId() {
			return Id;
		}
	}
}
