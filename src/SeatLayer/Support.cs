using System.Text.Json;
using System.Text.RegularExpressions;

namespace SeatLayer;

/// <summary>Options for <see cref="SeatLayerClient"/>.</summary>
public sealed class SeatLayerClientOptions
{
    /// <summary>API base URL. Override for staging or a proxy.</summary>
    public string BaseUrl { get; set; } = SeatLayerClient.DefaultBaseUrl;

    /// <summary>Total attempts, not extra attempts. Default 3.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Per-attempt timeout. Ignored when <see cref="HttpClient"/> is supplied.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Your own HttpClient — from IHttpClientFactory, or with a custom handler. When set,
    /// the SDK does not dispose it, because it does not own its lifetime.
    /// </summary>
    public HttpClient? HttpClient { get; set; }
}

/// <summary>Validates the Idempotency-Key charset the API enforces.</summary>
internal static partial class IdempotencyKey
{
    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,128}$")]
    private static partial Regex Pattern();

    internal static void Validate(string key)
    {
        if (!Pattern().IsMatch(key))
        {
            throw new ArgumentException(
                $"Invalid Idempotency-Key \"{key}\": allowed characters are "
                + "A-Z a-z 0-9 . _ : - and the length must be 1-128.",
                nameof(key));
        }
    }
}

/// <summary>
/// JSON helpers.
/// </summary>
/// <remarks>
/// Responses decode to <c>IReadOnlyDictionary&lt;string, object?&gt;</c> rather than typed
/// models. The API's payloads evolve additively, and a strongly typed model would drop
/// fields a caller might need until the SDK caught up.
/// </remarks>
internal static class Json
{
    internal static IReadOnlyDictionary<string, object?> ToDictionary(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object
            ? (IReadOnlyDictionary<string, object?>)Convert(document.RootElement)!
            : new Dictionary<string, object?> { ["data"] = Convert(document.RootElement) };
    }

    internal static IReadOnlyDictionary<string, object?> TryToDictionary(string json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? new Dictionary<string, object?>()
                : ToDictionary(json);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }

    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .ToDictionary(property => property.Name, property => Convert(property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Convert).ToList(),
        JsonValueKind.String => element.GetString(),
        // Integers stay long so an epoch-millis value does not come back as 1.75E+12.
        // The (object) cast is load-bearing: without it C# unifies the ternary's long and
        // double branches to double, silently defeating the whole point.
        JsonValueKind.Number => element.TryGetInt64(out var whole) ? (object)whole : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}

/// <summary>One page of a list endpoint, plus the cursor for the next.</summary>
/// <param name="Items">The rows on this page.</param>
/// <param name="NextCursor">Null once the list is exhausted.</param>
public sealed record Page(IReadOnlyList<IReadOnlyDictionary<string, object?>> Items, string? NextCursor)
{
    internal static Page From(IReadOnlyDictionary<string, object?> response, string key)
    {
        var items = response.TryGetValue(key, out var value) && value is List<object?> list
            ? list.OfType<IReadOnlyDictionary<string, object?>>().ToList()
            : new List<IReadOnlyDictionary<string, object?>>();

        var cursor = response.TryGetValue("nextCursor", out var raw) && raw is string text && text.Length > 0
            ? text
            : null;

        return new Page(items, cursor);
    }
}

/// <summary>Builds a request body, dropping nulls so optional fields stay optional.</summary>
internal static class Body
{
    internal static Dictionary<string, object?> Of(params (string Key, object? Value)[] pairs)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
            {
                result[key] = value;
            }
        }

        return result;
    }
}
