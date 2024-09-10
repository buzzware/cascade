using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Buzzware.StandardExceptions;
using Easy.Common.Extensions;
using Serilog;

namespace Buzzware.Cascade {

	/// <summary>
	/// Methods to manipulate properties and association relationships between models.
	/// </summary>
	public partial class CascadeDataLayer {

		public static ImmutableArray<Type> AssociationAttributes = ImmutableArray.Create<Type>(
			typeof(BelongsToAttribute),
			typeof(HasManyAttribute),
			typeof(HasOneAttribute)
		);

		/// <summary>
		/// Sets a property value on a model, taking into account whether the model is a SuperModel and ensure changes happen on the main thread which is necessary for any bound UI.
		/// </summary>
		/// <param name="model">The model to update.</param>
		/// <param name="pi">The metadata of the property to set.</param>
		/// <param name="value">The value to assign to the property.</param>
		private Task SetModelProperty(object model, CascadePropertyInfo pi, object? value) {
			return cascadePlatform.InvokeOnMainThreadNow(() => {
				if (model is SuperModel superModel) {
					superModel.__mutateWith(model => pi.SetValue(model, value));
				}
				else {
					pi.SetValue(model, value);
				}
			});
		}
		
		/// <summary>
		/// Sets a property value on a model, taking into account whether the model is a SuperModel and ensure changes happen on the main thread which is necessary for any bound UI.
		/// </summary>
		/// <param name="model">The model to update.</param>
		/// <param name="propertyInfo">The metadata of the property to set.</param>
		/// <param name="value">The value to assign to the property.</param>
		private Task SetModelProperty(object model, string name, object? value) {
			var pi = FastReflection.GetPropertyInfo(model.GetType(),name);
			return SetModelProperty(model, pi, value);
		}
		
