// Copyright (c) 2011-2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System.IO;
using IniParser.Model;
using LfMerge.Core.Queues;
using LfMerge.Core.Settings;
using SIL.TestUtilities;
using NUnit.Framework;

namespace LfMerge.Core.Tests
{
	[TestFixture]
	public class LfMergeSettingsTests
	{
		class LfMergeSettingsAccessor: LfMergeSettings
		{
			public void Initialize(string basePath, string webworkDir = null,
				string templateDir = null)
			{
				System.Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_BaseDir, basePath);
				if (!string.IsNullOrEmpty(webworkDir))
					System.Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_WebworkDir, webworkDir);
				if (!string.IsNullOrEmpty(templateDir))
					System.Environment.SetEnvironmentVariable(MagicStrings.SettingsEnvVar_TemplatesDir, templateDir);
				base.Initialize();
			}
		}

		[Test]
		public void LcmDirs_RelativePathsAreSubdirsOfBasedir()
		{
			var sut = new LfMergeSettingsAccessor();
			sut.Initialize(Path.GetTempPath(), "projects", "templates");

			Assert.That(sut.LcmDirectorySettings.ProjectsDirectory, Is.EqualTo(Path.Combine(Path.GetTempPath(), "projects")));
			Assert.That(sut.LcmDirectorySettings.DefaultProjectsDirectory, Is.EqualTo(Path.Combine(Path.GetTempPath(), "projects")));
			Assert.That(sut.LcmDirectorySettings.TemplateDirectory, Is.EqualTo(Path.Combine(Path.GetTempPath(), "templates")));
		}

		[Test]
		public void LcmDirs_AbsolutePathsRemainAbsolute()
		{
			var sut = new LfMergeSettingsAccessor();
			sut.Initialize(Path.GetTempPath(), "/projects", "/foo/templates");

			Assert.That(sut.LcmDirectorySettings.ProjectsDirectory, Is.EqualTo("/projects"));
			Assert.That(sut.LcmDirectorySettings.DefaultProjectsDirectory, Is.EqualTo("/projects"));
			Assert.That(sut.LcmDirectorySettings.TemplateDirectory, Is.EqualTo("/foo/templates"));
		}

		[Test]
		public void StateDirectory_Correct()
		{
			var sut = new LfMergeSettingsAccessor();
			sut.Initialize(Path.GetTempPath());

			Assert.That(sut.StateDirectory, Is.EqualTo(Path.Combine(Path.GetTempPath(), "state")));
		}

		[Test]
		public void GetStateFileName_Correct()
		{
			// Setup
			using (var temp = new TemporaryFolder("StateFile"))
			{
				var sut = new LfMergeSettingsAccessor();
				sut.Initialize(temp.Path);

				// Exercise
				var stateFile = sut.GetStateFileName("ProjA");

				// Verify
				Assert.That(stateFile, Is.EqualTo(Path.Combine(temp.Path, "state", "ProjA.state")));
				Assert.That(Directory.Exists(Path.GetDirectoryName(stateFile)), Is.True,
					"State directory didn't get created");
			}
		}

		public void GetQueueDirectory_Correct()
		{
			// Setup
			using (var temp = new TemporaryFolder("QueueDirectory"))
			{
				var sut = new LfMergeSettingsAccessor();
				sut.Initialize(temp.Path);

				// Exercise
				var queueDir = sut.GetQueueDirectory();

				// Verify
				Assert.That(Path.GetFileName(queueDir), Is.EqualTo("syncqueue"));
			}
		}
	}
}

