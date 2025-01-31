using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Autofac;
using LfMerge.Core.Actions;
using LfMerge.Core.FieldWorks;
using LfMerge.Core.MongoConnector;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using SIL.LCModel;
using SIL.TestUtilities;

namespace LfMerge.Core.Tests.E2E
{
	[TestFixture]
	[Category("LongRunning")]
	[Category("IntegrationTests")]
	public class E2ETestBase
	{
		public LfMerge.Core.Logging.ILogger Logger => MainClass.Logger;
		public TemporaryFolder TempFolderForClass { get; set; }
		public TemporaryFolder TempFolderForTest { get; set; }
		public string Sena3ZipPath { get; set; }
		private readonly HashSet<Guid> ProjectIdsToDelete = [];
		public SRTestEnvironment TestEnv { get; set; }

		public IMongoConnection _mongoConnection;
		public MongoProjectRecordFactory _recordFactory;

		public E2ETestBase()
		{
		}

		private static string TestName => TestContext.CurrentContext.Test.Name;
		private static string TestNameForPath => string.Concat(TestName.Split(Path.GetInvalidPathChars())); // Easiest way to strip out all invalid chars

		[OneTimeSetUp]
		public async Task FixtureSetup()
		{
			// Ensure top-level /tmp/LfMergeSRTests folder and subfolders exist
			var tempPath = Path.Join(Path.GetTempPath(), "LfMergeSRTests");
			Directory.CreateDirectory(tempPath);
			var testDataPath = Path.Join(tempPath, "data");
			Directory.CreateDirectory(testDataPath);
			var lcmDataPath = Path.Join(tempPath, "lcm-common");
			Directory.CreateDirectory(lcmDataPath);
			Environment.SetEnvironmentVariable("FW_CommonAppData", lcmDataPath);

			var derivedClassName = this.GetType().Name;
			TempFolderForClass = new TemporaryFolder(Path.Join(tempPath, derivedClassName));

			// Ensure sena-3.zip is available to all tests as a starting point
			Sena3ZipPath = Path.Join(testDataPath, "sena-3.zip");
			if (!File.Exists(Sena3ZipPath)) {
				using var testEnv = new SRTestEnvironment(TempFolderForTest);
				if (!await testEnv.IsLexBoxAvailable()) {
					Assert.Ignore("Can't run E2E tests without a copy of LexBox to test against. Please either launch LexBox on localhost port 80, or set the appropriate environment variables to point to a running copy of LexBox.");
				}
				await testEnv.Login();
				await testEnv.DownloadProjectBackup("sena-3", Sena3ZipPath);
			}
		}

		[OneTimeTearDown]
		public void FixtureTeardown()
		{
			Environment.SetEnvironmentVariable("FW_CommonAppData", null);
			var result = TestContext.CurrentContext.Result;
			var nonSuccess = result.FailCount + result.InconclusiveCount + result.WarningCount;
			// Only delete class temp folder if we passed or skipped all tests
			if (nonSuccess == 0) TempFolderForClass.Dispose();
		}

