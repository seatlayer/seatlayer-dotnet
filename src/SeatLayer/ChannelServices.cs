namespace SeatLayer;

/// <summary>Fields used to create a private allocation channel.</summary>
public sealed class CreateChannelRequest
{
    /// <summary>Human-readable channel name.</summary>
    public required string Name { get; set; }

    /// <summary>Display colour.</summary>
    public string? Color { get; set; }

    /// <summary>Short display marker.</summary>
    public string? Marker { get; set; }

    /// <summary>Your stable external reference.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>How access to the allocation is intended to be distributed.</summary>
    public string? AccessIntent { get; set; }

    /// <summary>Audit reason for the change.</summary>
    public string? Reason { get; set; }

    /// <summary>Collapses retried creation into the original mutation.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Mutable fields for an allocation channel.</summary>
public sealed class UpdateChannelRequest
{
    /// <summary>New channel name.</summary>
    public string? Name { get; set; }

    /// <summary>New access intent.</summary>
    public string? AccessIntent { get; set; }

    /// <summary>Explicitly acknowledges a change to live buyer access.</summary>
    public bool? AcknowledgeLiveAccess { get; set; }

    /// <summary>Audit reason for the change.</summary>
    public string? Reason { get; set; }
}

/// <summary>Security boundaries for an origin-bound buyer access token.</summary>
public sealed class BuyerAccessSessionRequest
{
    /// <summary>Whether public inventory is visible alongside channel inventory.</summary>
    public bool IncludePublic { get; set; }

    /// <summary>Exact browser origin allowed to consume the token.</summary>
    public required string AllowedOrigin { get; set; }

    /// <summary>Private allocation channels visible to the buyer.</summary>
    public IEnumerable<string>? ChannelIds { get; set; }

    /// <summary>Requested token lifetime.</summary>
    public int? ExpiresInSeconds { get; set; }

    /// <summary>Maximum quantity this buyer may select.</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>Your buyer reference.</summary>
    public string? BuyerRef { get; set; }

    /// <summary>Your partner reference.</summary>
    public string? PartnerRef { get; set; }

    /// <summary>Caller-generated request correlation id.</summary>
    public string? ClientRequestId { get; set; }

    /// <summary>Collapses retried creation into the original mutation.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Filters and paging for buyer access sessions.</summary>
public sealed class BuyerAccessSessionListRequest
{
    /// <summary>Restricts results to one state.</summary>
    public string? State { get; set; }

    /// <summary>Page size.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }
}

/// <summary>Private allocations, reporting, and origin-bound buyer access.</summary>
public sealed class ChannelsService
{
    private readonly SeatLayerClient _client;

    internal ChannelsService(SeatLayerClient client) => _client = client;

    private static string Path(string eventKey, string suffix = "")
        => $"/v1/events/{SeatLayerClient.Escape(eventKey)}/channels{suffix}";

    /// <summary>Lists allocation channels for an event.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListAsync(
        string eventKey, bool includeArchived = false, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(eventKey),
            includeArchived ? new Dictionary<string, string?> { ["includeArchived"] = "1" } : null,
            cancellationToken);

    /// <summary>Creates an allocation channel.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        string eventKey, CreateChannelRequest request, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey),
            Body.Of(
                ("name", request.Name), ("color", request.Color), ("marker", request.Marker),
                ("externalRef", request.ExternalRef), ("accessIntent", request.AccessIntent),
                ("reason", request.Reason)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Updates an allocation channel.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        string eventKey,
        string channelId,
        UpdateChannelRequest request,
        CancellationToken cancellationToken = default)
        => _client.PatchAsync(
            Path(eventKey, $"/{SeatLayerClient.Escape(channelId)}"),
            Body.Of(
                ("name", request.Name), ("accessIntent", request.AccessIntent),
                ("acknowledgeLiveAccess", request.AcknowledgeLiveAccess), ("reason", request.Reason)),
            cancellationToken);

    /// <summary>Assigns objects to a channel, or to public inventory when the target is null.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAssignmentsAsync(
        string eventKey,
        IEnumerable<string> labels,
        long assignmentVersion,
        string? targetChannelId = null,
        string? reason = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var body = Body.Of(
            ("labels", labels.ToList()), ("assignmentVersion", assignmentVersion), ("reason", reason));
        body["targetChannelId"] = targetChannelId;

        return _client.PostAsync(Path(eventKey, "/assignments"), body, idempotencyKey, cancellationToken);
    }

    /// <summary>Lists the current allocation ledger.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListAllocationAsync(
        string eventKey,
        string? afterLabel = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(eventKey, "/allocation"),
            new Dictionary<string, string?>
            {
                ["afterLabel"] = afterLabel,
                ["limit"] = limit?.ToString(),
            },
            cancellationToken);

    /// <summary>Previews which inventory a buyer-access scope can see.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAccessPreviewAsync(
        string eventKey,
        IEnumerable<string>? channelIds = null,
        bool? includePublic = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(eventKey, "/preview"),
            new Dictionary<string, string?>
            {
                ["channelIds"] = channelIds is null ? null : string.Join(",", channelIds),
                ["includePublic"] = includePublic is null ? null : includePublic.Value ? "1" : "0",
            },
            cancellationToken);

    /// <summary>Retrieves the channel allocation report.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveReportAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(eventKey, "/report"), null, cancellationToken);

    /// <summary>Pauses a channel.</summary>
    public Task<IReadOnlyDictionary<string, object?>> PauseAsync(
        string eventKey, string channelId, string? reason = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, $"/{SeatLayerClient.Escape(channelId)}/pause"),
            Body.Of(("reason", reason)), null, cancellationToken);

    /// <summary>Restores a paused channel.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UnpauseAsync(
        string eventKey, string channelId, string? reason = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, $"/{SeatLayerClient.Escape(channelId)}/unpause"),
            Body.Of(("reason", reason)), null, cancellationToken);

    /// <summary>Archives a channel and chooses where its inventory moves.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ArchiveAsync(
        string eventKey,
        string channelId,
        string destination,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(eventKey, $"/{SeatLayerClient.Escape(channelId)}/archive"),
            Body.Of(("destination", destination), ("reason", reason)),
            null,
            cancellationToken);

    /// <summary>Mints a short-lived, origin-bound buyer access token.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateBuyerAccessSessionAsync(
        string eventKey,
        BuyerAccessSessionRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/buyer-access-sessions",
            Body.Of(
                ("channelIds", request.ChannelIds?.ToList()), ("includePublic", request.IncludePublic),
                ("allowedOrigin", request.AllowedOrigin), ("expiresInSeconds", request.ExpiresInSeconds),
                ("maxQuantity", request.MaxQuantity), ("buyerRef", request.BuyerRef),
                ("partnerRef", request.PartnerRef), ("clientRequestId", request.ClientRequestId)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists buyer access sessions.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListBuyerAccessSessionsAsync(
        string eventKey,
        BuyerAccessSessionListRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new BuyerAccessSessionListRequest();
        return _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/buyer-access-sessions",
            new Dictionary<string, string?>
            {
                ["state"] = request.State,
                ["limit"] = request.Limit?.ToString(),
                ["cursor"] = request.Cursor,
            },
            cancellationToken);
    }

    /// <summary>Revokes a buyer access token before it expires.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeBuyerAccessSessionAsync(
        string eventKey, string sessionId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/buyer-access-sessions/"
            + SeatLayerClient.Escape(sessionId),
            cancellationToken);
}
