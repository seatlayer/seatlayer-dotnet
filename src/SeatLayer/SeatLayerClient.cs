using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

namespace SeatLayer;

/// <summary>
/// The SeatLayer server API client.
/// </summary>
/// <remarks>
/// <para>
/// Server-side only: this class authenticates with your secret key. Never ship it in a
/// client application — browser surfaces get short-lived, origin-bound tokens that you
/// mint with <see cref="Sessions"/>.
/// </para>
/// <para>
/// Register it as a singleton. It is thread-safe, and the underlying
/// <see cref="System.Net.Http.HttpClient"/> is meant to be long-lived — constructing one
/// per request exhausts sockets.
/// </para>
/// <example>
/// <code>
/// var client = new SeatLayerClient(Environment.GetEnvironmentVariable("SEATLAYER_SECRET_KEY")!);
/// var held = await client.Inventory.HoldBestAvailableAsync("summer-gala", new BestAvailableRequest { Qty = 4 });
/// </code>
/// </example>
/// </remarks>
public sealed class SeatLayerClient : IDisposable
{
    /// <summary>The public API.</summary>
    public const string DefaultBaseUrl = "https://api.seatlayer.io";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _secretKey;
    private readonly string _baseUrl;
    private readonly int _maxRetries;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    /// <summary>Creates a client.</summary>
    /// <param name="secretKey">An <c>sk_live_…</c> or <c>sk_test_…</c> key.</param>
    /// <param name="options">Base URL, retry count, timeout, or your own HttpClient.</param>
    /// <exception cref="ArgumentException">The key is missing or is not a secret key.</exception>
    public SeatLayerClient(string secretKey, SeatLayerClientOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new ArgumentException("A SeatLayer secret key is required.", nameof(secretKey));
        }

        // Caught here rather than as a 401 three round-trips later. The pk_ case gets its
        // own message: it is the one people paste by mistake.
        if (secretKey.StartsWith("pk_", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "That is a publishable key. The server SDK needs a secret key (sk_live_… or sk_test_…).",
                nameof(secretKey));
        }

        if (!secretKey.StartsWith("sk_", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A SeatLayer secret key starts with sk_live_ or sk_test_.", nameof(secretKey));
        }

        options ??= new SeatLayerClientOptions();

        _secretKey = secretKey;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _maxRetries = options.MaxRetries;
        _ownsHttpClient = options.HttpClient is null;
        _http = options.HttpClient ?? new HttpClient { Timeout = options.Timeout };

        Mode = secretKey.StartsWith("sk_test_", StringComparison.Ordinal) ? "test"
            : secretKey.StartsWith("sk_live_", StringComparison.Ordinal) ? "live"
            : "unknown";

        Charts = new ChartsService(this);
        Events = new EventsService(this);
        Inventory = new InventoryService(this);
        Sessions = new SessionsService(this);
        Webhooks = new WebhooksService(this);
        Workspaces = new WorkspacesService(this);
    }

    /// <summary><c>"live"</c> or <c>"test"</c>, derived from the key prefix.</summary>
    public string Mode { get; }

    /// <summary>Seat-map definitions that events are created from.</summary>
    public ChartsService Charts { get; }

    /// <summary>Event lifecycle, metadata and reports.</summary>
    public EventsService Events { get; }

    /// <summary>Holds, booking, blocking and availability.</summary>
    public InventoryService Inventory { get; }

    /// <summary>Short-lived, origin-bound browser tokens.</summary>
    public SessionsService Sessions { get; }

    /// <summary>Webhook subscription management.</summary>
    public WebhooksService Webhooks { get; }

    /// <summary>Workspaces, which isolate one tenant from another.</summary>
    public WorkspacesService Workspaces { get; }

    /// <summary>Dependency-aware readiness probe.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ReadyAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, "/health/ready", cancellationToken: cancellationToken);

    /// <summary>
    /// Escape hatch for surface this SDK does not wrap yet. Carries the same auth,
    /// retries, idempotency and error mapping.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> SendAsync(
        HttpMethod method,
        string path,
        IDictionary<string, string?>? query = null,
        object? body = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var url = _baseUrl + path;
        if (query is not null)
        {
            var builder = new StringBuilder();
            foreach (var pair in query)
            {
                if (pair.Value is null)
                {
                    continue;
                }

                builder.Append(builder.Length == 0 ? '?' : '&')
                    .Append(HttpUtility.UrlEncode(pair.Key))
                    .Append('=')
                    .Append(HttpUtility.UrlEncode(pair.Value));
            }

            url += builder.ToString();
        }

