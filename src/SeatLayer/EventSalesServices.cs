namespace SeatLayer;

/// <summary>Chooses who takes payment for an event.</summary>
public sealed class EventBuyerCheckoutReplaceRequest
{
    /// <summary><c>seatlayer</c> for SeatLayer checkout, or <c>integrator</c> for your own.</summary>
    public required string BuyerCheckout { get; set; }

    /// <summary>Revision returned by the latest buyer-checkout read.</summary>
    public required long ExpectedRevision { get; set; }
}

/// <summary>
/// Assigns one of the organization's connected payment gateways to an event. Assign null to send
/// a field as null; leave a field unassigned to leave it out.
/// </summary>
public sealed class EventPaymentGatewayReplaceRequest
{
    private string? _connectionId;
    private string? _provider;
    private bool _connectionIdSet;
    private bool _providerSet;

    /// <summary>Connected gateway to assign, or null.</summary>
    public string? ConnectionId
    {
        get => _connectionId;
        set
        {
            _connectionId = value;
            _connectionIdSet = true;
        }
    }

    /// <summary>Gateway provider, or null.</summary>
    public string? Provider
    {
        get => _provider;
        set
        {
            _provider = value;
            _providerSet = true;
        }
    }

    internal Dictionary<string, object?> ToBody()
    {
        // Body.Of drops nulls, but an assigned null is sent as null here.
        var body = new Dictionary<string, object?>();
        if (_connectionIdSet)
        {
            body["connectionId"] = _connectionId;
        }

        if (_providerSet)
        {
            body["provider"] = _provider;
        }

        return body;
    }
}

/// <summary>Moves an event's sales to another state.</summary>
public sealed class EventSalesTransitionRequest
{
    /// <summary><c>setup</c>, <c>testable</c>, <c>live</c>, <c>paused</c>, or <c>ended</c>.</summary>
    public required string To { get; set; }

    /// <summary>Revision returned by the latest sales readiness read.</summary>
    public required long ExpectedRevision { get; set; }

    /// <summary>Optional reason recorded with the change.</summary>
    public string? Reason { get; set; }
}

/// <summary>Creates a discount code for an event.</summary>
public sealed class EventDiscountCodeCreateRequest
{
    /// <summary>The code buyers type.</summary>
    public required string Code { get; set; }

    /// <summary><c>percent</c> or <c>amount</c>.</summary>
    public required string Kind { get; set; }

    /// <summary>Percent off, from 1 to 100, for a <c>percent</c> code.</summary>
    public int? Percent { get; set; }

    /// <summary>Amount off in minor currency units, for an <c>amount</c> code.</summary>
    public long? AmountMinor { get; set; }

    /// <summary>When the code starts working, in epoch milliseconds.</summary>
    public long? StartsAt { get; set; }

    /// <summary>When the code stops working, in epoch milliseconds.</summary>
    public long? EndsAt { get; set; }

    /// <summary>How many times the code can be used.</summary>
    public long? MaxUses { get; set; }

    /// <summary>Whether the code works as soon as it is created.</summary>
    public bool? Active { get; set; }
}

/// <summary>
/// Changes or pauses a discount code. Assign null to <see cref="StartsAt"/>,
/// <see cref="EndsAt"/> or <see cref="MaxUses"/> to clear that limit; leave a field unassigned
/// to keep it.
/// </summary>
public sealed class EventDiscountCodeUpdateRequest
{
    private long? _startsAt;
    private long? _endsAt;
    private long? _maxUses;
    private bool _startsAtSet;
    private bool _endsAtSet;
    private bool _maxUsesSet;

    /// <summary>Replacement code, when supplied.</summary>
    public string? Code { get; set; }

    /// <summary><c>percent</c> or <c>amount</c>, when supplied.</summary>
    public string? Kind { get; set; }

    /// <summary>Percent off, from 1 to 100, when supplied.</summary>
    public int? Percent { get; set; }

    /// <summary>Amount off in minor currency units, when supplied.</summary>
    public long? AmountMinor { get; set; }

    /// <summary>Start in epoch milliseconds, or null to remove the start limit.</summary>
    public long? StartsAt
    {
        get => _startsAt;
        set
        {
            _startsAt = value;
            _startsAtSet = true;
        }
    }

    /// <summary>End in epoch milliseconds, or null to remove the end limit.</summary>
    public long? EndsAt
    {
        get => _endsAt;
        set
        {
            _endsAt = value;
            _endsAtSet = true;
        }
    }

    /// <summary>Use limit, or null to remove it.</summary>
    public long? MaxUses
    {
        get => _maxUses;
        set
        {
            _maxUses = value;
            _maxUsesSet = true;
        }
    }

    /// <summary>Whether the code works, when supplied.</summary>
    public bool? Active { get; set; }

    /// <summary><c>active</c> or <c>paused</c>, when supplied.</summary>
    public string? State { get; set; }

