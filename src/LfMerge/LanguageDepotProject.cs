﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LfMerge
{
	public class LanguageDepotProject: ILanguageDepotProject
	{
		public void Initialize(string lfProjectCode)
		{
			var client = new MongoClient("mongodb://" + LfMergeSettings.Current.MongoDbHostNameAndPort);
			var database = client.GetDatabase("scriptureforge");
			var projectCollection = database.GetCollection<BsonDocument>("projects");
			//var userCollection = database.GetCollection<BsonDocument>("users");

			var projectFilter = new BsonDocument("projectCode", lfProjectCode);
			var list = projectCollection.Find(projectFilter).ToListAsync();
			list.Wait();

			var project = list.Result.FirstOrDefault();
			if (project == null)
				throw new ArgumentException("Can't find project code", "lfProjectCode");

			BsonValue value;
			if (project.TryGetValue("ldProjectCode", out value))
				ProjectCode = value.AsString;
			// TODO: need to get S/R server (language depot public, language depot private, custom, etc).
			// TODO: ldUsername and ldPassword should come from the users collection
			if (project.TryGetValue("ldUsername", out value))
				Username = value.AsString;
			if (project.TryGetValue("ldPassword", out value))
				Password = value.AsString;
		}

		public string Username { get; private set; }

		public string Password { get; private set; }

		public string ProjectCode { get; private set; }
	}
}

