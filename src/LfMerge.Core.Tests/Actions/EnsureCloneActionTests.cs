﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using LfMerge.Core.MongoConnector;
using NUnit.Framework;

namespace LfMerge.Core.Tests.Actions
{
	/// <summary>
	/// These tests test the behavior of LfMerge in various conditions related to cloning a
	/// project from LD. The cloning is mocked; the important part is how LfMerge behaves.
	/// </summary>
	[TestFixture]
	public class EnsureCloneActionTests
	{
		private TestEnvironment _env;
		private string _projectCode;
		private MongoProjectRecordFactoryDouble _mongoProjectRecordFactory;
		private MongoConnectionDouble _mongoConnection;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_projectCode = TestContext.CurrentContext.Test.Name.ToLowerInvariant();
			var _projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(_projectDir);
			// Making a stub file so Chorus model.TargetLocationIsUnused will be false
			File.Create(Path.Combine(_projectDir, "stub"));
			_mongoProjectRecordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			_mongoConnection = _mongoProjectRecordFactory.Connection as MongoConnectionDouble;
			if (_mongoConnection == null)
				throw new AssertionException("EnsureClone action tests need a mock MongoConnection that stores data in order to work.");

		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[Test]
		public void EnsureClone_NonExistingProject_SetsStateOnError()
		{
			// for this test we don't want the test double for InternetCloneSettingsModel
			_env.Dispose();
			_env = new TestEnvironment(registerSettingsModelDouble: false);

			// Setup
			var nonExistingProjectCode = Path.GetRandomFileName().ToLowerInvariant();
			var lfProject = LanguageForgeProject.Create(nonExistingProjectCode);

			// Execute
			Assert.That( () => new EnsureCloneActionDouble(_env.Settings, _env.Logger,
				_mongoProjectRecordFactory, _mongoConnection, false).Run(lfProject),
				Throws.Nothing);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
		}

		[Test]
		public void EnsureClone_OtherException_SetsStateOnHold()
		{
			// for this test we don't want the test double for InternetCloneSettingsModel
			_env.Dispose();
			_env = new TestEnvironment(registerSettingsModelDouble: false);

			// Setup
			var nonExistingProjectCode = Path.GetRandomFileName().ToLowerInvariant();
			var lfProject = LanguageForgeProject.Create(nonExistingProjectCode);

			// Execute/Verify
			Assert.That( () => new EnsureCloneActionDouble(_env.Settings, _env.Logger,
				_mongoProjectRecordFactory, _mongoConnection, false, false).Run(lfProject),
				Throws.Exception);

			// In the real app the exception gets caught and the status set in Program.cs
		}

