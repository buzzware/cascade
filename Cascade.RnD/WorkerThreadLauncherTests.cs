using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Serilog;
using Serilog.Events;

namespace Cascade.RnD {
	
	
	
	
	
	[TestFixture]
	public class WorkerThreadLauncherTests {

		public static string RepeatedString(string s, int times) {
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < times; i++)
				sb.Append(s);
			return sb.ToString();		
		}
		
		private string tempDir;
		private string testString;

		[SetUp]
		
		public void SetUp() {
			Log.Logger = new LoggerConfiguration()
					.MinimumLevel.Is(LogEventLevel.Debug)
					//.WriteTo.Console()
					.WriteTo.Debug()
				.CreateLogger();
			
			tempDir = "/Users/gary/repos/civmec/CivtracDispatch/cascade/Cascade.RnD/temp/WorkerThreadLauncherTests";
			Log.Debug($"Cascade cache directory {tempDir}");
			if (Directory.Exists(tempDir))
				Directory.Delete(tempDir,true);
			Directory.CreateDirectory(tempDir);
		}
		
		
		[Test]
		public async Task TestWorkerThreadLauncher() {

			testString = RepeatedString("TestText", 125);
			
			var launcher = new WorkerThreadLauncher(
				token => {
					CascadeUtils.EnsureFileOperationSync(() => {
						CascadeUtils.WriteStringToFile(Path.Combine(tempDir, "multiwritefile.txt"), testString);
					},5,100);
				}, 
				1000, 
				10000
			);

			launcher.Start();
			await launcher.WaitCompleteAsync();

			Log.Debug($"{launcher.Exceptions.Count()} exceptions");
			foreach (var exception in launcher.Exceptions) {
				Log.Debug("Exception {Exception}", exception.GetType().FullName);
			}
			
			Log.Debug("");
			
			
			//Assert.That(launcher.Exceptions.Count() + launcher.SuccessfulTasks, Is.EqualTo(100));
		}
	}
}
