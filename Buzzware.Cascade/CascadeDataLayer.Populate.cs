using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// Methods to populate properties of models with data based on their association attributes.
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Populates one or more association properties on each of the given models using the association attribute definitions.
		/// </summary>
		/// <param name="models">A collection of models to act upon</param>
		/// <param name="associations">A list of associations to populate for each model</param>
		/// <param name="freshnessSeconds">Specifies how fresh the data should be by setting the freshness in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness setting, used if the main requirement cannot be fulfilled. Defaults to FRESHNESS_ANY.</param>
		/// <param name="sequenceBeganMs">Optional request time (milliseconds since 1970) for optimizing caching when multiple requests share the same time</param>
		/// <param name="skipIfSet">If set to true, skips any associations where the property is already filled</param>
		/// <param name="hold">Specifies whether to maintain the acquired data after the method completes</param>
		/// <returns>A Task that resolves when the operation is complete for all models and associations</returns>
		public async Task Populate(
			IEnumerable<SuperModel> models, 
			IEnumerable<string> associations, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool skipIfSet = false, 
			bool? hold = null, 
			long? sequenceBeganMs = null
		) {
			var superModels = models.ToArray();
			if (!superModels.Any())
				return;
			var first = superModels.First();
			var modelType = first.GetType();
			foreach (var association in associations) {
				var piAssociation = FastReflection.GetPropertyInfo(modelType,association);
				
				// reduce models according to skipIfSet
				var models2 = skipIfSet ? superModels.Where(s => piAssociation!.GetValue(s)==null).ToArray() : superModels;
				if (!models2.Any())
					continue;

				if (piAssociation?.KindAttribute is BelongsToAttribute belongsTo) {
					// construct a dictionary of id values => list of models with that value in IdProperty
					var valueModelList = new IdKeyDictionary<List<SuperModel>>();
					foreach (var model in models2) {
						var idValue = FastReflection.GetValue(model, belongsTo.IdProperty);
						var modelList = valueModelList.TryGetValue(idValue, out var list) ? list : null;
						if (modelList == null)
							valueModelList[idValue] = new List<SuperModel>(new SuperModel[] { model });		// add entry to dictionary
						else
							modelList.Add(model);		// add model to list for this id value
					}

					// get referenced association models with the matching ids (except null) 
					var modelResponses = await GetModelsForIds(piAssociation.Type, valueModelList.Keys.Where(k=>k!=null), freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
					// construct dictionary association id => association model
					var lookup = modelResponses.ToDictionary(r => r.RequestOp.Id!, r => r.Result as SuperModel);
					
					// for each id value and model list
					foreach (var pair in valueModelList) {
						var modelsWithIdPropertyValue = pair.Value!; 
						var associationValue = pair.Key!=null ? lookup[pair.Key] : null;
						// set the association on every model in the list to the lookup value
						await cascadePlatform.InvokeOnMainThreadNow(() => {
							foreach (var model in modelsWithIdPropertyValue) {
								model!.__mutateWith(m => piAssociation.SetValue(m, associationValue));
							}
						});
					}
				} else {
					if (piAssociation?.KindAttribute is FromBlobAttribute fromBlob) {
						foreach (var model in models2)
							await processFromBlob(model, modelType, piAssociation!, fromBlob, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
					} else if (piAssociation?.KindAttribute is HasManyAttribute hasMany) {
						foreach (var model in models2)
							await processHasMany(model, modelType, piAssociation!, hasMany, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
					} else if (piAssociation?.KindAttribute is HasOneAttribute hasOne) {
						foreach (var model in models2)
							await processHasOne(model, modelType, piAssociation!, hasOne, freshnessSeconds, fallbackFreshnessSeconds, hold, sequenceBeganMs);
					} else if (piAssociation?.KindAttribute is FromPropertyAttribute fromProperty) {
						foreach (var model in models2)
							await processFromProperty(model, modelType, piAssociation!, fromProperty);
					}
				}
			}
		}
		
		/// <summary>
		/// Populates a specified association property on a given model based on its association attribute definition (BelongsTo/HasMany/HasOne).
		/// </summary>
		/// <param name="model">The model to apply the association on</param>
		/// <param name="association">The property name on the model that is to be populated</param>
		/// <param name="freshnessSeconds">Defines how fresh the data should be, measured in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, the operation will be skipped for performance reasons</param>
		/// <param name="hold">Determines whether to hold the data or not during the operation</param>
		/// <param name="sequenceBeganMs">Optional request time (milliseconds since 1970) used for optimizing caching when a group of requests share the same time</param>
		/// <returns>A Task that resolves when the operation has completed</returns>
		public async Task Populate(
			SuperModel model, 
			string association, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool skipIfSet = false, 
			bool? hold = null, 
			long? sequenceBeganMs = null
		) {
			await Populate(new[] {model}, new[] {association}, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, sequenceBeganMs);
		}
		
		/// <summary>
		/// Populates specified association properties on a given model with data based on association attribute definitions.
		/// </summary>
		/// <param name="model">The model to which the association properties will be applied</param>
		/// <param name="associations">List of associations to populate</param>
		/// <param name="freshnessSeconds">Defines how fresh the data should be, measured in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true, skips all associations for which the properties are already set</param>
		/// <param name="hold">Determines whether to hold the data during the operation</param>
		/// <param name="sequenceBeganMs">Optional request time (milliseconds since 1970) for optimizing caching</param>
		/// <returns>A Task that resolves when the operation has completed</returns>
		public async Task Populate(SuperModel model, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? sequenceBeganMs = null) {
			await Populate(new [] {model}, associations, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, sequenceBeganMs);
		}


		/// <summary>
		/// Populates a specific association property on each of the provided models using its association attribute definition.
		/// </summary>
		/// <param name="models">The collection of models to be populated</param>
		/// <param name="property">The property name that each model should populate based on its association attribute definition</param>
		/// <param name="association">The association to populate for each given model</param>
		/// <param name="freshnessSeconds">The maximum age in seconds for the data to be considered fresh</param>
		/// <param name="fallbackFreshnessSeconds">Defines a fallback freshness if the main constraint cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">Skip the operation for properties that have already been set, improving performance</param>
		/// <param name="hold">Determines if the data will persist after execution</param>
		/// <param name="sequenceBeganMs">An optional time in milliseconds since 1970 to be applied to optimize caching for grouped requests</param>
		/// <returns>A Task that resolves when the operation is complete across all models and associations specified</returns>
		public async Task Populate(IEnumerable<SuperModel> models, string association, int? freshnessSeconds = null,int? fallbackFreshnessSeconds = null,  bool skipIfSet = false, bool? hold = null, long? sequenceBeganMs = null) {
			await Populate(models, new [] {association}, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, sequenceBeganMs);
		}

	}
}
