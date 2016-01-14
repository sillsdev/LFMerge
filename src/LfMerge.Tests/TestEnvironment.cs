﻿// Copyright (c) 2011-2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Autofac;
using Chorus.Model;
using LfMerge.FieldWorks;
using LfMerge.MongoConnector;
using LibFLExBridgeChorusPlugin.Infrastructure;
using NUnit.Framework;
using SIL.TestUtilities;

namespace LfMerge.Tests
{
	class TestEnvironment : IDisposable
	{
		private readonly TemporaryFolder _languageForgeServerFolder;
		public ILfMergeSettings Settings;

		public TestEnvironment(bool registerSettingsModelDouble = true,
			bool registerProcessingStateDouble = true)
		{
			_languageForgeServerFolder = new TemporaryFolder(TestContext.CurrentContext.Test.Name
				+ Path.GetRandomFileName());
			MainClass.Container = RegisterTypes(registerSettingsModelDouble,
				registerProcessingStateDouble, LanguageForgeFolder).Build();
			Settings = MainClass.Container.Resolve<ILfMergeSettings>();
		}

		private static ContainerBuilder RegisterTypes(bool registerSettingsModel,
			bool registerProcessingStateDouble, string temporaryFolder)
		{
			ContainerBuilder containerBuilder = MainClass.RegisterTypes();
			if (registerSettingsModel)
			{
				containerBuilder.RegisterType<LfMergeSettingsDouble>().As<ILfMergeSettings>()
					.WithParameter(new TypedParameter(typeof(string), temporaryFolder));
				containerBuilder.RegisterType<InternetCloneSettingsModelDouble>().As<InternetCloneSettingsModel>();
				containerBuilder.RegisterType<UpdateBranchHelperFlexDouble>().As<UpdateBranchHelperFlex>();
				containerBuilder.RegisterType<FlexHelperDouble>().As<FlexHelper>();
				containerBuilder.RegisterType<MongoProjectRecordFactoryDouble>().As<MongoProjectRecordFactory>();
			}

			var ldProj = new LanguageDepotProjectDouble {
				Username = "foo",
				Password = "secret"
			};
			containerBuilder.RegisterInstance(ldProj)
				.As<ILanguageDepotProject>().AsSelf().SingleInstance();

			if (registerProcessingStateDouble)
			{
				containerBuilder.RegisterType<ProcessingStateFactoryDouble>()
					.As<IProcessingStateDeserialize>().AsSelf().SingleInstance();
			}
			return containerBuilder;
		}

		public void Dispose()
		{
			_languageForgeServerFolder.Dispose();
			MainClass.Container.Dispose();
			MainClass.Container = null;
			LanguageForgeProjectAccessor.Reset();
			LfMergeSettingsAccessor.ResetCurrent();
			Settings = null;
		}

		public string LanguageForgeFolder
		{
			get { return _languageForgeServerFolder.Path; }
		}

		public ILfMergeSettings LangForgeDirFinder
		{
			get { return Settings; }
		}

		public string ProjectPath(string projectCode)
		{
			return Path.Combine(LanguageForgeFolder, projectCode);
		}

		public void CreateProjectUpdateFolder(string projectCode)
		{
			Directory.CreateDirectory(ProjectPath(projectCode));
		}

//		public string WriteFile(string fileName, string xmlForEntries, string directory)
//		{
//			string content;
//			using (var writer = File.CreateText(Path.Combine(directory, fileName)))
//			{
//				content = string.Format("<?xml version=\"1.0\" encoding=\"utf-8\"?> " +
//					"<lift version =\"{0}\" producer=\"WeSay.1Pt0Alpha\" " +
//					"xmlns:flex=\"http://fieldworks.sil.org\">{1}" +
//					"</lift>", Validator.LiftVersion, xmlForEntries);
//				writer.Write(content);
//			}
//
//			new FileInfo(Path.Combine(directory, fileName)).LastWriteTime = DateTime.Now.AddSeconds(1);
//
//			return content;
//		}
//
//		public void CreateLiftInputFile(IList<string> data, string fileName, string directory)
//		{
//			// NOTE: if the parameter differentTimeStamps is true we wait a second before
//			// creating the next file. This allows the files to have different timestamps.
//			// Originally the code used 100ms instead of 1s, but the resolution of file
//			// timestamps in the Mono implementation is 1s. However, if is possible in the
//			// real app that two files get created within a few milliseconds and we rely on
//			// the file timestamp to do the sorting of files, then we have a real problem.
//
//			var path = Path.Combine(directory, fileName);
//			if (File.Exists(path))
//				File.Delete(path);
//			using (var wrtr = File.CreateText(path))
//			{
//				for (var i = 0; i < data.Count; ++i)
//					wrtr.WriteLine(data[i]);
//				wrtr.Close();
//			}
//		}
//
//		public void CreateLiftUpdateFile(IList<string> data, string fileName,
//			string directory)
//		{
//			string path = Path.Combine(directory, fileName);
//			if (File.Exists(path))
//				File.Delete(path);
//			var bldr = new StringBuilder();
//			for (var i = 0; i < data.Count; ++i)
//				bldr.AppendLine(data[i]);
//
//			WriteFile(fileName, bldr.ToString(), directory);
//		}

//		public void VerifyEntryInnerText(XmlDocument xmlDoc, string xPath, string innerText)
//		{
//			var selectedEntries = VerifyEntryExists(xmlDoc, xPath);
//			Assert.That(selectedEntries[0].InnerText, Is.EqualTo(innerText), "Text for entry is wrong");
//		}
//
//		public XmlNodeList VerifyEntryExists(XmlDocument xmlDoc, string xPath)
//		{
//			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
//			Assert.IsNotNull(selectedEntries);
//			Assert.AreEqual(1, selectedEntries.Count,
//				String.Format("An entry with the following criteria should exist:{0}", xPath));
//			return selectedEntries;
//		}
//
//		public void VerifyEntryDoesNotExist(XmlDocument xmlDoc, string xPath)
//		{
//			XmlNodeList selectedEntries = xmlDoc.SelectNodes(xPath);
//			Assert.IsNotNull(selectedEntries);
//			Assert.AreEqual(0, selectedEntries.Count,
//				String.Format("An entry with the following criteria should not exist:{0}", xPath));
//		}
	}
}

