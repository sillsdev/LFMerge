﻿// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Logging;
using LfMerge.Core.Settings;
using SIL.LCModel;
using SIL.Progress;
using SIL.WritingSystems;

namespace LfMergeAuxTool
{
	internal static class LfMergeAuxToolMainClass
	{
		[STAThread]
		public static void Main(string[] args)
		{
			ExceptionLogging.Initialize("17a42e4a67dd2e42d4aa40d8bf2d23ee", Assembly.GetExecutingAssembly().GetName().Name);
			var options = AuxToolOptions.ParseCommandLineArgs(args);
			if (options == null)
				return;

			if (!File.Exists(options.Project))
			{
				Console.WriteLine("Can't find project file '{0}'", options.Project);
				return;
			}

			Icu.Wrapper.Init();

			// initialize the SLDR
			Sldr.Initialize();

			try
			{
				var projectFileInfo = new FileInfo(options.Project);
				options.Project = projectFileInfo.FullName;

				if (options.InfoOnly)
					DisplayInfo(options);

				ExceptionLogging.Client.AddInfo(options.Project,
					FwProject.GetModelVersion(options.Project));

				if (options.Migrate)
					MigrateData(options);

				if (options.Commit)
					CommitData(options);
			}
			finally
			{
				if (Sldr.IsInitialized)
					Sldr.Cleanup();

				Icu.Wrapper.Cleanup();
			}
		}

		private static void DisplayInfo(AuxToolOptions options)
		{
			Console.WriteLine("{0} has model version {1}",
				Path.GetFileName(options.Project),
				FwProject.GetModelVersion(options.Project));
		}

		private static void CommitData(AuxToolOptions options)
		{
			var hgDir = Path.Combine(Path.GetDirectoryName(options.Project), ".hg");
			if (!Directory.Exists(hgDir))
			{
				Console.WriteLine(
					"It looks the project isn't setup for S/R - can't find '{0}'",
					hgDir);
				return;
			}

			// Call into LF Bridge to do the work.
			string syncResult;
			var mergeBridgeOptions = new Dictionary<string, string> {
				{ "fullPathToProject", Path.GetDirectoryName(options.Project) },
				{ "fwdataFilename", options.Project },
				{ "fdoDataModelVersion", LcmCache.ModelVersion.ToString() },
			};
			var result = LfMergeBridge.LfMergeBridge.Execute(
				"Language_Forge_Auxiliary_Commit",
				new NullProgress(), mergeBridgeOptions, out syncResult);

			Console.WriteLine(syncResult);
			if (result)
				Console.WriteLine("Successfully updated .hg files");
		}

		private static void MigrateData(AuxToolOptions options)
		{
			var oldVersion = FwProject.GetModelVersion(options.Project);
			try
			{
				var project = Path.Combine(Path.GetDirectoryName(options.Project),
					Path.GetFileNameWithoutExtension(options.Project));
				FwProject.AllowDataMigration = true;
				using (new FwProject(new LfMergeSettings(), project))
				{
					Console.WriteLine("Migrated {0} from {1} to {2}",
						Path.GetFileName(options.Project), oldVersion,
						FwProject.GetModelVersion(options.Project));
				}
			}
			catch (LcmDataMigrationForbiddenException)
			{
				Console.WriteLine("LCM: Incompatible version (can't migrate data)");
			}
			catch (LcmNewerVersionException)
			{
				Console.WriteLine(
					"LCM: Incompatible version (version number newer than expected)");
			}
			catch (LcmFileLockedException)
			{
				Console.WriteLine("LCM: Access denied");
			}
			catch (Exception e)
			{
				Console.WriteLine("LCM: Unknown error: {0} exception: {1}", e.GetType(),
					e.Message);
			}
		}
	}
}
