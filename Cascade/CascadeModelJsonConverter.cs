using System;
using System.Reflection;
using Buzzware;
using Newtonsoft.Json.Linq;

namespace Cascade {

	public class CascadeModelJsonConverter<M> : BzCreationConverter<M> {

		protected override M Create(Type aType, JObject aJObject)
		{
			var tpa = aType.GetTypeInfo().GetCustomAttribute<TypePropertyAttribute>();
			if (tpa == null)
				return base.Create(aType, aJObject);
 
			var type = (string)aJObject[tpa.Property]; // use provided class
			if (type == null) {     // else use given type
				if (aType != null)
					type = aType.Name;
				else
					throw new Exception("type not set");
			}
			type = "SomeNamespace." + type;
			Type queryType = Type.GetType(type);
			if (queryType == null)
				throw new Exception("Type " + type + " not found");
			return (M)Activator.CreateInstance(queryType);
		}
	}
}
