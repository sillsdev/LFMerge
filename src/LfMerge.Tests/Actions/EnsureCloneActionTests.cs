﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace LfMerge.Tests.Actions
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

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_projectCode = TestContext.CurrentContext.Test.Name.ToLowerInvariant();
			var _projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Directory.CreateDirectory(_projectDir);
			// Making a stub file so Chorus model.TargetLocationIsUnused will be false
			File.Create(Path.Combine(_projectDir, "stub"));
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[Test]
		public void EnsureClone_NonExistingProject_SetsStateOnHold()
		{
			// for this test we don't want the test double for InternetCloneSettingsModel
			_env.Dispose();
			_env = new TestEnvironment(false);

			// Setup
			var nonExistingProjectCode = Path.GetRandomFileName().ToLowerInvariant();
			var lfProject = LanguageForgeProject.Create(_env.Settings, nonExistingProjectCode);

			// Execute
			Assert.That( () => new EnsureCloneActionDouble(_env.Settings, _env.Logger, false).Run(lfProject),
				Throws.Exception.TypeOf(Type.GetType("Chorus.VcsDrivers.Mercurial.RepositoryAuthorizationException")));

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void EnsureClone_StateFileDoesntExistAndStateNotCloning_ClonesProject()
		{
			// Not a valid real-world scenario, but we test this anyway
			var lfProject = LanguageForgeProject.Create(_env.Settings, _projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			lfProject.State.SRState = ProcessingState.SendReceiveStates.HOLD;
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.False,
				"State file shouldn't exist");
			Assert.AreNotEqual(lfProject.State.SRState, ProcessingState.SendReceiveStates.CLONING);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger).Run(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING),
				"State should be SYNCING");
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
			var lfProject = LanguageForgeProject.Create(_env.Settings, projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, projectCode);
			lfProject.State.SRState = ProcessingState.SendReceiveStates.CLONING;
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.False,
				"State file shouldn't exist");
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.CLONING));
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger).Run(lfProject);

			// Verify
			Assert.That(lfProject.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.SYNCING),
				"State should be SYNCING");
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_StateFileExistsStateNotCloning_DoesntCloneProject()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, _projectCode);
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
			new EnsureCloneActionDouble(_env.Settings, _env.Logger).Run(lfProject);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");
		}

		[Test]
		public void EnsureClone_StateFileExistsStateCloning_CloneProject()
		{
			// Setup
			var lfProject = LanguageForgeProject.Create(_env.Settings, _projectCode);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			File.Create(_env.Settings.GetStateFileName(_projectCode));
			lfProject.State.SRState = ProcessingState.SendReceiveStates.CLONING;
			Assert.That(File.Exists(_env.Settings.GetStateFileName(_projectCode)), Is.True,
				"State file should exist");
			Assert.AreEqual(lfProject.State.SRState, ProcessingState.SendReceiveStates.CLONING);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger).Run(lfProject);

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
			var lfProject = LanguageForgeProject.Create(_env.Settings, _projectCode);
			Assert.That(Directory.Exists(projectDir), Is.True,
				"Didn't create webwork directory: " + projectDir);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.False,
				"Clone of project shouldn't exist yet");

			// Execute
			new EnsureCloneActionDouble(_env.Settings, _env.Logger).Run(lfProject);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void EnsureClone_ProjectDirDoesExist_DeleteAndClonesProject()
		{
			// Setup and clone once
			var ensureCloneAction = new EnsureCloneActionDouble(_env.Settings, _env.Logger);
			var lfProject = LanguageForgeProject.Create(_env.Settings, _projectCode);
			ensureCloneAction.Run(lfProject);
			var projectDir = Path.Combine(_env.Settings.WebWorkDirectory, _projectCode);
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project the first time");
			DateTime originalCreationDateTime = Directory.GetCreationTimeUtc(projectDir);

			// wait 1s so that we get a different timestamp when we
			// execute another clone into the same directory.
			// Note: since test double doesn't write state files, EnsureClone will do another initial clone
			Thread.Sleep(1000);
			ensureCloneAction.Run(lfProject);
			DateTime newCreationDateTime = Directory.GetCreationTimeUtc(projectDir);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projectDir, ".hg")), Is.True,
				"Didn't clone project the second time");
			Assert.Less(originalCreationDateTime, newCreationDateTime,
				"Second project creation time {0} is not newer than first project creation time {1}",
				newCreationDateTime, originalCreationDateTime);
		}
	}
}
