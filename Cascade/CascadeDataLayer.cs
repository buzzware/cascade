using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Easy.Common.Extensions;
using Guards.Extensions;
using Serilog;
using Serilog.Events;
using StandardExceptions;

namespace Cascade {
	
	/// <summary>
	/// The main class for all data requests and other operations.
	/// This would generally only be created once on startup of an application.
	/// </summary>
	public class CascadeDataLayer : INotifyPropertyChanged {
		public const int FRESHNESS_ANY = Int32.MaxValue;

		public static ImmutableArray<Type> AssociationAttributes = ImmutableArray.Create<Type>(
			typeof(BelongsToAttribute),
			typeof(HasManyAttribute),
			typeof(HasOneAttribute)
		);
		
		private readonly IEnumerable<ICascadeCache> CacheLayers;
		private readonly ICascadePlatform cascadePlatform;
		public readonly CascadeConfig Config;
		private readonly ErrorControl errorControl;
		private readonly object lockObject;
		private readonly ICascadeOrigin Origin;
		private readonly CascadeJsonSerialization serialization;

		private bool _connectionOnline = true;

		/// <summary>
		/// CascadeDataLayer main constructor
		/// </summary>
		/// <param name="origin">Origin server</param>
		/// <param name="cacheLayers">Cache layers in order. Typically this would be an instance of a memory cache followed by a file based cache.</param>
		/// <param name="config">configuration for cascade</param>
		/// <param name="cascadePlatform">platform specific implementation</param>
		/// <param name="errorControl">instance for managing exceptions</param>
		/// <param name="serialization">instance for serializing models</param>
		public CascadeDataLayer(
			ICascadeOrigin origin,
			IEnumerable<ICascadeCache> cacheLayers,
			CascadeConfig config,
			ICascadePlatform cascadePlatform,
			ErrorControl errorControl, 
			CascadeJsonSerialization serialization
		) {
			Origin = origin;
			Origin.Cascade = this;
			CacheLayers = cacheLayers;
			foreach (var cache in cacheLayers)
				cache.Cascade = this;
			Config = config;
			this.cascadePlatform = cascadePlatform;
			lockObject = new object();
			this.errorControl = errorControl;
			this.serialization = serialization;
		}
		

		/// <summary>
		/// Use this timestamp to keep in sync with the framework. Especially useful for testing
		/// as time can then be controlled by your origin implementation. 
		/// Milliseconds since 1970
		/// </summary>
		public long NowMs => Origin.NowMs;
		
		
		/// <summary>
		/// This property determines whether the framework acts in online (true) or offline (false) mode.
		/// It can be set to offline at any time, but should not be set to online unless the changes pending list is empty.
		/// <see cref="ReconnectOnline">ReconnectOnline() uploads changes and sets ConnectionOnline=true for you.</see>  
		/// </summary>
		public bool ConnectionOnline {
			get => _connectionOnline;
			set {
				if (value != _connectionOnline) 
					cascadePlatform.InvokeOnMainThreadNow(() => {
						_connectionOnline = value;
						OnPropertyChanged(nameof(ConnectionOnline));
					});	
			}
		}

		/// <summary>
		/// Used for watching properties on this (normally ConnectionOnline)
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;
		
		// Showing Pending Counter on the Home Screen
		// To trigger a PropertyChanged event on this or any other property, use RaisePropertyChanged
		public int PendingCount
		{
			get
			{
	      // Get Count from the ChangesPendingList but only when Disconnected (Offline)
	      if (!ConnectionOnline)
	      {
	          return GetChangesPendingList().Count();
	      }
	      return 0;
			}
		}
		
		
		protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		/// <summary>
		/// Raises this object's PropertyChanged event.
		/// </summary>
		/// <param name="propertyName">Name of the property used to notify listeners. This
		/// value is optional and can be provided automatically when invoked from compilers
		/// that support <see cref="CallerMemberNameAttribute"/>.</param>
		public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
		{
			OnPropertyChanged(propertyName);
		}
		
		
		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		
		/// <summary>
		/// Get data from cache/origin of model type M and return result or null
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <typeparam name="M">model type - subclass of SuperModel</typeparam>
		/// <returns>model of type M or null</returns>
		public async Task<M?> Get<M>(
			int id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) where M : class {
			return (await this.GetResponse(typeof(M),id, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold)).Result as M;
		}

		/// <summary>
		/// Get data from cache/origin of model type M and return result or null
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <typeparam name="M">model type - subclass of SuperModel</typeparam>
		/// <returns>model of type M or null</returns>
		public async Task<M?> Get<M>(
			string id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) where M : class {
			return (await this.GetResponse(typeof(M),id, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold)).Result as M;
		}

		
		/// <summary>
		/// Get data from cache/origin of model type M and return result or null
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <typeparam name="M">model type - subclass of SuperModel</typeparam>
		/// <returns>model of type M or null</returns>
		public async Task<M?> Get<M>(
			long id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) where M : class {
			return (await this.GetResponse(typeof(M),id, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold)).Result as M;
		}

		
		
		
		
		
		
		/// <summary>
		/// Get a model instance of given model type and id with a full detail OpResponse object
		/// </summary>
		/// <param name="id">id of model to get</param>
		/// <param name="populate">Enumerable association property names to set with data for convenience. Equivalent to multiple Get/Query requests</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <param name="hold">whether to mark the main main object and populated associations to be held in cache (protected from cache clearing and a candidate to be taken offline)</param>
		/// <returns>OpResponse</returns>
		public Task<OpResponse> GetResponse(
			Type modelType,
			object id,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) {
			var req = RequestOp.GetOp(
				modelType,
				id,
				NowMs,
				populate,
				freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				populateFreshnessSeconds ?? Config.DefaultPopulateFreshnessSeconds,
				fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,
				hold
			);
			return ProcessRequest(req);
		}

		
		/// <summary>
		/// Gets a collection literally ie an enumerable of ids
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <typeparam name="M">the type of the collection</typeparam>
		/// <returns>enumerable of ids</returns>
		public async Task<IEnumerable<object>?> GetCollection<M>(string collectionName) where M : class {
			return (await this.GetCollectionResponse<M>(collectionName)).Result as IEnumerable<object>;
		}

		/// <summary>
		/// Gets a collection literally ie an enumerable of ids with the full detail OpResponse
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <typeparam name="M">the type of the collection</typeparam>
		/// <returns>OpResponse with Results = enumerable of ids</returns>
		public Task<OpResponse> GetCollectionResponse<M>(string collectionName) {
			var req = RequestOp.GetCollectionOp<M>(
				collectionName,
				NowMs
			);
			return ProcessRequest(req);
		}