		[Test]
		public void EnsureClone_StateFileDoesntExistAndStateNotCloning_ClonesProject()
		{
			// Not a valid real-world scenario, but we test this anyway
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			lfProject.State.SRState = ProcessingState.SendReceiveStates.HOLD;
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.False,
				"State file shouldn't exist");
			Assert.AreNotEqual(lfProject.State.SRState, ProcessingState.SendReceiveStates.CLONING);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection).Run(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONED),
				"State should be CLONED");
			// TestDouble doesn't write state file
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.False,
				"State file shouldn't exist yet");
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_StateFileDoesntExistAndStateCloning_CloneProject()
		{
			// Setup
			var projectCode = TestContext.CurrentContext.Test.Name.ToLowerInvariant();
			var lfProject = LanguageForgeProject.Create(projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, projectCode);
			lfProject.State.SRState = ProcessingState.SendReceiveStates.CLONING;
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.False,
				"State file shouldn't exist");
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection).Run(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONED),
				"State should be CLONED");
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_StateFileExistsStateNotCloning_DoesntCloneProject()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			lfProject.State.SRState = ProcessingState.SendReceiveStates.HOLD;
			File.Create(_env.Settings.GetStateFileName(_projectCode));
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.True,
				"State file should exist");
			Assert.That(lfProject.State.SRState, !Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");
			var me = Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories).Length == 0;
			Console.WriteLine("{0}", me);

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection).Run(lfProject);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");
		}

		[Test]
		public void EnsureClone_StateFileExistsStateCloning_CloneProject()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			File.Create(_env.Settings.GetStateFileName(_projectCode));
			lfProject.State.SRState = ProcessingState.SendReceiveStates.CLONING;
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.True,
				"State file should exist");
			Assert.AreEqual(lfProject.State.SRState, ProcessingState.SendReceiveStates.CLONING);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection).Run(lfProject);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_ProjectDirExistHgDoesntExist_ClonesProject()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			Assert.That(Directory.Exists(projectDir), Is.True,
				"Didn't create webwork directory: " + projectDir);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist yet");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection).Run(lfProject);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_ProjectThatHasNeverBeenCloned_RunsInitialClone()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var action = new EnsureCloneActionDoubleMockingInitialTransfer(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection);
			Assert.That(action.InitialCloneWasRun, Is.False);

			// Execute
			action.Run(lfProject);

			// Verify
			Assert.That(action.InitialCloneWasRun, Is.True);
		}

		[Test]
		public void EnsureClone_ProjectThatHasAPreviouslyClonedDate_DoesNotRunInitialClone()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			_mongoConnection.SetLastSyncedDate(lfProject, DateTime.UtcNow);
			var action = new EnsureCloneActionDoubleMockingInitialTransfer(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection);
			Assert.That(action.InitialCloneWasRun, Is.False);

			// Execute
			action.Run(lfProject);

			// Verify
			Assert.That(action.InitialCloneWasRun, Is.False);
		}

		[Test]
		public void EnsureClone_ProjectThatHasAPreviouslyClonedDateButDoesNotHaveAnSRProjectCode_RunsInitialClone()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			_mongoConnection.SetLastSyncedDate(lfProject, DateTime.UtcNow);
			var projectRecord = _mongoProjectRecordFactory.Create(lfProject);
			projectRecord.SendReceiveProjectIdentifier = null;
			var action = new EnsureCloneActionDoubleMockingInitialTransfer(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection);
			Assert.That(action.InitialCloneWasRun, Is.False);

			// Execute
			action.Run(lfProject);

			// Verify
			Assert.That(action.InitialCloneWasRun, Is.True);
		}

		[Test]
		public void EnsureClone_ProjectThatHasPreviousUserData_DoesNotRunInitialClone()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var data = new SampleData();
			_mongoConnection.UpdateMockLfLexEntry(data.bsonTestData);
			var action = new EnsureCloneActionDoubleMockingInitialTransfer(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection);
			Assert.That(action.InitialCloneWasRun, Is.False);

			// Execute
			action.Run(lfProject);

			// Verify
			Assert.That(action.InitialCloneWasRun, Is.False);
		}

		[Test]
		public void EnsureClone_CloneEmptyRepo_SetsRecoverableError()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var action = new EnsureCloneActionDoubleMockErrorCondition(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection,
				"Clone failure: new repository with no commits. Clone deleted.");

			// Execute
			action.Run(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(lfProject.State.ErrorCode, Is.EqualTo((int)ProcessingState.ErrorCodes.EmptyProject));
			Assert.That(lfProject.State.ErrorMessage,
				Is.EqualTo(
					$"Recoverable error during initial clone of {_projectCode}: Clone failure: new repository with no commits. Clone deleted."));
		}

		[Test]
		public void EnsureClone_NotFlexProject_SetsRecoverableError()
		{
			// Setup
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(projectDir);
			var lfProject = LanguageForgeProject.Create(_projectCode);
			var action = new EnsureCloneActionDoubleMockErrorCondition(_env.Settings, _env.Logger, _mongoProjectRecordFactory, _mongoConnection,
				"Clone failure: clone is not a FLEx project: Clone deleted.");

			// Execute
			action.Run(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.ERROR));
			Assert.That(lfProject.State.ErrorCode, Is.EqualTo((int)ProcessingState.ErrorCodes.NoFlexProject));
			Assert.That(lfProject.State.ErrorMessage,
				Is.EqualTo(
					$"Recoverable error during initial clone of {_projectCode}: Clone failure: clone is not a FLEx project: Clone deleted."));
		}

	}
}
