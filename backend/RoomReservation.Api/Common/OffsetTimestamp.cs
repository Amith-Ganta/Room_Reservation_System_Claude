using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RoomReservation.Api.Common;

/// <summary>
/// Parses ISO 8601 timestamps that carry an explicit offset (<c>Z</c> or <c>+hh:mm</c>).
/// The framework would read a value without an offset as server-local time; the API rejects it instead.
/// </summary>
public static partial class OffsetTimestamp
{
    public const string Example = "2026-11-02T09:00:00Z";

    // \z (not $) and [0-9] (not \d): no trailing newline and no non-ASCII digits.
    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}(:[0-9]{2}(\.[0-9]+)?)?(Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetShape();

    public static bool TryParse(string? text, out DateTimeOffset value)
    {
        value = default;
        return text is not null
            && OffsetShape().IsMatch(text)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }
}

/// <summary>JSON converter for request bodies: a timestamp without an offset is a 400, not server-local time.</summary>
public sealed class OffsetDateTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && OffsetTimestamp.TryParse(reader.GetString(), out var value))
        {
            return value;
        }

        throw new JsonException($"The value must be an ISO 8601 timestamp with an offset, for example {OffsetTimestamp.Example}.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>Model binder for optional query-string timestamps (<c>from</c>, <c>to</c>); same offset rule as the body.</summary>
public sealed class OffsetDateTimeBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var name = bindingContext.ModelName;
        var result = bindingContext.ValueProvider.GetValue(name);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(name, result);
        var text = result.FirstValue;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        if (OffsetTimestamp.TryParse(text, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(name,
                $"must be an ISO 8601 timestamp with an offset, for example {OffsetTimestamp.Example}.");
        }

        return Task.CompletedTask;
    }
}
