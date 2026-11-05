namespace SeatLayer;

/// <summary>Filters and cursor paging for orders across events and Seasons.</summary>
public sealed class OrderListRequest
{
    /// <summary>Restrict results to one event.</summary>
    public string? EventKey { get; set; }

    /// <summary>Restrict results to one Season.</summary>
    public string? SeasonKey { get; set; }

    /// <summary>Restrict results to one order status.</summary>
    public string? Status { get; set; }

    /// <summary>Earliest order time, in epoch milliseconds.</summary>
    public long? From { get; set; }

    /// <summary>Latest order time, in epoch milliseconds.</summary>
    public long? To { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Page size, capped by the API.</summary>
    public int? Limit { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["eventKey"] = EventKey,
        ["seasonKey"] = SeasonKey,
        ["status"] = Status,
        ["from"] = From?.ToString(),
        ["to"] = To?.ToString(),
        ["cursor"] = Cursor,
        ["limit"] = Limit?.ToString(),
    };
}

/// <summary>Search filters for one event's orders.</summary>
public class EventOrderFilterRequest
{
    /// <summary>Search text, sent as <c>q</c>.</summary>
    public string? Query { get; set; }

    /// <summary>Restrict results to one order status.</summary>
    public string? Status { get; set; }

    /// <summary>Restrict results by how the order was paid.</summary>
    public string? PaidBy { get; set; }

    /// <summary>Earliest order time, in epoch milliseconds.</summary>
    public long? From { get; set; }

    /// <summary>Latest order time, in epoch milliseconds.</summary>
    public long? To { get; set; }

    internal Dictionary<string, string?> ToFilterQuery() => new()
    {
        ["q"] = Query,
        ["status"] = Status,
        ["paidBy"] = PaidBy,
        ["from"] = From?.ToString(),
        ["to"] = To?.ToString(),
    };
}

/// <summary>Search filters and cursor paging for one event's orders.</summary>
public sealed class EventOrderListRequest : EventOrderFilterRequest
{
    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Page size, capped by the API.</summary>
    public int? Limit { get; set; }

    internal Dictionary<string, string?> ToQuery()
    {
        var query = ToFilterQuery();
        query["cursor"] = Cursor;
        query["limit"] = Limit?.ToString();
        return query;
    }
}

/// <summary>Refunds an event or Season order through its payment provider.</summary>
public sealed class OrderRefundRequest
{
    /// <summary>Amount to refund in minor currency units; the whole order when omitted.</summary>
    public long? AmountMinor { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Cancels an event or Season order without moving money.</summary>
public sealed class OrderCancelRequest
{
    /// <summary><c>lost_dispute</c>, <c>refunded_elsewhere</c>, or <c>other</c>.</summary>
    public required string Reason { get; set; }

    /// <summary>Operator note, up to 500 characters.</summary>
    public string? Note { get; set; }

    /// <summary>Confirms any money was settled outside SeatLayer; needed for a paid order.</summary>
    public bool? RefundConfirmed { get; set; }

    /// <summary>Acknowledges tickets that were already used; needed when any were.</summary>
    public bool? UsedTicketsAcknowledged { get; set; }

    /// <summary>Whether to email the buyer about the cancellation.</summary>
    public bool? NotifyBuyer { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }

    internal Dictionary<string, object?> ToBody()
        => Body.Of(
            ("reason", Reason),
            ("note", Note),
            ("refundConfirmed", RefundConfirmed),
            ("usedTicketsAcknowledged", UsedTicketsAcknowledged),
            ("notifyBuyer", NotifyBuyer));
}

/// <summary>
/// Orders across events and Seasons, and one event's orders with refunds, cancellations and
/// ticket emails.
/// </summary>
/// <remarks>
/// Refunds and cancellations use exact header replay and retry with one stable key. Resending
/// tickets is single-attempt.
/// </remarks>
public sealed class OrdersService
{
    private readonly SeatLayerClient _client;

    internal OrdersService(SeatLayerClient client) => _client = client;

    /// <summary>Lists orders across events and Seasons.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListOrdersAsync(
        OrderListRequest? request = null, CancellationToken cancellationToken = default)
        => _client.GetAsync("/v1/orders", (request ?? new()).ToQuery(), cancellationToken);

    /// <summary>Retrieves an order and its lines.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveOrderAsync(
        string orderId, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/orders/{SeatLayerClient.Escape(orderId)}", null, cancellationToken);

    /// <summary>Lists an order's tickets with scan links.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListOrderTicketsAsync(
        string orderId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/orders/{SeatLayerClient.Escape(orderId)}/tickets", null, cancellationToken);

    /// <summary>Lists one event's orders.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListEventOrdersAsync(
        string eventKey, EventOrderListRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/orders",
            (request ?? new()).ToQuery(),
            cancellationToken);

    /// <summary>Retrieves one event order with its guests and tickets.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveEventOrderAsync(
        string eventKey, string orderId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/orders/{SeatLayerClient.Escape(orderId)}",
            null,
            cancellationToken);

    /// <summary>Downloads one event's orders as CSV, returned as raw text.</summary>
    public Task<string> ExportEventOrdersCsvAsync(
        string eventKey, EventOrderFilterRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetCsvAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/orders.csv",
            request?.ToFilterQuery(),
            cancellationToken);

    /// <summary>Refunds an event order through its payment provider, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RefundEventOrderAsync(
        string eventKey, string orderId, OrderRefundRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/orders/{SeatLayerClient.Escape(orderId)}/refund",
            Body.Of(("amountMinor", request?.AmountMinor)),
            request?.IdempotencyKey,
            cancellationToken);

    /// <summary>Cancels an event order without moving money, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CancelEventOrderAsync(
        string eventKey, string orderId, OrderCancelRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/orders/{SeatLayerClient.Escape(orderId)}/cancel",
            request.ToBody(),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Emails an event order's tickets again. Single-attempt: the API has no replay contract
    /// for it.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ResendEventOrderTicketsAsync(
        string eventKey, string orderId, string? email = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/orders/{SeatLayerClient.Escape(orderId)}/resend",
            Body.Of(("email", email)),
            null,
            cancellationToken);
}
