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
		/// Populates a specified association property on a given model based on its association attribute definition (BelongsTo/HasMany/HasOne).
		/// </summary>
		/// <param name="model">The model to apply the association on</param>
		/// <param name="property">The property name on the model that is to be populated</param>
		/// <param name="freshnessSeconds">Defines how fresh the data should be, measured in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, the operation will be skipped for performance reasons</param>
		/// <param name="hold">Determines whether to hold the data or not during the operation</param>
		/// <param name="timeMs">Optional request time (milliseconds since 1970) used for optimizing caching when a group of requests share the same time</param>
		/// <returns>A Task that resolves when the operation has completed</returns>
		public async Task Populate(
			SuperModel model, 
			string property, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool skipIfSet = false, 
			bool? hold = null, 
			long? timeMs = null
		) {
			// Retrieve the type information for the model
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);

			// Check if the target property is already set and skip if necessary
			if (skipIfSet && model != null && propertyInfo.GetValue(model) != null) {
				Log.Debug($"Skipping Populate {nameof(modelType)}.{property}");
				return;
			}

			// Handle property population based on associated attribute type
			if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute), true).FirstOrDefault() is HasManyAttribute hasMany) {
				await processHasMany(model, modelType, propertyInfo!, hasMany, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(HasOneAttribute), true).FirstOrDefault() is HasOneAttribute hasOne) {
				await processHasOne(model, modelType, propertyInfo!, hasOne, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(BelongsToAttribute), true).FirstOrDefault() is BelongsToAttribute belongsTo) {
				await processBelongsTo(model, modelType, propertyInfo!, belongsTo, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(FromBlobAttribute), true).FirstOrDefault() is FromBlobAttribute fromBlob) {
				await processFromBlob(model, modelType, propertyInfo!, fromBlob, freshnessSeconds, fallbackFreshnessSeconds, hold, timeMs);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(FromPropertyAttribute), true).FirstOrDefault() is FromPropertyAttribute fromProperty) {
				await processFromProperty(model, modelType, propertyInfo!, fromProperty);
			}
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
		/// <param name="timeMs">Optional request time (milliseconds since 1970) for optimizing caching</param>
		/// <returns>A Task that resolves when the operation has completed</returns>
		public async Task Populate(SuperModel model, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var association in associations) {
				await Populate(model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
			}
		}

		/// <summary>
		/// Populates one or more association properties on each of the given models using the association attribute definitions.
		/// </summary>
		/// <param name="models">A collection of models to act upon</param>
		/// <param name="associations">A list of associations to populate for each model</param>
		/// <param name="freshnessSeconds">Specifies how fresh the data should be by setting the freshness in seconds</param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness setting, used if the main requirement cannot be fulfilled. Defaults to FRESHNESS_ANY.</param>
		/// <param name="timeMs">Optional request time (milliseconds since 1970) for optimizing caching when multiple requests share the same time</param>
		/// <param name="skipIfSet">If set to true, skips any associations where the property is already filled</param>
		/// <param name="hold">Specifies whether to maintain the acquired data after the method completes</param>
		/// <returns>A Task that resolves when the operation is complete for all models and associations</returns>
		public async Task Populate(IEnumerable<SuperModel> models, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var model in models) {
				foreach (var association in associations) {
					await Populate((SuperModel)model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
				}
			}
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
		/// <param name="timeMs">An optional time in milliseconds since 1970 to be applied to optimize caching for grouped requests</param>
		/// <returns>A Task that resolves when the operation is complete across all models and associations specified</returns>
		public async Task Populate(IEnumerable<SuperModel> models, string association, int? freshnessSeconds = null,int? fallbackFreshnessSeconds = null,  bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var model in models) {
				await Populate((SuperModel)model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
			}
		}

	}
}
