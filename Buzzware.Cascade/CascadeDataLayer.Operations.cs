using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Easy.Common.Extensions;
using Serilog;
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
			var requestOp = new RequestOp(
				sequenceBeganMs ?? NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,  
				hold: hold, 
				criteria: new Dictionary<string, object?>() { [attribute.ForeignIdProperty] = modelId }, 
				key: key
			);
			var opResponse = await InnerProcess(requestOp, this.ConnectionOnline);
			await StoreInPreviousCaches(opResponse);
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
			var requestOp = new RequestOp(
				sequenceBeganMs ?? NowMs,
				foreignType,
				RequestVerb.Query,
				null,
				value: null,
				freshnessSeconds: freshnessSeconds ?? Config.DefaultFreshnessSeconds,
				fallbackFreshnessSeconds: fallbackFreshnessSeconds ?? Config.DefaultFallbackFreshnessSeconds,
				hold: hold, 
				criteria: new Dictionary<string, object?>() { [attribute.ForeignIdProperty] = modelId }, 
				key: key
			);
			var opResponse = await InnerProcess(requestOp, this.ConnectionOnline);
			await StoreInPreviousCaches(opResponse);
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
					var modelResponses = await GetModelsForIds(
						requestOp.Type,
						opResponse.ResultIds,
						requestOp.FreshnessSeconds,
						fallbackFreshnessSeconds: requestOp.FallbackFreshnessSeconds,
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
		/// Transfer any associations that are set on the requestOp to the opResponse
		/// </summary>
		/// <param name="requestOp"></param>
		/// <param name="opResponse"></param>
		/// <exception cref="ArgumentException"></exception>
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
				if (changes?.TryGetValue(pi.Name, out var change) ?? false)
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
							await Populate(outgoingModel, pi.Name, FRESHNESS_ANY);
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
			if (Log.Logger.IsEnabled(LogEventLevel.Debug)) {
				Log.Debug("ProcessRequest before criteria");
				var criteria = serialization.Serialize(requestOp.Criteria);
				Log.Debug("ProcessRequest RequestOp: {@Verb} {@Id} {@Type} {@Key} {@Freshness} {@Criteria}",
					requestOp.Verb, requestOp.Id, requestOp.Type, requestOp.Key, requestOp.FreshnessSeconds, criteria);
				Log.Debug("ProcessRequest after criteria");
			}
			
			var opResponse = await InnerProcess(requestOp, this.ConnectionOnline);
			
			await StoreInPreviousCaches(opResponse); // just store ResultIds
			
			if (Log.Logger.IsEnabled(LogEventLevel.Debug))
				Log.Debug("ProcessRequest OpResponse: Exists: {@Exists}", opResponse.Exists);
			var isBlobVerb = requestOp.Verb == RequestVerb.BlobGet || requestOp.Verb == RequestVerb.BlobPut;
			if (Log.Logger.IsEnabled(LogEventLevel.Verbose) && !isBlobVerb)
				Log.Verbose("ProcessRequest OpResponse: Result: {@Result}",opResponse.Result);
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
		/// Processes a request to retrieve a collection of ids based on the given request operation and 
		/// current connection state. Attempts to fetch data from cache layers and provides fallback if necessary.
		/// </summary>
		/// <param name="requestOp">The operation request detailing the type of operation and data parameters.</param>
		/// <param name="connectionOnline">A boolean indicating if the connection is online or not.</param>
		/// <returns>OpResponse object containing the operation response data.</returns>
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
			if (requestOp.FreshnessSeconds > RequestOp.FRESHNESS_INSIST) {
				RequestOp cacheReq;
				cacheReq = requestOp.CloneWith(freshnessSeconds: FRESHNESS_ANY);										

				var layers = CacheLayers.ToArray();
				for (var i = 0; i < layers.Length; i++) {
					var layer = layers[i];
					var res = await layer.Fetch(cacheReq);
					if (res.Exists) {
						res.LayerIndex = i;
						var arrivedAt = res.ArrivedAtMs == null ? "" : CascadeUtils.fromUnixMilliseconds((long)res.ArrivedAtMs).ToLocalTime().ToLongTimeString();
						if (requestOp.Verb == RequestVerb.Get)
							Log.Debug($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Type.Name} {requestOp.Id} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
						else if (requestOp.Verb == RequestVerb.Query)
							Log.Debug($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Type.Name} {requestOp.Key} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
						else if (requestOp.Verb == RequestVerb.BlobGet)
							Log.Debug($"Buzzware.Cascade {requestOp.Verb} Returning: {requestOp.Id} (layer {res.SourceName} freshness {requestOp.FreshnessSeconds} ArrivedAtMs {arrivedAt})");
						
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
					((requestOp.FreshnessSeconds>0) && (cacheResponse.ArrivedAtMs >= requestOp.FreshAfterMs))
				)
			) {
				opResponse = cacheResponse;	// in cache and offline or meets freshness
			} else {
				if (!connectionOnline)		// mustn't be in cache and we're offline, so not much we can do
					throw new DataNotAvailableOffline();
				OpResponse originResponse;
				bool connected = false;
				try {
					if (requestOp.Verb == RequestVerb.BlobGet && cacheResponse?.ETag != null) {
						requestOp = requestOp.CloneWith(eTag: cacheResponse.ETag);
					}
					originResponse = await Origin.ProcessRequest(requestOp, connectionOnline);
					connected = connectionOnline;
				} catch (Exception e) {
					if (e is NoNetworkException)
						originResponse = OpResponse.ConnectionFailure(requestOp,requestOp.TimeMs,Origin.GetType().Name);
					else
						throw;
				}
				originResponse.LayerIndex = -1;
				if (connected) {
					if ( // originResponse indicates matching eTag, so return cacheResponse
					    requestOp.Verb == RequestVerb.BlobGet &&
					    originResponse.Result == null &&
					    cacheResponse!.Exists &&
					    originResponse.ETag != null && originResponse.ETag == cacheResponse!.ETag
					) {
						opResponse = cacheResponse.withChanges(arrivedAtMs: originResponse.ArrivedAtMs ?? NowMs);
						await NotifyCacheBlobIsFresh(opResponse.RequestOp.IdAsString!,(long)opResponse.ArrivedAtMs!);
					} else {
						opResponse = originResponse;
					}
				} else {
					if ( // online but connection failure and meets fallback freshness
					    cacheResponse?.Exists==true &&
					    //requestOp.FallbackFreshnessSeconds != null &&
					    (requestOp.FallbackFreshnessSeconds == RequestOp.FRESHNESS_ANY || ((requestOp.TimeMs - cacheResponse.ArrivedAtMs) <= requestOp.FallbackFreshnessSeconds * 1000))
					) {
						Debug.WriteLine("Buzzware.Cascade fallback to cached value");
						opResponse = cacheResponse;
					} else {
						throw new DataNotAvailableOffline();
					}
				}
			}
			
			if (requestOp.Hold && opResponse.LayerIndex!=0 /* We don't want to slow down the first cache layer (probably memory) by setting Hold */ && !(opResponse?.ResultIsEmpty() ?? false)) {
				if (requestOp.Verb == RequestVerb.Get) {
					Hold(requestOp.Type, requestOp.Id);
				} else if (requestOp.Verb == RequestVerb.BlobGet) {
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
		/// Processes a replace operation request, handling both online and offline scenarios. Replaces an 
		/// existing instance of the data model and manages pending changes if offline.
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
		/// existing instance of the data model and adds a pending changes if offline.
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
