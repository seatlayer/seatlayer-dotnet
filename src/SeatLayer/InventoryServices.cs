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

    /// <summary>Optional caller key forwarded to the API; it does not enable automatic retries.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>Private allocation channels whose inventory this sale may use.</summary>
    public IEnumerable<string>? ChannelIds { get; set; }

    /// <summary>Explicit privileged override for allocation restrictions.</summary>
    public bool? IgnoreChannelRestrictions { get; set; }

    /// <summary>Audit reason for channel use or a privileged override.</summary>
    public string? Reason { get; set; }
}

/// <summary>Full public hold request, including variable-capacity selections.</summary>
public sealed class HoldRequest
{
    /// <summary>Simple object labels to hold.</summary>
    public IEnumerable<string>? Labels { get; set; }

    /// <summary>Tier and quantity-aware selections.</summary>
    public IEnumerable<IDictionary<string, object?>>? Selections { get; set; }

    /// <summary>Checkout lifetime override.</summary>
    public long? TtlMs { get; set; }

    /// <summary>Active hold to atomically replace.</summary>
    public string? ReplaceHoldId { get; set; }

    /// <summary>Private channels whose inventory may be used.</summary>
    public IEnumerable<string>? ChannelIds { get; set; }

    /// <summary>Explicit privileged allocation override.</summary>
    public bool? IgnoreChannelRestrictions { get; set; }

    /// <summary>Audit reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional caller key; it does not enable automatic retries.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Full booking request for a hold, labels, or both.</summary>
public sealed class BookRequest
{
    /// <summary>Active hold to convert into a booking.</summary>
    public string? HoldId { get; set; }

    /// <summary>Labels to book directly or alongside the hold.</summary>
    public IEnumerable<string>? Labels { get; set; }

    /// <summary>Required stable sale reference.</summary>
    public required string BookingRef { get; set; }

    /// <summary>Private channels whose inventory may be used.</summary>
    public IEnumerable<string>? ChannelIds { get; set; }

    /// <summary>Explicit privileged allocation override.</summary>
    public bool? IgnoreChannelRestrictions { get; set; }

    /// <summary>Audit reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional caller key; it does not enable automatic retries.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Filters and paging for booking lifecycle records.</summary>
public sealed class BookingListRequest
{
    /// <summary>Searches stable booking references.</summary>
    public string? Query { get; set; }

    /// <summary>Restricts results to one lifecycle state.</summary>
    public string? State { get; set; }