        var payload = body is null ? null : JsonSerializer.Serialize(body, JsonOptions);

        // Every mutation carries one. A retried POST that creates a second hold is worse
        // than a failed POST, and the caller cannot tell from outside — so the SDK, which
        // knows it retried, is the right place to guarantee it.
        string? resolvedKey = null;
        if (method != HttpMethod.Get && method != HttpMethod.Head)
        {
            resolvedKey = idempotencyKey ?? Guid.NewGuid().ToString();
            IdempotencyKey.Validate(resolvedKey);
        }

        Exception? lastError = null;

        for (var attempt = 0; attempt < _maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("seatlayer-dotnet");
            if (resolvedKey is not null)
            {
                request.Headers.Add("Idempotency-Key", resolvedKey);
            }

            if (payload is not null)
            {
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A cancelled token is the caller's decision, not a transient fault.
                throw;
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
            {
                lastError = new SeatLayerConnectionException(
                    $"Request to {method} {path} failed: {error.Message}", error);

                if (attempt < _maxRetries - 1)
                {
                    await DelayAsync(Backoff(attempt, null), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw lastError;
            }

            using (response)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(content)
                        ? new Dictionary<string, object?>()
                        : Json.ToDictionary(content);
                }

                // A proxy or WAF can answer with HTML; that must not become a parse crash
                // that hides the real status from the caller.
                var errorBody = Json.TryToDictionary(content);
                var requestId = response.Headers.TryGetValues("X-Request-ID", out var ids)
                    ? ids.FirstOrDefault()
                    : null;
                var retryAfter = ParseRetryAfter(response, errorBody);
                var status = (int)response.StatusCode;

                if (IsRetryable(status) && attempt < _maxRetries - 1)
                {
                    var wait = status == 429 ? TimeSpan.FromSeconds(retryAfter) : Backoff(attempt, null);
                    await DelayAsync(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw SeatLayerException.FromResponse(status, errorBody, requestId, retryAfter);
            }
        }

        throw lastError ?? new SeatLayerConnectionException("Request failed with no attempts made.", null);
    }

    internal Task<IReadOnlyDictionary<string, object?>> GetAsync(
        string path, IDictionary<string, string?>? query = null, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, path, query, cancellationToken: cancellationToken);

    internal Task<IReadOnlyDictionary<string, object?>> PostAsync(
        string path, object? body = null, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, path, null, body, idempotencyKey, cancellationToken);

    internal Task<IReadOnlyDictionary<string, object?>> PutAsync(
        string path, object body, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Put, path, null, body, cancellationToken: cancellationToken);

    internal Task<IReadOnlyDictionary<string, object?>> PatchAsync(
        string path, object body, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Patch, path, null, body, cancellationToken: cancellationToken);

    internal Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string path, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, path, cancellationToken: cancellationToken);

    /// <summary>Percent-encodes a path segment, including slashes.</summary>
    internal static string Escape(string segment) => Uri.EscapeDataString(segment);

    /// <summary>
    /// Retry only what is safe to retry. 429 and 5xx are transient by definition; a 4xx
    /// is the API saying the request itself is wrong, and retrying only burns rate-limit
    /// budget and delays the error the caller needs to see.
    /// </summary>
    private static bool IsRetryable(int status)
        => status is 429 or 408 || (status >= 500 && status < 600);

    /// <summary>
    /// Exponential with full jitter, so a fleet of workers limited at the same moment does
    /// not retry in lockstep and re-limit itself.
    /// </summary>
    private static TimeSpan Backoff(int attempt, double? retryAfterSeconds)
    {
        if (retryAfterSeconds is { } seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        var ceiling = Math.Min(8.0, 0.25 * Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(Random.Shared.NextDouble() * ceiling);
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);

    private static double ParseRetryAfter(
        HttpResponseMessage response, IReadOnlyDictionary<string, object?> body)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values)
            && double.TryParse(values.FirstOrDefault(), out var seconds)
            && seconds >= 0)
        {
            return seconds;
        }

        // Fall back to the JSON field for routes that predate the headers. Match both
        // long and double: an integral value decodes as long, so testing only for double
        // would silently fall through to the 1-second default.
        if (body.TryGetValue("retryAfterSeconds", out var value))
        {
            return value switch
            {
                long whole => whole,
                double fraction => fraction,
                _ => 1.0,
            };
        }

        return 1.0;
    }

    /// <summary>Disposes the internally created HttpClient, if this client owns one.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }
}
