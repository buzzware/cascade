using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Easy.Common.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Buzzware {
/*
	            This post shows that the author of JSON.Net doesn't appear to 'get' the need when deserializing, to create objects based on a type property in the JSON,
	            which explains why this isn't easy in JSON.Net. http://json.codeplex.com/discussions/56031

	            This class based on the example from there, by http://www.codeplex.com/site/users/view/nonplus gives us the opportunity to create the right class based on a property of the incoming object

	            //This class can be then derived to implement the actual construction of objects:
	            class CVehicleConverter : JsonCreationConverter<CVehicle>
	            {
	                protected override CVehicle Create(Type objectType, JObject jObject)
	                {
	                    var type = (string)jObject.Property("Type");
	                    switch (type)
	                    {
	                        case "Car":
	                            return new CCar();
	                        case "Bike":
	                            return new CBike();
	                    }
	                    throw new ApplicationException(String.Format("The given vehicle type {0} is not supported!", type));
	                }
	            }

	            References
	            http://skrift.io/articles/archive/bulletproof-interface-deserialization-in-jsonnet/

	            http://stackoverflow.com/questions/5780888/casting-interfaces-for-deserialization-in-json-net

	            For future proper array support :
	            http://stackoverflow.com/questions/36429749/jsonconverter-return-a-single-object-or-a-listobject-based-on-inbound-json

	            Copy JsonSerializer settings :
	            http://stackoverflow.com/questions/38230326/copy-jsonserializersettings-from-jsonserializer-to-new-jsonserializer/38230327#38230327

							Ignore by lambda :
							http://stackoverflow.com/questions/13588022/exclude-property-from-serialization-via-custom-attribute-json-net

							Should Serialize :
							http://www.newtonsoft.com/json/help/html/conditionalproperties.htm

							Filter fields by expressions :
							http://www.c-sharpcorner.com/uploadfile/f7a3ed/fields-filtering-in-asp-net-web-api/

							Interesting performance alternative Jil :
							https://github.com/kevin-montrose/Jil


	            */


	//[AttributeUsage(AttributeTargets.Class)]
	//public class TypePropertyAttribute : Attribute {

	//	protected static Dictionary<Type, List<String>> modelNames;

	//	public string Property { get; set; }

	//	public static void Reset() {
	//		modelNames = new Dictionary<Type, List<String>>();
	//	}

	//	public static void AddTypeModelName(Type aType, String aModelName) {
	//		var item = modelNames.FirstOrDefault(i => i.Key == aType);
	//		if (item.Key == null) {
	//			item = new KeyValuePair<Type, List<String>>(aType, new List<String>());
	//		}
	//		if (!item.Value.Contains(aModelName))
	//			item.Value.Add(aModelName);
	//	}
	
	//	public TypePropertyAttribute(string aProperty, params string[] aModelNames) {
	//		Reset();		
	//		var t = this.GetType();
	//		if (aModelNames == null || aModelNames.Length == 0) {
	//			AddTypeModelName(t, t.Name);
	//		} else {
	//			foreach(var n in aModelNames)
	//				AddTypeModelName(t,n);
	//		}
	//		Property = aProperty;
	//	}
	//}


	            
	[AttributeUsage(AttributeTargets.Class)]
	public class TypePropertyAttribute : Attribute
	{
		public TypePropertyAttribute(string aProperty)
		{
			Property = aProperty;
		}

		public string Property { get; set; }
	}

	// from http://stackoverflow.com/questions/7814247/serialize-nan-values-into-json-as-nulls-in-json-net/12824768
	class LawAbidingFloatConverter : JsonConverter {
		public override bool CanRead => false;
		public override bool CanWrite => true;
		
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			var val = value as double? ?? (double?)(value as float?);
			if (val == null || Double.IsNaN((double)val) || Double.IsInfinity((double)val)) {
				writer.WriteNull();
				return;
			}
			writer.WriteValue((double)val);
		}
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
			throw new NotImplementedException();
		}
		public override bool CanConvert(Type objectType) {
			return objectType == typeof(double) || objectType == typeof(float);
		}
	}

	public class StaticValueProvider : IValueProvider
	{
		private readonly string _value;

		public StaticValueProvider(string value)
		{
			_value = value;
		}

		public void SetValue(object target, object value)
		{
		}

		public object GetValue(object target)
		{
			return _value;
		}
	}

	public class BzContractResolver : DefaultContractResolver
	{
		protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
			IList<JsonProperty> result = base.CreateProperties(type, memberSerialization);
			
			// don't automatically include attached objects, arrays etc
			foreach (var p in result) {
				if (p.Readable && !p.PropertyType.IsSimpleType())		// Readable = Serializable, Writable = Deserializable
					p.Readable = false;
			}						
			
			var tpa = type.GetCustomAttribute<TypePropertyAttribute>();		// was var tpa = type.GetAttribute<TypePropertyAttribute>();
			if (tpa != null) {
				var prop = createProperty(type, tpa.Property, type.Name);
				result.Add(prop);
			}

			return result;
		}

		//override Create

		//protected override List<MemberInfo> GetSerializableMembers(Type objectType) {
		//	var result = base.GetSerializableMembers(objectType);
		//	result = result.Where((p) => {
		//		var pi = p as PropertyInfo;
		//		if (pi == null)
		//			return true;
		//		var ti = pi.PropertyType.GetTypeInfo();
		//		return ti.IsPrimitive;
		//		//return typeof(IEnumerable).GetTypeInfo().IsAssignableFrom();
		//	}).ToList();
		//	return result;
		//}

		private JsonProperty createProperty(Type declaringType, string propertyName, string propertyValue)
		{
			return new JsonProperty
			{
				PropertyType = typeof(string),
				DeclaringType = declaringType,
				PropertyName = propertyName,
				ValueProvider = new StaticValueProvider(propertyValue),
				Readable = true,
				Writable = true
			};
		}
	}


	public class BzJsonSerialization {
	
		public List<JsonConverter> converters = new List<JsonConverter>();

		public BzJsonSerialization(){
			Debug.WriteLine("public BzJsonSerialization");
		}

		JsonSerializer _serializer = null;

		public JsonSerializer serializer
		{
			get
			{
				if (_serializer != null)
					return _serializer;
				_serializer = JsonSerializer.CreateDefault();
				foreach (var c in converters)
					if (!_serializer.Converters.Contains(c))
						_serializer.Converters.Add(c);
				_serializer.ContractResolver = new BzContractResolver();
				return _serializer;
			}
		}

		JsonSerializerSettings _settings = null;

		public JsonSerializerSettings settings
		{
			get
			{
				if (_settings != null)
					return _settings;
				_settings = new JsonSerializerSettings()
				{
					Converters = converters.ToList()
				};
				return _settings;
			}
		}


		public void initialise(JsonConverter aConverter)
		{
			if (!converters.Contains(aConverter))
				converters.Add(aConverter);
		}

		public JToken Deserialize(string aSource) {
			return JToken.Parse(aSource);
		}

		public object Deserialize(string aSource, Type aType)
		{
			var jt = JToken.Parse(aSource);
			// deserialize first, then convert to classes, which means we could sniff the type
			return jt.ToObject(aType, serializer);
		}

		public T Deserialize<T>(string aSource)
		{
			var jt = JToken.Parse(aSource);
			// deserialize first, then convert to classes, which means we could sniff the type
			if (jt is JArray)
				throw new Exception("The data is an array, but a single object was expected");
			return jt.ToObject<T>(serializer);
		}

		public T[] DeserializeArray<T>(string aSource)
		{
			var jt = JToken.Parse(aSource);
			// deserialize first, then convert to classes, which means we could sniff the type
			if (!(jt is JArray))
				throw new Exception("An array was expected, but the data is not an array");

			return jt.Select(i => i.ToObject<T>(serializer)).ToArray();
			// not great - should happen in ReadJson, but it blows up
		}

		public string Serialize(Object aSource)
		{
			if (aSource == null)
				return null;
			var jt = JToken.FromObject(aSource, serializer);
			return jt.ToString(Formatting.None);
			//return JsonConvert.SerializeObject(jt, settings);
		}

		public T Deserialize<T>(JObject aSource)
		{
			return aSource.ToObject<T>(serializer);
		}
	}


	public class BzCreationConverter<T> : JsonConverter {

		// You possibly should implement these
		public override bool CanWrite => false;
		//public override bool CanRead => true;

		/// <summary>
		/// Creates an object which will then be populated by the serializer.
		/// </summary>
		/// <param name="objectType">Type of the object.</param>
		/// <returns>The created object.</returns>
		protected virtual T Create(Type aType, JObject aJObject)
		{
			return (T) Activator.CreateInstance(aType);
		}

		/// <summary>
		/// Determines whether this instance can convert the specified object type.
		/// </summary>
		/// <param name="objectType">Type of the object.</param>
		/// <returns>
		/// 	<c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
		/// </returns>
		public override bool CanConvert(Type aType)
		{
			var ti = aType.GetTypeInfo();
			return typeof(T).GetTypeInfo().IsAssignableFrom(ti) || typeof(T[]).GetTypeInfo().IsAssignableFrom(ti);
		}

		// From http://stackoverflow.com/questions/8030538/how-to-implement-custom-jsonconverter-in-json-net-to-deserialize-a-list-of-base
		/// <summary>Creates a new reader for the specified jObject by copying the settings
		/// from an existing reader.</summary>
		/// <param name="reader">The reader whose settings should be copied.</param>
		/// <param name="jObject">The jObject to create a new reader for.</param>
		/// <returns>The new disposable reader.</returns>
		public static JsonReader CopyReaderForObject(JsonReader reader, JObject jObject)
		{
			JsonReader jObjectReader = jObject.CreateReader();
			jObjectReader.Culture = reader.Culture;
			jObjectReader.DateFormatString = reader.DateFormatString;
			jObjectReader.DateParseHandling = reader.DateParseHandling;
			jObjectReader.DateTimeZoneHandling = reader.DateTimeZoneHandling;
			jObjectReader.FloatParseHandling = reader.FloatParseHandling;
			jObjectReader.MaxDepth = reader.MaxDepth;
			jObjectReader.SupportMultipleContent = reader.SupportMultipleContent;
			return jObjectReader;
		}

		/// <summary>
		/// Reads the JSON representation of the object.
		/// </summary>
		/// <param name="reader">The <see cref="JsonReader"/> to read from.</param>
		/// <param name="objectType">Type of the object.</param>
		/// <param name="existingValue">The existing value of object being read.</param>
		/// <param name="serializer">The calling serializer.</param>
		/// <returns>The object value.</returns>
		public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
			JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
			{
				return null;
			}
			else if (reader.TokenType == JsonToken.StartArray)
			{
				JArray array = JArray.Load(reader);
				var typed = array.ToObject<T[]>(); // serializer); this blows up with a deep stack trace - why?
				return typed;
			}

			JObject jObject = JObject.Load(reader);
			T value = Create(objectType, jObject);
			if (value == null)
				throw new JsonSerializationException("No object created.");
			using (JsonReader jObjectReader = CopyReaderForObject(reader, jObject))
			{
				serializer.Populate(jObjectReader, value);
			}
			AfterPopulate(value, jObject);
			return value;
		}

		protected virtual void AfterPopulate(T aModel, JObject aJObject)
		{
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			throw new NotImplementedException();
		}
	}
}
