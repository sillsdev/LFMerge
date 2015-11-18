﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using NUnit.Framework;
using LfMerge.Actions;
using System.IO;
using Autofac;
using Chorus.Model;

namespace LfMerge.Tests.Actions
{
	[TestFixture]
	public class ReceiveActionTests
	{
		private TestEnvironment _env;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
		}

		[TearDown]
		public void TearDown()
		{
			_env.Dispose();
		}

		[Test]
		public void DoRun_ProjectDoesntExistSetsStateOnHold()
		{
			// Setup
			var nonExistingProject = Path.GetRandomFileName();

			// for this test we don't want the test double for InternetCloneSettingsModel
			_env.Dispose();
			_env = new TestEnvironment(false);

			var lfProj = LanguageForgeProject.Create(nonExistingProject);
			var sut = LfMerge.Actions.Action.GetAction(ActionNames.Receive);

			// Execute/Verify
			Assert.That(() => sut.Run(lfProj), Throws.InstanceOf<UnauthorizedAccessException>());

			// Verify
			Assert.That(lfProj.State.SRState, Is.EqualTo(ProcessingState.SendReceiveStates.HOLD));
		}

		[Test]
		public void DoRun_DirDoesntExistClonesProject()
		{
			// Setup
			var projCode = TestContext.CurrentContext.Test.Name;
			var lfProj = LanguageForgeProject.Create(projCode);
			var sut = LfMerge.Actions.Action.GetAction(ActionNames.Receive);

			// Execute
			sut.Run(lfProj);

			// Verify
			var projDir = Path.Combine(LfMergeSettings.Current.WebWorkDirectory, projCode);
			Assert.That(Directory.Exists(projDir), Is.True,
				"Didn't create webwork directory");
			Assert.That(Directory.Exists(Path.Combine(projDir, ".hg")), Is.True,
				"Didn't clone project");
		}

		[Test]
		public void DoRun_HgDoesntExistClonesProject()
		{
			// Setup
			var projCode = TestContext.CurrentContext.Test.Name;
			var projDir = Path.Combine(LfMergeSettings.Current.WebWorkDirectory, projCode);
			Directory.CreateDirectory(projDir);
			var lfProj = LanguageForgeProject.Create(projCode);
			var sut = LfMerge.Actions.Action.GetAction(ActionNames.Receive);

			// Execute
			sut.Run(lfProj);

			// Verify
			Assert.That(Directory.Exists(Path.Combine(projDir, ".hg")), Is.True,
				"Didn't clone project");
		}

	}
}

