using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PatchworkIsland;

/// <summary>
/// Compares the authored reference content with the runtime mission file.
/// Every field is compared, including nested arrays and objects, so a change to
/// documentation-only fields cannot silently drift between the two files.
/// </summary>
public static class MissionContentConsistency
{
    public static bool Compare(string referenceJson, string runtimeJson, out string error)
    {
        error = "";
        JsonDocument reference;
        JsonDocument runtime;
        try
        {
            reference = JsonDocument.Parse(referenceJson);
        }
        catch (JsonException ex)
        {
            error = "Reference content is not valid JSON: " + ex.Message;
            return false;
        }
        try
        {
            runtime = JsonDocument.Parse(runtimeJson);
        }
        catch (JsonException ex)
        {
            reference.Dispose();
            error = "Runtime content is not valid JSON: " + ex.Message;
            return false;
        }

        using (reference)
        using (runtime)
        {
            return CompareElement(reference.RootElement, runtime.RootElement, "$", out error);
        }
    }

    private static bool CompareElement(JsonElement reference, JsonElement runtime, string path, out string error)
    {
        error = "";
        if (reference.ValueKind != runtime.ValueKind)
        {
            error = $"{path}: expected {reference.ValueKind}, found {runtime.ValueKind}.";
            return false;
        }

        switch (reference.ValueKind)
        {
            case JsonValueKind.Object:
                var referenceProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var property in reference.EnumerateObject())
                    referenceProperties[property.Name] = property.Value;
                var runtimeProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var property in runtime.EnumerateObject())
                    runtimeProperties[property.Name] = property.Value;

                foreach (var pair in referenceProperties)
                {
                    if (!runtimeProperties.TryGetValue(pair.Key, out var runtimeValue))
                    {
                        error = $"{path}.{pair.Key}: missing from runtime content.";
                        return false;
                    }
                    if (!CompareElement(pair.Value, runtimeValue, $"{path}.{pair.Key}", out error))
                        return false;
                }
                foreach (string key in runtimeProperties.Keys)
                {
                    if (!referenceProperties.ContainsKey(key))
                    {
                        error = $"{path}.{key}: unexpected field in runtime content.";
                        return false;
                    }
                }
                return true;

            case JsonValueKind.Array:
                int referenceLength = reference.GetArrayLength();
                int runtimeLength = runtime.GetArrayLength();
                if (referenceLength != runtimeLength)
                {
                    error = $"{path}: expected {referenceLength} items, found {runtimeLength}.";
                    return false;
                }
                for (int i = 0; i < referenceLength; i++)
                {
                    if (!CompareElement(reference[i], runtime[i], $"{path}[{i}]", out error))
                        return false;
                }
                return true;

            case JsonValueKind.Number:
                if (reference.TryGetDecimal(out decimal referenceNumber) &&
                    runtime.TryGetDecimal(out decimal runtimeNumber))
                {
                    if (referenceNumber != runtimeNumber)
                    {
                        error = $"{path}: expected {reference.GetRawText()}, found {runtime.GetRawText()}.";
                        return false;
                    }
                    return true;
                }
                if (!string.Equals(reference.GetRawText(), runtime.GetRawText(), StringComparison.Ordinal))
                {
                    error = $"{path}: expected {reference.GetRawText()}, found {runtime.GetRawText()}.";
                    return false;
                }
                return true;

            case JsonValueKind.String:
                if (!string.Equals(reference.GetString(), runtime.GetString(), StringComparison.Ordinal))
                {
                    error = $"{path}: expected {Quote(reference.GetString())}, found {Quote(runtime.GetString())}.";
                    return false;
                }
                return true;

            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;

            default:
                error = $"{path}: unsupported JSON value kind {reference.ValueKind}.";
                return false;
        }
    }

    private static string Quote(string value) => value == null ? "null" : "\"" + value + "\"";
}
