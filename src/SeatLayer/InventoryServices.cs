namespace SeatLayer;

/// <summary>Asks SeatLayer to choose the objects.</summary>
public sealed class BestAvailableRequest
{
    /// <summary>How many to pick. Clamped to the server maximum rather than rejected.</summary>
    public int Qty { get; set; }

    /// <summary>Restrict to one price category.</summary>
    public string? CategoryKey { get; set; }

    /// <summary>Restrict to one zone. An unknown zone answers 422, never silently ignored.</summary>
    public string? ZoneId { get; set; }

    /// <summary>Overrides the event's checkout window. Ignored when booking outright.</summary>
    public long? TtlMs { get; set; }

    /// <summary>Required when booking outright, so the sale can be reconciled.</summary>
    public string? BookingRef { get; set; }

    /// <summary>Makes a retried call collapse into the original.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Holds, booking, blocking and availability.
/// </summary>
/// <remarks>
/// <para>Two complete flows, both first-class:</para>
/// <code>
/// browser holds → RetrieveHoldAsync for authoritative pricing → charge → BookAsync(holdId)
/// backend books labels directly — box office, phone sales, comps
/// </code>
/// <para>
/// Never price from what the browser tells you. <see cref="RetrieveHoldAsync"/> is the
/// authoritative answer, which is why it is a separate call.
/// </para>
/// </remarks>
public sealed class InventoryService
{
    private readonly SeatLayerClient _client;

    internal InventoryService(SeatLayerClient client) => _client = client;

    private static string Path(string eventKey, string suffix)
        => $"/v1/events/{SeatLayerClient.Escape(eventKey)}{suffix}";

    /// <summary>Reserves the named objects.</summary>
    public Task<IReadOnlyDictionary<string, object?>> HoldAsync(
        string eventKey,
        IEnumerable<string> labels,
        long? ttlMs = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/hold"),
            Body.Of(("labels", labels.ToList()), ("ttlMs", ttlMs)),
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Picks the best free objects and holds them.
    /// </summary>
    /// <remarks>
    /// The picker is the one the buyer widget uses, so a phone order and a web order get
    /// the same answer for the same inventory.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> HoldBestAvailableAsync(
        string eventKey, BestAvailableRequest request, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/best-available"),
            Body.Of(
                ("qty", request.Qty), ("categoryKey", request.CategoryKey),
                ("zoneId", request.ZoneId), ("ttlMs", request.TtlMs)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Picks and books in one call — the box-office shape.
    /// </summary>
    /// <remarks>
    /// Prefer this over hold-then-book when payment is already taken: a failure between
    /// two calls would strand inventory until the TTL expired.
    /// </remarks>
    /// <exception cref="ArgumentException"><c>BookingRef</c> was not supplied.</exception>
    public Task<IReadOnlyDictionary<string, object?>> BookBestAvailableAsync(
        string eventKey, BestAvailableRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BookingRef))
        {
            // Caught here rather than as a 400 after a round-trip.
            throw new ArgumentException(
                "BookingRef is required when booking best available, so the sale can be reconciled.",
                nameof(request));
        }

        return _client.PostAsync(
            Path(eventKey, "/best-available-book"),
            Body.Of(
                ("qty", request.Qty), ("bookingRef", request.BookingRef),
                ("categoryKey", request.CategoryKey), ("zoneId", request.ZoneId)),
            request.IdempotencyKey,
            cancellationToken);
    }

    /// <summary>
    /// Pushes an active hold's expiry out by a fresh window before it lapses.
    /// </summary>
    /// <remarks>
    /// Use this rather than release-and-re-hold when an order takes longer than the
    /// checkout window — invoiced sales, a phone order on hold. Releasing first hands the
    /// seats to whoever is racing for them in between. A hold that is gone, expired, or at
    /// its renewal cap answers 409 <c>cannot_extend</c>.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> ExtendHoldAsync(
        string eventKey, string holdId, long? ttlMs = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/extend"),
            Body.Of(("holdId", holdId), ("ttlMs", ttlMs)),
            null,
            cancellationToken);

