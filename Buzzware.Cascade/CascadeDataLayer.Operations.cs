using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buzzware.Cascade.Utilities;
using Buzzware.StandardExceptions;
using Easy.Common.Extensions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Buzzware.Cascade {

	/// <summary>
	/// Methods for internal core data operations (Get/Query/Create/Update/Destroy) including processing relationships
	/// such as HasMany, HasOne, BelongsTo, and FromBlob on a data model. It interacts with 
	/// various cache layers and origin layers to fulfill data requests and ensure data consistency.
	/// </summary>
	public partial class CascadeDataLayer {

		/// <summary>
		/// Processes the HasMany relationship by retrieving and setting a collection of related foreign models 
		/// in a parent model. Checks cache and origin layers to fulfill the request based on configured parameters.
		/// </summary>
		/// <param name="model">The parent model containing the HasMany relationship.</param>
		/// <param name="modelType">The type of the parent model.</param>
		/// <param name="propertyInfo">The property information for the HasMany relationship.</param>
		/// <param name="attribute">The HasManyAttribute containing metadata for the relationship.</param>
		/// <param name="freshnessSeconds">Optional freshness requirement in seconds.</param>
		/// <param name="fallbackFreshnessSeconds">Optional fallback freshness requirement in seconds.</param>
		/// <param name="hold">Optional parameter to hold the data in memory for quick access.</param>
		/// <param name="sequenceBeganMs">Optional timestamp in milliseconds for when the request is made.</param>
		private async Task processHasMany(
			SuperModel model, 
			Type modelType, 
			CascadePropertyInfo propertyInfo, 
			HasManyAttribute attribute, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool? hold = null,
			long? sequenceBeganMs = null			
		) {
			// var propertyType = propertyInfo.NotNullType;
			// var isEnumerable = (propertyType?.Implements<IEnumerable>() ?? false) && propertyType != typeof(string);
			// var foreignType = isEnumerable ? CascadeTypeUtils.InnerType(propertyType!) : null;
			// foreignType = foreignType != null ? CascadeTypeUtils.DeNullType(foreignType) : null;
			var foreignType = propertyInfo.InnerNotNullType!;
			if (foreignType == null)
				throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");
			
			object modelId = CascadeTypeUtils.GetCascadeId(model);
			var key = CascadeUtils.WhereCollectionKey(foreignType.Name, attribute.ForeignIdProperty, modelId.ToString());
			freshnessSeconds = Config.GetFreshnessSeconds(foreignType,freshnessSeconds);
			fallbackFreshnessSeconds = Math.Max((int)freshnessSeconds,Config.GetFallbackFreshnessSeconds(foreignType));
			var requestOp = new RequestOp(
				sequenceBeganMs ?? NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds,  
				hold: hold, 
				criteria: new Dictionary<string, object?>() { [attribute.ForeignIdProperty] = modelId }, 
				key: key
			);
			var opResponse = await ProcessRequest(requestOp);       
      await SetModelCollectionProperty(model, propertyInfo, opResponse.Results);
		}

		/// <summary>
		/// Processes the HasOne relationship by retrieving and setting a single related foreign model 
		/// in a parent model. Checks cache and origin layers to fulfill the request based on configured parameters.
		/// </summary>
		/// <param name="model">The model containing the HasOne relationship.</param>
		/// <param name="modelType">The type of the model.</param>
		/// <param name="propertyInfo">The property information for the HasOne relationship.</param>
		/// <param name="attribute">The HasOneAttribute containing metadata for the relationship.</param>
		/// <param name="freshnessSeconds">Optional freshness requirement in seconds.</param>
		/// <param name="fallbackFreshnessSeconds">Optional fallback freshness requirement in seconds.</param>
		/// <param name="hold">Optional parameter to hold the data in memory for quick access.</param>
		/// <param name="sequenceBeganMs">Optional timestamp in milliseconds</param>
		private async Task processHasOne(
			SuperModel model, 
			Type modelType, 
			CascadePropertyInfo propertyInfo, 
			HasOneAttribute attribute, 
			int? freshnessSeconds = null, 
			int? fallbackFreshnessSeconds = null, 
			bool? hold = null,
			long? sequenceBeganMs = null
		) {
			if (propertyInfo.IsTypeEnumerable)
				throw new ArgumentException("HasOne property should not be of type IEnumerable");
			
			var foreignType = propertyInfo.NotNullType;
			if (foreignType == null)
				throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");
			
			object modelId = CascadeTypeUtils.GetCascadeId(model);
			var key = CascadeUtils.WhereCollectionKey(foreignType.Name, attribute.ForeignIdProperty, modelId.ToString());
			freshnessSeconds = Config.GetFreshnessSeconds(foreignType,freshnessSeconds);
			fallbackFreshnessSeconds = Math.Max((int)freshnessSeconds,Config.GetFallbackFreshnessSeconds(foreignType));
			var requestOp = new RequestOp(
				sequenceBeganMs ?? NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds,
				hold: hold, 
				criteria: new Dictionary<string, object?>() { [attribute.ForeignIdProperty] = modelId }, 
				key: key
			);
			var opResponse = await ProcessRequest(requestOp);
			await SetModelProperty(model, propertyInfo, opResponse.FirstResult);
		}

		/// <summary>
		/// Inner processing mechanism that handles different types of request operations (e.g., Get, Query, Create,
		/// Replace, etc.) and coordinates fetching from cache or origin layers, and managing populated results.
		/// </summary>
		/// <param name="requestOp">The operation request detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
		private async Task<OpResponse> InnerProcess(RequestOp requestOp, bool connectionOnline) {
			OpResponse opResponse = await errorControl.FilterGuard(() => {
				switch (requestOp.Verb) {
					case RequestVerb.Get:
					case RequestVerb.Query:
					case RequestVerb.BlobGet:
					case RequestVerb.BlobGetFilePath:
						return ProcessGetOrQuery(requestOp, connectionOnline);
					case RequestVerb.GetCollection: 
						return ProcessGetCollection(requestOp, connectionOnline);
					case RequestVerb.Create:
						return ProcessCreate(requestOp, connectionOnline);
					case RequestVerb.Replace:
					case RequestVerb.BlobPut:
						return ProcessReplace(requestOp, connectionOnline);
					case RequestVerb.Update:
						return ProcessUpdate(requestOp, connectionOnline);
					case RequestVerb.Destroy:
					case RequestVerb.BlobDestroy:
						return ProcessDestroy(requestOp, connectionOnline);
					case RequestVerb.Execute:
						return ProcessExecute(requestOp, connectionOnline);
					default:
						throw new ArgumentException("Unsupported verb");
				}
			});

			var isModelRead = requestOp.Verb == RequestVerb.Get || requestOp.Verb == RequestVerb.Query;
			var transferAssociations = requestOp.Verb == RequestVerb.Update || requestOp.Verb == RequestVerb.Replace || requestOp.Verb == RequestVerb.Create;  

			if (isModelRead) {
				// Begin to handle populate operations on the response
				var populate = requestOp.Populate?.ToArray() ?? new string[] { };
				if (requestOp.Verb == RequestVerb.Query && opResponse.IsIdResults) {
					var isCachedCollection = opResponse.LayerIndex >= 0;	// if query collection came from cache, then we'll accept any cached models ie avoid server requests because we're probably offline or network failed
					var modelResponses = await GetModelResponsesForIds(
						requestOp.Type!,
						opResponse.ResultIds,
						freshnessSeconds: isCachedCollection ? RequestOp.FRESHNESS_ANY : requestOp.FreshnessSeconds,
						fallbackFreshnessSeconds: isCachedCollection ? RequestOp.FALLBACK_ANY : requestOp.FallbackFreshnessSeconds,
						hold: requestOp.Hold,
						sequenceBeganMs: requestOp.TimeMs
					);
					IEnumerable<SuperModel> models = modelResponses.Select(r => (SuperModel)r.Result).ToImmutableArray();
					if (populate.Any()) {
						await Populate(models, populate, freshnessSeconds: requestOp.PopulateFreshnessSeconds, hold: requestOp.Hold, sequenceBeganMs: requestOp.TimeMs);
					}
					opResponse = opResponse.withChanges(result: models); // modify the response with models instead of ids
				} else {
					if (populate.Any()) {
						IEnumerable<SuperModel> results = opResponse.Results.Cast<SuperModel>();
						await Populate(results, populate, freshnessSeconds: requestOp.PopulateFreshnessSeconds, hold: requestOp.Hold, sequenceBeganMs: requestOp.TimeMs);
					}
				}
				// End populate operations handling
			}
			if (transferAssociations) {
				await TransferAssociations(requestOp, opResponse);
			}

			// Set the operation response results to be immutable
			SetResultsImmutable(opResponse);
			return opResponse;
		}

		/// <summary>
		/// Transfers association property values from the incoming model on the requestOp (Value or Extra) to the
		/// outgoing result model on the opResponse. For BelongsTo/FromBlob associations the value is only copied when
		/// the id/path key property matches on both models; otherwise the association is repopulated.
		/// </summary>
		/// <param name="requestOp">The request containing the incoming model whose association values are transferred.</param>
		/// <param name="opResponse">The response whose result model receives the association values.</param>
		/// <exception cref="ArgumentException">Thrown if the incoming and outgoing model types differ.</exception>
		private async Task TransferAssociations(RequestOp requestOp, OpResponse opResponse) {
			var incomingModel = (requestOp.Value as SuperModel) ?? (requestOp.Extra as SuperModel);
			var outgoingModel = opResponse.Result as SuperModel;
			if (incomingModel==null || outgoingModel==null)
				return;
			var classInfo = FastReflection.GetClassInfo(incomingModel);
			if (outgoingModel.GetType() != classInfo.Type)
				throw new ArgumentException("Incoming model type is not the same as outgoing model type - unsupported mismatch");
			var changes = requestOp.Verb==RequestVerb.Update ? requestOp.Value as IDictionary<string, object?> : null;
			foreach (var pi in classInfo.Associationinfos.Values) {
				object? value = null;
				object? change = null;
				if (changes?.TryGetValue(pi.Name, out change) ?? false)
					value = change;
				else
					value = pi.GetValue(incomingModel);
				if (value==null)
					continue;
				switch (pi.Kind) {
					case CascadePropertyKind.HasMany:
					case CascadePropertyKind.HasOne:
						await SetModelProperty(outgoingModel, pi, value);
						break;
					case CascadePropertyKind.BelongsTo:
					case CascadePropertyKind.FromBlob:
						string? assocProperty = null;
						if (pi.Kind==CascadePropertyKind.BelongsTo)
							assocProperty = (pi.KindAttribute as BelongsToAttribute)?.IdProperty;
						else if (pi.Kind==CascadePropertyKind.FromBlob)
							assocProperty = (pi.KindAttribute as FromBlobAttribute)?.PathProperty;
						var incomingAssocKeyValue = assocProperty!=null ? classInfo.GetValue(incomingModel,assocProperty) : null;
						var outgoingAssocKeyValue = assocProperty!=null ? classInfo.GetValue(outgoingModel,assocProperty) : null;
						if (incomingAssocKeyValue == outgoingAssocKeyValue)
							await SetModelProperty(outgoingModel, pi, value);
						else
							await Populate(outgoingModel, pi.Name, RequestOp.FRESHNESS_ANY);
						break;
				}
			}
		}

		/// <summary>
		/// Coordinates the entire process of handling a data RequestOp, including logging for debug,
		/// processing with fallback options, and storing results in previous caches.
		/// </summary>
		/// <param name="requestOp">The operation request detailing the type of operation and data parameters.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
		private async Task<OpResponse> ProcessRequest(RequestOp requestOp) {

			// if (Config.GlobalMaximumFreshnessSeconds != null) {
			// 	var maximumFreshness = Config.ModelConfig[requestOp.Type!].MaximumFreshnessSeconds;
			// 	
			// 	
			// 	requestOp = requestOp.CloneWith(
			// 		freshnessSeconds: Math.Min(requestOp.FreshnessSeconds, Config.GlobalMaximumFreshnessSeconds.Value),
			// 		populateFreshnessSeconds: Math.Min(requestOp.PopulateFreshnessSeconds, Config.GlobalMaximumFreshnessSeconds.Value)
			// 	);
			// }
			
			TimingProfiler? profiler = null;
			if (Log.Logger.IsEnabled(LogEventLevel.Verbose)) {
				profiler = new TimingProfiler($"ProcessRequest {requestOp.Verb} {requestOp.Type?.Name} {requestOp.Id}");
				profiler.Start();
				var criteria = serialization.Serialize(requestOp.Criteria);
				Log.Verbose("ProcessRequest: {@Type} {@Verb} {@Id} {@Freshness} {@Fallback} {@Criteria} {@Key}",
					requestOp.Type?.Name, requestOp.Verb, requestOp.Id, requestOp.FreshnessSeconds, requestOp.FallbackFreshnessSeconds, criteria, requestOp.Key);
			}
			
			var opResponse = await InnerProcess(requestOp, this.ConnectionOnline);

			// // Convert Stream results from origin to byte[] so cache layers (which expect byte[]) can store them
			// if (opResponse.Result is Stream blobStream &&
			//     (requestOp.Verb == RequestVerb.BlobGet || requestOp.Verb == RequestVerb.BlobGetFilePath)) {
			// 	using (blobStream) {
			// 		using var ms = new MemoryStream();
			// 		await blobStream.CopyToAsync(ms);
			// 		opResponse = opResponse.withChanges(result: ms.ToArray());
			// 	}
			// }
			var resultType = opResponse.Result?.GetType();
			var attemptToStoreInCache = resultType == null ||
			                            CascadeTypeUtils.IsEnumerableType(resultType) ||
			                            CascadeTypeUtils.IsModelType(resultType) ||
			                            CascadeTypeUtils.IsEnumerableModelType(resultType) ||
			                            opResponse.ResultIsBlob();

			if (attemptToStoreInCache)
				opResponse = await StoreInPreviousCaches(opResponse); // just store ResultIds

			if (requestOp.Verb == RequestVerb.BlobGetFilePath) {
				var blobFileCache = CacheLayers.LastOrDefault(layer => layer.SupportsGetBlobAbsoluteFilePath);
				if (blobFileCache == null)
					throw new AssumptionException("No cache registered can store a blob. To use BlobGetFilePath or BlobDownload you must register a cache with SupportsGetBlobAbsoluteFilePath=true");

				if (requestOp.Id is String blobPath &&
				    opResponse.LayerIndex == -1 &&
				    opResponse.Exists &&
				    opResponse.Result != null
				   ) {
					var blobFilePath = blobFileCache.GetBlobAbsoluteFilePath(blobPath);
					opResponse = opResponse.withChanges(result: blobFilePath);
				}
			}

			var isBlobVerb = requestOp.Verb == RequestVerb.BlobGet || requestOp.Verb == RequestVerb.BlobGetFilePath || requestOp.Verb == RequestVerb.BlobPut;
			if (Log.Logger.IsEnabled(LogEventLevel.Verbose)) {
				if (profiler != null) {
					profiler.Stop();
					Log.Verbose(profiler.Report());
				}
				//if (!isBlobVerb) Log.Verbose("ProcessRequest OpResponse: Result: {@Result}",opResponse.Result);
			}

			return opResponse;
		}
		
		/// <summary>
		/// Processes data retrieval from a specified property in the model and converts the data to 
		/// the target type defined in the destination property. Uses a converter along with any given arguments.
		/// </summary>
		/// <param name="model">The model containing the FromProperty attribute.</param>
		/// <param name="modelType">The type of the model.</param>
		/// <param name="propertyInfo">The property information defines where to set the converted value.</param>
		/// <param name="attribute">The FromPropertyAttribute containing metadata for the conversion process.</param>
		private async Task processFromProperty(object model, Type modelType, CascadePropertyInfo propertyInfo, FromPropertyAttribute attribute) {
			var destinationPropertyType = propertyInfo.NotNullType;
			var sourceProperty = modelType.GetProperty(attribute.SourcePropertyName);
			var sourceValue = sourceProperty!.GetValue(model);
			var destValue = await attribute.Converter!.Convert(sourceValue, destinationPropertyType, attribute.Arguments);
			await SetModelProperty(model, propertyInfo, destValue);
		}
		
		/// <summary>
		/// Processes a request to retrieve a named collection of ids from the cache layers only - the origin is not contacted.
		/// When offline (unless freshness is negative), freshness is relaxed to FRESHNESS_ANY so any cached collection is accepted.
		/// </summary>
		/// <param name="requestOp">The operation request detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse from the first cache layer holding the collection, or an empty (None) response if no layer has it.</returns>
		private async Task<OpResponse> ProcessGetCollection(RequestOp requestOp, bool connectionOnline) {
			object? value;
			ICascadeCache? layerFound = null;
			OpResponse? opResponse = null;

			RequestOp cacheReq;
			if (connectionOnline || requestOp.FreshnessSeconds < 0)
				cacheReq = requestOp;
			else
				cacheReq = requestOp.CloneWith(freshnessSeconds: RequestOp.FRESHNESS_ANY);
			
			// Try to fetch data from each cache layer
			foreach (var layer in CacheLayers) {
				var res = await layer.Fetch(cacheReq);
				if (res.Exists) {
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
		
		/// <summary>
		/// Processes a request to retrieve or query data based on the given request operation and current 
		/// connection state. Manages data retrieval from cache layers or origin, and provides error handling.
		/// </summary>
		/// <param name="requestOp">The operation request detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
		private async Task<OpResponse> ProcessGetOrQuery(RequestOp requestOp, bool connectionOnline) {
			OpResponse? opResponse = null;
			OpResponse? cacheResponse = null;

			// If offline or freshness not zero, proceed with cache retrieval
			if (requestOp.FreshnessSeconds > RequestOp.FRESHNESS_INSIST && !(requestOp.Verb==RequestVerb.Query && requestOp.Key==null)) {
				RequestOp cacheReq;
				cacheReq = requestOp.CloneWith(freshnessSeconds: RequestOp.FRESHNESS_ANY);										

				var layers = CacheLayers.ToArray();
				for (var i = 0; i < layers.Length; i++) {
					var layer = layers[i];
					var res = await layer.Fetch(cacheReq);
					if (res.Exists) {
						res.LayerIndex = i;
						LogIf.Verbose(() => {
							var arrivedAt = res.ArrivedAtMs == null ? "" : CascadeUtils.fromUnixMilliseconds((long)res.ArrivedAtMs).ToLocalTime().ToLongTimeString();
							if (requestOp.Verb == RequestVerb.Get)
								Log.Verbose($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Type?.Name} {requestOp.Id} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
							else if (requestOp.Verb == RequestVerb.Query)
								Log.Verbose($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Type?.Name} {requestOp.Key} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
							else if (requestOp.Verb == RequestVerb.BlobGet)
								Log.Verbose($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Id} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
							else if (requestOp.Verb == RequestVerb.BlobGetFilePath)
								Log.Verbose($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Id} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
						});
						cacheResponse = res;
						break;
					}
				}
			}

			var gotCacheValue = cacheResponse?.Exists == true; 
			var withinFreshness = gotCacheValue && ((requestOp.FreshnessSeconds == RequestOp.FRESHNESS_ANY) || ((requestOp.FreshnessSeconds > RequestOp.FRESHNESS_FRESHEST) && (cacheResponse!.ArrivedAtMs >= requestOp.FreshAfterMs))); 
			var withinFallback = gotCacheValue && ((requestOp.FallbackFreshnessSeconds == RequestOp.FALLBACK_ANY) || ((requestOp.FallbackFreshnessSeconds > RequestOp.FALLBACK_NEVER) && (cacheResponse!.ArrivedAtMs >= requestOp.FallbackFreshAfterMs)));
			
			if (gotCacheValue && (connectionOnline ? withinFreshness : withinFallback)) {
				opResponse = cacheResponse;	// in cache and offline or meets freshness
			} else {
				if (!connectionOnline && requestOp.LocalOnly != true) {			// mustn't be in cache and we're offline, so not much we can do
					Log.Verbose($"ProcessGetOrQuery: DataNotAvailableOffline gotCacheValue {gotCacheValue} withinFreshness {withinFreshness} withinFallback {withinFallback}");	
					throw new DataNotAvailableOffline();
				}
				OpResponse originResponse;
				bool connected = false;
				try {
					if ((requestOp.Verb == RequestVerb.BlobGet || requestOp.Verb == RequestVerb.BlobGetFilePath) && cacheResponse?.ETag != null) {
						requestOp = requestOp.CloneWith(eTag: cacheResponse.ETag);
					}
					originResponse = await Origin.ProcessRequest(requestOp, connectionOnline);
					
					connected = connectionOnline || requestOp.LocalOnly==true;
				} catch (Exception e) {
					if (e is NoNetworkException)
						originResponse = OpResponse.ConnectionFailure(requestOp,requestOp.TimeMs,Origin.GetType().Name);
					else
						throw;
				}
				originResponse.LayerIndex = -1;
				if (connected) {
					//bool cacheResponseExists = cacheResponse?.Exists??false;

          if ( // originResponse indicates matching eTag, so return cacheResponse
					    (requestOp.Verb == RequestVerb.BlobGet || requestOp.Verb == RequestVerb.BlobGetFilePath) &&
					    originResponse.Result == null &&
					    gotCacheValue &&
					    originResponse.ETag != null && originResponse.ETag == cacheResponse!.ETag
					) {
						opResponse = cacheResponse.withChanges(arrivedAtMs: originResponse.ArrivedAtMs ?? NowMs);
						await NotifyCacheBlobIsFresh(opResponse.RequestOp.IdAsString!,(long)opResponse.ArrivedAtMs!);
					} else {
						opResponse = originResponse;
					}
				} else {
					if (gotCacheValue && withinFallback) { // online but connection failure and meets fallback freshness
						Debug.WriteLine("Buzzware.Cascade fallback to cached value");
						opResponse = cacheResponse;
					} else {
						throw new OriginAccessFailure();
					}
				}
			}
			
			if (requestOp.Hold && opResponse.LayerIndex!=0 /* We don't want to slow down the first cache layer (probably memory) by setting Hold */ && !(opResponse?.ResultIsEmpty() ?? false)) {
				if (requestOp.Verb == RequestVerb.Get) {
					Hold(requestOp.Type, requestOp.Id);
				} else if (requestOp.Verb == RequestVerb.BlobGet || requestOp.Verb == RequestVerb.BlobGetFilePath) {
					HoldBlob(((string)requestOp.Id)!);
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

		/// <summary>
		/// Notifies all cache layers that the blob at the given path is known to be fresh as at the given time.
		/// </summary>
		/// <param name="blobPath">The path identifying the blob.</param>
		/// <param name="arrivedAtMs">The time (milliseconds since 1970) at which the blob was confirmed fresh.</param>
		private async Task NotifyCacheBlobIsFresh(string blobPath, long arrivedAtMs) {
			foreach (var cacheLayer in CacheLayers) {
				await cacheLayer.NotifyBlobIsFresh(blobPath, arrivedAtMs);
			}
		}

		/// <summary>
		/// Sets the results of the operation response to be immutable. Indicates that the results should
		/// not be modified after retrieval, ensuring data integrity.
		/// </summary>
		/// <param name="opResponse">The OpResponse object containing the result data.</param>
		private void SetResultsImmutable(OpResponse opResponse) {
			if (opResponse.ResultIsEmpty() || opResponse.ResultIsBlob())
				return;
			foreach (var result in opResponse.Results) {
				if (result is SuperModel superModel)
					superModel.__mutable = false;
			}
		}

		/// <summary>
		/// Processes a create operation request, handling both online and offline scenarios. Creates a new
		/// instance of the data model and adds a pending change if offline.
		/// </summary>
		/// <param name="req">Request operation detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
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
					true,
					NowMs,
					result
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
			}
			return opResponse!;
		}

		/// <summary>
		/// Processes a replace operation request (also used for BlobPut), handling both online and offline scenarios.
		/// Replaces an existing instance of the data model and adds a pending change if offline.
		/// </summary>
		/// <param name="req">Request operation detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
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
					true,
					NowMs,
					result
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
			}
			return opResponse!;
		}

		/// <summary>
		/// Processes an update operation request, handling both online and offline scenarios. Updates an 
		/// existing instance of the data model and adds a pending change if offline.
		/// </summary>
		/// <param name="req">Request operation detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
		private async Task<OpResponse> ProcessUpdate(RequestOp req, bool connectionOnline) {
			OpResponse opResponse;
			if (connectionOnline) {
				opResponse = await Origin.ProcessRequest(req, connectionOnline);
				opResponse.LayerIndex = -1;
			} else {
				var result = ((SuperModel)req.Extra).Clone((IDictionary<string, object?>)req.Value); 
				await AddPendingChange(req);
				opResponse = new OpResponse(
					req,
					NowMs,
					true,
					NowMs,
					result
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
			}
			return opResponse!;
		}

		/// <summary>
		/// Processes a destroy operation request, handling both online and offline scenarios. Removes an 
		/// existing instance of the data model and adds a pending change if offline.
		/// </summary>
		/// <param name="req">Request operation detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
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
					NowMs,
					null
				);
				opResponse.SourceName = this.GetType().Name;
				opResponse.LayerIndex = -2;
				return opResponse;
			}
			return opResponse!;
		}

		/// <summary>
		/// Processes an execute operation request, which can perform custom operations depending on
		/// the defined specifications in the request. Supports both online and offline scenarios.
		/// </summary>
		/// <param name="req">Request operation detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
		private async Task<OpResponse> ProcessExecute(RequestOp req, bool connectionOnline) {
			if (!connectionOnline) {
				await AddPendingChange(req);
			}
			OpResponse opResponse = await Origin.ProcessRequest(req,connectionOnline);
			opResponse.LayerIndex = connectionOnline ? -1 : -2;
			return opResponse!;
		}

	}
}
