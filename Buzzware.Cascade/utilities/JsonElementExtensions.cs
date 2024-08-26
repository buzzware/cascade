using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Provides extension methods for the JsonElement class.
/// </summary>
public static class JsonElementExtensions
{
  /// <summary>
  /// Checks if a property exists with the specified name.
  /// </summary>
  /// <param name="element">The JsonElement to check.</param>
  /// <param name="propertyName">The name of the property to check for.</param>
  /// <returns>True if the property exists; otherwise, false.</returns>
  public static bool HasProperty(this JsonElement element, string propertyName)
  {
    return element.TryGetProperty(propertyName, out _);
  }
}

/// <summary>
/// Provides extension methods for the JsonNode class
/// </summary>
public static class JsonNodeExtensions
{
  /// <summary>
  /// True if contains a specified key.
  /// </summary>
  /// <param name="node">The JsonNode to check.</param>
  /// <param name="propertyName">The name of the key to check for.</param>
  /// <returns>True if the key exists in the JsonObject; otherwise, false.</returns>
  public static bool HasKey(this JsonNode node, string propertyName)
  {
    if (node is JsonObject jsonObject)
      return jsonObject.ContainsKey(propertyName);
    else
      return false;
  }
  
  /// <summary>
  /// Retrieves all keys
  /// </summary>
  /// <param name="node">The JsonNode from which to retrieve keys.</param>
  /// <returns>An IEnumerable of strings representing the keys of the JsonObject; an empty IEnumerable if not a JsonObject.</returns>
  public static IEnumerable<string> Keys(this JsonNode node) {
    if (node is JsonObject jsonObject)
      return ((IDictionary<string, JsonNode?>)node).Keys;
    else
      return new string[] {};
  }
}