    /// <summary>Authoritative items and prices. Charge from this, not the browser.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveHoldAsync(
        string eventKey, string holdId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(eventKey, $"/holds/{SeatLayerClient.Escape(holdId)}"), null, cancellationToken);

    /// <summary>Frees held objects before the TTL expires.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ReleaseAsync(
        string eventKey, IEnumerable<string> labels, string holdId,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/release"),
            Body.Of(("labels", labels.ToList()), ("holdId", holdId)),
            null,
            cancellationToken);

    /// <summary>Books a previously held selection.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BookAsync(
        string eventKey, string holdId, string? bookingRef = null,
        string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/book"),
            Body.Of(("holdId", holdId), ("bookingRef", bookingRef)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Books labels outright, with no prior hold.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BookLabelsAsync(
        string eventKey, IEnumerable<string> labels, string bookingRef,
        string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/book"),
            Body.Of(("labels", labels.ToList()), ("bookingRef", bookingRef)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Books named objects as a box-office sale.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BoxOfficeBookAsync(
        string eventKey, IEnumerable<string> labels, string bookingRef,
        string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/box-book"),
            Body.Of(("labels", labels.ToList()), ("bookingRef", bookingRef)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Reverses a booking. Requires a key with cancel authority.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UnbookAsync(
        string eventKey, IEnumerable<string> labels, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/unbook"), Body.Of(("labels", labels.ToList())), null, cancellationToken);

    /// <summary>Holds inventory back from sale (house seats, production holds).</summary>
    public Task<IReadOnlyDictionary<string, object?>> BlockAsync(
        string eventKey, IEnumerable<string> labels, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/block"), Body.Of(("labels", labels.ToList())), null, cancellationToken);

    /// <summary>Returns blocked objects to sale.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UnblockAsync(
        string eventKey, IEnumerable<string> labels, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/unblock"), Body.Of(("labels", labels.ToList())), null, cancellationToken);

    /// <summary>Returns every blocked object in an event to sale.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UnblockAllAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(Path(eventKey, "/unblock-all"), null, null, cancellationToken);

    /// <summary>Reads per-object availability rules.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAvailabilityAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(eventKey, "/availability"), null, cancellationToken);

    /// <summary>Replaces per-object availability rules.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAvailabilityAsync(
        string eventKey, IDictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _client.PostAsync(Path(eventKey, "/availability"), fields, null, cancellationToken);
}

/// <summary>
/// Short-lived, origin-bound browser tokens.
/// </summary>
/// <remarks>
/// The governing rule: the SDK mints tokens, widgets consume them. Your secret key never
/// reaches a browser.
/// </remarks>
public sealed class SessionsService
{
    /// <summary>Capability granting read access to the control room.</summary>
    public const string CapabilityView = "event:view";

    /// <summary>Capability granting the ability to block and unblock inventory.</summary>
    public const string CapabilityBlock = "event:block";

    /// <summary>Capability granting the ability to reverse paid bookings.</summary>
    public const string CapabilityCancel = "event:cancel";

    /// <summary>Capability granting access to reports and the audit log.</summary>
    public const string CapabilityReports = "event:reports";

    private readonly SeatLayerClient _client;

    internal SessionsService(SeatLayerClient client) => _client = client;

    /// <summary>
    /// Mints a manage-session token for the control room.
    /// </summary>
    /// <remarks>
    /// <paramref name="capabilities"/> is required here even though the API defaults it.
    /// That default grants all four — including <c>event:cancel</c>, which un-books paid
    /// inventory. Granting the ability to reverse sales by forgetting an argument is not a
    /// default worth inheriting.
    /// </remarks>
    /// <exception cref="ArgumentException">No capabilities were supplied.</exception>
    public Task<IReadOnlyDictionary<string, object?>> CreateManageSessionAsync(
        string eventKey,
        string allowedOrigin,
        IEnumerable<string> capabilities,
        int? expiresInSeconds = null,
        CancellationToken cancellationToken = default)
    {
        var granted = capabilities?.ToList() ?? new List<string>();
        if (granted.Count == 0)
        {
            throw new ArgumentException(
                "capabilities is required: pass the smallest set the page needs, e.g. "
                + "[SessionsService.CapabilityView]. Omitting it server-side grants "
                + "event:cancel, which can reverse paid bookings.",
                nameof(capabilities));
        }

        return _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/manage-sessions",
            Body.Of(
                ("allowedOrigin", allowedOrigin), ("capabilities", granted),
                ("expiresInSeconds", expiresInSeconds)),
            null,
            cancellationToken);
    }

    /// <summary>Revokes a manage token before it expires.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeManageSessionAsync(
        string eventKey, string sessionId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/manage-sessions/{SeatLayerClient.Escape(sessionId)}",
            cancellationToken);

    /// <summary>
    /// Mints a designer token so an organiser can edit a chart inside your own UI.
    /// Requires a chart id that already exists — create or copy one first.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateDesignerSessionAsync(
        string workspaceId,
        string chartId,
        string allowedOrigin,
        string? authority = null,
        string? mode = null,
        int? expiresInSeconds = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            "/v1/designer/sessions",
            Body.Of(
                ("workspaceId", workspaceId), ("chartId", chartId), ("allowedOrigin", allowedOrigin),
                ("authority", authority), ("mode", mode), ("expiresInSeconds", expiresInSeconds)),
            null,
            cancellationToken);

    /// <summary>Revokes a designer token before it expires.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeDesignerSessionAsync(
        string sessionId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            $"/v1/designer/sessions/{SeatLayerClient.Escape(sessionId)}", cancellationToken);
}

/// <summary>Webhook subscription management. To VERIFY a delivery, see <see cref="Webhook"/>.</summary>
public sealed class WebhooksService
{
    private readonly SeatLayerClient _client;

    internal WebhooksService(SeatLayerClient client) => _client = client;

    /// <summary>Lists webhook subscriptions.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListAsync(CancellationToken cancellationToken = default)
        => _client.GetAsync("/v1/webhooks", null, cancellationToken);

    /// <summary>Registers a subscription. The response carries the signing secret once.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        string url, IEnumerable<string> events, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            "/v1/webhooks", Body.Of(("url", url), ("events", events.ToList())), null, cancellationToken);

    /// <summary>Updates a subscription.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        string webhookId, IDictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _client.PatchAsync($"/v1/webhooks/{SeatLayerClient.Escape(webhookId)}", fields, cancellationToken);

    /// <summary>Removes a subscription.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string webhookId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync($"/v1/webhooks/{SeatLayerClient.Escape(webhookId)}", cancellationToken);

    /// <summary>Lists recent delivery attempts for a subscription.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListDeliveriesAsync(
        string webhookId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/webhooks/{SeatLayerClient.Escape(webhookId)}/deliveries", null, cancellationToken);
}

/// <summary>Workspaces isolate one tenant's charts and events from another's.</summary>
public sealed class WorkspacesService
{
    private readonly SeatLayerClient _client;

    internal WorkspacesService(SeatLayerClient client) => _client = client;

    /// <summary>Lists the organisation's workspaces.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListAsync(CancellationToken cancellationToken = default)
        => _client.GetAsync("/v1/workspaces", null, cancellationToken);

    /// <summary>Provisions a workspace, typically one per tenant.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        string name, string? externalRef = null, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            "/v1/workspaces", Body.Of(("name", name), ("externalRef", externalRef)),
            idempotencyKey, cancellationToken);

    /// <summary>Retrieves one workspace.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAsync(
        string workspaceId, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/workspaces/{SeatLayerClient.Escape(workspaceId)}", null, cancellationToken);

    /// <summary>
    /// Renames, re-references, or disables a workspace.
    /// </summary>
    /// <remarks>
    /// The organisation's default workspace cannot be disabled — the API answers 409
    /// <c>default_workspace_required</c>. Promote another one first.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        string workspaceId, IDictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _client.PatchAsync(
            $"/v1/workspaces/{SeatLayerClient.Escape(workspaceId)}", fields, cancellationToken);
}
