﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using Chorus.Model;
using LibFLExBridgeChorusPlugin;
using LibFLExBridgeChorusPlugin.Infrastructure;
using SIL.Progress;
using SIL.Reporting;
using LibTriboroughBridgeChorusPlugin.Infrastructure;
using LibTriboroughBridgeChorusPlugin;

namespace LfMerge.Actions
{
	public class ReceiveAction: Action
	{
		private IProgress Progress { get; set; }

		protected override ProcessingState.SendReceiveStates StateForCurrentAction
		{
			get { return ProcessingState.SendReceiveStates.RECEIVING; }
		}

		protected override void DoRun(ILfProject project)
		{
			Progress = new ConsoleProgress();
			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var model = scope.Resolve<InternetCloneSettingsModel>();
				model.ParentDirectoryToPutCloneIn = LfMergeSettings.Current.WebWorkDirectory;
				model.AccountName = project.LanguageDepotProject.Username;
				model.Password = project.LanguageDepotProject.Password;
				model.ProjectId = project.LanguageDepotProject.ProjectCode;
				model.LocalFolderName = project.LfProjectName;
				model.AddProgress(Progress);

				try
				{
					if (!Directory.Exists(model.ParentDirectoryToPutCloneIn) ||
						model.TargetLocationIsUnused)
					{
						InitialClone(model);
						if (!FinishClone(project))
							project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
					}
				}
				catch (UnauthorizedAccessException)
				{
					project.State.SRState = ProcessingState.SendReceiveStates.HOLD;
					throw;
				}
			}
		}

		protected override ActionNames NextActionName
		{
			get { return ActionNames.Merge; }
		}

		private static string GetProjectDirectory(string projectCode)
		{
			return Path.Combine(LfMergeSettings.Current.WebWorkDirectory, projectCode);
		}

		private void InitialClone(InternetCloneSettingsModel model)
		{
			model.DoClone();
		}

		private bool FinishClone(ILfProject project)
		{
			var actualCloneResult = new ActualCloneResult();

			var cloneLocation = GetProjectDirectory(project.LfProjectName);
			var newProjectFilename = Path.GetFileName(project.LfProjectName) + SharedConstants.FwXmlExtension;
			var newFwProjectPathname = Path.Combine(cloneLocation, newProjectFilename);

			using (var scope = MainClass.Container.BeginLifetimeScope())
			{
				var helper = scope.Resolve<UpdateBranchHelperFlex>();
				if (!helper.UpdateToTheCorrectBranchHeadIfPossible(
				/*FDOBackendProvider.ModelVersion*/ "7000068", actualCloneResult, cloneLocation))
				{
					actualCloneResult.Message = "Flex version is too old";
				}

				switch (actualCloneResult.FinalCloneResult)
				{
					case FinalCloneResult.ExistingCloneTargetFolder:
						Logger.WriteEvent("Clone failed: Flex project exists: {0}", cloneLocation);
						if (Directory.Exists(cloneLocation))
							Directory.Delete(cloneLocation, true);
						return false;
					case FinalCloneResult.FlexVersionIsTooOld:
						Logger.WriteEvent("Clone failed: Flex version is too old; project: {0}",
							project.LfProjectName);
						if (Directory.Exists(cloneLocation))
							Directory.Delete(cloneLocation, true);
						return false;
					case FinalCloneResult.Cloned:
						break;
				}

				var projectUnifier = scope.Resolve<IProjectUnifier>();
				projectUnifier.PutHumptyTogetherAgain(Progress, false, newFwProjectPathname);
				return true;
			}
		}
	}
}

