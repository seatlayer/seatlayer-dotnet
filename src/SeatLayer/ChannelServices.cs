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

    /// <summary>Optional caller key forwarded to the API; it does not enable automatic retries.</summary>
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

    /// <summary>Optional caller key forwarded to the API; it does not enable automatic retries.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Limit for the latest buyer access sessions.</summary>
public sealed class BuyerAccessSessionListRequest
{
    /// <summary>Page size.</summary>
    public int? Limit { get; set; }
}

/// <summary>Options for minting a one-time-revealed hosted channel access link.</summary>
public sealed class AccessLinkCreateRequest
{
    /// <summary>Operator-facing label.</summary>
    public string? Label { get; set; }
    /// <summary>Whether public inventory is visible too.</summary>
    public bool? IncludePublic { get; set; }
    /// <summary>Absolute expiry epoch milliseconds.</summary>
    public long? ExpiresAt { get; set; }
    /// <summary>Maximum successful redemptions.</summary>
    public int? MaxRedemptions { get; set; }
    /// <summary>Maximum quantity per redeemed session.</summary>
    public int? MaxQuantity { get; set; }
    /// <summary>Lifetime of a redeemed buyer session.</summary>
    public int? SessionTtlSeconds { get; set; }
    /// <summary>Audit reason.</summary>
    public string? Reason { get; set; }
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
                ["includePublic"] = includePublic == true ? "1" : null,
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
        string? destination,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var body = Body.Of(("reason", reason));
        body["destination"] = destination;
        return _client.PostAsync(
            Path(eventKey, $"/{SeatLayerClient.Escape(channelId)}/archive"),
            body,
            null,
            cancellationToken);
    }

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
                ["limit"] = request.Limit?.ToString(),
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

    /// <summary>Mints a hosted access link; its capability is revealed once.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAccessLinkAsync(
        string eventKey,
        string channelId,
        AccessLinkCreateRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new AccessLinkCreateRequest();
        return _client.PostAsync(
            AccessLinkPath(eventKey, channelId),
            Body.Of(
                ("label", request.Label), ("includePublic", request.IncludePublic),
                ("expiresAt", request.ExpiresAt), ("maxRedemptions", request.MaxRedemptions),
                ("maxQuantity", request.MaxQuantity),
                ("sessionTtlSeconds", request.SessionTtlSeconds), ("reason", request.Reason)),
            null,
            cancellationToken);
    }

    /// <summary>Lists link status without re-revealing capabilities.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListAccessLinksAsync(
        string eventKey, string channelId, CancellationToken cancellationToken = default)
        => _client.GetAsync(AccessLinkPath(eventKey, channelId), null, cancellationToken);

    /// <summary>Rotates a hosted link and reveals the replacement once.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RotateAccessLinkAsync(
        string eventKey,
        string channelId,
        string linkId,
        bool endActiveSessions,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            AccessLinkPath(eventKey, channelId, $"/{SeatLayerClient.Escape(linkId)}/rotate"),
            Body.Of(("endActiveSessions", endActiveSessions), ("reason", reason)),
            null,
            cancellationToken);

    /// <summary>Revokes a hosted link and optionally ends sessions it minted.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeAccessLinkAsync(
        string eventKey,
        string channelId,
        string linkId,
        bool endActiveSessions = false,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            AccessLinkPath(eventKey, channelId, $"/{SeatLayerClient.Escape(linkId)}"),
            new Dictionary<string, string?>
            {
                ["endActiveSessions"] = endActiveSessions ? "1" : null,
                ["reason"] = reason,
            },
            cancellationToken);

    private static string AccessLinkPath(string eventKey, string channelId, string suffix = "")
        => Path(eventKey, $"/{SeatLayerClient.Escape(channelId)}/access-links{suffix}");
}
