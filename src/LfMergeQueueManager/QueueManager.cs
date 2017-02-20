﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using Autofac;
using LfMerge.Core;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;
using Palaso.IO.FileLock;
using SIL.FieldWorks.FDO;

namespace LfMerge.QueueManager
{
	public static class QueueManager
	{
		[STAThread]
		public static void Main(string[] args)
		{
			var options = QueueManagerOptions.ParseCommandLineArgs(args);
			if (options == null)
				return;

			MainClass.Logger.Notice("LfMergeQueueManager starting with args: {0}", string.Join(" ", args));

			var settings = MainClass.Container.Resolve<LfMergeSettings>();
			var fileLock = SimpleFileLock.CreateFromFilePath(settings.LockFile);
			try
			{
				if (!fileLock.TryAcquireLock())
				{
					MainClass.Logger.Error("Can't acquire file lock - is another instance running?");
					return;
				}
				MainClass.Logger.Notice("Lock acquired");

				if (!CheckSetup(settings))
					return;

				for (var queue = Queue.FirstQueueWithWork;
					queue != null;
					queue = queue.NextQueueWithWork)
				{
					var clonedQueue = queue.QueuedProjects.ToList();
					foreach (var projectCode in clonedQueue)
					{
						var projectPath = Path.Combine(settings.FdoDirectorySettings.ProjectsDirectory,
							projectCode, string.Format("{0}{1}", projectCode,
								FdoFileHelper.ksFwDataXmlFileExtension));
						var modelVersion = FwProject.GetModelVersion(projectPath);
						MainClass.StartLfMerge(projectCode, queue.CurrentActionName,
							modelVersion, true);

						// TODO: Verify actions complete before dequeuing
						queue.DequeueProject(projectCode);
					}
				}
			}
			catch (Exception e)
			{
				MainClass.Logger.Error("Unhandled Exception:\n{0}", e);
				throw;
			}
			finally
			{
				if (fileLock != null)
					fileLock.ReleaseLock();

				MainClass.Container.Dispose();
			}

			MainClass.Logger.Notice("LfMergeQueueManager finished");
		}

		private static bool CheckSetup(LfMergeSettings settings)
		{
			var homeFolder = Environment.GetEnvironmentVariable("HOME") ?? "/var/www";
			string[] folderPaths = { Path.Combine(homeFolder, ".local"),
				Path.GetDirectoryName(settings.WebWorkDirectory)
			};
			foreach (string folderPath in folderPaths)
			{
				if (!Directory.Exists(folderPath))
				{
					MainClass.Logger.Notice("Folder '{0}' doesn't exist", folderPath);
					return false;
				}
			}

			return true;
		}
	}
}