		/// <summary>
		/// Clear a collection from all caches
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		public async Task ClearCollection<Model>(string collectionName) {
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.StoreCollection(typeof(Model), collectionName, null, NowMs);
				}
			});
		}

		/// <summary>
		/// Replace a collection with a set of ids in all caches
		/// </summary>
		/// <param name="collectionName">your chosen name for the collection</param>
		/// <param name="ids">an enumerable of ids</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		public async Task<IEnumerable<object>> SetCollection<Model>(string collectionName, IEnumerable<object> ids) where Model : class {
			var result = ids.ToImmutableArray();
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.StoreCollection(typeof(Model), collectionName, result, NowMs);
				}
			});
			return result;
		}

		/// <summary>
		/// A kind of query like that used for populating HasMany/HasOne associations. Not normally used.
		/// </summary>
		/// <param name="propertyName">Name of foreign key</param>
		/// <param name="propertyValue">Value of foreign key</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		/// <returns>OpResponse</returns>
		public async Task<OpResponse> GetWhereCollectionResponse<Model>(string propertyName, string propertyValue, int? freshnessSeconds = null, int? populateFreshnessSeconds = null) {
			var key = CascadeUtils.WhereCollectionKey(typeof(Model).Name, propertyName, propertyValue);
			var requestOp = new RequestOp(
				NowMs,
				typeof(Model),
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				populateFreshnessSeconds: populateFreshnessSeconds ?? Config.DefaultPopulateFreshnessSeconds,
				criteria: new Dictionary<string, object>() { [propertyName] = propertyValue },
				key: key
			);
			var opResponse = await ProcessRequest(requestOp);
			return opResponse;
		}

		/// <summary>
		/// A kind of query like that used for populating HasMany/HasOne associations. Not normally used.
		/// </summary>
		/// <param name="propertyName">Name of foreign key</param>
		/// <param name="propertyValue">Value of foreign key</param>
		/// <param name="freshnessSeconds">freshness for the main object</param>
		/// <param name="populateFreshnessSeconds">freshness for any populated associations</param>
		/// <typeparam name="Model">the type of the collection</typeparam>
		/// <returns>Enumerable of models of type M</returns>
		public async Task<IEnumerable<M>> GetWhereCollection<M>(string propertyName, string propertyValue, int? freshnessSeconds = null, int? populateFreshnessSeconds = null) where M : class {
			var response = await this.GetWhereCollectionResponse<M>(propertyName, propertyValue, freshnessSeconds, populateFreshnessSeconds);
			var results = response.Results.Cast<M>().ToImmutableArray();
			return results;
		}

		/// <summary>
		/// Replaces the cached values for HasMany/HasOne like associations. Not normally used.
		/// </summary>
		/// <param name="modelType">type of model</param>
		/// <param name="propertyName">Name of foreign key</param>
		/// <param name="propertyValue">Value of foreign key</param>
		/// <param name="collection">enumerable of ids for the collection</param>
		/// <returns>void</returns>
		public async Task SetCacheWhereCollection(Type modelType, string propertyName, string propertyValue, IEnumerable<object> collection) {
			IEnumerable<object>? ids;
			var enumerable = collection as object[] ?? collection.ToArray();
			if (!enumerable.Any()) {
				ids = ImmutableArray<object>.Empty;
			}
			else if (CascadeTypeUtils.IsModel(enumerable.First())) {
				ids = enumerable.Select(m => CascadeTypeUtils.GetCascadeId(m)).ToImmutableArray();
			}
			else if (CascadeTypeUtils.IsId(enumerable.First())) {
				ids = enumerable.Cast<object>().ToImmutableArray();
			}
			else
				throw new ArgumentException("collection not recognised as ids or models");

			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					var key = CascadeUtils.WhereCollectionKey(modelType.Name, propertyName, propertyValue);
					await layer.StoreCollection(modelType, key, ids, NowMs);
				}
			});
		}

		/// <summary>
		/// Replaces the cached model in all caches 
		/// </summary>
		/// <param name="id">id of model to replace</param>
		/// <param name="model">model to replace with. The model type is derived from this</param>
		public async Task SetCacheRecord(object id, object model) {
			var modelType = model.GetType();
			await errorControl.FilterGuard(async () => {
				foreach (var layer in CacheLayers.Reverse()) {
					await layer.Store(modelType, id, model, NowMs);
				}
			});
		}

		/// <summary>
		/// Do a search on the origin with the given model and criteria and cache the resulting collection under the collectionKey.
		/// Models are cached and returned, as are populated association models.
		/// </summary>
		/// <param name="collectionKey"></param>
		/// <param name="criteria"></param>
		/// <param name="populate"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="populateFreshnessSeconds"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>IEnumerable<M></returns>
		public async Task<IEnumerable<M>> Query<M>(
			string? collectionKey,
			object? criteria = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) {
			var response = await QueryResponse<M>(collectionKey, criteria, populate, freshnessSeconds, populateFreshnessSeconds, fallbackFreshnessSeconds, hold);
			var results = response.Results.Cast<M>().ToImmutableArray();
			//return Array.ConvertAll<object,M>(response.Results) ?? Array.Empty<M>();
			return results;
		}

		/// <summary>
		/// Do a search on the origin with the given model and criteria for a single record, and cache the resulting collection under the collectionKey.
		/// Models are cached and returned, as are populated association models.
		/// </summary>
		/// <param name="collectionKey"></param>
		/// <param name="criteria"></param>
		/// <param name="populate"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="populateFreshnessSeconds"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns></returns>
		public async Task<M?> QueryOne<M>(
			string? collectionKey,
			object criteria = null,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) {
			return (await this.Query<M>(collectionKey, criteria, populate, freshnessSeconds: freshnessSeconds, populateFreshnessSeconds: populateFreshnessSeconds, fallbackFreshnessSeconds: fallbackFreshnessSeconds, hold: hold)).FirstOrDefault();
		}
		
		/// <summary>
		/// Do a search on the origin with the given model and criteria and cache the resulting collection under the collectionKey and return full detail OpResponse.
		/// Models are cached and returned, as are populated association models.
		/// </summary>
		/// <param name="collectionKey"></param>
		/// <param name="criteria"></param>
		/// <param name="populate"></param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="populateFreshnessSeconds"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>OpResponse</returns>
		public Task<OpResponse> QueryResponse<M>(string collectionName,
			object criteria,
			IEnumerable<string>? populate = null,
			int? freshnessSeconds = null,
			int? populateFreshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) {
			var req = RequestOp.QueryOp<M>(
				collectionName,
				criteria,
				NowMs,
				populate: populate,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				populateFreshnessSeconds: populateFreshnessSeconds ?? Config.DefaultPopulateFreshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,
				hold ?? false
			);
			return ProcessRequest(req);
		}



		/// <summary>
		/// Populates (sets the given association property(s) on the given model each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). 
		/// </summary>
		/// <param name="model">model to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		public async Task Populate(SuperModel model, string property, int? freshnessSeconds = null, bool skipIfSet = false, bool? hold = null) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);

			if (skipIfSet && model != null && propertyInfo.GetValue(model) != null) {
				Log.Debug($"Skipping Populate {nameof(modelType)}.{property}");
				return;
			}

			if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute), true).FirstOrDefault() is HasManyAttribute hasMany) {
				await processHasMany(model, modelType, propertyInfo!, hasMany, freshnessSeconds, hold);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(HasOneAttribute), true).FirstOrDefault() is HasOneAttribute hasOne) {
				await processHasOne(model, modelType, propertyInfo!, hasOne, freshnessSeconds, hold);
			}
			else if (propertyInfo?.GetCustomAttributes(typeof(BelongsToAttribute), true).FirstOrDefault() is BelongsToAttribute belongsTo) {
				await processBelongsTo(model, modelType, propertyInfo!, belongsTo, freshnessSeconds, hold);
			}
		}

		/// <summary>
		/// Populates (sets the given association property(s) on the given model each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). 
		/// </summary>
		/// <param name="model">model to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		public async Task Populate(SuperModel model, IEnumerable<string> associations, int? freshnessSeconds = null, bool skipIfSet = false, bool? hold = null) {
			foreach (var association in associations) {
				await Populate(model, association, freshnessSeconds, skipIfSet, hold);
			}
		}

		/// <summary>
		/// Populates (sets the given association property(s) on the given models each according to their definition attribute (BelongsTo/HasMany/HasOne)) with
		/// the resulting model(s) from their internal query(s). This is useful for setting association(s) on a list of models.
		/// In future, this could be optimised for when many are associated with the same. 
		/// </summary>
		/// <param name="models">models to act on</param>
		/// <param name="property">nameof(Model.someProperty)</param>
		/// <param name="freshnessSeconds"></param>
		/// <param name="skipIfSet">If true and the property is already set, don't do anything (for performance reasons)</param>
		/// <param name="hold"></param>
		public async Task Populate(IEnumerable<SuperModel> models, IEnumerable<string> associations, int? freshnessSeconds = null, bool skipIfSet = false, bool? hold = null) {
			foreach (var model in models) {
				foreach (var association in associations) {
					await Populate((SuperModel)model, association, freshnessSeconds, skipIfSet, hold);
				}
			}
		}
		
		/// <summary>
		/// Replaces the value of the given HasMany property with the given IEnumerable of models and updates the caches appropriately.
		/// This is needed eg. when you add models to a HasMany association
		/// </summary>
		/// <param name="model"></param>
		/// <param name="property"></param>
		/// <param name="models"></param>
		/// <exception cref="ArgumentException"></exception>
		public async Task UpdateHasMany(SuperModel model, string property, IEnumerable<object> models) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);
			if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute), true).FirstOrDefault() is HasManyAttribute hasMany) {
				var propertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
				var isEnumerable = (propertyType?.Implements<IEnumerable>() ?? false) && propertyType != typeof(string);
				var foreignType = isEnumerable ? CascadeTypeUtils.InnerType(propertyType!) : null;
				foreignType = foreignType != null ? CascadeTypeUtils.DeNullType(foreignType) : null;
				if (foreignType == null)
					throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");

				object modelId = CascadeTypeUtils.GetCascadeId(model);
				await SetCacheWhereCollection(foreignType, hasMany.ForeignIdProperty, modelId.ToString(), models);
				await SetModelCollectionProperty(model, propertyInfo, models);
			}
			else {
				throw new ArgumentException($"{property} is not a [HasMany] property");
			}
		}

		/// <summary>
		/// Replaces the value of the given HasOne property with the given model.
		/// Note: This should update the caches with a collection of one, but currently does not
		/// </summary>
		/// <param name="model">the main model</param>
		/// <param name="property">name of the HasOne property on the main model</param>
		/// <param name="value">the new model for the association</param>
		/// <exception cref="ArgumentException"></exception>
		public async Task UpdateHasOne(SuperModel model, string property, object value) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);
			if (propertyInfo?.GetCustomAttributes(typeof(HasOneAttribute), true).FirstOrDefault() is HasOneAttribute hasOne) {
				var propertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
				var foreignType = propertyType;
				if (foreignType == null)
					throw new ArgumentException("Unable to get foreign model type. Property should be of type a ChildModel");

				//object modelId = CascadeTypeUtils.GetCascadeId(model);
				await SetModelProperty(model, propertyInfo, value);
			}
			else {
				throw new ArgumentException($"{property} is not a [HasMany] property");
			}
		}

		// Consider using UpdateHasMany & UpdateHasOne instead eg. UpdateHasMany updates the underlying collection 
		// Use this when you have a value for the association, rather than using Populate()
		
		
		/// <summary>
		/// Use this when you only want to set an association property to a value you already have.
		/// Consider using UpdateHasMany/UpdateHasOne/UpdateBelongsTo instead.
		/// Note that this method does not update the caches, but the more specific methods do.
		/// </summary>
		/// <param name="target">model to act on</param>
		/// <param name="propertyName">name of association property to set</param>
		/// <param name="value"></param>
		public async Task SetAssociation(object target, string propertyName, object value) {
			var propertyInfo = target.GetType().GetProperty(propertyName)!;
			await SetModelProperty(target, propertyInfo, value);
		}

		// update the association on many models with the same property and value
		
		
		
		/// <summary>
		/// Use this when you only want to set an association property to a value you already have.
		/// Consider using UpdateHasMany/UpdateHasOne/UpdateBelongsTo instead.
		/// Note that this method does not update the caches, but the more specific methods do.
		/// </summary>
		/// <param name="targets">models to act on</param>
		/// <param name="propertyName">name of association property to set</param>
		/// <param name="value"></param>
		public async Task SetAssociation(IEnumerable targets, string propertyName, object value) {
			PropertyInfo propertyInfo = null;
			foreach (object target in targets) {
				if (propertyInfo == null)
					propertyInfo = target.GetType().GetProperty(propertyName)!;
				await SetModelProperty(target, propertyInfo, value);
			}
		}

		/// <summary>
		/// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
		/// instance is returned from the origin.
		/// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
		/// any call(s) to Populate() as required. 
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>model of type M</returns>
		public async Task<M> Create<M>(M model, bool hold = false) {
			var response = await CreateResponse<M>(model,hold: hold);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}
		
		/// <summary>
		/// Create and return a model of the given type. An instance is used to pass in the values, and a newly created
		/// instance is returned from the origin.
		/// Note: the populate option will be removed from all write methods. Instead Create should be called followed by
		/// any call(s) to Populate() as required. 
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hold"></param>
		/// <typeparam name="M"></typeparam>
		/// <returns>OpResponse with full detail of operation, including Result of type M</returns>
		public Task<OpResponse> CreateResponse<M>(M model, bool hold = false) {
			var req = RequestOp.CreateOp(
				model!,
				NowMs,
				hold: hold
			);
			return ProcessRequest(req);
		}

		public async Task<M> Replace<M>(M model) {
			var response = await ReplaceResponse<M>(model);
			if (response.Result is not M result)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return result;
		}

		public Task<OpResponse> ReplaceResponse<M>(M model) {
			var req = RequestOp.ReplaceOp(
				model!,
				NowMs
			);
			return ProcessRequest(req);
		}

		// may return null if the record no longer exists
		public async Task<M?> Update<M>(M model, IDictionary<string, object> changes) where M : class {
			var response = await UpdateResponse<M>(model, changes);
			if (response.Result != null && response.Result is not M)
				throw new AssumptionException($"Should be of type {typeof(M).Name}");
			return response.Result as M;
		}
		
		public Task<OpResponse> UpdateResponse<M>(M model, IDictionary<string, object> changes) {
			var req = RequestOp.UpdateOp(
				model!,
				changes,
				NowMs
			);
			return ProcessRequest(req);
		}
		
		public async Task Destroy<M>(M model) {
			var response = await DestroyResponse<M>(model);
		}

		public Task<OpResponse> DestroyResponse<M>(M model) {
			var req = RequestOp.DestroyOp<M>(
				model,
				NowMs
			);
			return ProcessRequest(req);
		}

		// ModelType : what model type are you executing the action on? Useful when implementing the action on the origin
		// ReturnType : the type you will be returning - eg. same as ModelType or IEnumerable<ModelType> or anything else
		public async Task<ReturnType> Execute<ModelType, ReturnType>(string action, IDictionary<string, object> parameters) {
			var response = await ExecuteResponse<ModelType, ReturnType>(
				action,
				parameters
			);
			if (response.Result is not ReturnType result)
				throw new AssumptionException($"Should be of type {typeof(ReturnType).Name}");
			return (ReturnType)response.Result;
		}

		public Task<OpResponse> ExecuteResponse<ModelType, ReturnType>(string action, IDictionary<string, object> parameters) {
			var req = RequestOp.ExecuteOp<ModelType, ReturnType>(
				action,
				parameters,
				NowMs
			);
			return ProcessRequest(req);
		}

		// =================== PRIVATE METHODS =========================


		public static object ConvertType(object aSource, Type singularType) {
			throw new NotImplementedException();
		}

		private async Task processHasMany(SuperModel model, Type modelType, PropertyInfo propertyInfo, HasManyAttribute attribute, int? freshnessSeconds = null, bool? hold = null) {
			var propertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
			var isEnumerable = (propertyType?.Implements<IEnumerable>() ?? false) && propertyType != typeof(string);
			var foreignType = isEnumerable ? CascadeTypeUtils.InnerType(propertyType!) : null;
			foreignType = foreignType != null ? CascadeTypeUtils.DeNullType(foreignType) : null;
			if (foreignType == null)
				throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");

			object modelId = CascadeTypeUtils.GetCascadeId(model);
			var key = CascadeUtils.WhereCollectionKey(foreignType.Name, attribute.ForeignIdProperty, modelId.ToString());
			var requestOp = new RequestOp(
				NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				hold: hold, 
				criteria: new Dictionary<string, object>() { [attribute.ForeignIdProperty] = modelId }, 
				key: key
			);
			var opResponse = await InnerProcessWithFallback(requestOp);
			await StoreInPreviousCaches(opResponse);
			await SetModelCollectionProperty(model, propertyInfo, opResponse.Results);
		}

		private async Task processHasOne(SuperModel model, Type modelType, PropertyInfo propertyInfo, HasOneAttribute attribute, int? freshnessSeconds = null, bool? hold = null) {
			var propertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
			var isEnumerable = (propertyType?.Implements<IEnumerable>() ?? false) && propertyType != typeof(string);
			if (isEnumerable)
				throw new ArgumentException("HasOne property should not be of type IEnumerable");

			var foreignType = propertyType;
			foreignType = foreignType != null ? CascadeTypeUtils.DeNullType(foreignType) : null;
			if (foreignType == null)
				throw new ArgumentException("Unable to get foreign model type. Property should be of type <ChildModel>");

			object modelId = CascadeTypeUtils.GetCascadeId(model);
			// var requestOp = new RequestOp(
			// 	NowMs,
			// 	foreignType,
			// 	RequestVerb.Get,
			// 	modelId,
			// 	freshnessSeconds: freshnessSeconds
			// );
			var key = CascadeUtils.WhereCollectionKey(foreignType.Name, attribute.ForeignIdProperty, modelId.ToString());
			var requestOp = new RequestOp(
				NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				hold: hold, 
				criteria: new Dictionary<string, object>() { [attribute.ForeignIdProperty] = modelId }, 
				key: key
			);
			var opResponse = await InnerProcessWithFallback(requestOp);
			await StoreInPreviousCaches(opResponse);
			await SetModelProperty(model, propertyInfo, opResponse.FirstResult);
		}

		private async Task<OpResponse> InnerProcess(RequestOp requestOp, bool connectionOnline) {
			OpResponse opResponse = await errorControl.FilterGuard(() => {
				lock (lockObject) {
					// !!! probably should remove this try/catch - its here for debugging
					try {
						switch (requestOp.Verb) {
							case RequestVerb.Get:
							case RequestVerb.Query:
								return ProcessGetOrQuery(requestOp, connectionOnline);
							case RequestVerb.GetCollection:
								return ProcessGetCollection(requestOp, connectionOnline);
							case RequestVerb.Create:
								return ProcessCreate(requestOp, connectionOnline);
							case RequestVerb.Replace:
								return ProcessReplace(requestOp, connectionOnline);
							case RequestVerb.Update:
								return ProcessUpdate(requestOp, connectionOnline);
							case RequestVerb.Destroy:
								return ProcessDestroy(requestOp, connectionOnline);
							case RequestVerb.Execute:
								return ProcessExecute(requestOp, connectionOnline);
							default:
								throw new ArgumentException("Unsupported verb");
						}
					} catch (Exception e) {
						Log.Error(e,"error");
						throw;
					}
				}
			});
			
			// BEGIN Populate
			var populate = requestOp.Populate?.ToArray() ?? new string[] { };

			if (requestOp.Verb == RequestVerb.Query && opResponse.IsIdResults) {
				var modelResponses = await GetModelsForIds(
					requestOp.Type,
					opResponse.ResultIds,
					requestOp.FreshnessSeconds ?? Config.DefaultFreshnessSeconds,
					fallbackFreshnessSeconds: requestOp.FallbackFreshnessSeconds,
					hold: requestOp.Hold
				);
				IEnumerable<SuperModel> models = modelResponses.Select(r => (SuperModel)r.Result).ToImmutableArray();
				if (populate.Any()) {
					await Populate(models, populate, freshnessSeconds: requestOp.PopulateFreshnessSeconds, hold: requestOp.Hold);
				}
				opResponse = opResponse.withChanges(result: models); // modify the response with models instead of ids
			} else {
				if (populate.Any()) {
					IEnumerable<SuperModel> results = opResponse.Results.Cast<SuperModel>();
					await Populate(results, populate, freshnessSeconds: requestOp.PopulateFreshnessSeconds, hold: requestOp.Hold);
				}
			}
			// END Populate
			
			SetResultsImmutable(opResponse);
			return opResponse;
		}

		private async Task<OpResponse> ProcessRequest(RequestOp requestOp) {
			if (Log.Logger.IsEnabled(LogEventLevel.Debug))
				Log.Debug("ProcessRequest RequestOp: {@Verb} {@Id} {@Type} {@Key} {@Freshness} {@Criteria}", 
					requestOp.Verb, requestOp.Id, requestOp.Type, requestOp.Key, requestOp.FreshnessSeconds, requestOp.Criteria);

			// if (HavePendingChanges && shouldAttemptUploadPendingChanges) {
			// 	try {        
			// 		if (Cascade.ConnectionMode!=UploadingPendingChanges)
			// 			Cascade.ConnectionMode = UploadingPendingChanges;
			// 		UploadPendingChanges
			// 		InnerProcess(mode=online)            // !!! should only attempt online processing
			// 		Cascade.ConnectionMode = Online
			// 	} catch (OfflineException) {
			// 		// probably smother exceptions. If we can't complete UploadPendingChanges(), stay offline
			// 	}
			// }

			OpResponse opResponse = await InnerProcessWithFallback(requestOp);
			
			await StoreInPreviousCaches(opResponse); // just store ResultIds
			
			if (Log.Logger.IsEnabled(LogEventLevel.Debug))
				Log.Debug("ProcessRequest OpResponse: Connected: {@Connected} Exists: {@Exists} Result: {@Result}", opResponse.Connected,opResponse.Exists,opResponse.Result);
			return opResponse;
		}

		private async Task<OpResponse> InnerProcessWithFallback(RequestOp req) {
			OpResponse? result = null;
			// bool loop = false;
			// do {
			// 	try {
			// 		loop = false;
					result = await InnerProcess(req, this.ConnectionOnline);
			// 	}
			// 	catch (Exception e) {
			// 		if (
			// 			e is NoNetworkException ||
			// 			e is System.Net.Sockets.SocketException || 
			// 			e is System.Net.WebException
			// 		) {
			// 			if (this.ConnectionOnline) {
			// 				this.ConnectionOnline = false;
			// 				loop = true;
			// 			}
			// 			else {
			// 				Log.Warning("Should not get OfflineException when ConnectionMode != Online");
			// 			}
			// 		} else {
			// 			throw e;
			// 		}
			// 	}
			// } while (loop);
			//
			return result!;
		}

		private async Task processBelongsTo(object model, Type modelType, PropertyInfo propertyInfo, BelongsToAttribute attribute, int? freshnessSeconds = null, bool? hold = null) {
			var foreignModelType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
			var idProperty = modelType.GetProperty(attribute.IdProperty);
			var id = idProperty.GetValue(model);
			if (id == null)
				return;

			var requestOp = new RequestOp(
				NowMs,
				foreignModelType,
				RequestVerb.Get,
				id,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				hold: hold
			);
			var opResponse = await InnerProcessWithFallback(requestOp);
			await StoreInPreviousCaches(opResponse);
			await SetModelProperty(model, propertyInfo, opResponse.Result);
		}

		private async Task<OpResponse> ProcessGetCollection(RequestOp requestOp, bool connectionOnline) {
			object? value;
			ICascadeCache? layerFound = null;
			OpResponse? opResponse = null;

			RequestOp cacheReq;
			if (connectionOnline || requestOp.FreshnessSeconds < 0)
				cacheReq = requestOp;
			else
				cacheReq = requestOp.CloneWith(freshnessSeconds: RequestOp.FRESHNESS_ANY);
			
			// try each cache layer
			foreach (var layer in CacheLayers) {
				var res = await layer.Fetch(cacheReq);
				if (res.Connected && res.Exists) {
					layerFound = layer;
					opResponse = res;
					break;
				}
			}

			if (opResponse == null) {
				return OpResponse.None(requestOp, NowMs);
			}
			return opResponse!;
		}


		private async Task<OpResponse> ProcessGetOrQuery(RequestOp requestOp, bool connectionOnline) {
			OpResponse? opResponse = null;
			OpResponse? cacheResponse = null;

			if (requestOp.FreshnessSeconds >= 0) {
				// RequestOp cacheReq;
				// if (connectionOnline || requestOp.FreshnessSeconds < 0)
				// 	cacheReq = requestOp;
				// else
				// 	cacheReq = requestOp.CloneWith(freshnessSeconds: RequestOp.FRESHNESS_ANY);

				var layers = CacheLayers.ToArray();
				for (var i = 0; i < layers.Length; i++) {
					var layer = layers[i];
					var res = await layer.Fetch(requestOp);
					if (res.Connected && res.Exists) {
						res.LayerIndex = i;
						var arrivedAt = res.ArrivedAtMs == null ? "" : CascadeUtils.fromUnixMilliseconds((long)res.ArrivedAtMs).ToLocalTime().ToLongTimeString();
						if (requestOp.Verb == RequestVerb.Get)
							Log.Debug($"Cascade {requestOp.Verb} Returning: {requestOp.Type.Name} {requestOp.Id} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
						else if (requestOp.Verb == RequestVerb.Query)
							Log.Debug($"Cascade {requestOp.Verb} Returning: {requestOp.Type.Name} {requestOp.Key} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
						// layerFound = layer;
						cacheResponse = res;
						break;
					}
				}
			}

			if (
				(cacheResponse?.Exists == true) && // in cache
				(
					!connectionOnline ||		// offline
					(requestOp.FreshnessSeconds == RequestOp.FRESHNESS_ANY) ||	// freshness not required 
					((requestOp.FreshnessSeconds>0) && ((NowMs - cacheResponse.ArrivedAtMs) <= requestOp.FreshnessSeconds*1000))
				)
			) {
				opResponse = cacheResponse;	// in cache and offline or meets freshness
			} else {
				if (!connectionOnline)		// mustn't be in cache and we're offline, so not much we can do
					throw new DataNotAvailableOffline();
				OpResponse originResponse;
				try {
					originResponse = await Origin.ProcessRequest(requestOp, connectionOnline);
				} catch (Exception e) {
					if (e is NoNetworkException)
						originResponse = OpResponse.ConnectionFailure(requestOp,NowMs,Origin.GetType().Name);
					else
						throw;
				}
				originResponse.LayerIndex = -1;
				if (originResponse.Connected) {
					opResponse = originResponse;
				} else {
					if ( // online but connection failure and meets fallback freshness
					    cacheResponse?.Exists==true &&
					    requestOp.FallbackFreshnessSeconds != null &&
					    (requestOp.FallbackFreshnessSeconds == RequestOp.FRESHNESS_ANY || ((NowMs - cacheResponse.ArrivedAtMs) <= requestOp.FallbackFreshnessSeconds * 1000))
					) {
						Debug.WriteLine("Cascade fallback to cached value");
						opResponse = cacheResponse;
					} else {
						throw new DataNotAvailableOffline();
					}
				}
			}
			
			if (requestOp.Hold && opResponse.LayerIndex!=0 /* We don't want to slow down the first cache layer (probably memory) by setting Hold */ && !(opResponse?.ResultIsEmpty() ?? false)) {
				if (requestOp.Verb == RequestVerb.Get) {
					Hold(requestOp.Type, requestOp.Id);
				} else if (requestOp.Verb == RequestVerb.Query) {
					var isIdResults = opResponse.IsIdResults;
					var type = requestOp.Type ?? (isIdResults ? null : opResponse.FirstResult?.GetType());
					if (type != null) {
						foreach (var r in opResponse.Results) {
							var id = isIdResults ? r : CascadeTypeUtils.GetCascadeId(r);
							if (id != null)
								Hold(type, id);
						}
						HoldCollection(type,requestOp.Key);
					}
				}
			}
			return opResponse!;
		}
		
		private void SetResultsImmutable(OpResponse opResponse) {
			if (opResponse.ResultIsEmpty())
				return;
			foreach (var result in opResponse.Results) {
				if (result is SuperModel superModel)
					superModel.__mutable = false;
			}
		}

		private async Task<OpResponse> ProcessCreate(RequestOp req, bool connectionOnline) {
			OpResponse opResponse;
			if (connectionOnline) {
				opResponse = await Origin.ProcessRequest(req, connectionOnline);
				opResponse.LayerIndex = -1;
			} else {
				var result = OfflineUtils.CreateOffline((SuperModel)req.Value, () => Origin.NewGuid());
				var reqWithId = req.CloneWith(id: CascadeTypeUtils.GetCascadeId(result), value: result);
				await AddPendingChange(reqWithId);
				opResponse = new OpResponse(
					req,
					NowMs,
					false,
					true,
					NowMs,
					result
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
			}
			return opResponse!;
		}

		private async Task<OpResponse> ProcessReplace(RequestOp req, bool connectionOnline) {
			OpResponse opResponse;
			if (connectionOnline) {
				opResponse = await Origin.ProcessRequest(req, connectionOnline);
				opResponse.LayerIndex = -1;
			} else {
				var result = req.Value; 
				await AddPendingChange(req);
				opResponse = new OpResponse(
					req,
					NowMs,
					false,
					true,
					NowMs,
					result
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
			}
			return opResponse!;
		}

		private async Task<OpResponse> ProcessUpdate(RequestOp req, bool connectionOnline) {
			OpResponse opResponse;
			if (connectionOnline) {
				opResponse = await Origin.ProcessRequest(req, connectionOnline);
				opResponse.LayerIndex = -1;
			} else {
				var result = ((SuperModel)req.Extra).Clone((IDictionary<string, object>)req.Value); 
				await AddPendingChange(req);
				opResponse = new OpResponse(
					req,
					NowMs,
					false,
					true,
					NowMs,
					result
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
			}
			return opResponse!;
		}

		private async Task<OpResponse> ProcessDestroy(RequestOp req, bool connectionOnline) {
			OpResponse opResponse;
			if (connectionOnline) {
				opResponse = await Origin.ProcessRequest(req, connectionOnline);
				opResponse.LayerIndex = -1;
			} else {
				await AddPendingChange(req);
				opResponse = new OpResponse(
					req,
					NowMs,
					false,
					false,
					NowMs,
					null
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
				return opResponse;
			}
			return opResponse!;
		}

		private async Task<OpResponse> ProcessExecute(RequestOp req, bool connectionOnline) {
			if (!connectionOnline) {
				await AddPendingChange(req);
			}
			OpResponse opResponse = await Origin.ProcessRequest(req,connectionOnline);
			opResponse.LayerIndex = connectionOnline ? -1 : -2;
			return opResponse!;
		}

		public async Task<IEnumerable<OpResponse>> GetModelsForIds(
			Type type,
			IEnumerable iids,
			int? freshnessSeconds = null,
			int? fallbackFreshnessSeconds = null,
			bool? hold = null
		) {
			const int MaxParallelRequests = 10;
			var ids = iids.Cast<object>().ToImmutableArray();
			Log.Debug("BEGIN GetModelsForIds");
			OpResponse[] allResponses = new OpResponse[ids.Count()];
			for (var i = 0; i < ids.Count(); i += MaxParallelRequests) {
				var someIds = ids.Skip(i).Take(MaxParallelRequests).ToImmutableArray();

				var tasks = someIds.Select(id => {
					return ProcessRequest( // map each id to a get request and process it
						new RequestOp(
							NowMs,
							type,
							RequestVerb.Get,
							id,
							freshnessSeconds: freshnessSeconds,
							fallbackFreshnessSeconds: freshnessSeconds,
							hold: hold
						)
					);
				}).ToImmutableArray();
				var someGetResponses = await Task.WhenAll(tasks); // wait on all requests in parallel
				for (int j = 0; j < someGetResponses.Length; j++) // fill allResponses array from responses
					allResponses[i + j] = someGetResponses[j];
			}

			Log.Debug("END GetModelsForIds");
			return allResponses.ToImmutableArray();
		}

		private async Task StoreInPreviousCaches(OpResponse opResponse) {
			await errorControl.FilterGuard(async () => {
				ICascadeCache? layerFound = null;
				var layers = CacheLayers.ToArray();
				if (opResponse.LayerIndex>=0 && opResponse.LayerIndex<layers.Length)
					layerFound = layers[opResponse.LayerIndex];
				var beforeLayer = layerFound == null;
				foreach (var layer in CacheLayers.Reverse()) {
					if (!beforeLayer && layer == layerFound)
						beforeLayer = true;
					if (!beforeLayer)
						continue;
					if (opResponse.RequestOp.Verb == RequestVerb.GetCollection)
						await layer.StoreCollection(opResponse.RequestOp.Type, opResponse.RequestOp.Key!, opResponse.Results, opResponse.TimeMs);
					else
						await layer.Store(opResponse);
				}
			});
		}

		private Task SetModelProperty(object model, PropertyInfo propertyInfo, object? value) {
			return cascadePlatform.InvokeOnMainThreadNow(() => {
				if (model is SuperModel superModel) {
					superModel.__mutateWith(model => propertyInfo.SetValue(model, value));
				}
				else {
					propertyInfo.SetValue(model, value);
				}
			});
		}

		protected async Task SetModelCollectionProperty(object target, PropertyInfo propertyInfo, object value) {
			Type propertyType = propertyInfo.PropertyType;
			var nonNullableTargetType = CascadeTypeUtils.DeNullType(propertyType);
			var isEnumerable = CascadeTypeUtils.IsEnumerableType(nonNullableTargetType);
			if (!isEnumerable)
				throw new ArgumentException("Property type should be IEnumerable");
			var singularType = isEnumerable ? CascadeTypeUtils.InnerType(nonNullableTargetType)! : nonNullableTargetType;
			if (CascadeTypeUtils.IsNullableType(singularType))
				throw new ArgumentException("Singular type cannot be nullable");

			var valueType = value.GetType();
			if (!CascadeTypeUtils.IsEnumerableType(valueType))
				throw new ArgumentException("Value must be IEnumerable");
			var newValue = value;
			if (!propertyType.IsAssignableFrom(valueType)) {
				var valueSingularType = CascadeTypeUtils.GetSingularType(valueType);
				if (valueSingularType != singularType) {
					var valueSingularIsUntyped = valueSingularType == typeof(object);
					var isAssignable = singularType.IsAssignableFrom(valueSingularType);
					if (isAssignable || valueSingularIsUntyped) {
						newValue = CascadeTypeUtils.ImmutableArrayOfType(singularType, (IEnumerable)value);
					}
					else {
						throw new ArgumentException($"Singular type of value {valueType.FullName} must match property singular type {singularType.FullName}");
					}
				}
			}

			await SetModelProperty(target, propertyInfo, newValue);
		}

		public async Task EnsureAuthenticated(Type? type = null) {
			await Origin.EnsureAuthenticated(type);
		}
		
		public async Task ClearLayer(int index, bool exceptHeld=true) {
			await CacheLayers.ToArray()[index].ClearAll(exceptHeld);
		}
		
		public async Task ClearLayers(bool exceptHeld = true, DateTime? olderThan = null) {
			foreach (var layer in CacheLayers) {
				await layer.ClearAll(exceptHeld,olderThan);
			}
		}

		private string FindNumericFileDoesntExist(string folder, long number, string format, string suffix) {
			string filePath;
			var i = 0;
			do {
				filePath = Path.Combine(folder, (number+i).ToString(format) + suffix);
				i++;
			} while (File.Exists(filePath));
			return filePath;
		}

		public string? SerializeRequestOp(RequestOp op) {
			var dic = new Dictionary<string, object>();
			dic[nameof(op.Verb)] = op.Verb.ToString();
			dic[nameof(op.Type)] = op.Type.FullName;
			dic[nameof(op.Id)] = op.Id;
			dic[nameof(op.TimeMs)] = op.TimeMs;
			dic[nameof(op.Value)] = serialization.SerializeToNode(op.Value);
			if (op.Criteria!=null)
				dic[nameof(op.Criteria)] = serialization.SerializeToNode(op.Criteria);
			if (op.Extra!=null)
				dic[nameof(op.Extra)] = serialization.SerializeToNode(op.Extra);
			dic[nameof(op.Value)] = serialization.SerializeToNode(op.Value);
			var str = serialization.Serialize(dic);
			return str;
		}

		public RequestOp DeserializeRequestOp(string? s) {
			Log.Debug("DeserializeRequestOp: "+s);
			var el = serialization.DeserializeElement(s);
			var typeName = el.GetProperty(nameof(RequestOp.Type)).GetString();
			var type = Origin.LookupModelType(typeName); // Type.GetType(typeName,true);
			Enum.TryParse<RequestVerb>(el.GetProperty(nameof(RequestOp.Verb)).GetString(), out var verb);
			object id = null;
			var idProperty = el.GetProperty(nameof(RequestOp.Id));
			var idType = CascadeTypeUtils.GetCascadeIdType(type);
			if (idProperty.ValueKind == JsonValueKind.Number)
				id = CascadeTypeUtils.ConvertTo(idType, idProperty.GetInt64());
			else if (idProperty.ValueKind == JsonValueKind.String)
				id = CascadeTypeUtils.ConvertTo(idType, idProperty.GetString());
			else if (verb == RequestVerb.Create || verb == RequestVerb.Execute)
				id = null;
			else
				throw new TypeAccessException("Failed to interpret id value in correct type");
			object value = null,criteria = null;
			if (verb == RequestVerb.Update) {
				value = serialization.DeserializeDictionaryOfNormalTypes(el.GetProperty(nameof(RequestOp.Value)));
			} else if (verb == RequestVerb.Execute) {
				value = el.GetProperty(nameof(RequestOp.Value)).ToString();
				criteria = serialization.DeserializeDictionaryOfNormalTypes(el.GetProperty(nameof(RequestOp.Criteria)));
			} else {
				value = serialization.DeserializeType(type, el.GetProperty(nameof(RequestOp.Value)));
			}
			return new RequestOp(
				el.GetProperty(nameof(RequestOp.TimeMs)).GetInt64(),
				type,
				verb,
				id,
				value: value,
				populate: null,
				freshnessSeconds: null,
				populateFreshnessSeconds: null,
				criteria: criteria,
				key: null,
				extra: null
			);
		}
		
		public async Task<string> AddPendingChange(RequestOp op) {
			var typeStr = op.Type.Name;
			var folder = Config.PendingChangesPath; //Path.Combine(Config.PendingChangesPath, typeStr);
			if (!Directory.Exists(folder))
				Directory.CreateDirectory(folder);
			//var filePath = FindNumericFileDoesntExist(folder, op.TimeMs, "D15", $"__{typeStr}__{op.IdAsString}.json");
			var content = SerializeRequestOp(op);
			string? filePath=null;
			await CascadeUtils.EnsureFileOperation(async () => {
				filePath = FindNumericFileDoesntExist(folder, op.TimeMs, "D15", ".json");
				using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true)) {
					using (var writer = new StreamWriter(stream)) {
						await writer.WriteAsync(content); //.ConfigureAwait(false);
					}
				}
			});
			return filePath!;
		}

		public IEnumerable<string> GetChangesPendingList() {
			if (!Directory.Exists(Config.PendingChangesPath))
				return new string[] {};
			var items = Directory.GetFiles(Config.PendingChangesPath);
			return items.Select(Path.GetFileName).ToImmutableArray().Sort();
		}
		
		private async Task RemoveChangePending(string filename) {
			var filepath = Path.Combine(Config.PendingChangesPath, filename);
			CascadeUtils.EnsureFileOperationSync(() => {
				if (File.Exists(filepath))
					File.Delete(filepath);
			});
		}
		
		public async Task<IEnumerable<Tuple<string,RequestOp>>> GetChangesPending() {
			var changes = new List<Tuple<string,RequestOp>>();
			var list = GetChangesPendingList();
			foreach (var filename in list) {
				var content = CascadeUtils.LoadFileAsString(Path.Combine(Config.PendingChangesPath, filename));
				changes.Add(new Tuple<string, RequestOp>(filename,DeserializeRequestOp(content)));
			}
			return changes;
		}

		public bool HasChangesPending() {
			return GetChangesPendingList().Any();
		}
		
		public async Task ClearChangesPending() {
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(Config.PendingChangesPath))
					Directory.Delete(Config.PendingChangesPath, true);
			});
			RaisePropertyChanged(nameof(PendingCount));
		}
		
		public async Task UploadChangesPending(Action<string>? progressMessage = null,Action<int>? progressCount = null) {
			progressMessage?.Invoke("Load Changes");
			var changes = (await GetChangesPending()).ToImmutableArray();
			progressCount?.Invoke(changes.Length);
			for (var index = 0; index < changes.Length; index++) {
				var pair = changes[index];
				progressMessage?.Invoke($"Uploading Changes");
				await InnerProcess(pair.Item2, true);
				await RemoveChangePending(pair.Item1);
				progressCount?.Invoke(changes.Length - index - 1);
			}
			// Update Home Screen Pending Count after Uploading Changes
			RaisePropertyChanged(nameof(PendingCount));
			progressMessage?.Invoke("Changes Uploaded.");
		}
		
		public IEnumerable<Type> ListModelTypes() {
			return Origin.ListModelTypes();
		}

		#region Meta
		// The "meta" feature offers key/value persistent storage 

		public string MetaResolvePath(string path) {
			if (path != null && path.Contains(".."))
				throw new ArgumentException("Path cannot contain ..");
			path = path!.TrimStart(new[] { '/', '\\' });
			path = Path.Combine(Config.MetaPath, path);
			return path;
		}
		
		// Sets the key to a value
		public void MetaSet(
			string path,	// forward-slash relative path to a document (the key)
			string value	// a string or null (the value)
		) {
			path = MetaResolvePath(path);
			var folder = Path.GetDirectoryName(path)!;
			CascadeUtils.EnsureFileOperationSync(() => {
				if (!Directory.Exists(folder) && value!=null)
					Directory.CreateDirectory(folder);
				if (value == null) {
					if (File.Exists(path)) {
						Log.Debug($"MetaSet file {path}");
						File.Delete(path);
					}
				} else {
					File.WriteAllText(path, value);	
				}
			});
		}

		// Gets the value of a key
		public string? MetaGet(
			string path
		) {
			path = MetaResolvePath(path);
			return CascadeUtils.EnsureFileOperationSync(() => {
				if (!File.Exists(path))
					return null;
				return File.ReadAllText(path);
			});
		}

		public bool MetaExists(
			string path
		) {
			path = MetaResolvePath(path);
			return File.Exists(path);
		}

		public IEnumerable<string> MetaList(string path, bool recursive = false, bool sort = true) {
			path = MetaResolvePath(path);
			if (!Directory.Exists(path))
				return ImmutableArray<string>.Empty;
			if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
				path += Path.DirectorySeparatorChar;
			
			var items = Directory.GetFiles(path,"*.*",recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
			var result = items.Select(p => p.Substring(path.Length)).ToImmutableArray();
			if (sort)
				result = result.Sort();
			return result;
		}
		
		public void MetaClearPath(string path, DateTime? olderThan=null, bool recursive = false) {
			if (String.IsNullOrWhiteSpace(path))
				throw new ArgumentException("path cannot be empty");
			var folderPath = MetaResolvePath(path);
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(folderPath)) {
					if (olderThan == null && recursive) {
						Log.Debug($"MetaClearPath folder {folderPath}");
						Directory.Delete(folderPath, true);
					}
					else {
						var files = Directory.GetFiles(folderPath, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
						foreach (var file in files) {
							var fileTime = File.GetLastWriteTimeUtc(file);
							if (olderThan == null) {
								Log.Debug($"MetaClearPath file {file}");
								File.Delete(file);
							}
							else {
								if (fileTime.IsLessThanOrEqual(olderThan.Value)) {
									Log.Debug($"MetaClearPath file {file}");
									File.Delete(file);
								}
							}
						}
					}
				}
			});
		}

		public void MetaClearAll() {
			var path = MetaResolvePath("");
			CascadeUtils.EnsureFileOperationSync(() => {
				if (Directory.Exists(path)) {
					Log.Debug($"MetaClearAll folder {path}");
					Directory.Delete(path, true);
				}
			});
		}

		#endregion
		
		#region Holding

		public const string HOLD = "Hold";
		
		private string HoldModelPath(Type modelType) {
			return Path.Combine(HOLD,"Model", modelType.FullName);
		}
		
		private string HoldModelPath(Type modelType,object id) {
			if (id is null or "" or 0)
				throw new ArgumentException("Id Cannot be null or empty string or 0");
			return Path.Combine(HOLD,"Model", modelType.FullName, id.ToString());
		}

		public void Hold<Model>(object id) {
			Hold(typeof(Model), id);
		}

		public void Hold(object model) {
			Hold(model.GetType(), CascadeTypeUtils.GetCascadeId(model));
		}
		
		public void Hold(Type modelType, object id) {
			Log.Debug($"CascadeDataLayer Hold {modelType.FullName} id {id}");
			var path = HoldModelPath(modelType,id);
			// if (MetaExists(path))
			// 	return;
			MetaSet(path, String.Empty);
		}

		// public void Hold<Model>(IEnumerable<Model> models) {
		// 	
		// 	
		// 	
		// }
		
		public bool IsHeld<Model>(object id) {
			return MetaExists(HoldModelPath(typeof(Model), id));
		}

		public void Unhold<Model>(object id) {
			Log.Debug($"CascadeDataLayer Unhold {nameof(Model)} id {id}");
			MetaSet(HoldModelPath(typeof(Model),id),null);
		}
		
		public IEnumerable<object> ListHeldIds<Model>() {
			return ListHeldIds(typeof(Model)); // var modelPath = HoldModelPath<Model>();
		}
		
		public IEnumerable<object> ListHeldIds(Type modelType) {
			var path = HoldModelPath(modelType);
			var idType = CascadeTypeUtils.GetCascadeIdType(modelType);

			var items = MetaList(path);
			
			return items
				.Select<string, object>(name =>
				{
					if (idType == typeof(string))
						return name;
					else
						return CascadeTypeUtils.ConvertTo(idType, name)!;
				})
				.ToImmutableArray()
				.Sort();
		}

		private string HoldCollectionPath(Type modelType) {
			return Path.Combine(HOLD, "Collection", modelType.FullName);
		}
		
		private string HoldCollectionPath(Type modelType,string key) {
			if (key is null or "")
				throw new ArgumentException("name Cannot be null or empty string");
			return Path.Combine(HOLD, "Collection", modelType.FullName, key);
		}
		
		public void HoldCollection<Model>(string name) {
			HoldCollection(typeof(Model),name);
		}

		public void HoldCollection(Type modelType, string name) {
			Log.Debug($"CascadeDataLayer HoldCollection {modelType.FullName} collection {name}");
			var path = HoldCollectionPath(modelType,name);
			// if (MetaExists(path))
			// 	return;
			MetaSet(path, String.Empty);
		}
		
		public void UnholdCollection<Model>(string name) {
			Log.Debug($"CascadeDataLayer UnholdCollection {nameof(Model)} collection {name}");
			var path = HoldCollectionPath(typeof(Model),name);
			if (MetaExists(path))
				MetaSet(path, null);
		}
		
		public bool IsCollectionHeld<Model>(string name) {
			return MetaExists(HoldCollectionPath(typeof(Model),name));
		}
		
		public IEnumerable<object> ListHeldCollections(Type modelType) {
			return MetaList(HoldCollectionPath(modelType));
		}

		public void UnholdAll(Type modelType, DateTime? olderThan=null) {
			MetaClearPath(HoldCollectionPath(modelType),olderThan);
			MetaClearPath(HoldModelPath(modelType),olderThan);
		}
		
		public void UnholdAll(DateTime? olderThan=null) {
			MetaClearPath(HOLD, olderThan, true);
		}
		
		public void UnholdAll() {
			MetaClearAll();
		}
		
		#endregion

	}
}	
