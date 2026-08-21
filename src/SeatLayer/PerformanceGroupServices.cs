namespace SeatLayer;

/// <summary>Filters and paging for fixed multi-performance runs.</summary>
public sealed class PerformanceGroupListRequest
{
    /// <summary>Restrict results to one workspace.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Find a run by your own external reference.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>Restrict results to a lifecycle state.</summary>
    public string? State { get; set; }

    /// <summary>Page size, capped by the API.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["workspaceId"] = WorkspaceId,
        ["externalRef"] = ExternalRef,
        ["state"] = State,
        ["limit"] = Limit?.ToString(),
        ["cursor"] = Cursor,
    };
}

/// <summary>Creates one fixed run from two to eight compatible assigned-seat events.</summary>
public sealed class PerformanceGroupCreateRequest
{
    /// <summary>Operator-facing name for the run.</summary>
    public required string Name { get; set; }

    /// <summary>Ordered event keys for the run.</summary>
    public required IEnumerable<string> EventKeys { get; set; }

    /// <summary>Your stable reference for this run.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>Optional caller key for the route's exact server-side response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Required optimistic-concurrency revision for activation or closing.</summary>
public sealed class PerformanceGroupLifecycleRequest
{
    /// <summary>Revision returned by the latest group read.</summary>
    public required long ExpectedRevision { get; set; }
}

/// <summary>Security boundaries for a one-time Performance Group browser bearer.</summary>
public sealed class PerformanceGroupBuyerAccessSessionRequest
{
    /// <summary>Exact browser origin allowed to consume the revealed token.</summary>
    public required string AllowedOrigin { get; set; }

    /// <summary>Whether public inventory is visible alongside private allocations.</summary>
    public bool IncludePublic { get; set; }

    /// <summary>Private channel ids allowed for each event key in the group.</summary>
    public IDictionary<string, IEnumerable<string>>? ChannelIdsByEvent { get; set; }

    /// <summary>Requested token lifetime in seconds.</summary>
    public int? ExpiresInSeconds { get; set; }

    /// <summary>Maximum quantity this buyer may select.</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>Your buyer reference.</summary>
    public string? BuyerRef { get; set; }

    /// <summary>Your partner reference.</summary>
    public string? PartnerRef { get; set; }
}

/// <summary>Limit for the newest Performance Group buyer-session records.</summary>
public sealed class PerformanceGroupBuyerAccessSessionListRequest
{
    /// <summary>Page size, capped at 100.</summary>
    public int? Limit { get; set; }
}

/// <summary>Stable identifiers that confirm external payment for a group hold.</summary>
public sealed class PerformanceGroupBookRequest
{
    /// <summary>Stable action id for this book attempt.</summary>
    public required string BookActionId { get; set; }

    /// <summary>Stable reference from your payment or order system.</summary>
    public required string BookingRef { get; set; }
}

/// <summary>
/// Fixed multi-performance run lifecycle, browser access, and confirmed booking.
/// </summary>
/// <remarks>
/// This is a server-only workflow. Mint a buyer token here, pass the revealed value to
/// <c>PerformanceGroupPicker</c> in the browser SDK, inspect the trusted hold here, then
/// charge externally and confirm the hold with a stable action id and booking reference.
/// </remarks>
public sealed class PerformanceGroupsService
{
    private readonly SeatLayerClient _client;

    internal PerformanceGroupsService(SeatLayerClient client) => _client = client;

    private static string Path(string performanceGroupKey, string suffix = "")
        => $"/v1/performance-groups/{SeatLayerClient.Escape(performanceGroupKey)}{suffix}";

    /// <summary>Lists fixed multi-performance runs.</summary>
    public async Task<Page> ListAsync(
        PerformanceGroupListRequest? request = null, CancellationToken cancellationToken = default)
        => Page.From(
            await _client.GetAsync(
                "/v1/performance-groups", (request ?? new()).ToQuery(), cancellationToken)
                .ConfigureAwait(false),
            "performanceGroups");

