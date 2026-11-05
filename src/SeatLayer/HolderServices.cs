namespace SeatLayer;

/// <summary>Filters and cursor paging for the customer list.</summary>
public sealed class CustomerListRequest
{
    /// <summary>Only the customer with this email.</summary>
    public string? Email { get; set; }

    /// <summary>Only the customer linked to this holder reference.</summary>
    public string? HolderRef { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Page size. Defaults to 50 and is capped at 100.</summary>
    public int? Limit { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["email"] = Email,
        ["holderRef"] = HolderRef,
        ["cursor"] = Cursor,
        ["limit"] = Limit?.ToString(),
    };
}

/// <summary>
/// A holder's email, name or phone. Assign null to clear the name or phone; leave a field
/// unassigned to keep its current value.
/// </summary>
public sealed class HolderUpdateRequest
{
    private string? _name;
    private string? _phone;
    private bool _nameSet;
    private bool _phoneSet;

    /// <summary>Replacement email, when supplied.</summary>
    public string? Email { get; set; }

    /// <summary>Replacement name, or null to clear it.</summary>
    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _nameSet = true;
        }
    }

    /// <summary>Replacement phone number, or null to clear it.</summary>
    public string? Phone
    {
        get => _phone;
        set
        {
            _phone = value;
            _phoneSet = true;
        }
    }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }

    internal Dictionary<string, object?> ToBody()
    {
        // Body.Of drops nulls, but an assigned null is the clear instruction here.
        var body = Body.Of(("email", Email));
        if (_nameSet)
        {
            body["name"] = _name;
        }

        if (_phoneSet)
        {
            body["phone"] = _phone;
        }

        return body;
    }
}

/// <summary>Filters and cursor paging for a holder's tickets.</summary>
public sealed class HolderTicketListRequest
{
    /// <summary>Restrict results to one Season.</summary>
    public string? SeasonKey { get; set; }

    /// <summary>Earliest date, in epoch milliseconds.</summary>
    public long? From { get; set; }

    /// <summary>Latest date, in epoch milliseconds.</summary>
    public long? To { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Page size, capped by the API.</summary>
    public int? Limit { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["seasonKey"] = SeasonKey,
        ["from"] = From?.ToString(),
        ["to"] = To?.ToString(),
        ["cursor"] = Cursor,
        ["limit"] = Limit?.ToString(),
    };
}

/// <summary>Sends some of a holder's Season tickets to an email address.</summary>
public sealed class HolderTransferCreateRequest
{
    /// <summary>Tickets to transfer.</summary>
    public required IEnumerable<string> TicketIds { get; set; }

    /// <summary>Recipient email address.</summary>
    public required string ToEmail { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Season ticket holders: the customer behind a holder reference, their Seasons and tickets,
/// ticket transfers, and the customer list.
/// </summary>
/// <remarks>
/// A holder reference is your own reference, as your imports and bookings carry it; a hosted
/// Season purchase uses its order id.
/// </remarks>
public sealed class HoldersService
{
    private readonly SeatLayerClient _client;

    internal HoldersService(SeatLayerClient client) => _client = client;

    private static string Path(string holderRef, string suffix = "")
        => $"/v1/holders/{SeatLayerClient.Escape(holderRef)}{suffix}";

    /// <summary>Lists or finds customers by email or holder reference.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListCustomersAsync(
        CustomerListRequest? request = null, CancellationToken cancellationToken = default)
        => _client.GetAsync("/v1/customers", (request ?? new()).ToQuery(), cancellationToken);

    /// <summary>Retrieves the customer behind a holder reference.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveHolderAsync(
        string holderRef, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(holderRef), null, cancellationToken);

    /// <summary>Sets a holder's email, name or phone, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateHolderAsync(
        string holderRef, HolderUpdateRequest request, CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Patch, Path(holderRef), request.ToBody(), request.IdempotencyKey, cancellationToken);

    /// <summary>
    /// Signs a holder's customer out of the ticket site: every session, or only the one given.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeHolderSessionsAsync(
        string holderRef, string? sessionId = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(holderRef, "/sessions/revoke"),
            Body.Of(("sessionId", sessionId)),
            null,
            cancellationToken);

    /// <summary>Lists the Seasons a holder holds seats in.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListHolderSeasonsAsync(
        string holderRef, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(holderRef, "/seasons"), null, cancellationToken);

    /// <summary>Lists a holder's tickets, date by date.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListHolderTicketsAsync(
        string holderRef, HolderTicketListRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(holderRef, "/tickets"), (request ?? new()).ToQuery(), cancellationToken);

    /// <summary>Retrieves one of a holder's tickets with its QR.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveHolderTicketAsync(
        string holderRef, string ticketId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(holderRef, $"/tickets/{SeatLayerClient.Escape(ticketId)}"), null, cancellationToken);

    /// <summary>Sends some of a holder's Season tickets to an email address, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateHolderTransferAsync(
        string holderRef, HolderTransferCreateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(holderRef, "/transfers"),
            Body.Of(("ticketIds", request.TicketIds.ToList()), ("toEmail", request.ToEmail)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists transfers a holder sent or was sent.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListHolderTransfersAsync(
        string holderRef, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(holderRef, "/transfers"), null, cancellationToken);

    /// <summary>Cancels a transfer that has not been accepted. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CancelHolderTransferAsync(
        string holderRef, string transferId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(holderRef, $"/transfers/{SeatLayerClient.Escape(transferId)}/cancel"),
            null,
            null,
            cancellationToken);

    /// <summary>Accepts a transfer for the recipient holder. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> AcceptHolderTransferAsync(
        string holderRef, string transferId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(holderRef, $"/transfers/{SeatLayerClient.Escape(transferId)}/accept"),
            null,
            null,
            cancellationToken);
}
