namespace SeatLayer;

/// <summary>
/// Base class for every non-2xx response.
/// </summary>
/// <remarks>
/// The API answers failures with <c>{"error":…, "code":…, "message":…}</c> and a status.
/// Surfacing that as one opaque exception leaves every caller string-matching on
/// <c>error</c>. The subclasses below are the ones an integration actually branches on —
/// a sold-out seat is a business outcome that belongs in its own catch, not lumped in
/// with a bad key.
/// </remarks>
public class SeatLayerException : Exception
{
    internal SeatLayerException(
        int status,
        string code,
        IReadOnlyDictionary<string, object?> body,
        string? requestId,
        string message)
        : base(message)
    {
        Status = status;
        Code = code;
        Body = body;
        RequestId = requestId;
    }

    /// <summary>HTTP status the API answered with.</summary>
    public int Status { get; }

    /// <summary>Machine-readable slug: body <c>code</c>, falling back to <c>error</c>.</summary>
    public string Code { get; }

    /// <summary>The decoded error body, for fields this SDK does not model.</summary>
    public IReadOnlyDictionary<string, object?> Body { get; }

    /// <summary>Correlation id from <c>X-Request-ID</c>. Quote it in support requests.</summary>
    public string? RequestId { get; }

    internal static SeatLayerException FromResponse(
        int status,
        IReadOnlyDictionary<string, object?> body,
        string? requestId,
        double retryAfterSeconds)
    {
        var code = FirstString(body, "code") ?? FirstString(body, "error") ?? "unknown_error";
        var message = FirstString(body, "message") ?? $"SeatLayer API error {status} ({code})";

        return status switch
        {
            401 or 403 => new SeatLayerAuthException(status, code, body, requestId, message),
            404 => new SeatLayerNotFoundException(status, code, body, requestId, message),
            409 => new SeatLayerConflictException(status, code, body, requestId, message),
            422 => new SeatLayerValidationException(status, code, body, requestId, message),
            429 => new SeatLayerRateLimitException(
                status, code, body, requestId, message, retryAfterSeconds),
            _ => new SeatLayerException(status, code, body, requestId, message),
        };
    }

    private static string? FirstString(IReadOnlyDictionary<string, object?> body, string key)
        => body.TryGetValue(key, out var value) && value is string text && text.Length > 0 ? text : null;
}

/// <summary>401 or 403 — bad key, revoked key, or a live key used against a test event.</summary>
public sealed class SeatLayerAuthException : SeatLayerException
{
    internal SeatLayerAuthException(
        int status, string code, IReadOnlyDictionary<string, object?> body, string? requestId, string message)
        : base(status, code, body, requestId, message)
    {
    }

    /// <summary>
    /// The key's mode and the event's mode disagree — the most common cause of a
    /// "works locally, 403s in production" report.
    /// </summary>
    public bool IsModeMismatch => Code == "mode_mismatch";
}

/// <summary>
/// 404, including another organisation's resource.
/// </summary>
/// <remarks>
/// Asking for something owned by a different organisation answers 404, never 403: a 403
/// would confirm the resource exists, which is not something one customer should be able
/// to learn about another.
/// </remarks>
public sealed class SeatLayerNotFoundException : SeatLayerException
{
    internal SeatLayerNotFoundException(
        int status, string code, IReadOnlyDictionary<string, object?> body, string? requestId, string message)
        : base(status, code, body, requestId, message)
    {
    }
}

/// <summary>
/// 409 — the seats moved under you.
/// </summary>
/// <remarks>
/// Normal in ticketing, not exceptional: two buyers wanted the same seat and one lost.
/// </remarks>
public sealed class SeatLayerConflictException : SeatLayerException
{
    internal SeatLayerConflictException(
        int status, string code, IReadOnlyDictionary<string, object?> body, string? requestId, string message)
        : base(status, code, body, requestId, message)
    {
    }

    /// <summary>Per-object conflicts, when the endpoint reports them.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Conflicts =>
        Body.TryGetValue("conflicts", out var value)
        && value is IReadOnlyList<object?> list
            ? list.OfType<IReadOnlyDictionary<string, object?>>().ToList()
            : Array.Empty<IReadOnlyDictionary<string, object?>>();

    /// <summary>Best-available could not find enough free inventory.</summary>
    public bool IsSoldOut =>
        Body.TryGetValue("reason", out var reason)
        && reason is string text
        && (text == "sold_out" || text == "not_enough_together");
}

/// <summary>422 — the request was understood and rejected.</summary>
public sealed class SeatLayerValidationException : SeatLayerException
{
    internal SeatLayerValidationException(
        int status, string code, IReadOnlyDictionary<string, object?> body, string? requestId, string message)
        : base(status, code, body, requestId, message)
    {
    }
}

/// <summary>429. <see cref="RetryAfterSeconds"/> prefers the header over the JSON field.</summary>
public sealed class SeatLayerRateLimitException : SeatLayerException
{
    internal SeatLayerRateLimitException(
        int status,
        string code,
        IReadOnlyDictionary<string, object?> body,
        string? requestId,
        string message,
        double retryAfterSeconds)
        : base(status, code, body, requestId, message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>How long to wait before retrying, in seconds.</summary>
    public double RetryAfterSeconds { get; }
}

/// <summary>The request never got an answer: DNS, TLS, socket, or a cancelled token.</summary>
public sealed class SeatLayerConnectionException : Exception
{
    internal SeatLayerConnectionException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

/// <summary>The webhook delivery did not come from SeatLayer. Respond 400; do not process it.</summary>
public sealed class SeatLayerWebhookVerificationException : Exception
{
    internal SeatLayerWebhookVerificationException(string message)
        : base(message)
    {
    }
}
