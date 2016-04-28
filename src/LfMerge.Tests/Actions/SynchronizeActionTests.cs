﻿// Copyright (c) 2016 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using Autofac;
using LfMerge;
using LfMerge.Actions;
using LfMerge.DataConverters;
using LfMerge.LanguageForge.Model;
using LfMerge.MongoConnector;
using LfMerge.Settings;
using LfMerge.Tests;
using LfMerge.Tests.Fdo;
using NUnit.Framework;
using Palaso.Progress;
using Palaso.TestUtilities;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LfMerge.Tests.Actions
{
#if GETTING_RID_OF_FB_AND_CHORUS
// GETTING_RID_OF_FB_AND_CHORUS TODO: You should not test SynchronizeAction, but assume what it calls in FB works properly.
// GETTING_RID_OF_FB_AND_CHORUS TODO: I suppose you can use the before and expected after data for the tests here,
// GETTING_RID_OF_FB_AND_CHORUS TODO: but then that gets into testing other code, whic is probably best tested elsewhere.
#endif
	[TestFixture, Explicit, Category("LongRunning"), Ignore("For now SynchronizeAction just throws, when its DoRun method is called.")]
	public class SynchronizeActionTests
	{
		public static string LDProjectFolderPath;

		// testlangproj is the original LD repo
		// testlangproj-modified contains a modified entry, deleted entry, and added entry
		private const string testProjectCode = "testlangproj";
		private const string modifiedTestProjectCode = "testlangproj-modified";
		private const int originalNumOfFdoEntries = 63;
		private const string testEntryGuidStr = "1a705846-a814-4289-8594-4b874faca6cc";
		private const string testCreatedEntryGuidStr = "e670d0e8-c0f7-457d-a6d1-055c83663820";
		private const string testDeletedEntryGuidStr = "c5f97698-dade-4ba0-9f91-580ab19ff411";

		private TestEnvironment _env;
		private MongoConnectionDouble _mongoConnection;
		private MongoProjectRecordFactory _recordFactory;
		private LanguageForgeProject _lfProject;
		private LanguageDepotMock _lDProject;
		private LfMergeSettingsIni _lDSettings;
		private TemporaryFolder _languageDepotFolder;
		private Guid _testEntryGuid;
		private Guid _testCreatedEntryGuid;
		private Guid _testDeletedEntryGuid;
		private TransferFdoToMongoAction _transferFdoToMongo;
		private SynchronizeAction _sutSynchronize;

		[SetUp]
		public void Setup()
		{
			_env = new TestEnvironment();
			_env.Settings.CommitWhenDone = true;
			_lfProject = LanguageForgeProject.Create(_env.Settings, testProjectCode);
			FdoTestFixture.CopyFwProjectTo(testProjectCode, _env.Settings.WebWorkDirectory);

			// Guids are named for the diffs for the modified test project
			_testEntryGuid = Guid.Parse(testEntryGuidStr);
			_testCreatedEntryGuid = Guid.Parse(testCreatedEntryGuidStr);
			_testDeletedEntryGuid = Guid.Parse(testDeletedEntryGuidStr);

			_languageDepotFolder = new TemporaryFolder("SyncTestLD");
			_lDSettings = new LfMergeSettingsDouble(_languageDepotFolder.Path);
			Directory.CreateDirectory(_lDSettings.WebWorkDirectory);
			LDProjectFolderPath = Path.Combine(_lDSettings.WebWorkDirectory, testProjectCode);

			_mongoConnection = MainClass.Container.Resolve<IMongoConnection>() as MongoConnectionDouble;
			if (_mongoConnection == null)
				throw new AssertionException("Sync tests need a mock MongoConnection that stores data in order to work.");
			_recordFactory = MainClass.Container.Resolve<MongoProjectRecordFactory>() as MongoProjectRecordFactoryDouble;
			if (_recordFactory == null)
				throw new AssertionException("Sync tests need a mock MongoProjectRecordFactory in order to work.");

			_transferFdoToMongo = new TransferFdoToMongoAction(_env.Settings, _env.Logger, _mongoConnection);
			_sutSynchronize = new SynchronizeAction(_env.Settings, _env.Logger);
		}

		[TearDown]
		public void Teardown()
		{
			LanguageForgeProject.DisposeFwProject(_lfProject);
			LanguageDepotMock.DisposeFwProject(_lDProject);
			_languageDepotFolder.Dispose();
			_env.Dispose();
			_mongoConnection.Reset();
		}

		[Test]
		public void SynchronizeAction_NoCloneNoChangedData_GlossUnchanged()
		{
			// Setup
			FdoTestFixture.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			LfLexEntry lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo("English gloss"));
		}

		[Test]
		public void SynchronizeAction_LDDataChanged_GlossChanged()
		{
			// Setup
			_env.Settings.CommitWhenDone = false; // TODO: remove when multipara is no longer changing GUIDs when there are no changes. IJH 2016-03
			FdoTestFixture.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LDProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);
			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string ldChangedGloss = unchangedGloss + " - changed in FW";

			lfEntry = originalMongoData.First(e => e.Guid == _testDeletedEntryGuid);
			Assert.That(lfEntry.Lexeme["qaa-x-kal"].Value, Is.EqualTo("ken"));

			int createdEntryCount = originalMongoData.Count(e => e.Guid == _testCreatedEntryGuid);
			Assert.That(createdEntryCount, Is.EqualTo(0));

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(ldChangedGloss));

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			var cache = _lfProject.FieldWorksProject.Cache;
			var lfFdoEntry = cache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lfFdoEntry, Is.Not.Null);
			Assert.That(lfFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lfFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(ldChangedGloss));

			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(ldChangedGloss));

			lfEntry = receivedMongoData.First(e => e.Guid == _testCreatedEntryGuid);
			Assert.That(lfEntry.Lexeme["qaa-x-kal"].Value, Is.EqualTo("Ira"));

			int deletedEntryCount = receivedMongoData.Count(e => e.Guid == _testDeletedEntryGuid);
			Assert.That(deletedEntryCount, Is.EqualTo(0));

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			lDcache = _lDProject.FieldWorksProject.Cache;
			lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry, Is.Not.Null);
			Assert.That(lDFdoEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(ldChangedGloss));
		}

		[Test]
		public void SynchronizeAction_LFDataChangedLDDataChanged_LFWins()
		{
			//Setup
			FdoTestFixture.CopyFwProjectTo(modifiedTestProjectCode, _lDSettings.WebWorkDirectory);
			Directory.Move(Path.Combine(_lDSettings.WebWorkDirectory, modifiedTestProjectCode), LDProjectFolderPath);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);
			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;
			string fwChangedGloss = unchangedGloss + " - changed in FW";
			string lfChangedGloss = unchangedGloss + " - changed in LF";
			lfEntry.Senses[0].Gloss["en"].Value = lfChangedGloss;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(fwChangedGloss));

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries));

			lfEntry = receivedMongoData.First(e => e.Guid == _testEntryGuid);
			Assert.That(lfEntry.Senses[0].Gloss["en"].Value, Is.EqualTo(lfChangedGloss));
		}

		[Test]
		public void SynchronizeAction_LFDataDeleted_EntryRemoved()
		{
			// Setup
			FdoTestFixture.CopyFwProjectTo(testProjectCode, _lDSettings.WebWorkDirectory);

			_lfProject.IsInitialClone = true;
			_transferFdoToMongo.Run(_lfProject);

			IEnumerable<LfLexEntry> originalMongoData = _mongoConnection.GetLfLexEntries();
			LfLexEntry lfEntry = originalMongoData.First(e => e.Guid == _testEntryGuid);

			string unchangedGloss = lfEntry.Senses[0].Gloss["en"].Value;

			// Don't use _mongoConnection.RemoveRecord to delete the entry.  LF uses the "IsDeleted" field
			lfEntry.IsDeleted = true;
			_mongoConnection.UpdateRecord(_lfProject, lfEntry);
			IEnumerable<LfLexEntry> updatedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(updatedMongoData.First(e => e.Guid == _testEntryGuid).IsDeleted, Is.True);

			_lDProject = new LanguageDepotMock(_lDSettings, testProjectCode);
			var lDcache = _lDProject.FieldWorksProject.Cache;
			var lDFdoEntry = lDcache.ServiceLocator.GetObject(_testEntryGuid) as ILexEntry;
			Assert.That(lDFdoEntry.SensesOS[0].Gloss.AnalysisDefaultWritingSystem.Text, Is.EqualTo(unchangedGloss));

			// Exercise
			_sutSynchronize.Run(_lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedMongoData = _mongoConnection.GetLfLexEntries();
			Assert.That(receivedMongoData, Is.Not.Null);
			Assert.That(receivedMongoData, Is.Not.Empty);
			Assert.That(receivedMongoData.Count(), Is.EqualTo(originalNumOfFdoEntries-1));
			Assert.IsFalse(receivedMongoData.Any(e => e.Guid ==_testEntryGuid));

			var cache = _lfProject.FieldWorksProject.Cache;
			Assert.Throws<KeyNotFoundException>(()=> cache.ServiceLocator.GetObject(_testEntryGuid));
		}
	}
}
