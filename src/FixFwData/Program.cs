// Copyright (c) 2011-2013 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
//
// File: Program.cs
// Responsibility: FLEx team

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using LfMerge.Core.Logging;
using SIL.LCModel.FixData;
using SIL.LCModel.Utils;

namespace FixFwData
{
	class Program
	{
		[SuppressMessage("Gendarme.Rules.Portability", "ExitCodeIsLimitedOnUnixRule",
			Justification = "Appears to be a bug in Gendarme...not recognizing that 0 and 1 are in correct range (0..255)")]
		private static int Main(string[] args)
		{
			SetUpErrorHandling();
			var pathname = args[0];
			using (var prog = new LoggingProgress())
			{
				var data = new FwDataFixer(pathname, prog, logError, getErrorCount);
				data.FixErrorsAndSave();
			}
			return errorsOccurred ? 1 : 0;
		}

		private static bool errorsOccurred = false;
		private static int errorCount = 0;

		private static void logError(string description, bool errorFixed)
		{
			Console.WriteLine(description);

			errorsOccurred = true;
			if (errorFixed)
				++errorCount;
		}

		private static int getErrorCount()
		{
			return errorCount;
		}

		private static void SetUpErrorHandling()
		{
			ExceptionLogging.Initialize("17a42e4a67dd2e42d4aa40d8bf2d23ee", Assembly.GetExecutingAssembly().GetName().Name);
		}

		private sealed class LoggingProgress : IProgress, IDisposable
		{
			public event CancelEventHandler Canceling;

			public void Step(int amount)
			{
				if (Canceling != null)
				{
					// don't do anything -- this just shuts up the compiler about the
					// event handler never being used.
				}
			}

			public string Title { get; set; }

			public string Message
			{
				get { return null; }
				set
				{
					Console.Out.WriteLine(value);
				}
			}

			public int Position { get; set; }
			public int StepSize { get; set; }
			public int Minimum { get; set; }
			public int Maximum { get; set; }
			public ISynchronizeInvoke SynchronizeInvoke { get; private set; }
			public bool IsIndeterminate
			{
				get { return false; }
				set { }
			}

			public bool AllowCancel
			{
				get { return false; }
				set { }
			}
			#region Gendarme required cruft
#if DEBUG
			/// <summary/>
			~LoggingProgress()
			{
				Dispose(false);
			}
#endif

			/// <summary/>
			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			/// <summary/>
			private void Dispose(bool fDisposing)
			{
				System.Diagnostics.Debug.WriteLineIf(!fDisposing, "****** Missing Dispose() call for " + GetType() + ". *******");
			}
			#endregion
		}
	}
}
