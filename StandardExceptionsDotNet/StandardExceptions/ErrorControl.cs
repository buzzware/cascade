using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StandardExceptions {
	
	/*
	
	Filters map an exception to either another exception (perhaps wrapping 
	the first one using the StandardException Inner property) OR
	to null which causes the incoming exception to be smothered.
	
	Reporters are handlers that report an exception eg. to the console or an 
	error service like Raygun.  
		
	FilterGuard calls the provided function, and any exceptions thrown will be filtered and rethrown.
		
	There is potential for other types of guards eg ones that will never "leak" any exceptions, and ones 
	that call Report. 
	
	Ideas : 
	FilterReportSafeGuard() - exceptions are filtered and reported and never leaked out  
		
	*/
	
	public interface IErrorControlFilter {
		Exception Filter(Exception e);
	}
	
	public interface IErrorControlReporter {
		Task Report(Exception exception);
	}

	public class ErrorControl {
		
		protected static ErrorControl instance;
		public static void Reset(ErrorControl errorControl = null) {
			instance = errorControl;
		} 
		
		public static ErrorControl Instance {
			get {
				if (instance == null)
					instance = new ErrorControl();
				return instance;
			}
		}

		// public static Exception Filter(Exception e) {
		// 	return ErrorControl.Instance.Filter(e);
		// }
		
		protected IList<IErrorControlFilter> filters = new List<IErrorControlFilter>();
		protected IList<IErrorControlReporter> reporters = new List<IErrorControlReporter>();

		public void PrependFilter(IErrorControlFilter filter) {
			filters.Insert(0,filter);
		}

		public void AppendFilter(IErrorControlFilter filter) {
			filters.Add(filter);
		}

		public Exception Filter(Exception exception) {
			Exception result = exception;
			foreach (var f in filters) {
				result = f.Filter(result);
			}
			return result;
		}
		
		public void PrependReporter(IErrorControlReporter reporter) {
			reporters.Insert(0,reporter);
		}

		public void AppendReporter(IErrorControlReporter reporter) {
			reporters.Add(reporter);
		}
		
		public async Task Report(Exception exception) {
			foreach (var r in reporters) {
				await r.Report(exception);
			}
		}

		public async Task<T> FilterGuard<T>(Func<Task<T>> f) {
			try {
				return await f();
			} catch(Exception e) {
				var filtered_e = this.Filter(e);
				if (filtered_e != null) {
					if (filtered_e == e)
						throw;
					else
						throw filtered_e;
				} 
			}
			return default;
		}
		
		public T FilterGuard<T>(Func<T> f) {
			try {
				return f();
			} catch(Exception e) {
				var filtered_e = this.Filter(e);
				if (filtered_e != null) {
					if (filtered_e == e)
						throw;
					else
						throw filtered_e;
				} 
			}
			return default;
		}
	}
}
