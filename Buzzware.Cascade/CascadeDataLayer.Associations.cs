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
	/// </summary>
	public partial class CascadeDataLayer {

		public static ImmutableArray<Type> AssociationAttributes = ImmutableArray.Create<Type>(
			typeof(BelongsToAttribute),
			typeof(HasManyAttribute),
			typeof(HasOneAttribute)
		);

		
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
		
		
		/// <summary>
		/// Replaces the value of the given HasMany property with the given IEnumerable of models and updates the caches appropriately.
		/// This is needed eg. when you add models to a HasMany association
		/// </summary>
		/// <param name="model"></param>
		/// <param name="property"></param>
		/// <param name="models"></param>
		/// <exception cref="ArgumentException"></exception>
		public async Task HasManyReplace(SuperModel model, string property, IEnumerable<object> models) {
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

		public async Task HasManyAddItem(SuperModel model, string property, SuperModel hasManyItem) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);
			if (propertyInfo?.GetCustomAttributes(typeof(HasManyAttribute), true).FirstOrDefault() is HasManyAttribute hasMany) {
				var propertyType = CascadeTypeUtils.DeNullType(propertyInfo.PropertyType);
				var isEnumerable = (propertyType?.Implements<IEnumerable>() ?? false) && propertyType != typeof(string);
				var foreignType = isEnumerable ? CascadeTypeUtils.InnerType(propertyType!) : null;
				foreignType = foreignType != null ? CascadeTypeUtils.DeNullType(foreignType) : null;
				if (foreignType == null)
					throw new ArgumentException("Unable to get foreign model type. Property should be of type ImmutableArray<ChildModel>");

				var hasManyModels = ((IEnumerable)(propertyInfo!.GetValue(model) ?? Array.Empty<object>())).Cast<object>().ToList();
				hasManyModels.Add(hasManyItem);
				
				object modelId = CascadeTypeUtils.GetCascadeId(model);
				await SetCacheWhereCollection(foreignType, hasMany.ForeignIdProperty, modelId.ToString(), hasManyModels.ToImmutableArray());
				await SetModelCollectionProperty(model, propertyInfo, hasManyModels);
			} else {
				throw new ArgumentException($"{property} is not a [HasMany] property");
			}
		}
		
		protected async Task HasManyReplaceRemoveItem(SuperModel model, string property, SuperModel hasManyItem, bool remove = false, bool ensureItem = false) {
			var modelType = model.GetType();
			var propertyInfo = modelType.GetProperty(property);
			var id = CascadeTypeUtils.GetCascadeId(hasManyItem);
			
			var hasManyModels = ((IEnumerable)propertyInfo!.GetValue(model)).Cast<object>().ToList();
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

		// Replaces an item in the association property and cached collection, matching by id
		public async Task HasManyReplaceItem(SuperModel model, string property, SuperModel hasManyItem) {
			await HasManyReplaceRemoveItem(model, property, hasManyItem, remove: false);
		}
		
		// Removes an item in the association property and cached collection, matching by id
		public async Task HasManyRemoveItem(SuperModel model, string property, SuperModel hasManyItem) {
			await HasManyReplaceRemoveItem(model, property, hasManyItem, remove: true);
		}
		
		// Ensures that an item occurs in the association property and cached collection, matching by id ie adds or replaces as appropriate to avoid multiple occurrances
		public async Task HasManyEnsureItem(SuperModel model, string property, SuperModel hasManyItem) {
			await HasManyReplaceRemoveItem(model, property, hasManyItem, remove: false, ensureItem: true);
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
		
	}
}