    /// <summary>Creates a draft run with exact header-replay idempotency.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        PerformanceGroupCreateRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            "/v1/performance-groups",
            Body.Of(
                ("name", request.Name), ("eventKeys", request.EventKeys.ToList()),
                ("externalRef", request.ExternalRef)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Retrieves a fixed run and its ordered performances.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAsync(
        string performanceGroupKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(performanceGroupKey), null, cancellationToken);

    /// <summary>Deletes an unused draft run. Activated runs retain their audit identity.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string performanceGroupKey, CancellationToken cancellationToken = default)
        => _client.DeleteAsync(Path(performanceGroupKey), cancellationToken);

    /// <summary>
    /// Activates a fixed run. A 202 response is in progress; poll
    /// <see cref="RetrieveLifecycleAsync(string, string, CancellationToken)"/> until terminal.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ActivateAsync(
        string performanceGroupKey,
        PerformanceGroupLifecycleRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(performanceGroupKey, "/activate"),
            Body.Of(("expectedRevision", request.ExpectedRevision)),
            null,
            cancellationToken);

    /// <summary>
    /// Stops new group sales. A 202 response is in progress; poll
    /// <see cref="RetrieveLifecycleAsync(string, string, CancellationToken)"/> until terminal.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CloseAsync(
        string performanceGroupKey,
        PerformanceGroupLifecycleRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(performanceGroupKey, "/close"),
            Body.Of(("expectedRevision", request.ExpectedRevision)),
            null,
            cancellationToken);

    /// <summary>Returns the lifecycle operation for activation or closing.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveLifecycleAsync(
        string performanceGroupKey,
        string operationId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(performanceGroupKey, $"/lifecycle/{SeatLayerClient.Escape(operationId)}"),
            null,
            cancellationToken);

    /// <summary>
    /// Creates and reveals a one-time, origin-bound browser bearer. This call intentionally
    /// remains single-attempt: retrying could lose the only reveal of a valid token.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateBuyerAccessSessionAsync(
        string performanceGroupKey,
        PerformanceGroupBuyerAccessSessionRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(performanceGroupKey, "/buyer-access-sessions"),
            Body.Of(
                ("allowedOrigin", request.AllowedOrigin), ("includePublic", request.IncludePublic),
                ("channelIdsByEvent", request.ChannelIdsByEvent?.ToDictionary(
                    pair => pair.Key, pair => pair.Value.ToList())),
                ("expiresInSeconds", request.ExpiresInSeconds), ("maxQuantity", request.MaxQuantity),
                ("buyerRef", request.BuyerRef), ("partnerRef", request.PartnerRef)),
            null,
            cancellationToken);

    /// <summary>Lists buyer-session metadata; the bearer value is never returned again.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListBuyerAccessSessionsAsync(
        string performanceGroupKey,
        PerformanceGroupBuyerAccessSessionListRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(performanceGroupKey, "/buyer-access-sessions"),
            new Dictionary<string, string?> { ["limit"] = request?.Limit?.ToString() },
            cancellationToken);

    /// <summary>Revokes a browser bearer before it expires.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeBuyerAccessSessionAsync(
        string performanceGroupKey,
        string sessionId,
        CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            Path(performanceGroupKey, $"/buyer-access-sessions/{SeatLayerClient.Escape(sessionId)}"),
            cancellationToken);

    /// <summary>Returns the trusted server projection of a buyer-created group hold.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveHoldAsync(
        string performanceGroupKey,
        string operationId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(performanceGroupKey, $"/holds/{SeatLayerClient.Escape(operationId)}"),
            null,
            cancellationToken);

    /// <summary>
    /// Confirms external payment for a committed hold. A 202 response is in progress; poll
    /// <see cref="RetrieveBookingAsync(string, string, CancellationToken)"/> until terminal.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> BookHoldAsync(
        string performanceGroupKey,
        string operationId,
        PerformanceGroupBookRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(performanceGroupKey, $"/holds/{SeatLayerClient.Escape(operationId)}/book"),
            Body.Of(
                ("bookActionId", request.BookActionId),
                ("bookingRef", NormalizeBookingRef(request.BookingRef))),
            null,
            cancellationToken);

    /// <summary>Returns a group booking action and its terminal or pending state.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveBookingAsync(
        string performanceGroupKey,
        string actionId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(performanceGroupKey, $"/bookings/{SeatLayerClient.Escape(actionId)}"),
            null,
            cancellationToken);

    private static string NormalizeBookingRef(string? bookingRef)
    {
        var value = bookingRef?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException(
                "bookingRef is required and must be a non-empty stable reference.",
                nameof(bookingRef));
        }

        return value;
    }
}
