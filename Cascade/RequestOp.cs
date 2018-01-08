using System.Collections.Generic;

namespace Cascade {
	public class RequestOp {

		public enum Verbs {
			None,
			Create,
			Read,
			ReadAll,
			Update,
			Destroy,
			Execute
		};

		public static bool IsWriteVerb(Verbs aVerb) {
			return aVerb == RequestOp.Verbs.Create ||
			       aVerb == RequestOp.Verbs.Update ||
			       aVerb == RequestOp.Verbs.Execute;
		}

		public static Verbs VerbFromString(string aString) {
			Verbs verb;
			return Verbs.TryParse(aString, true, out verb) ? verb : Verbs.None;
		}
				
		public Verbs Verb;		// what we are doing
		public int Index;		
		
		// only one of Key or Id would normally be used
		public string Key;		// eg. Products or Products__34
		public string Id;			// eg. 34
		
		public object Value;			// the value we are writing/updating/creating with
		public string ResultKey;	// a key for storing the result value under

		public bool Fresh = false;		// must go direct to the server first instead of caches
		public bool Fallback = true;	// when the first source request fails (eg. does not contain or is offline), try other sources

		public IDictionary<string, string> Params;	// app specific paramters for the request
		public bool Exclusive = false;				// probably should deprecate this
	}
}