		/// <summary>
		/// Sets a collection property on a model. It ensures the property is enumerable and makes necessary type conversions.
		/// </summary>
		/// <param name="target">The model on which the property is being set.</param>
		/// <param name="propertyInfo">The metadata of the property to set.</param>
		/// <param name="value">The value to set for the property. Must be an IEnumerable.</param>
		/// <exception cref="ArgumentException">Thrown when the property type or value types are incorrect.</exception>
		public async Task SetModelCollectionProperty(object target, CascadePropertyInfo propertyInfo, object value) {
			if (!propertyInfo.IsTypeEnumerable)
				throw new ArgumentException("Property type should be IEnumerable");
			
			var singularType = propertyInfo.InnerType ?? propertyInfo.NotNullType;
			if (CascadeTypeUtils.IsNullableType(singularType))
				throw new ArgumentException("Singular type cannot be nullable");

			var valueType = value.GetType();
			if (!CascadeTypeUtils.IsEnumerableType(valueType))
				throw new ArgumentException("Value must be IEnumerable");

			// Convert value to the appropriate singular type if necessary.
			var newValue = value;
			if (!propertyInfo.Type.IsAssignableFrom(valueType)) {
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
		
		/// <summary>
		/// Replaces the value of the given HasMany property with the given IEnumerable of models and updates the caches appropriately.
		/// This is needed eg. when you add models to a HasMany association.
		/// </summary>
		/// <param name="model">The model for which the property is being replaced.</param>
		/// <param name="property">The name of the HasMany property on the model.</param>
		/// <param name="models">The new models to set for the association.</param>
		/// <exception cref="ArgumentException"></exception>
		public async Task HasManyReplace(SuperModel model, string property, IEnumerable<object> models) {
			var ci = FastReflection.GetClassInfo(model);
			var pi = ci.GetPropertyInfo(property);
			if (pi?.KindAttribute is HasManyAttribute hasMany) {
				if (pi.InnerNotNullType == null)
					throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");
				object modelId = CascadeTypeUtils.GetCascadeId(model);
				await SetCacheWhereCollection(pi.InnerNotNullType, hasMany.ForeignIdProperty, modelId.ToString(), models);
				await SetModelCollectionProperty(model, pi, models);
			}
			else {
				throw new ArgumentException($"{property} is not a [HasMany] property");
			}
		}

		/// <summary>
		/// Adds an item to the HasMany association of a model, updating both the property and the caches.
		/// </summary>
		/// <param name="model">The main model to be updated.</param>
		/// <param name="property">The name of the HasMany property on the model.</param>
		/// <param name="hasManyItem">The item to add to the association.</param>
		/// <exception cref="ArgumentException">Thrown if the property is not a HasMany property.</exception>
		public async Task HasManyAddItem(SuperModel model, string property, SuperModel hasManyItem) {
			var ci = FastReflection.GetClassInfo(model);
			var pi = ci.GetPropertyInfo(property);
			if (pi?.KindAttribute is HasManyAttribute hasMany) {
				if (pi.InnerNotNullType == null)
					throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");

				var hasManyModels = ((IEnumerable)(pi!.GetValue(model) ?? Array.Empty<object>())).Cast<object>().ToList();
				hasManyModels.Add(hasManyItem);
				
				object modelId = CascadeTypeUtils.GetCascadeId(model);
				await SetCacheWhereCollection(pi.InnerNotNullType, hasMany.ForeignIdProperty, modelId.ToString(), hasManyModels.ToImmutableArray());
				await SetModelCollectionProperty(model, pi, hasManyModels);
			} else {
				throw new ArgumentException($"{property} is not a [HasMany] property");
			}
		}
		
		/// <summary>
		/// Utility method to either replace or remove an item from a HasMany association while optionally ensuring the item is present.
		/// </summary>
		/// <param name="model">The main model to be updated.</param>
		/// <param name="property">The name of the HasMany property on the model.</param>
		/// <param name="hasManyItem">The item to be removed, replaced, or ensured in the association.</param>
		/// <param name="remove">Specifies whether to remove the item from the association.</param>
		/// <param name="ensureItem">Indicates if the item should be ensured in the association, avoiding duplicates.</param>
		protected async Task HasManyReplaceRemoveItem(SuperModel model, string property, SuperModel hasManyItem, bool remove = false, bool ensureItem = false) {
			var ci = FastReflection.GetClassInfo(model);
			var pi = ci.GetPropertyInfo(property);
			var id = CascadeTypeUtils.GetCascadeId(hasManyItem);
			
			// Modify the list of associated models by either removing, replacing, or ensuring an item.
			var hasManyModels = ((IEnumerable)pi!.GetValue(model)).Cast<object>().ToList();
			var modified = false;
			for (var i = 0; i < hasManyModels.Count; i++) {
				var existing = hasManyModels[i];
				var existingId = CascadeTypeUtils.GetCascadeId(existing); 
				if (EqualityComparer<object>.Default.Equals(existingId,id)) {
					hasManyModels.RemoveAt(i);
					if (!remove)
						hasManyModels.Insert(i, hasManyItem);
					modified = true;
					break;
				}
			}
			if (modified)
				await HasManyReplace(model, property, hasManyModels);
			else if (ensureItem) {
				hasManyModels.Add(hasManyItem);
				await HasManyReplace(model, property, hasManyModels);
			}
		}

		/// <summary>
		/// Replaces an item in the association property and cached collection, matching by id.
		/// </summary>
		/// <param name="model">The model in which the item is being replaced.</param>
		/// <param name="property">The name of the association property.</param>
		/// <param name="hasManyItem">The new item for the association.</param>
		public async Task HasManyReplaceItem(SuperModel model, string property, SuperModel hasManyItem) {
			await HasManyReplaceRemoveItem(model, property, hasManyItem, remove: false);
		}
		
		/// <summary>
		/// Removes an item from the association property and cached collection, matching by id.
		/// </summary>
		/// <param name="model">The model from which the item is being removed.</param>
		/// <param name="property">The name of the association property.</param>
		/// <param name="hasManyItem">The item to remove from the association.</param>
		public async Task HasManyRemoveItem(SuperModel model, string property, SuperModel hasManyItem) {
			await HasManyReplaceRemoveItem(model, property, hasManyItem, remove: true);
		}
		
		/// <summary>
		/// Ensures that an item occurs in the association property and cached collection, matching by id (adds or replaces as necessary to avoid duplicates).
		/// </summary>
		/// <param name="model">The model to update.</param>
		/// <param name="property">The name of the association property.</param>
		/// <param name="hasManyItem">The item to ensure in the association.</param>
		public async Task HasManyEnsureItem(SuperModel model, string property, SuperModel hasManyItem) {
			await HasManyReplaceRemoveItem(model, property, hasManyItem, remove: false, ensureItem: true);
		}
		
		/// <summary>
		/// Replaces the value of the given HasOne property with the given model.
		/// </summary>
		/// <param name="model">The main model to update.</param>
		/// <param name="property">The name of the HasOne property on the main model.</param>
		/// <param name="value">The new model for the association.</param>
		/// <exception cref="ArgumentException">Thrown if the property is not a HasOne property.</exception>
		public async Task UpdateHasOne(SuperModel model, string property, object value) {
			var ci = FastReflection.GetClassInfo(model);
			var pi = ci.GetPropertyInfo(property);
			
			// var modelType = model.GetType();
			// var propertyInfo = modelType.GetProperty(property);
			if (pi?.KindAttribute is HasOneAttribute hasOne) {
				var propertyType = pi.NotNullType;
				var foreignType = propertyType;
				if (foreignType == null)
					throw new ArgumentException("Unable to get foreign model type. Property should be of type a ChildModel");

				// Setting the model property with new value.
				await SetModelProperty(model, pi, value);
			}
			else {
				throw new ArgumentException($"{property} is not a [HasMany] property");
			}
		}

		// Consider using UpdateHasMany & UpdateHasOne instead eg. UpdateHasMany updates the underlying collection 
		// Use this when you have a value for the association, rather than using Populate()
		
		/// <summary>
		/// Use this when you only want to set an association property to a value you already have.
		/// Consider using UpdateHasMany/UpdateHasOne/UpdateBelongsTo instead. This method does not update the caches.
		/// </summary>
		/// <param name="target">The model to act on.</param>
		/// <param name="propertyName">The name of the association property to set.</param>
		/// <param name="value">The value to set for the association property.</param>
		public Task SetAssociation(object target, string propertyName, object value) {
			return SetModelProperty(target, propertyName, value);
		}

		// update the association on many models with the same property and value
		
		/// <summary>
		/// Use this when you only want to set an association property to a value you already have.
		/// Consider using UpdateHasMany/UpdateHasOne/UpdateBelongsTo instead. This method does not update the caches.
		/// </summary>
		/// <param name="targets">The models to act on.</param>
		/// <param name="propertyName">The name of the association property to set.</param>
		/// <param name="value">The value to set for the association property.</param>
		public async Task SetAssociation(IEnumerable targets, string propertyName, object value) {
			var targetsArray = targets.Cast<Object>().ToArray();
			if (!targetsArray.Any())
				return;
			var ci = FastReflection.GetClassInfo(targetsArray.First());
			if (!ci.Associationinfos.TryGetValue(propertyName,out var pi))
				throw new ArgumentException(propertyName+" is not an association property");
			foreach (object target in targetsArray) {
				await SetModelProperty(target, pi, value);
			}
		}
		
	}
}
