using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class JsonElementExtensions
{
	public static bool HasProperty(this JsonElement element, string propertyName)
	{
		return element.TryGetProperty(propertyName, out _);
	}
}

public static class JsonNodeExtensions
{
	public static bool HasKey(this JsonNode node, string propertyName)
	{
		if (node is JsonObject jsonObject)
			return jsonObject.ContainsKey(propertyName);
		else
			return false;
	}
	
	public static IEnumerable<string> Keys(this JsonNode node) {
		if (node is JsonObject jsonObject)
			return ((IDictionary<string, JsonNode?>)node).Keys;
		else
			return new string[] {};
	}
}