		[SetUp]
		public async Task TestSetup()
		{
			TempFolderForTest = new TemporaryFolder(TempFolderForClass, TestNameForPath);
			TestEnv = new SRTestEnvironment(TempFolderForTest);
			if (!await TestEnv.IsLexBoxAvailable()) {
				Assert.Ignore("Can't run E2E tests without a copy of LexBox to test against. Please either launch LexBox on localhost port 80, or set the appropriate environment variables to point to a running copy of LexBox.");
			}
			await TestEnv.Login();
			TestEnv.LaunchMongo();

			MagicStrings.SetMinimalModelVersion(LcmCache.ModelVersion);
			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>();
			var _mongoConnectionDouble = _mongoConnection as MongoConnectionDouble;
			if (_mongoConnectionDouble != null)
				throw new AssertionException("E2E tests need a real MongoConnection, not a mock.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>();
		}

		[TearDown]
		public async Task TestTeardown()
		{
			var outcome = TestContext.CurrentContext.Result.Outcome;
			var success = outcome == ResultState.Success || outcome == ResultState.Ignored;
			// Only delete temp folder if test passed, otherwise we'll want to leave it in place for post-test investigation
			TestEnv.CleanUpTestData = success;
			// On failure, also leave LexBox project(s) in place for post-test investigation, even though this might tend to clutter things up a little
			if (success) {
				foreach (var projId in ProjectIdsToDelete) {
					await TestEnv.DeleteLexBoxProject(projId);
				}
			}
			ProjectIdsToDelete.Clear();
			TestEnv.Dispose();
		}

		public string TestFolderForProject(string projectCode) => Path.Join(TempFolderForTest.Path, "webwork", projectCode);
		public string FwDataPathForProject(string projectCode) => Path.Join(TestFolderForProject(projectCode), $"{projectCode}.fwdata");

		public string CloneRepoFromLexbox(string code, string? newCode = null, TimeSpan? waitTime = null)
		{
			var projUrl = SRTestEnvironment.LexboxUrlForProjectWithAuth(code);
			newCode ??= code;
			var dest = TestFolderForProject(newCode);
			if (waitTime is null) {
				MercurialTestHelper.CloneRepo(projUrl.AbsoluteUri, dest);
			} else {
				var start = DateTime.UtcNow;
				var success = false;
				while (!success) {
					try {
						MercurialTestHelper.CloneRepo(projUrl.AbsoluteUri, dest);
					} catch {
						if (DateTime.UtcNow > start + waitTime) {
							throw; // Give up
						}
						System.Threading.Thread.Sleep(250);
						continue;
					}
					// If we got this far, no exception so we succeeded
					success = true;
				}
			}
			return dest;
		}

		public FwProject CloneFwProjectFromLexbox(string code, string? newCode = null, TimeSpan? waitTime = null)
		{
			var dest = CloneRepoFromLexbox(code, newCode, waitTime);
			var dirInfo = new DirectoryInfo(dest);
			if (!dirInfo.Exists) throw new InvalidOperationException($"Failed to clone {code} from lexbox, cannot create FwProject");
			var dirname = dirInfo.Name;
			var fwdataPath = Path.Join(dest, $"{dirname}.fwdata");
			MercurialTestHelper.ChangeBranch(dest, "tip");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(TestEnv.NullProgress, false, fwdataPath);
			var settings = new LfMergeSettingsDouble(TempFolderForTest.Path);
			return new FwProject(settings, dirname);
		}

		public async Task<string> CreateEmptyFlexProjectInLexbox()
		{
			var randomGuid = Guid.NewGuid();
			var testCode = $"sr-{randomGuid}";
			var testPath = TestFolderForProject(testCode);
			MercurialTestHelper.InitializeHgRepo(testPath);
			MercurialTestHelper.CreateFlexRepo(testPath);
			// Now create project in LexBox
			var result = await TestEnv.CreateLexBoxProject(testCode, randomGuid);
			var pushUrl = SRTestEnvironment.LexboxUrlForProjectWithAuth(testCode).AbsoluteUri;
			MercurialTestHelper.HgPush(testPath, pushUrl);
			if (result.Id.HasValue) ProjectIdsToDelete.Add(result.Id.Value);
			return testCode;
		}

		public async Task<string> CreateNewProjectFromTemplate(string origZipPath)
		{
			var randomGuid = Guid.NewGuid();
			var testCode = $"sr-{randomGuid}";
			var testPath = TestFolderForProject(testCode);
			// Now create project in LexBox
			var result = await TestEnv.CreateLexBoxProject(testCode, randomGuid);
			await TestEnv.ResetAndUploadZip(testCode, origZipPath);
			if (result.Id.HasValue) ProjectIdsToDelete.Add(result.Id.Value);
			return testCode;
		}

		public Task<string> CreateNewProjectFromSena3() => CreateNewProjectFromTemplate(Sena3ZipPath);

		public void CommitAndPush(FwProject project, string code, string? localCode = null, string? commitMsg = null)
		{
			TestEnv.CommitAndPush(project, code, TempFolderForTest.Path, localCode, commitMsg);
		}

		public async Task<LanguageForgeProject> CreateLfProjectFromSena3()
		{
			var projCode = await CreateNewProjectFromSena3();
			var projPath = CloneRepoFromLexbox(projCode, waitTime:TimeSpan.FromSeconds(5));
			MercurialTestHelper.ChangeBranch(projPath, "tip");
			var fwdataPath = Path.Join(projPath, $"{projCode}.fwdata");
			LfMergeBridge.LfMergeBridge.ReassembleFwdataFile(TestEnv.NullProgress, false, fwdataPath);

			// Do an initial clone from LexBox to the mock LF
			var lfProject = LanguageForgeProject.Create(projCode, TestEnv.Settings);
			lfProject.IsInitialClone = true;
			var transferLcmToMongo = new TransferLcmToMongoAction(TestEnv.Settings, TestEnv.NullLogger, _mongoConnection, _recordFactory);
			transferLcmToMongo.Run(lfProject);

			return lfProject;
		}

		public void SendReceiveToLexbox(LanguageForgeProject lfProject)
		{
			TestEnv.Settings.LanguageDepotRepoUri = SRTestEnvironment.LexboxUrlForProjectWithAuth(lfProject.ProjectCode).AbsoluteUri;
			var syncAction = new SynchronizeAction(TestEnv.Settings, TestEnv.Logger);
			syncAction.Run(lfProject);
		}

		public (string, DateTime, DateTime) UpdateFwGloss(FwProject project, Guid entryId, Func<string, string> textConverter)
		{
			var fwEntry = LcmTestHelper.GetEntry(project, entryId);
			Assert.That(fwEntry, Is.Not.Null);
			var origModifiedDate = fwEntry.DateModified;
			var unchangedGloss = LcmTestHelper.UpdateAnalysisText(project, fwEntry.SensesOS[0].Gloss, textConverter);
			return (unchangedGloss, origModifiedDate, fwEntry.DateModified);
		}

		public (string, DateTime, DateTime) UpdateLfGloss(LanguageForgeProject lfProject, Guid entryId, string wsId, Func<string, string> textConverter)
		{
			var lfEntry = _mongoConnection.GetLfLexEntryByGuid(lfProject, entryId);
			Assert.That(lfEntry, Is.Not.Null);
			var unchangedGloss = lfEntry.Senses[0].Gloss[wsId].Value;
			lfEntry.Senses[0].Gloss["pt"].Value = textConverter(unchangedGloss);
			// Remember that in LfMerge it's AuthorInfo that corresponds to the Lcm modified date
			DateTime origModifiedDate = lfEntry.AuthorInfo.ModifiedDate;
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_mongoConnection.UpdateRecord(lfProject, lfEntry);
			return (unchangedGloss, origModifiedDate, lfEntry.AuthorInfo.ModifiedDate);
		}
	}
}
