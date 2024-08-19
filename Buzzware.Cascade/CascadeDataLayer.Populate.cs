using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Populates (sets the given association property(s) on the given model each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). 
		/// </summary>
		/// <param name="model">model to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task Populate(
			SuperModel model, 
			string property, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool skipIfSet = false, 
			bool? hold = null, 
			long? timeMs = null
		) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);

			if (skipIfSet && model != null && propertyInfo.GetValue(model) != null) {
				Log.Debug($"Skipping Populate {nameof(modelType)}.{property}");
				return;
			}

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
		/// Populates (sets the given association property(s) on the given model each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). 
		/// </summary>
		/// <param name="model">model to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task Populate(SuperModel model, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var association in associations) {
				await Populate(model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
			}
		}

		/// <summary>
		/// Populates (sets the given association property(s) on the given models each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). This is useful for setting association(s) on a list of models.
		/// In future, this could be optimised for when many are associated with the same. 
		/// </summary>
		/// <param name="models">models to act on</param>
		/// <param name="associations"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="property">nameof(Model.someProperty)</param>
		public async Task Populate(IEnumerable<SuperModel> models, IEnumerable<string> associations, int? freshnessSeconds = null, int? fallbackFreshnessSeconds = null, bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var model in models) {
				foreach (var association in associations) {
					await Populate((SuperModel)model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
				}
			}
		}

		/// <summary>
		/// Populates (sets a given association property on the given models according to the definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). This is useful for setting association(s) on a list of models.
		/// In future, this could be optimised for when many are associated with the same. 
		/// </summary>
		/// <param name="models">models to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="association"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="fallbackFreshnessSeconds">Fallback freshness requirement if the main requirement cannot be met. Defaults to FRESHNESS_ANY.</param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		/// <param name="timeMs">(optional) request time (milliseconds since 1970) - ideally a group of requests will be given the same time to optimise caching</param>
		public async Task Populate(IEnumerable<SuperModel> models, string association, int? freshnessSeconds = null,int? fallbackFreshnessSeconds = null,  bool skipIfSet = false, bool? hold = null, long? timeMs = null) {
			foreach (var model in models) {
				await Populate((SuperModel)model, association, freshnessSeconds, fallbackFreshnessSeconds, skipIfSet, hold, timeMs);
			}
		}

	}
}