    internal Dictionary<string, object?> ToBody()
    {
        // Body.Of drops nulls, but an assigned null removes a limit here.
        var body = Body.Of(
            ("code", Code), ("kind", Kind), ("percent", Percent), ("amountMinor", AmountMinor),
            ("active", Active), ("state", State));
        if (_startsAtSet)
        {
            body["startsAt"] = _startsAt;
        }

        if (_endsAtSet)
        {
            body["endsAt"] = _endsAt;
        }

        if (_maxUsesSet)
        {
            body["maxUses"] = _maxUses;
        }

        return body;
    }
}

// Event sales setup: who takes payment, the payment gateway, sales readiness and state, the
// booking fee, and discount codes. The rest of EventsService is in Services.cs.
public sealed partial class EventsService
{
    /// <summary>Retrieves who takes payment for an event, and what blocks a change.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveEventBuyerCheckoutAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/buyer-checkout", null, cancellationToken);

    /// <summary>
    /// Chooses SeatLayer checkout or your own for an event. The expected revision stops concurrent
    /// changes overwriting each other; the call is single-attempt.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReplaceEventBuyerCheckoutAsync(
        string eventKey, EventBuyerCheckoutReplaceRequest request,
        CancellationToken cancellationToken = default)
        => _client.PutAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/buyer-checkout",
            Body.Of(
                ("buyerCheckout", request.BuyerCheckout),
                ("expectedRevision", request.ExpectedRevision)),
            cancellationToken);

    /// <summary>
    /// Retrieves an event's payment gateway and the ones it could use, optionally checked against
    /// another currency.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveEventPaymentGatewayAsync(
        string eventKey, string? currency = null, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/payment-gateway",
            new Dictionary<string, string?> { ["currency"] = currency },
            cancellationToken);

    /// <summary>
    /// Assigns one of the organization's connected gateways to an event. Single-attempt: the API
    /// has no replay contract for it.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReplaceEventPaymentGatewayAsync(
        string eventKey, EventPaymentGatewayReplaceRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.PutAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/payment-gateway",
            request?.ToBody() ?? new Dictionary<string, object?>(),
            cancellationToken);

    /// <summary>Retrieves what an event still needs before its next sales step.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveEventSalesReadinessAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/sales/readiness", null, cancellationToken);

    /// <summary>Records a test order as the event's sales preflight. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> PreflightEventSalesAsync(
        string eventKey, string orderId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/sales/preflight",
            Body.Of(("orderId", orderId)),
            null,
            cancellationToken);

    /// <summary>
    /// Moves an event's sales to testable, live, paused or ended. The expected revision stops
    /// concurrent changes overwriting each other; the call is single-attempt.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> TransitionEventSalesAsync(
        string eventKey, EventSalesTransitionRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/sales/transition",
            Body.Of(
                ("to", request.To),
                ("expectedRevision", request.ExpectedRevision),
                ("reason", request.Reason)),
            null,
            cancellationToken);

    /// <summary>Retrieves an event's booking fee.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveEventBookingFeeAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/booking-fee", null, cancellationToken);

    /// <summary>
    /// Sets an event's booking fee in minor currency units, or clears it with null. Single-attempt.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReplaceEventBookingFeeAsync(
        string eventKey, long? bookingFeeMinor, CancellationToken cancellationToken = default)
    {
        // Body.Of drops nulls, but null is the clear instruction here.
        var body = new Dictionary<string, object?> { ["bookingFeeMinor"] = bookingFeeMinor };
        return _client.PutAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/booking-fee", body, cancellationToken);
    }

    /// <summary>Lists an event's discount codes.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListEventDiscountCodesAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/discount-codes", null, cancellationToken);

    /// <summary>Creates a discount code for an event. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateEventDiscountCodeAsync(
        string eventKey, EventDiscountCodeCreateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/discount-codes",
            Body.Of(
                ("code", request.Code),
                ("kind", request.Kind),
                ("percent", request.Percent),
                ("amountMinor", request.AmountMinor),
                ("startsAt", request.StartsAt),
                ("endsAt", request.EndsAt),
                ("maxUses", request.MaxUses),
                ("active", request.Active)),
            null,
            cancellationToken);

    /// <summary>Changes or pauses a discount code. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateEventDiscountCodeAsync(
        string eventKey, string codeId, EventDiscountCodeUpdateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PatchAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/discount-codes/{SeatLayerClient.Escape(codeId)}",
            request.ToBody(),
            cancellationToken);

    /// <summary>Ends a discount code for good. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> EndEventDiscountCodeAsync(
        string eventKey, string codeId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/discount-codes/{SeatLayerClient.Escape(codeId)}/end",
            null,
            null,
            cancellationToken);

    /// <summary>
    /// Deletes a discount code nobody has used. The API answers 204 with no body, so this returns
    /// nothing. Single-attempt.
    /// </summary>
    public async Task DeleteEventDiscountCodeAsync(
        string eventKey, string codeId, CancellationToken cancellationToken = default)
        => await _client.DeleteAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/discount-codes/{SeatLayerClient.Escape(codeId)}",
            cancellationToken).ConfigureAwait(false);
}
