﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT license (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Linq;
using LfMerge.LanguageForge.Model;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using SIL.CoreImpl;
using SIL.FieldWorks.FDO;
using SIL.FieldWorks.FDO.Application;
using SIL.FieldWorks.FDO.Infrastructure;
using SIL.FieldWorks.Common.COMInterfaces;

namespace LfMerge.FieldWorks
{
	public class CustomFieldConverter
	{
		private FdoCache cache;
		private IFdoServiceLocator servLoc;
		private IFwMetaDataCacheManaged fdoMetaData;

		public CustomFieldConverter(FdoCache cache)
		{
			this.cache = cache;
			servLoc = cache.ServiceLocator;
			fdoMetaData = (IFwMetaDataCacheManaged)cache.MetaDataCacheAccessor;
		}

		/// <summary>
		/// Returns value of custom fields for this CmObject.
		/// </summary>
		/// <returns>A BsonDocument with the following structure: <br />
		/// { <br />
		///     "customFields": { fieldName: fieldValue, fieldName2: fieldValue2, etc. } <br />
		///     "customFieldGuids": { fieldName: "Guid-as-string", fieldName2: "Guid2-as-string", etc. } <br />
		/// } <br />
		/// -OR- <br />
		/// { <br />
		///     "customFields": { fieldName: fieldValue, fieldName2: fieldValue2, etc. } <br />
		///     "customFieldGuids": { fieldName: ["guid1", "guid2", "guid3"], fieldName2: "Guid2-as-string", etc. } <br />
		/// } <br />
		/// The format of the fieldName keys will be "customField_FOO_field_name_with_underscores",
		/// where FOO is one of "entry", "senses", or "examples". <br />
		/// Some fields have no need for a GUID (e.g., a custom number field), so not all fieldNames will appear in customFieldGuids.
		/// </returns>
		/// <param name="cmObj">Cm object.</param>
		/// <param name="objectType">Either "entry", "senses", or "examples"</param>
		public BsonDocument CustomFieldsForThisCmObject(ICmObject cmObj, string objectType = "entry")
		{
			if (cmObj == null) return null;

			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

			var customFieldData = new BsonDocument();
			var customFieldGuids = new BsonDocument();

			foreach (int flid in customFieldIds)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return null;
				fieldName = NormalizedFieldName(fieldName, objectType);
				BsonDocument bsonForThisField = GetCustomFieldData(cmObj.Hvo, flid);
				if (bsonForThisField != null)
				{
					customFieldData.Add(fieldName, bsonForThisField["value"]);
					BsonValue guid;
					if (bsonForThisField.TryGetValue("guid", out guid))
						customFieldGuids.Add(fieldName, guid);
				}
			}

			BsonDocument result = new BsonDocument();
			result.Add("customFields", customFieldData);
			result.Add("customFieldGuids", customFieldGuids);
			return result;
		}

		private string NormalizedFieldName(string fieldName, string fieldSourceType)
		{
			fieldName = fieldName.Replace(' ', '_');
			return String.Format("customField_{0}_{1}", fieldSourceType, fieldName);
		}

		/// <summary>
		/// Gets the data for one custom field, and any relevant GUIDs.
		/// </summary>
		/// <returns>A BsonDocument with the following structure: <br />
		/// { fieldName: { "value": BsonValue, "guid": "some-guid-as-a-string" } } <br />
		/// -OR- <br />
		/// { fieldName: { "value": BsonValue, "guid": ["guid1", "guid2", "guid3"] } } <br />
		/// The format of the fieldName key will be "customField_FOO_field_name_with_underscores",
		/// where FOO is one of "entry", "senses", or "examples". <br />
		/// The type of the "guid" value (array or string) will determine whether there is a single GUID,
		/// or a list of GUIDs that happens to contain only one entry.
		/// If there is no "guid" key, that field has no need for a GUID. (E.g., a number).
		/// </returns>
		/// <param name="hvo">Hvo of object we're getting the field for.</param>
		/// <param name="flid">Flid for this field.</param>
		/// <param name="fieldSourceType">Either "entry", "senses" or "examples". Could also be "allomorphs", eventually.</param>
		private BsonDocument GetCustomFieldData(int hvo, int flid, string fieldSourceType = "entry")
		{
			BsonValue fieldValue = null;
			BsonValue fieldGuid = null; // Might be a single value, might be a list (as a BsonArray)
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			var dataGuids = new List<Guid>();

			switch (fieldType)
			{
			case CellarPropertyType.GenDate:
				GenDate genDate = data.get_GenDateProp(hvo, flid);
				string genDateStr = genDate.ToLongString();
				fieldValue = String.IsNullOrEmpty(genDateStr) ? null : new BsonString(genDateStr);
				break;
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Integer:
				fieldValue = new BsonInt32(data.get_IntProp(hvo, flid));
				break;

			case CellarPropertyType.MultiString:
			case CellarPropertyType.MultiUnicode:
				var fdoMultiString = (IMultiAccessorBase)data.get_MultiStringProp(hvo, flid);
				LfMultiText multiTextValue = LfMultiText.FromFdoMultiString(fdoMultiString, servLoc.WritingSystemManager);
				fieldValue = (multiTextValue == null || multiTextValue.Count == 0) ? null : new BsonDocument(multiTextValue.AsBsonDocument());
				// No need to save GUIDs for multistrings
				break;

			case CellarPropertyType.OwningAtomic:
			case CellarPropertyType.ReferenceAtomic:
				int ownedHvo = data.get_ObjectProp(hvo, flid);
				fieldValue = GetCustomReferencedObject(ownedHvo, flid, ref dataGuids);
				if (fieldValue != null && fieldType == CellarPropertyType.ReferenceAtomic)
				{
					// Single CmPossiblity reference - LF expects format like { "value": "name of possibility" }
					fieldValue = new BsonDocument("value", fieldValue);
				}
				fieldGuid = new BsonString(dataGuids.FirstOrDefault().ToString());
				break;

			case CellarPropertyType.OwningCollection:
			case CellarPropertyType.OwningSequence:
			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				int[] listHvos = data.VecProp(hvo, flid);
				var innerValues = new BsonArray(listHvos.Select(listHvo => GetCustomReferencedObject(listHvo, flid, ref dataGuids)).Where(x => x != null));
				fieldValue = new BsonDocument("values", innerValues);
				fieldGuid = new BsonArray(dataGuids.Select(guid => guid.ToString()));
				break;

			case CellarPropertyType.String:
				ITsString iTsValue = data.get_StringProp(hvo, flid);
				if (iTsValue == null || String.IsNullOrEmpty(iTsValue.Text))
					fieldValue = null;
				else
					fieldValue = LfMultiText.FromSingleITsString(iTsValue, cache.ServiceLocator.WritingSystemManager).AsBsonDocument();
				break;

			case CellarPropertyType.Unicode:
				string UnicodeValue = data.get_UnicodeProp(hvo, flid);
				fieldValue = String.IsNullOrEmpty(UnicodeValue) ? null : new BsonString(UnicodeValue);
				break;

			default:
				fieldValue = null;
				break;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			var result = new BsonDocument();
			result.Add("value", fieldValue ?? BsonNull.Value); // BsonValues aren't allowed to have C# nulls; they have their own null representation
			if (fieldGuid is BsonArray)
				result.Add("guid", fieldGuid, ((BsonArray)fieldGuid).Count > 0);
			else
				result.Add("guid", fieldGuid, fieldGuid != null);
			return result;
		}

		private BsonValue GetCustomStTextValues(IStText obj, int flid)
		{
			if (obj == null) return null;
			List<ITsString> paras = obj.ParagraphsOS.OfType<IStTxtPara>().Select(para => para.Contents).ToList();
			List<string> htmlParas = paras.Where(para => para != null).Select(para => String.Format("<p>{0}</p>", para.Text)).ToList();
			WritingSystemManager wsManager = cache.ServiceLocator.WritingSystemManager;
			int fieldWs = cache.MetaDataCacheAccessor.GetFieldWs(flid);
			string wsStr = wsManager.GetStrFromWs(fieldWs);
			if (wsStr == null) wsStr = wsManager.GetStrFromWs(cache.DefaultUserWs); // TODO: Should that be DefaultAnalWs instead?
			return new BsonDocument(wsStr, new BsonDocument("value", new BsonString(String.Join("", htmlParas))));
		}

		private BsonValue GetCustomListValues(ICmPossibility obj, int flid)
		{
			if (obj == null) return null;
			// TODO: Consider using obj.NameHierarchyString instead of obj.Name.BestAnalysisVernacularAlternative.Text
			return new BsonString(obj.Name.BestAnalysisVernacularAlternative.Text);
		}

		private List<string> ParseCustomStTextValuesFromBson(BsonDocument source, out int wsId)
		{
			var result = new List<string>();
			wsId = 0;
			if (source.ElementCount <= 0)
				return result;
			LfMultiText valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(source);
			KeyValuePair<int, string> kv = valueAsMultiText.WsIdAndFirstNonEmptyString(cache);
			wsId = kv.Key;
			string htmlContents = kv.Value;
			result.AddRange(htmlContents.Split(new string[] { "</p>" }, StringSplitOptions.RemoveEmptyEntries)
				.Select(para => para.StartsWith("<p>") ? para.Substring(3) : para));
			// No need to trim trailing </p> as String.Split has already done that for us
			return result;
		}

		/// <summary>
		/// Get a BsonValue and GUID for the object referenced by a Reference or Owning field.
		/// The GUID will be returned by adding it to a list passed in by reference, so that
		/// ReferenceCollection, OwningSequence, and similar fields will be easy to process.
		/// Note that we guarantee that a GUID will always be added to the list, even if this function
		/// returns null for the object's data. That way Select(hvo => GetCustomReferencedObject(hvo, flid, ref myGuidList))
		/// will always return the same number of items as the number of GUIDs in myGuidList.
		/// </summary>
		/// <returns>The custom referenced object's data converted to a BsonValue.</returns>
		/// <param name="hvo">Hvo of referenced object.</param>
		/// <param name="flid">Flid of referring field (required to get correct writing system for an StText).</param>
		/// <param name="referencedObjectGuids">List to which referenced object's GUID will be added.</param>
		private BsonValue GetCustomReferencedObject(int hvo, int flid, ref List<Guid> referencedObjectGuids)
		{
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			if (hvo == 0 || !data.get_IsValidObject(hvo))
			{
				referencedObjectGuids.Add(Guid.Empty);
				return null;
			}
			ICmObject referencedObject = cache.GetAtomicPropObject(hvo);
			if (referencedObject == null)
			{
				referencedObjectGuids.Add(Guid.Empty);
				return null;
			}
			referencedObjectGuids.Add(referencedObject.Guid);
			if (referencedObject is IStText)
				return GetCustomStTextValues((IStText)referencedObject, flid);
			else if (referencedObject is ICmPossibility)
				return GetCustomListValues((ICmPossibility)referencedObject, flid);
			else
				return null;
		}

		public Guid ParseGuidOrDefault(string input)
		{
			Guid result = default(Guid);
			Guid.TryParse(input, out result);
			return result;
		}

		public ICmPossibilityList GetParentListForField(int flid)
		{
			Guid parentListGuid = fdoMetaData.GetFieldListRoot(flid);
			if (parentListGuid == Guid.Empty)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				Console.WriteLine("No possibility list found for custom field {0}; giving up", fieldName);
				return null;
				// TODO: If this happens, we're probably importing a newly-created possibility list, so we should
				// probably create it in FDO. Implementation needed.
			}
			return (ICmPossibilityList)servLoc.GetObject(parentListGuid);
		}

		/// <summary>
		/// Set custom field data for one field (specified by owner HVO and field ID).
		/// </summary>
		/// <returns><c>true</c>, if custom field data was set, <c>false</c> otherwise
		/// (e.g., if value was null, or field type was one not implemented in FDO, such as CellarPropertyType.Float).</returns>
		/// <param name="hvo">HVO of object whose field we're setting.</param>
		/// <param name="flid">Field ID of custom field to set.</param>
		/// <param name="value">Field's new value (as returned by GetCustomFieldData).</param>
		/// <param name="guidOrGuids">GUID or guids associated with new value (as returned by GetCustomFieldData).
		/// May be null or BsonNull.Value if no GUIDs associated with this value.</param>
		public bool SetCustomFieldData(int hvo, int flid, BsonValue value, BsonValue guidOrGuids)
		{
			if (value == null || value == BsonNull.Value)
				return false;
			List<Guid> fieldGuids = new List<Guid>();
			if (guidOrGuids == null || guidOrGuids == BsonNull.Value)
			{
				fieldGuids.Add(Guid.Empty);
			}
			else
			{
				if (guidOrGuids is BsonArray)
					fieldGuids.AddRange(guidOrGuids.AsBsonArray.Select(bsonValue => ParseGuidOrDefault(bsonValue.AsString)));
				else
					fieldGuids.Add(ParseGuidOrDefault(guidOrGuids.AsString));
			}
			ISilDataAccessManaged data = (ISilDataAccessManaged)cache.DomainDataByFlid;
			CellarPropertyType fieldType = (CellarPropertyType)fdoMetaData.GetFieldType(flid);
			Console.WriteLine("This field type is {0}", fieldType.ToString());
			string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
			if (fieldName == null)
				return false;

			switch (fieldType)
			{
			case CellarPropertyType.GenDate:
				GenDate genDate; // = data.get_GenDateProp(hvo, flid);
				if (GenDate.TryParse(value.AsString, out genDate))
				{
					data.SetGenDate(hvo, flid, genDate);
					return true;
				}
				return false;
				// When parsing, will use GenDate.TryParse(str, out genDate)

			case CellarPropertyType.Integer:
				data.SetInt(hvo, flid, value.AsInt32);
				return true;

			case CellarPropertyType.MultiString:
			case CellarPropertyType.MultiUnicode:
				{
					LfMultiText valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
					Console.WriteLine("Custom field {0} contained MultiText that looks like:", fieldName);
					foreach (KeyValuePair<string, LfStringField> kv in valueAsMultiText)
					{
						if (kv.Value == null)
							continue;
						string s = kv.Value.Value;
						int wsId = servLoc.WritingSystemManager.GetWsFromStr(kv.Key);
						if (wsId == 0)
							continue;
						Console.WriteLine("  {0}: {1}", kv.Key, s);
						data.SetMultiStringAlt(hvo, flid, wsId, TsStringUtils.MakeTss(s, wsId));
					}
					return true;
				}

			case CellarPropertyType.OwningAtomic:
				{
					// Custom field is a MultiparagraphText
					if (fieldGuids.First() != Guid.Empty)
					{
						// TODO: In the future, we should use this GUID to look up the current
						// value of the field. Then instead of deleting and re-creating the field
						// contents, we'll compare its contents to what's coming in, and only
						// delete and re-create paragraphs that have *changed*. That will make for
						// much less "churn" in Mercurial history.
					}
					// Delete and re-create field (TODO: Change this once we implement compare-to-current-value algorithm)
					int currentFieldContentsHvo = data.get_ObjectProp(hvo, flid);
					if (currentFieldContentsHvo != FdoCache.kNullHvo)
						data.DeleteObjOwner(hvo, currentFieldContentsHvo, flid, 0);
					// NOTE: I don't like the "magic" -2 number below, but FDO doesn't seem to have an enum for this. 2015-11 RM
					int newStTextHvo = data.MakeNewObject(cache.GetDestinationClass(flid), hvo, flid, -2);

					Console.WriteLine("Creating new StTxtPara for custom field {0} that has value {1}", fieldName, value.ToJson());

					int wsId;
					List<string> texts = ParseCustomStTextValuesFromBson(value.AsBsonDocument, out wsId);
					// TODO: Right now the assumption is baked in that FDO custom fields of OwningAtomic are ONLY multiparagraph texts.
					// But if this field's destination class is ever NOT StText, this cast will fail. If that happens, this code will
					// need to be modified.
					IStText newStText = (IStText)cache.GetAtomicPropObject(newStTextHvo);
					// TODO: In the future when we don't create a new object but re-use the existing one, we'll need
					// to compare paragraph contents and call newStText.AddNewTextPara() or newStText.DeleteParagraph() as many
					// times as needed to have the right # of paras. Then set the contents of each paragraph to their new values.
					//
					// For now, though, we just add a number of paragraphs to the brand-new object.
					foreach (string paraContents in texts)
					{
						IStTxtPara newPara = newStText.AddNewTextPara(null);
						newPara.Contents = TsStringUtils.MakeTss(paraContents, wsId);
						// TODO: Do we need to set anything else on the new paragraph object?
						Console.WriteLine("New paragraph contents: {0}", paraContents);
					}
					return true;
				}

			case CellarPropertyType.ReferenceAtomic:
				Console.WriteLine("ReferenceAtomic field named {0} with value {1}", fieldName, value.ToJson());
				int log_fieldWs = fdoMetaData.GetFieldWs(flid);
				string log_fieldWsStr = servLoc.WritingSystemManager.GetStrFromWs(log_fieldWs);
				Console.WriteLine("Writing system for this field has ID {0} and name ({1})", log_fieldWs, log_fieldWsStr);
				if (fieldGuids.First() != Guid.Empty)
				{
					int referencedHvo = data.get_ObjFromGuid(fieldGuids.First());
					data.SetObjProp(hvo, flid, referencedHvo);
					// TODO: What if the value of the referenced object has changed in LanguageForge? (E.g., change that possibility's text from "foo" to "bar")
					// Need to implement that scenario.
					return true;
				}
				else
				{
					// It's a reference to an ICmPossibility instance: create a new entry in appropriate PossibilityList
					LfStringField valueAsLfStringField = BsonSerializer.Deserialize<LfStringField>(value.AsBsonDocument);
					string nameHierarchy = valueAsLfStringField.Value;
					if (nameHierarchy == null)
						return false;
					int fieldWs = fdoMetaData.GetFieldWs(flid);
					// Oddly, this can return 0 for some custom fields. TODO: Find out why: that seems like it would be an error.
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs;
					ICmPossibilityList parentList = GetParentListForField(flid);
					ICmPossibility newPoss = parentList.FindOrCreatePossibility(nameHierarchy, fieldWs);

					data.SetObjProp(hvo, flid, newPoss.Hvo);
					return true;
				}

			case CellarPropertyType.ReferenceCollection:
			case CellarPropertyType.ReferenceSequence:
				{
					if (value == null || value == BsonNull.Value) return false;
					int fieldWs = fdoMetaData.GetFieldWs(flid);
					// TODO: Investigate why this is sometimes coming back as 0 instead of as a real writing system ID
					if (fieldWs == 0)
						fieldWs = cache.DefaultUserWs;
					ICmPossibilityList parentList = GetParentListForField(flid);

					LfStringArrayField valueAsStringArray = BsonSerializer.Deserialize<LfStringArrayField>(value.AsBsonDocument);

					// Step 1: Check if any of the fieldGuids is Guid.Empty, which would indicate a brand-new object that wasn't in FDO
					List<string> fieldData = valueAsStringArray.Values;
					Console.WriteLine("Reference collection had values {0}", String.Join(", ", fieldData));
					Console.WriteLine("Reference collection had GUIDs {0}", guidOrGuids.ToJson());
					IEnumerable<ICmPossibility> fieldObjs = fieldGuids.Zip<Guid, string, ICmPossibility>(fieldData, (thisGuid, thisData) =>
					{
						ICmPossibility newPoss;
						if (thisGuid == default(Guid)) {
							newPoss = ((ICmPossibilityList)parentList).FindOrCreatePossibility(thisData, fieldWs);
							return newPoss;
						}
						else {
							newPoss = servLoc.GetObject(thisGuid) as ICmPossibility;
							return newPoss;
						}
					});

					// Step 2: Remove any objects from the "old" list that weren't in the "new" list
					// We have to look them up by HVO because that's the only public API available in FDO
					// Following logic inspired by XmlImportData.CopyCustomFieldData in FieldWorks source
					int[] oldHvosArray = data.VecProp(hvo, flid);
					int[] newHvosArray = fieldObjs.Select(poss => poss.Hvo).ToArray();
					HashSet<int> newHvos = new HashSet<int>(newHvosArray);
					HashSet<int> combinedHvos = new HashSet<int>();
					// Loop backwards so deleting items won't mess up indices of subsequent deletions
					for (int idx = oldHvosArray.Length - 1; idx >= 0; idx--)
					{
						int oldHvo = oldHvosArray[idx];
						if (newHvos.Contains(oldHvo))
							combinedHvos.Add(oldHvo);
						else
							data.Replace(hvo, flid, idx, idx + 1, null, 0); // Important to pass *both* null *and* 0 here to remove items
					}

					// Step 3: Add any objects from the "new" list that weren't in the "old" list
					foreach (int newHvo in newHvosArray)
					{
						if (combinedHvos.Contains(newHvo))
							continue;
						// This item was added in the new list
						data.Replace(hvo, flid, combinedHvos.Count, combinedHvos.Count, new int[] { newHvo }, 1);
						combinedHvos.Add(newHvo);
					}
					return true;
				}

			case CellarPropertyType.String:
				{
					Console.WriteLine("Got value {0} of type {1}", value, value.GetType());
					Console.WriteLine("Writing system #{0} is \"{1}\" for this field", fdoMetaData.GetFieldWs(flid), servLoc.WritingSystemManager.GetStrFromWs(fdoMetaData.GetFieldWs(flid)));
					var valueAsMultiText = BsonSerializer.Deserialize<LfMultiText>(value.AsBsonDocument);
					data.SetString(hvo, flid, TsStringUtils.MakeTss(valueAsMultiText.FirstNonEmptyString(), cache.DefaultAnalWs));
					// TODO: Somehow use WritingSystemServices.ActualWs to get the right writing system here, instead of just assuming analysis
					return true;
				}

			case CellarPropertyType.Unicode:
				{
					string valueStr = value.AsString;
					data.SetUnicode(hvo, flid, valueStr, valueStr.Length);
					return true;
				}

			default:
				return false;
				// TODO: Maybe issue a proper warning (or error) log message for "field type not recognized"?
			}
			// return false; // If compiler complains about unreachable code, GOOD! We got the switch statement right. Otherwise this is our catch-all.
		}

		public void SetCustomFieldsForThisCmObject(ICmObject cmObj, string objectType, BsonDocument customFieldValues, BsonDocument customFieldGuids)
		{
			if (customFieldValues == null) return;
			List<int> customFieldIds = new List<int>(
				fdoMetaData.GetFields(cmObj.ClassID, false, (int)CellarPropertyTypeFilter.All)
				.Where(flid => cache.GetIsCustomField(flid)));

			var remainingFieldNames = new HashSet<string>(customFieldValues.Select(elem => elem.Name));
			foreach (int flid in customFieldIds)
			{
				string fieldName = fdoMetaData.GetFieldNameOrNull(flid);
				if (fieldName == null)
					return;
				fieldName = NormalizedFieldName(fieldName, objectType);
				BsonValue fieldValue = customFieldValues.GetValue(fieldName, BsonNull.Value);
				BsonValue fieldGuidOrGuids = (customFieldGuids == null) ? BsonNull.Value : customFieldGuids.GetValue(fieldName, BsonNull.Value);
				// Persist Guid.Empty as null to save space
				if (fieldGuidOrGuids.BsonType == BsonType.String && fieldGuidOrGuids.AsString == "00000000-0000-0000-0000-000000000000")
					fieldGuidOrGuids = BsonNull.Value;
				remainingFieldNames.Remove(fieldName);
				Console.WriteLine("Setting custom field {0} with data {1} and GUID(s) {2}", fieldName, fieldValue.ToJson(), fieldGuidOrGuids.ToJson());
				// TODO: Detect when fieldValue is null and don't bother calling SetCustomFieldData
				SetCustomFieldData(cmObj.Hvo, flid, fieldValue, fieldGuidOrGuids);
				customFieldValues.Remove(fieldName);
				if (customFieldGuids != null && customFieldGuids != BsonNull.Value)
					customFieldGuids.Remove(fieldName);
			}
			foreach (string fieldName in remainingFieldNames)
			{
				// TODO: These are NEW CUSTOM FIELDS! Will need to create them in FDO, then do:
				// BsonValue fieldValue = customFieldValues.GetValue(fieldName, BsonNull.Value);
				// BsonValue fieldGuidOrGuids = customFieldGuids.GetValue(fieldName, BsonNull.Value);
				// SetCustomFieldData(cmObj.Hvo, flid, fieldValue, fieldGuidOrGuids);
				// Above lines commented out until we can create new custom fields correctly. 2015-11 RM
				Console.WriteLine("Custom field {0} skipped because we're not yet creating new custom fields in FDO", fieldName);
			}
		}
	}
}
