﻿// Copyright (c) 2016-2018 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)

using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.Core.DataConverters;
using LfMerge.Core.LanguageForge.Model;
using MongoDB.Bson;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;
using SIL.LCModel.Infrastructure;

namespace LfMerge.Core.Tests.Lcm
{
	[TestFixture, Explicit, Category("LongRunning")]
	public class RoundTripTests : RoundTripBase
	{
		[Test]
		public void RoundTrip_LcmToMongoToLcmToMongo_ShouldKeepOriginalValuesInEntries()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = _cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = _servLoc.GetInstance<ILexEntryRepository>().GetObject(entryGuid);
			Assert.That(entry, Is.Not.Null);

			// Save field values before test, to compare with values after test
			BsonDocument customFieldValues = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValues = GetFieldValues(cache, entry);

			// We no longer populate the semantic domain optionlist in Lcm->Mongo, so we need to populate it here
			var data = new SampleData();
			_conn.UpdateMockOptionList(data.bsonSemDomData);

			// Exercise
			SutLcmToMongo.Run(lfProject);

			// Save original Mongo data
			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			LfLexEntry originalLfEntry = originalData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(originalLfEntry, Is.Not.Null);

			// Exercise
			SutMongoToLcm.Run(lfProject);

			// Verify
			BsonDocument customFieldValuesAfterTest = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValuesAfterTest = GetFieldValues(cache, entry);
			IDictionary<string, Tuple<string, string>> differencesByName = GetLcmDifferences(cache, fieldValues, fieldValuesAfterTest);
			PrintDifferences(differencesByName);
			Assert.That(differencesByName, Is.Empty);
			Assert.That(customFieldValues, Is.EqualTo(customFieldValuesAfterTest));

			// Exercise
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(lfEntry, Is.Not.Null);

			differencesByName = GetMongoDifferences(originalLfEntry.ToBsonDocument(), lfEntry.ToBsonDocument());
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
		}

		[Test]
		public void RoundTrip_LcmToMongoToLcm_ShouldKeepOriginalValuesInSenses()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense[] senses = entry.SensesOS.ToArray();
			Assert.That(senses.Length, Is.EqualTo(2));

			BsonDocument[] customFieldValues = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValues = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();

			// We no longer populate the semantic domain optionlist in Lcm->Mongo, so we need to populate it here
			var data = new SampleData();
			_conn.UpdateMockOptionList(data.bsonSemDomData);

			// Exercise
			SutLcmToMongo.Run(lfProject);
			SutMongoToLcm.Run(lfProject);

			// Verify
			BsonDocument[] customFieldValuesAfterTest = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();

