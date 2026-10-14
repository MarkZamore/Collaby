using System.Text.Json;
using Collaby.EventBus.Contracts;

namespace Collaby.EventBus.Internal;

public sealed class KafkaSchemaValidator
{
    public void ValidatePayload(KafkaTopicContract contract, string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        ValidateElement(document.RootElement, contract.SchemaDocument, "$");
    }

    private static void ValidateElement(JsonElement value, JsonElement schema, string path)
    {
        if (schema.TryGetProperty("type", out var typeElement))
        {
            var allowedTypes = ReadTypes(typeElement);
            ValidateType(value, allowedTypes, path);
        }

        if (schema.TryGetProperty("enum", out var enumElement) && enumElement.ValueKind == JsonValueKind.Array)
        {
            var enumValues = enumElement.EnumerateArray().Select(entry => entry.GetString()).ToHashSet(StringComparer.Ordinal);
            var currentValue = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (currentValue is null || !enumValues.Contains(currentValue))
            {
                throw new InvalidOperationException($"Value at {path} must be one of [{string.Join(", ", enumValues)}].");
            }
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(value, schema, path);
                break;
            case JsonValueKind.String:
                ValidateString(value, schema, path);
                break;
            case JsonValueKind.Number:
                ValidateNumber(value, schema, path);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                break;
        }
    }

    private static void ValidateObject(JsonElement value, JsonElement schema, string path)
    {
        var knownProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (schema.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                knownProperties[property.Name] = property.Value;
            }
        }

        if (schema.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var requiredProperty in requiredElement.EnumerateArray().Select(entry => entry.GetString()).Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!))
            {
                if (!value.TryGetProperty(requiredProperty, out _))
                {
                    throw new InvalidOperationException($"Required property '{requiredProperty}' is missing at {path}.");
                }
            }
        }

        var allowAdditional = !schema.TryGetProperty("additionalProperties", out var additionalPropertiesElement)
            || additionalPropertiesElement.ValueKind != JsonValueKind.False;

        foreach (var property in value.EnumerateObject())
        {
            if (!knownProperties.TryGetValue(property.Name, out var propertySchema))
            {
                if (!allowAdditional)
                {
                    throw new InvalidOperationException($"Property '{property.Name}' is not allowed at {path}.");
                }

                continue;
            }

            ValidateElement(property.Value, propertySchema, $"{path}.{property.Name}");
        }
    }

    private static void ValidateString(JsonElement value, JsonElement schema, string path)
    {
        var currentValue = value.GetString() ?? string.Empty;

        if (schema.TryGetProperty("minLength", out var minLengthElement) &&
            minLengthElement.ValueKind == JsonValueKind.Number &&
            currentValue.Length < minLengthElement.GetInt32())
        {
            throw new InvalidOperationException($"String value at {path} is shorter than allowed minimum length.");
        }

        if (!schema.TryGetProperty("format", out var formatElement) || formatElement.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var format = formatElement.GetString();
        switch (format)
        {
            case "uuid" when !Guid.TryParse(currentValue, out _):
                throw new InvalidOperationException($"String value at {path} must be a UUID.");
            case "email" when !currentValue.Contains('@', StringComparison.Ordinal):
                throw new InvalidOperationException($"String value at {path} must be an email.");
            case "uri" when !Uri.TryCreate(currentValue, UriKind.Absolute, out _):
                throw new InvalidOperationException($"String value at {path} must be an absolute URI.");
        }
    }

    private static void ValidateNumber(JsonElement value, JsonElement schema, string path)
    {
        if (schema.TryGetProperty("minimum", out var minimumElement) && minimumElement.ValueKind == JsonValueKind.Number)
        {
            var current = value.GetInt64();
            var minimum = minimumElement.GetInt64();
            if (current < minimum)
            {
                throw new InvalidOperationException($"Numeric value at {path} is lower than allowed minimum {minimum}.");
            }
        }
    }

    private static IReadOnlyList<string> ReadTypes(JsonElement typeElement)
    {
        return typeElement.ValueKind switch
        {
            JsonValueKind.String => [typeElement.GetString() ?? string.Empty],
            JsonValueKind.Array => typeElement.EnumerateArray()
                .Select(entry => entry.GetString())
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .ToArray(),
            _ => Array.Empty<string>()
        };
    }

    private static void ValidateType(JsonElement value, IReadOnlyList<string> allowedTypes, string path)
    {
        if (allowedTypes.Count == 0)
        {
            return;
        }

        var matches = allowedTypes.Any(allowedType => allowedType switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number,
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        });

        if (!matches)
        {
            throw new InvalidOperationException($"Value at {path} does not match allowed schema types [{string.Join(", ", allowedTypes)}].");
        }
    }
}