    /// <summary>Page size.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["q"] = Query,
        ["state"] = State,
        ["limit"] = Limit?.ToString(),
        ["cursor"] = Cursor,
    };
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
        CancellationToken cancellationToken = default,
        IEnumerable<string>? channelIds = null,
        bool? ignoreChannelRestrictions = null,
        string? reason = null)
        => _client.PostAsync(
            Path(eventKey, "/hold"),
            Body.Of(
                ("labels", labels.ToList()), ("ttlMs", ttlMs),
                ("channelIds", channelIds?.ToList()),
                ("ignoreChannelRestrictions", ignoreChannelRestrictions), ("reason", reason)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Reserves labels or tier/quantity-aware selections.</summary>
    public Task<IReadOnlyDictionary<string, object?>> HoldAsync(
        string eventKey, HoldRequest request, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/hold"),
            Body.Of(
                ("labels", request.Labels?.ToList()),
                ("selections", request.Selections?.ToList()),
                ("ttlMs", request.TtlMs),
                ("replaceHoldId", request.ReplaceHoldId),
                ("channelIds", request.ChannelIds?.ToList()),
                ("ignoreChannelRestrictions", request.IgnoreChannelRestrictions),
                ("reason", request.Reason)),
            request.IdempotencyKey,
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
                ("zoneId", request.ZoneId), ("ttlMs", request.TtlMs),
                ("channelIds", request.ChannelIds?.ToList()),
                ("ignoreChannelRestrictions", request.IgnoreChannelRestrictions),
                ("reason", request.Reason)),
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
                ("qty", request.Qty), ("bookingRef", NormalizeBookingRef(request.BookingRef)),
                ("categoryKey", request.CategoryKey), ("zoneId", request.ZoneId),
                ("channelIds", request.ChannelIds?.ToList()),
                ("ignoreChannelRestrictions", request.IgnoreChannelRestrictions),
                ("reason", request.Reason)),
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
        string eventKey, string holdId, long? ttlMs = null, CancellationToken cancellationToken = default,
        IEnumerable<string>? channelIds = null,
        bool? ignoreChannelRestrictions = null,
        string? reason = null)
        => _client.PostAsync(
            Path(eventKey, "/extend"),
            Body.Of(
                ("holdId", holdId), ("ttlMs", ttlMs), ("channelIds", channelIds?.ToList()),
                ("ignoreChannelRestrictions", ignoreChannelRestrictions), ("reason", reason)),
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
        string eventKey, string holdId, string bookingRef,
        string? idempotencyKey = null, CancellationToken cancellationToken = default,
        IEnumerable<string>? channelIds = null,
        bool? ignoreChannelRestrictions = null,
        string? reason = null)
        => _client.PostAsync(
            Path(eventKey, "/book"),
            Body.Of(
                ("holdId", holdId), ("bookingRef", NormalizeBookingRef(bookingRef)),
                ("channelIds", channelIds?.ToList()),
                ("ignoreChannelRestrictions", ignoreChannelRestrictions), ("reason", reason)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Books a hold, labels, or both using the complete public request.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BookAsync(
        string eventKey, BookRequest request, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/book"),
            Body.Of(
                ("holdId", request.HoldId), ("labels", request.Labels?.ToList()),
                ("bookingRef", NormalizeBookingRef(request.BookingRef)),
                ("channelIds", request.ChannelIds?.ToList()),
                ("ignoreChannelRestrictions", request.IgnoreChannelRestrictions),
                ("reason", request.Reason)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Books labels outright, with no prior hold.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BookLabelsAsync(
        string eventKey, IEnumerable<string> labels, string bookingRef,
        string? idempotencyKey = null, CancellationToken cancellationToken = default,
        IEnumerable<string>? channelIds = null,
        bool? ignoreChannelRestrictions = null,
        string? reason = null)
        => _client.PostAsync(
            Path(eventKey, "/book"),
            Body.Of(
                ("labels", labels.ToList()), ("bookingRef", NormalizeBookingRef(bookingRef)),
                ("channelIds", channelIds?.ToList()),
                ("ignoreChannelRestrictions", ignoreChannelRestrictions), ("reason", reason)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Books named objects as a box-office sale.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BoxOfficeBookAsync(
        string eventKey, IEnumerable<string> labels, string bookingRef,
        string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/box-book"),
            Body.Of(("labels", labels.ToList()), ("bookingRef", NormalizeBookingRef(bookingRef))),
            idempotencyKey,
            cancellationToken);

    /// <summary>Reverses a booking. Requires a key with cancel authority.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UnbookAsync(
        string eventKey, IEnumerable<string> labels, string bookingRef,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/unbook"),
            Body.Of(("labels", labels.ToList()), ("bookingRef", NormalizeBookingRef(bookingRef))),
            null,
            cancellationToken);

    /// <summary>Holds inventory back from sale (house seats, production holds).</summary>
    public Task<IReadOnlyDictionary<string, object?>> BlockAsync(
        string eventKey, IEnumerable<string> labels, CancellationToken cancellationToken = default,
        long? releaseAt = null)
        => _client.PostAsync(
            Path(eventKey, "/block"),
            Body.Of(("labels", labels.ToList()), ("releaseAt", releaseAt)),
            null,
            cancellationToken);

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
        string eventKey, IDictionary<string, object?> rules, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, "/availability"), Body.Of(("rules", rules)), null, cancellationToken);

    /// <summary>Lists one page of booking lifecycle records, newest first.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListBookingsAsync(
        string eventKey,
        BookingListRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(eventKey, "/bookings"),
            (request ?? new BookingListRequest()).ToQuery(),
            cancellationToken);

    /// <summary>Retrieves a booking lifecycle by its stable reference.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveBookingAsync(
        string eventKey, string bookingRef, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(eventKey, $"/bookings/{SeatLayerClient.Escape(NormalizeBookingRef(bookingRef))}"),
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

/// <summary>Full authority and feature request for a designer session.</summary>
public sealed class DesignerSessionRequest
{
    /// <summary>Workspace containing the chart.</summary>
    public required string WorkspaceId { get; set; }

    /// <summary>Chart to open.</summary>
    public required string ChartId { get; set; }

    /// <summary>Exact browser origin allowed to consume the token.</summary>
    public required string AllowedOrigin { get; set; }

    /// <summary>read-only, edit, or publish.</summary>
    public string? Authority { get; set; }

    /// <summary>Legacy publication flag, reconciled by the server with Authority.</summary>
    public bool? CanPublish { get; set; }

    /// <summary>normal or safe.</summary>
    public string? Mode { get; set; }

    /// <summary>Safe-mode editing restrictions.</summary>
    public IDictionary<string, bool>? SafeModeOptions { get; set; }

    /// <summary>Requested feature policy inputs.</summary>
    public IDictionary<string, object?>? Features { get; set; }

    /// <summary>Requested lifetime from 300 to 14,400 seconds.</summary>
    public int? ExpiresInSeconds { get; set; }
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

    /// <summary>Capability granting read-only channel visibility.</summary>
    public const string CapabilityChannelsView = "event:channels:view";
    /// <summary>Capability granting channel administration.</summary>
    public const string CapabilityChannelsManage = "event:channels:manage";
    /// <summary>Capability granting order reads.</summary>
    public const string CapabilityOrdersRead = "event:orders:read";
    /// <summary>Capability granting refunds.</summary>
    public const string CapabilityRefund = "event:refund";
    /// <summary>Capability granting ticket delivery.</summary>
    public const string CapabilityTicketsSend = "event:tickets:send";
    /// <summary>Capability granting door reads.</summary>
    public const string CapabilityDoorView = "event:door:view";
    /// <summary>Capability granting check-in writes.</summary>
    public const string CapabilityDoorCheckin = "event:door:checkin";
    /// <summary>Capability granting box-office actions.</summary>
    public const string CapabilityBoxOffice = "event:boxoffice";

    private readonly SeatLayerClient _client;

    internal SessionsService(SeatLayerClient client) => _client = client;

    /// <summary>
    /// Mints a manage-session token for the control room.
    /// </summary>
    /// <remarks>
    /// <paramref name="capabilities"/> is required here even though the API defaults
    /// omission to <c>event:view</c>. Making the grant explicit keeps browser authority
    /// reviewable and prevents future server defaults from changing client intent.
    /// </remarks>
    /// <exception cref="ArgumentException">No capabilities were supplied.</exception>
    public Task<IReadOnlyDictionary<string, object?>> CreateManageSessionAsync(
        string eventKey,
        string allowedOrigin,
        IEnumerable<string> capabilities,
        int? expiresInSeconds = null,
        CancellationToken cancellationToken = default,
        string? workspaceId = null)
    {
        var granted = capabilities?.ToList() ?? new List<string>();
        if (granted.Count == 0)
        {
            throw new ArgumentException(
                "capabilities is required: pass the smallest explicit set the page needs, e.g. "
                + "[SessionsService.CapabilityView].",
                nameof(capabilities));
        }

        return _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/manage-sessions",
            Body.Of(
                ("allowedOrigin", allowedOrigin), ("capabilities", granted),
                ("expiresInSeconds", expiresInSeconds), ("workspaceId", workspaceId)),
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

    /// <summary>Mints a designer token with the complete safe-mode and feature request.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateDesignerSessionAsync(
        DesignerSessionRequest request, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            "/v1/designer/sessions",
            Body.Of(
                ("workspaceId", request.WorkspaceId), ("chartId", request.ChartId),
                ("allowedOrigin", request.AllowedOrigin), ("authority", request.Authority),
                ("canPublish", request.CanPublish), ("mode", request.Mode),
                ("safeModeOptions", request.SafeModeOptions), ("features", request.Features),
                ("expiresInSeconds", request.ExpiresInSeconds)),
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

    /// <summary>Lists recent delivery attempts with public status and time filters.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListDeliveriesAsync(
        string webhookId,
        int? limit,
        string? status,
        long? before,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/webhooks/{SeatLayerClient.Escape(webhookId)}/deliveries",
            new Dictionary<string, string?>
            {
                ["limit"] = limit?.ToString(),
                ["status"] = status,
                ["before"] = before?.ToString(),
            },
            cancellationToken);
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
        => _client.PostHeaderReplayAsync(
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