			var differencesByName1 = GetLcmDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetLcmDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			PrintDifferences(differencesByName1);
			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			PrintDifferences(differencesByName2);
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));
		}

		[Test]
		public void RoundTrip_LcmToMongoToLcm_ShouldKeepOriginalValuesInExampleSentences()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense senseWithExamples = entry.SensesOS.First(sense => sense.ExamplesOS.Count > 0);
			// Have to do it that way, because weirdly, the following line gets First() from MongoDB.Driver.Core!??!
			// ILexSense senseWithExamples = entry.SensesOS.First(sense => sense.ExamplesOS.Count > 0);
			ILexExampleSentence[] examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));

			BsonDocument[] customFieldValues = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValues = examples.Select(example => GetFieldValues(cache, example)).ToArray();

			// Exercise
			SutLcmToMongo.Run(lfProject);
			SutMongoToLcm.Run(lfProject);

			// Verify
			BsonDocument[] customFieldValuesAfterTest = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = examples.Select(example => GetFieldValues(cache, example)).ToArray();

			var differencesByName1 = GetLcmDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetLcmDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			PrintDifferences(differencesByName1);
			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			PrintDifferences(differencesByName2);
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));
		}

		[Test]
		public void RoundTrip_LcmToMongoToLcmToMongo_ShouldKeepModifiedValuesInEntries()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					entry.CitationForm.SetVernacularDefaultWritingSystem("New value with <angle> brackets & ampersands for this test");
				});

			// Save field values before test, to compare with values after test
			BsonDocument customFieldValues = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValues = GetFieldValues(cache, entry);

			// Exercise
			SutLcmToMongo.Run(lfProject);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					entry.CitationForm.SetVernacularDefaultWritingSystem("This value should be overwritten by MongoToLcm");
				});

			// Save original mongo data
			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			LfLexEntry originalLfEntry = originalData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);

			string vernacularWS = cache.ServiceLocator.WritingSystemManager.GetStrFromWs(cache.DefaultVernWs);
			string originalLexeme = originalLfEntry.Lexeme[vernacularWS].Value;
			string changedLexeme = "Changed lexeme for this test";
			originalLfEntry.Lexeme[vernacularWS].Value = changedLexeme;
			originalLfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(originalLfEntry);

			// We no longer populate the semantic domain optionlist in Lcm->Mongo, so we need to populate it here
			var data = new SampleData();
			_conn.UpdateMockOptionList(data.bsonSemDomData);

			// Exercise
			SutMongoToLcm.Run(lfProject);
			string changedLexemeDuringUpdate = "This value should be overwritten by LcmToMongo";
			originalLfEntry.Lexeme[vernacularWS].Value = changedLexemeDuringUpdate;
			originalLfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(originalLfEntry);
			SutLcmToMongo.Run(lfProject);

			// Verify
			Assert.That(entry.CitationForm.VernacularDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToLcm"));
			Assert.That(entry.CitationForm.VernacularDefaultWritingSystem.Text, Is.EqualTo("New value with <angle> brackets & ampersands for this test"));

			BsonDocument customFieldValuesAfterTest = GetCustomFieldValues(cache, entry, "entry");
			IDictionary<int, object> fieldValuesAfterTest = GetFieldValues(cache, entry);
			IDictionary<string, Tuple<string, string>> differencesByName = GetLcmDifferences(cache, fieldValues, fieldValuesAfterTest);
			if (differencesByName.ContainsKey("DateModified"))
				differencesByName.Remove("DateModified");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName, Is.Empty);
			Assert.That(customFieldValuesAfterTest, Is.EqualTo(customFieldValues));

			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(lfEntry, Is.Not.Null);
			Assert.That(lfEntry.Lexeme[vernacularWS].Value, Is.Not.EqualTo(changedLexemeDuringUpdate));
			Assert.That(lfEntry.Lexeme[vernacularWS].Value, Is.EqualTo(changedLexeme));

			originalLfEntry.Lexeme[vernacularWS].Value = originalLexeme;
			differencesByName = GetMongoDifferences(originalLfEntry.ToBsonDocument(), lfEntry.ToBsonDocument());
			differencesByName.Remove("lexeme");
			differencesByName.Remove("dateModified");
			differencesByName.Remove("authorInfo.modifiedDate");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
		}

		[Test]
		public void RoundTrip_LcmToMongoToLcmToMongo_ShouldKeepModifiedValuesInSenses()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense[] senses = entry.SensesOS.ToArray();
			Assert.That(senses.Length, Is.EqualTo(2));
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					senses[0].AnthroNote.SetAnalysisDefaultWritingSystem("New value for this test");
					senses[1].AnthroNote.SetAnalysisDefaultWritingSystem("Second value for this test");
				});

			BsonDocument[] customFieldValues = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValues = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();

			// We no longer populate the semantic domain optionlist in Lcm->Mongo, so we need to populate it here
			var data = new SampleData();
			_conn.UpdateMockOptionList(data.bsonSemDomData);

			// Exercise
			SutLcmToMongo.Run(lfProject);
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					senses[0].AnthroNote.SetAnalysisDefaultWritingSystem("This value should be overwritten by MongoToLcm");
					senses[1].AnthroNote.SetAnalysisDefaultWritingSystem("This value should be overwritten by MongoToLcm");
				});

			// Save original mongo data
			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			LfLexEntry originalEntry = originalData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));

			string originalSense0Definition = originalEntry.Senses[0].Definition["en"].Value;
			string originalSense1Definition = originalEntry.Senses[1].Definition["en"].Value;
			string changedSense0Definition = "Changed sense0 definition for this test";
			string changedSense1Definition = "Changed sense1 definition for this test";
			originalEntry.Senses[0].Definition["en"].Value = changedSense0Definition;
			originalEntry.Senses[1].Definition["en"].Value = changedSense1Definition;
			originalEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(originalEntry);

			// Exercise
			SutMongoToLcm.Run(lfProject);
			string changedDefinitionDuringUpdate = "This value should be overwritten by LcmToMongo";
			originalEntry.Senses[0].Definition["en"].Value = changedDefinitionDuringUpdate;
			originalEntry.Senses[1].Definition["en"].Value = changedDefinitionDuringUpdate;
			originalEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(originalEntry);

			// Verify
			Assert.That(senses[0].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToLcm"));
			Assert.That(senses[1].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToLcm"));
			Assert.That(senses[0].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.EqualTo("New value for this test"));
			Assert.That(senses[1].AnthroNote.AnalysisDefaultWritingSystem.Text, Is.EqualTo("Second value for this test"));

			BsonDocument[] customFieldValuesAfterTest = senses.Select(sense => GetCustomFieldValues(cache, sense, "senses")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = senses.Select(sense => GetFieldValues(cache, sense)).ToArray();
			var differencesByName1 = GetLcmDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetLcmDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			PrintDifferences(differencesByName1);
			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			PrintDifferences(differencesByName2);
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));

			// Exercise
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(lfEntry, Is.Not.Null);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(lfEntry.Senses[0].Definition["en"].Value, Is.Not.EqualTo(changedDefinitionDuringUpdate));
			Assert.That(lfEntry.Senses[1].Definition["en"].Value, Is.Not.EqualTo(changedDefinitionDuringUpdate));
			Assert.That(lfEntry.Senses[0].Definition["en"].Value, Is.EqualTo(changedSense0Definition));
			Assert.That(lfEntry.Senses[1].Definition["en"].Value, Is.EqualTo(changedSense1Definition));

			originalEntry.Senses[0].Definition["en"].Value = originalSense0Definition;
			originalEntry.Senses[1].Definition["en"].Value = originalSense1Definition;
			IDictionary<string, Tuple<string, string>> differencesByName =
				GetMongoDifferences(originalEntry.Senses[0].ToBsonDocument(), lfEntry.Senses[0].ToBsonDocument());
			differencesByName.Remove("definition");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
			differencesByName = GetMongoDifferences(originalEntry.Senses[1].ToBsonDocument(), lfEntry.Senses[1].ToBsonDocument());
			differencesByName.Remove("definition");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
			differencesByName = GetMongoDifferences(originalEntry.ToBsonDocument(), lfEntry.ToBsonDocument());
			differencesByName.Remove("senses");
			differencesByName.Remove("dateModified");
			differencesByName.Remove("authorInfo.modifiedDate");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
		}

		[Test]
		public void RoundTrip_LcmToMongoToLcmToMongo_ShouldKeepModifiedValuesInExample()
		{
			// Setup
			var lfProject = _lfProj;
			var cache = lfProject.FieldWorksProject.Cache;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(entry, Is.Not.Null);
			ILexSense senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			// Have to do it that way, because weirdly, the following line gets First() from MongoDB.Driver.Core!??!
			// ILexSense senseWithExamples = entry.SensesOS.First(sense => sense.ExamplesOS.Count > 0);
			ILexExampleSentence[] examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					examples[0].Example.SetVernacularDefaultWritingSystem("New value for this test");
					examples[1].Example.SetVernacularDefaultWritingSystem("Second value with < and & for this test");
				});

			BsonDocument[] customFieldValues = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValues = examples.Select(example => GetFieldValues(cache, example)).ToArray();

			// Exercise
			SutLcmToMongo.Run(lfProject);
			entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));
			UndoableUnitOfWorkHelper.DoUsingNewOrCurrentUOW("undo", "redo", cache.ActionHandlerAccessor, () =>
				{
					examples[0].Example.SetVernacularDefaultWritingSystem("This value should be overwritten by MongoToLcm");
					examples[1].Example.SetVernacularDefaultWritingSystem("This value should be overwritten by MongoToLcm");
				});

			// Save original mongo data
			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			LfLexEntry originalEntry = originalData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(originalEntry.Senses[0].Examples.Count, Is.EqualTo(2));

			string originalSense0Example0Translation = originalEntry.Senses[0].Examples[0].Translation["en"].Value;
			string originalSense0Example1Translation = originalEntry.Senses[0].Examples[1].Translation["en"].Value;
			string changedSense0Example0Translation = "Changed sense0 example0 sentence for this test";
			string changedSense0Example1Translation = "Changed sense0 example1 sentence for this test";
			originalEntry.Senses[0].Examples[0].Translation["en"].Value = changedSense0Example0Translation;
			originalEntry.Senses[0].Examples[1].Translation["en"].Value = changedSense0Example1Translation;
			originalEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(originalEntry);

			// Exercise
			SutMongoToLcm.Run(lfProject);
			string changedTranslationDuringUpdate = "This value should be overwritten by LcmToMongo";
			originalEntry.Senses[0].Examples[0].Translation["en"].Value = changedTranslationDuringUpdate;
			originalEntry.Senses[0].Examples[1].Translation["en"].Value = changedTranslationDuringUpdate;
			originalEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(originalEntry);

			// Verify
			entry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			senseWithExamples = Enumerable.First(entry.SensesOS, sense => sense.ExamplesOS.Count > 0);
			examples = senseWithExamples.ExamplesOS.ToArray();
			Assert.That(examples.Length, Is.EqualTo(2));
			Assert.That(examples[0].Example.VernacularDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToLcm"));
			Assert.That(examples[1].Example.VernacularDefaultWritingSystem.Text, Is.Not.EqualTo("This value should be overwritten by MongoToLcm"));
			Assert.That(examples[0].Example.VernacularDefaultWritingSystem.Text, Is.EqualTo("New value for this test"));
			Assert.That(examples[1].Example.VernacularDefaultWritingSystem.Text, Is.EqualTo("Second value with < and & for this test"));
			string spanText = ConvertLcmToMongoTsStrings.TextFromTsString(examples[1].Example.VernacularDefaultWritingSystem, cache.WritingSystemFactory);
			Assert.That(spanText, Is.EqualTo("Second value with &lt; and &amp; for this test"));

			BsonDocument[] customFieldValuesAfterTest = examples.Select(example => GetCustomFieldValues(cache, example, "examples")).ToArray();
			IDictionary<int, object>[] fieldValuesAfterTest = examples.Select(example => GetFieldValues(cache, example)).ToArray();
			var differencesByName1 = GetLcmDifferences(cache, fieldValues[0], fieldValuesAfterTest[0]);
			var differencesByName2 = GetLcmDifferences(cache, fieldValues[1], fieldValuesAfterTest[1]);

			PrintDifferences(differencesByName1);
			Assert.That(differencesByName1, Is.Empty);
			Assert.That(customFieldValues[0], Is.EqualTo(customFieldValuesAfterTest[0]));
			PrintDifferences(differencesByName2);
			Assert.That(differencesByName2, Is.Empty);
			Assert.That(customFieldValues[1], Is.EqualTo(customFieldValuesAfterTest[1]));

			// Exercise
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntry = receivedData.FirstOrDefault(e => e.Guid.ToString() == TestEntryGuidStr);
			Assert.That(lfEntry, Is.Not.Null);
			Assert.That(originalEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(lfEntry.Senses[0].Examples[0].Translation["en"].Value, Is.Not.EqualTo(changedTranslationDuringUpdate));
			Assert.That(lfEntry.Senses[0].Examples[1].Translation["en"].Value, Is.Not.EqualTo(changedTranslationDuringUpdate));
			Assert.That(lfEntry.Senses[0].Examples[0].Translation["en"].Value, Is.EqualTo(changedSense0Example0Translation));
			Assert.That(lfEntry.Senses[0].Examples[1].Translation["en"].Value, Is.EqualTo(changedSense0Example1Translation));

			originalEntry.Senses[0].Examples[0].Translation["en"].Value = originalSense0Example0Translation;
			originalEntry.Senses[0].Examples[1].Translation["en"].Value = originalSense0Example1Translation;
			IDictionary<string, Tuple<string, string>> differencesByName =
				GetMongoDifferences(originalEntry.Senses[0].Examples[0].ToBsonDocument(), lfEntry.Senses[0].Examples[0].ToBsonDocument());
			differencesByName.Remove("translation");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
			differencesByName = GetMongoDifferences(originalEntry.Senses[0].Examples[1].ToBsonDocument(), lfEntry.Senses[0].Examples[1].ToBsonDocument());
			differencesByName.Remove("translation");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
			differencesByName = GetMongoDifferences(originalEntry.Senses[0].ToBsonDocument(), lfEntry.Senses[0].ToBsonDocument());
			differencesByName.Remove("examples");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
			differencesByName = GetMongoDifferences(originalEntry.ToBsonDocument(), lfEntry.ToBsonDocument());
			differencesByName.Remove("senses");
			differencesByName.Remove("dateModified");
			differencesByName.Remove("authorInfo.modifiedDate");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));
		}

		[Test]
		public void RoundTrip_MongoToLcmToMongo_ShouldAddAndDeleteNewEntry()
		{
			// Create
			var lfProject = _lfProj;
			SutLcmToMongo.Run(lfProject);
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			Assert.That(entryRepo.Count, Is.EqualTo(LcmTestBase.OriginalNumOfLcmEntries));

			LfLexEntry newEntry = new LfLexEntry();
			newEntry.Guid = Guid.NewGuid();
			string vernacularWS = lfProject.FieldWorksProject.Cache.LanguageProject.DefaultVernacularWritingSystem.Id;
			string newLexeme = "new lexeme for this test";
			newEntry.Lexeme = LfMultiText.FromSingleStringMapping(vernacularWS, newLexeme);
			newEntry.AuthorInfo = new LfAuthorInfo();
			newEntry.AuthorInfo.CreatedDate = new DateTime();
			newEntry.AuthorInfo.ModifiedDate = newEntry.AuthorInfo.CreatedDate;
			_conn.UpdateMockLfLexEntry(newEntry);
			string newEntryGuidStr = newEntry.Guid.ToString();

			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries+1));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries+1));
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries+1));

			LfLexEntry entry = receivedData.FirstOrDefault(e => e.Guid.ToString() == newEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.IsDeleted, Is.EqualTo(false));

			IDictionary<string, Tuple<string, string>> differencesByName =
				GetMongoDifferences(newEntry.ToBsonDocument(), entry.ToBsonDocument());
			// LCM-to-Mongo direction populates LiftID even if it was null in original,
			// so don't consider that difference to be an error for this test.
			differencesByName.Remove("liftId");
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));

			// Delete
			newEntry.IsDeleted = true;
			newEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(newEntry);
			originalData = _conn.GetLfLexEntries();
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries+1));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			SutLcmToMongo.Run(lfProject);

			// Verify
			var dataAfterRoundTrip = _conn.GetLfLexEntries();
			Assert.That(dataAfterRoundTrip.Count(), Is.EqualTo(OriginalNumOfLcmEntries+1));
			entry = dataAfterRoundTrip.FirstOrDefault(e => e.Guid.ToString() == newEntryGuidStr);
			Assert.That(entry, Is.Not.Null);
			Assert.That(entry.IsDeleted, Is.EqualTo(true));
		}

		[Test]
		public void RoundTrip_MongoToLcmToMongo_ShouldAddAndDeleteNewSense()
		{
			// Create
			var lfProject = _lfProj;
			SutLcmToMongo.Run(lfProject);
			ILangProject langProj = lfProject.FieldWorksProject.Cache.LanguageProject;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			ILexSenseRepository senseRepo = _servLoc.GetInstance<ILexSenseRepository>();
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			int originalNumOfLcmSenses = senseRepo.Count;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var lcmEntry = entryRepo.GetObject(entryGuid);
			Assert.That(lcmEntry, Is.Not.Null);
			ILexSense[] senses = lcmEntry.SensesOS.ToArray();
			Assert.That(senses.Length, Is.EqualTo(2));

			string vernacularWS = langProj.DefaultVernacularWritingSystem.Id;
			string analysisWS = langProj.DefaultAnalysisWritingSystem.Id;
			string newDefinition = "new definition for this test";
			string newPartOfSpeech = "N"; // Noun
			LfLexEntry lfEntry = _conn.GetLfLexEntries().First(e => e.Guid == entryGuid);
			Assert.That(lfEntry.Senses.Count, Is.EqualTo(2));
			LfSense newSense = new LfSense();
			newSense.Guid = Guid.NewGuid();
			newSense.Definition = LfMultiText.FromSingleStringMapping(vernacularWS, newDefinition);
			newSense.PartOfSpeech = LfStringField.FromString(newPartOfSpeech);
			lfEntry.Senses.Add(newSense);
			Assert.That(lfEntry.Senses.Count, Is.EqualTo(3));
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			string newEntryGuidStr = lfEntry.Guid.ToString();

			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(senseRepo.Count, Is.EqualTo(originalNumOfLcmSenses + 1));
			lcmEntry = entryRepo.GetObject(entryGuid);
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.SensesOS.Count, Is.EqualTo(3));
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntryAfterTest = receivedData.FirstOrDefault(e => e.Guid.ToString() == newEntryGuidStr);
			Assert.That(lfEntryAfterTest, Is.Not.Null);
			Assert.That(lfEntryAfterTest.Senses.Count, Is.EqualTo(3));

			IDictionary<string, Tuple<string, string>> differencesByName =
				GetMongoDifferences(lfEntry.Senses.Last().ToBsonDocument(), lfEntryAfterTest.Senses.Last().ToBsonDocument());
			// LCM-to-Mongo direction populates LiftID even if it was null in original,
			// so don't consider that difference to be an error for this test.
			differencesByName.Remove("liftId"); // Automatically set by LCM
			differencesByName.Remove("guid"); // Automatically set by LCM
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));

			// Delete
			lfEntry.Senses.Remove(newSense);
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			originalData = _conn.GetLfLexEntries();
			Assert.That(lfEntry.Senses.Count(), Is.EqualTo(2));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			lcmEntry = entryRepo.GetObject(entryGuid);
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(senseRepo.Count, Is.EqualTo(originalNumOfLcmSenses));
			SutLcmToMongo.Run(lfProject);

			// Verify
			originalData = _conn.GetLfLexEntries();
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));
		}

		[Test]
		public void RoundTrip_MongoToLcmToMongo_ShouldAddAndDeleteNewExample()
		{
			// Create
			var lfProject = _lfProj;
			SutLcmToMongo.Run(lfProject);
			ILangProject langProj = lfProject.FieldWorksProject.Cache.LanguageProject;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			ILexExampleSentenceRepository exampleRepo = _servLoc.GetInstance<ILexExampleSentenceRepository>();
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			int originalNumOfLcmExamples = exampleRepo.Count;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var lcmEntry = entryRepo.GetObject(entryGuid);
			Assert.That(lcmEntry, Is.Not.Null);
			ILexSense lcmSense = lcmEntry.SensesOS.First();
			Assert.That(lcmSense, Is.Not.Null);
			Assert.That(lcmSense.ExamplesOS.Count, Is.EqualTo(2));

			string vernacularWS = langProj.DefaultVernacularWritingSystem.Id;
			string newSentence = "new sentence with <span lang=\"grc\">Ελλη<ν&amp;ικά</span> in it for this test";
			string newTranslation = "new translation for this test";
			LfLexEntry lfEntry = _conn.GetLfLexEntries().First(e => e.Guid == entryGuid);
			Assert.That(lfEntry.Senses.Count, Is.GreaterThan(0));
			LfSense lfSense = lfEntry.Senses.First();
			Assert.That(lfSense, Is.Not.Null);
			Assert.That(lfSense.Examples.Count, Is.EqualTo(2));
			LfExample newExample = new LfExample();
			newExample.Guid = Guid.NewGuid();
			newExample.Sentence = LfMultiText.FromSingleStringMapping(vernacularWS, newSentence);
			newExample.Translation = LfMultiText.FromSingleStringMapping(vernacularWS, newTranslation);
			lfSense.Examples.Add(newExample);
			Assert.That(lfSense.Examples.Count, Is.EqualTo(3));
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			string newEntryGuidStr = lfEntry.Guid.ToString();

			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(exampleRepo.Count, Is.EqualTo(originalNumOfLcmExamples + 1));
			lcmEntry = entryRepo.GetObject(entryGuid);
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.SensesOS.First().ExamplesOS.Count, Is.EqualTo(3));
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntryAfterTest = receivedData.FirstOrDefault(e => e.Guid.ToString() == newEntryGuidStr);
			Assert.That(lfEntryAfterTest, Is.Not.Null);
			LfSense lfSenseAfterTest = lfEntryAfterTest.Senses.First();
			Assert.That(lfSenseAfterTest, Is.Not.Null);
			Assert.That(lfSenseAfterTest.Examples.Count, Is.EqualTo(3));

			IDictionary<string, Tuple<string, string>> differencesByName =
				GetMongoDifferences(
					lfSense         .Examples.Last().ToBsonDocument(),
					lfSenseAfterTest.Examples.Last().ToBsonDocument()
				);
			// LCM-to-Mongo direction populates a few fields even if they were null in original,
			// so don't consider that difference to be an error for this test.
			differencesByName.Remove("translationGuid"); // Automatically set by LCM
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));

			// Delete
			lfSense.Examples.Remove(newExample);
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			originalData = _conn.GetLfLexEntries();
			Assert.That(lfEntry.Senses.Count, Is.EqualTo(2));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			lcmEntry = entryRepo.GetObject(entryGuid);
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(exampleRepo.Count, Is.EqualTo(originalNumOfLcmExamples));
			SutLcmToMongo.Run(lfProject);

			// Verify
			originalData = _conn.GetLfLexEntries();
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));
		}

		[Test]
		public void RoundTrip_MongoToLcmToMongo_ShouldAddAndDeleteNewPicture()
		{
			// Create
			var lfProject = _lfProj;
			SutLcmToMongo.Run(lfProject);
			ILangProject langProj = lfProject.FieldWorksProject.Cache.LanguageProject;
			ILexEntryRepository entryRepo = _servLoc.GetInstance<ILexEntryRepository>();
			ICmPictureRepository pictureRepo = _servLoc.GetInstance<ICmPictureRepository>();
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			int originalNumOfLcmPictures = pictureRepo.Count;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var lcmEntry = entryRepo.GetObject(entryGuid) as ILexEntry;
			Assert.That(lcmEntry, Is.Not.Null);
			ILexSense lcmSense = lcmEntry.SensesOS.First();
			Assert.That(lcmSense, Is.Not.Null);
			Assert.That(lcmSense.PicturesOS.Count, Is.EqualTo(1));

			string vernacularWS = langProj.DefaultVernacularWritingSystem.Id;
			string newCaption = "new caption for this test";
			string newFilename = "DoesNotExist_ABCXYZ.jpg";
			LfLexEntry lfEntry = _conn.GetLfLexEntries().First(e => e.Guid == entryGuid);
			Assert.That(lfEntry.Senses.Count, Is.GreaterThan(0));
			LfSense lfSense = lfEntry.Senses.First();
			Assert.That(lfSense, Is.Not.Null);
			Assert.That(lfSense.Pictures.Count, Is.EqualTo(1));
			LfPicture newPicture = new LfPicture();
			newPicture.Guid = Guid.NewGuid();
			newPicture.Caption = LfMultiText.FromSingleStringMapping(vernacularWS, newCaption);
			newPicture.FileName = newFilename;
			lfSense.Pictures.Add(newPicture);
			Assert.That(lfSense.Pictures.Count, Is.EqualTo(2));
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			string newEntryGuidStr = lfEntry.Guid.ToString();

			IEnumerable<LfLexEntry> originalData = _conn.GetLfLexEntries();
			Assert.That(originalData, Is.Not.Null);
			Assert.That(originalData, Is.Not.Empty);
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			Assert.That(pictureRepo.Count, Is.EqualTo(originalNumOfLcmPictures + 1));
			lcmEntry = entryRepo.GetObject(entryGuid) as ILexEntry;
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.SensesOS.First().PicturesOS.Count, Is.EqualTo(2));
			SutLcmToMongo.Run(lfProject);

			// Verify
			IEnumerable<LfLexEntry> receivedData = _conn.GetLfLexEntries();
			Assert.That(receivedData, Is.Not.Null);
			Assert.That(receivedData, Is.Not.Empty);
			Assert.That(receivedData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));

			LfLexEntry lfEntryAfterTest = receivedData.FirstOrDefault(e => e.Guid.ToString() == newEntryGuidStr);
			Assert.That(lfEntryAfterTest, Is.Not.Null);
			LfSense lfSenseAfterTest = lfEntryAfterTest.Senses.First();
			Assert.That(lfSenseAfterTest, Is.Not.Null);
			Assert.That(lfSenseAfterTest.Pictures.Count, Is.EqualTo(2));

			IDictionary<string, Tuple<string, string>> differencesByName =
				GetMongoDifferences(
					lfSense         .Examples.Last().ToBsonDocument(),
					lfSenseAfterTest.Examples.Last().ToBsonDocument()
				);
			// LCM-to-Mongo direction populates a few fields even if they were null in original,
			// so don't consider that difference to be an error for this test.
			//differencesByName.Remove("translationGuid"); // Automatically set by LCM
			PrintDifferences(differencesByName);
			Assert.That(differencesByName.Count(), Is.EqualTo(0));

			// Delete
			lfSense.Pictures.Remove(newPicture);
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);
			originalData = _conn.GetLfLexEntries();
			Assert.That(lfEntry.Senses.Count, Is.EqualTo(2));
			Assert.That(lfEntry.Senses.First().Pictures.Count, Is.EqualTo(1));

			// Exercise
			SutMongoToLcm.Run(lfProject);
			Assert.That(entryRepo.Count, Is.EqualTo(OriginalNumOfLcmEntries));
			lcmEntry = entryRepo.GetObject(entryGuid) as ILexEntry;
			Assert.That(lcmEntry, Is.Not.Null);
			Assert.That(lcmEntry.SensesOS.Count, Is.EqualTo(2));
			Assert.That(pictureRepo.Count, Is.EqualTo(originalNumOfLcmPictures));
			SutLcmToMongo.Run(lfProject);

			// Verify
			originalData = _conn.GetLfLexEntries();
			Assert.That(originalData.Count(), Is.EqualTo(OriginalNumOfLcmEntries));
		}

		[Test]
		public void RoundTrip_MongoToLcmToMongo_ShouldBeAbleToAddAndModifyParagraphsInCustomMultiParaField()
		{
			// Create
			var lfProject = _lfProj;
			SutLcmToMongo.Run(lfProject);

			LcmCache cache = _cache;
			IFwMetaDataCacheManaged mdc = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			ISilDataAccess data = cache.DomainDataByFlid;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var lcmEntry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(lcmEntry, Is.Not.Null, "Cannot test custom MultiPara field since the entry with GUID {0} was not found in the test data", TestEntryGuidStr);
			int flidMultiPara = mdc.GetFieldIds().Where(flid => mdc.GetFieldName(flid) == "Cust MultiPara").FirstOrDefault();
			Assert.That(flidMultiPara, Is.Not.EqualTo(0));
			int hvoMultiPara = data.get_ObjectProp(lcmEntry.Hvo, flidMultiPara);
			ICmObject referencedObject = cache.GetAtomicPropObject(hvoMultiPara);
			IStText lcmMultiPara = null;
			if (referencedObject is IStText)
				lcmMultiPara = (IStText)referencedObject;
			else
				Assert.Fail("Got something from the MultiPara field that wasn't an IStText. Test is unable to continue.");

			// Here we check two things:
			// 1) Can we add paragraphs?
			// 2) Can we change existing paragraphs?
			LfLexEntry lfEntry = _conn.GetLfLexEntryByGuid(entryGuid);
			// BsonDocument customFieldValues = GetCustomFieldValues(cache, lcmEntry, "entry");
			BsonDocument customFieldsBson = lfEntry.CustomFields;
			Assert.That(customFieldsBson.Contains("customField_entry_Cust_MultiPara"), Is.True,
				"Couldn't find custom MultiPara field. Expected customField_entry_Cust_MultiPara as a field name, but found: " +
				String.Join(", ", customFieldsBson.ToDictionary().Keys));
			BsonDocument multiParaBson = customFieldsBson["customField_entry_Cust_MultiPara"].AsBsonDocument;
			BsonArray paras = multiParaBson["paragraphs"].AsBsonArray;
			Assert.That(paras.Count, Is.EqualTo(2));
			// Save contents for later testing
			string   firstParaText = paras[0].AsBsonDocument["content"].AsString;
			string  secondParaText = paras[1].AsBsonDocument["content"].AsString;
			string changedParaText = "Modified paragraph with <span lang=\"grc\">Ελληνικα</span> in it";
			string changedMinusTag = "Modified paragraph with Ελληνικα in it";
			string   addedParaText = "New paragraph for &lt;this&gt; test";
			// Modify second paragraph
			paras[1].AsBsonDocument.Set("content", changedParaText);
			// And insert a new para in between the two
			paras.Insert(1, new BsonDocument("content", addedParaText));
			Assert.That(paras.Count, Is.EqualTo(3));
			// Rebuild customFieldValues
			multiParaBson.Set("paragraphs", paras);
			customFieldsBson.Set("customField_entry_Cust_MultiPara", multiParaBson);
			// Update Mongo connection double with rebuilt customFieldValues
			lfEntry.CustomFields = customFieldsBson;
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);

			// Exercise
			SutMongoToLcm.Run(lfProject);

			// Verify
			// First via BSON ...
			lcmEntry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			BsonDocument customFieldValues = GetCustomFieldValues(cache, lcmEntry, "entry");
			customFieldsBson = customFieldValues["customFields"].AsBsonDocument;
			multiParaBson = customFieldsBson["customField_entry_Cust_MultiPara"].AsBsonDocument;
			paras = multiParaBson["paragraphs"].AsBsonArray;
			Assert.That(paras.Count, Is.EqualTo(3));
			Assert.That(paras[0].AsBsonDocument["content"].AsString, Is.EqualTo(firstParaText));
			Assert.That(paras[1].AsBsonDocument["content"].AsString, Is.Not.EqualTo(secondParaText));
			Assert.That(paras[1].AsBsonDocument["content"].AsString, Is.EqualTo(addedParaText));
			Assert.That(paras[2].AsBsonDocument["content"].AsString, Is.Not.EqualTo(secondParaText));
			Assert.That(paras[2].AsBsonDocument["content"].AsString, Is.EqualTo(changedParaText));

			// ... then via LCM directly
			hvoMultiPara = data.get_ObjectProp(lcmEntry.Hvo, flidMultiPara);
			referencedObject = cache.GetAtomicPropObject(hvoMultiPara);
			lcmMultiPara = null;
			if (referencedObject is IStText)
				lcmMultiPara = (IStText)referencedObject;
			else
				Assert.Fail("After test, got something from the MultiPara field that wasn't an IStText. That's a test failure since it really shouldn't happen.");
			Assert.That(lcmMultiPara.ParagraphsOS.Count, Is.EqualTo(3));
			Assert.That(lcmMultiPara.ParagraphsOS[2].IsFinalParaInText, Is.True);
			Assert.That(lcmMultiPara.ParagraphsOS[0] is IStTxtPara, Is.True);
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[0]).Contents.Text, Is.EqualTo(firstParaText));
			Assert.That(lcmMultiPara.ParagraphsOS[1] is IStTxtPara, Is.True);
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[1]).Contents.Text, Is.Not.EqualTo(secondParaText));
			string escapedParaText = ConvertMongoToLcmTsStrings.HtmlDecode(addedParaText);
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[1]).Contents.Text, Is.EqualTo(escapedParaText));
			Assert.That(lcmMultiPara.ParagraphsOS[2] is IStTxtPara, Is.True);
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[2]).Contents.Text, Is.Not.EqualTo(secondParaText));
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[2]).Contents.Text, Is.EqualTo(changedMinusTag));
			string thirdParagraphWithSpans = ConvertLcmToMongoTsStrings.TextFromTsString(((IStTxtPara)lcmMultiPara.ParagraphsOS[2]).Contents, cache.WritingSystemFactory);
			Assert.That(thirdParagraphWithSpans, Is.EqualTo(changedParaText));
		}

		[Test]
		public void RoundTrip_MongoToLcmToMongo_ShouldBeAbleToDeleteParagraphsInCustomMultiParaField()
		{
			// Create
			var lfProject = _lfProj;
			SutLcmToMongo.Run(lfProject);

			LcmCache cache = lfProject.FieldWorksProject.Cache;
			IFwMetaDataCacheManaged mdc = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
			ISilDataAccess data = cache.DomainDataByFlid;
			Guid entryGuid = Guid.Parse(TestEntryGuidStr);
			var lcmEntry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			Assert.That(lcmEntry, Is.Not.Null, "Cannot test custom MultiPara field since the entry with GUID {0} was not found in the test data", TestEntryGuidStr);
			int flidMultiPara = mdc.GetFieldIds().Where(flid => mdc.GetFieldName(flid) == "Cust MultiPara").FirstOrDefault();
			Assert.That(flidMultiPara, Is.Not.EqualTo(0));
			int hvoMultiPara = data.get_ObjectProp(lcmEntry.Hvo, flidMultiPara);
			ICmObject referencedObject = cache.GetAtomicPropObject(hvoMultiPara);
			IStText lcmMultiPara = null;
			if (referencedObject is IStText)
				lcmMultiPara = (IStText)referencedObject;
			else
				Assert.Fail("Got something from the MultiPara field that wasn't an IStText. Test is unable to continue.");

			// Here we check just one thing:
			// 1) Can we delete paragraphs?
			LfLexEntry lfEntry = _conn.GetLfLexEntryByGuid(entryGuid);
			// BsonDocument customFieldValues = GetCustomFieldValues(cache, lcmEntry, "entry");
			BsonDocument customFieldsBson = lfEntry.CustomFields;
			Assert.That(customFieldsBson.Contains("customField_entry_Cust_MultiPara"), Is.True,
				"Couldn't find custom MultiPara field. Expected customField_entry_Cust_MultiPara as a field name, but found: " +
				String.Join(", ", customFieldsBson.ToDictionary().Keys));
			BsonDocument multiParaBson = customFieldsBson["customField_entry_Cust_MultiPara"].AsBsonDocument;
			BsonArray paras = multiParaBson["paragraphs"].AsBsonArray;
			Assert.That(paras.Count, Is.EqualTo(2));
			// Save contents for later testing
			string  firstParaText = paras[0].AsBsonDocument["content"].AsString;
			string secondParaText = paras[1].AsBsonDocument["content"].AsString;
			// Remove first paragraph
			paras.RemoveAt(0);
			Assert.That(paras.Count, Is.EqualTo(1));
			// Rebuild customFieldValues
			multiParaBson.Set("paragraphs", paras);
			customFieldsBson.Set("customField_entry_Cust_MultiPara", multiParaBson);
			// Update Mongo connection double with rebuilt customFieldValues
			lfEntry.CustomFields = customFieldsBson;
			lfEntry.AuthorInfo.ModifiedDate = DateTime.UtcNow;
			_conn.UpdateMockLfLexEntry(lfEntry);

			// Exercise
			SutMongoToLcm.Run(lfProject);

			// Verify
			// First via BSON ...
			lcmEntry = cache.ServiceLocator.GetObject(entryGuid) as ILexEntry;
			BsonDocument customFieldValues = GetCustomFieldValues(cache, lcmEntry, "entry");
			customFieldsBson = customFieldValues["customFields"].AsBsonDocument;
			multiParaBson = customFieldsBson["customField_entry_Cust_MultiPara"].AsBsonDocument;
			paras = multiParaBson["paragraphs"].AsBsonArray;
			Assert.That(paras.Count, Is.EqualTo(1));
			Assert.That(paras[0].AsBsonDocument["content"].AsString, Is.Not.EqualTo(firstParaText));
			Assert.That(paras[0].AsBsonDocument["content"].AsString, Is.EqualTo(secondParaText));

			// ... then via LCM directly
			hvoMultiPara = data.get_ObjectProp(lcmEntry.Hvo, flidMultiPara);
			referencedObject = cache.GetAtomicPropObject(hvoMultiPara);
			lcmMultiPara = null;
			if (referencedObject is IStText)
				lcmMultiPara = (IStText)referencedObject;
			else
				Assert.Fail("After test, got something from the MultiPara field that wasn't an IStText. That's a test failure since it really shouldn't happen.");
			Assert.That(lcmMultiPara.ParagraphsOS.Count, Is.EqualTo(1));
			Assert.That(lcmMultiPara.ParagraphsOS[0].IsFinalParaInText, Is.True);
			Assert.That(lcmMultiPara.ParagraphsOS[0] is IStTxtPara, Is.True);
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[0]).Contents.Text, Is.Not.EqualTo(firstParaText));
			Assert.That(((IStTxtPara)lcmMultiPara.ParagraphsOS[0]).Contents.Text, Is.EqualTo(secondParaText));
		}
	}
}
