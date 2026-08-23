using System.Runtime.CompilerServices;

namespace SeatLayer;

/// <summary>Filters and paging for a chart listing.</summary>
public sealed class ChartListRequest
{
    /// <summary>Restrict to one workspace.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Find charts by your own reference.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>List the archive instead of active charts.</summary>
    public bool Archived { get; set; }

    /// <summary>Page size. Clamped server-side; asking for more is not an error.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page. Leave null to start.</summary>
    public string? Cursor { get; set; }

    internal Dictionary<string, string?> ToQuery()
    {
        var query = new Dictionary<string, string?>
        {
            ["workspaceId"] = WorkspaceId,
            ["externalRef"] = ExternalRef,
            ["limit"] = Limit?.ToString(),
            ["cursor"] = Cursor,
        };
        if (Archived)
        {
            query["archived"] = "1";
        }

        return query;
    }
}

/// <summary>Optional overrides when copying a chart.</summary>
public sealed class ChartCopyRequest
{
    /// <summary>Name for the copied chart.</summary>
    public string? Name { get; set; }
    /// <summary>Caller-owned reference for the copy.</summary>
    public string? ExternalRef { get; set; }
    /// <summary>Destination workspace.</summary>
    public string? WorkspaceId { get; set; }
    /// <summary>Optional caller key for exact server replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Optional overrides when materializing a published template as a chart draft.</summary>
public sealed class TemplateInstantiateRequest
{
    /// <summary>Name for the materialized chart draft.</summary>
    public string? Name { get; set; }
    /// <summary>Workspace that owns the materialized draft.</summary>
    public string? WorkspaceId { get; set; }
    /// <summary>Optional complete chart document to validate in place of the template document.</summary>
    public IDictionary<string, object?>? EditedDoc { get; set; }
    /// <summary>Published template version to materialize.</summary>
    public int? Version { get; set; }
    /// <summary>SHA-256 of the expected published template version.</summary>
    public string? Sha256 { get; set; }
    /// <summary>Optional caller key for exact server replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Seat-map definitions that events are created from.
/// </summary>
/// <remarks>
/// Even when organisers draw their own venues in the embedded Designer you need this:
/// <see cref="SessionsService.CreateDesignerSessionAsync(DesignerSessionRequest, CancellationToken)"/>
/// requires a chart id that
/// already exists, so the usual platform flow is copy a template here, then hand over a
/// Designer session for it.
/// </remarks>
public sealed class ChartsService
{
    private readonly SeatLayerClient _client;

    internal ChartsService(SeatLayerClient client) => _client = client;

    /// <summary>One page of charts.</summary>
    public async Task<Page> ListAsync(
        ChartListRequest? request = null, CancellationToken cancellationToken = default)
        => Page.From(
            await _client.GetAsync("/v1/charts", (request ?? new()).ToQuery(), cancellationToken)
                .ConfigureAwait(false),
            "charts");

    /// <summary>
    /// Every chart, paging transparently.
    /// </summary>
    /// <remarks>
    /// An async stream rather than a materialised list: the point of paginating was to
    /// stop holding an unbounded result set in memory.
    /// <code>await foreach (var chart in client.Charts.ListAllAsync()) { … }</code>
    /// </remarks>
    public async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ListAllAsync(
        ChartListRequest? request = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = request ?? new ChartListRequest();
        string? cursor = null;

        do
        {
            query.Cursor = cursor;
            var page = await ListAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var chart in page.Items)
            {
                yield return chart;
            }

            // A null cursor terminates, so a caller looping cannot spin forever.
            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    /// <summary>Creates a chart. Pass <paramref name="doc"/> to import an existing document.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        string name,
        IDictionary<string, object?>? doc = null,
        string? externalRef = null,
        string? workspaceId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            "/v1/charts",
            Body.Of(("name", name), ("doc", doc), ("externalRef", externalRef), ("workspaceId", workspaceId)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Retrieves a chart and its document.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAsync(
        string chartId, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/charts/{SeatLayerClient.Escape(chartId)}", null, cancellationToken);

    /// <summary>
    /// Replaces a chart document.
    /// </summary>
    /// <remarks>
    /// <paramref name="expectedUpdatedAt"/> is required for optimistic concurrency and is
    /// not optional here either: without it two concurrent writers silently overwrite each
    /// other, and a seat map is exactly the document where that loses work. Read it from
    /// <see cref="RetrieveAsync"/> immediately before writing.
    /// <para>
    /// The Designer is the authoring surface. Use this for bulk programmatic edits and
    /// migrations, not for drawing.
    /// </para>
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        string chartId,
        IDictionary<string, object?> doc,
        long expectedUpdatedAt,
        CancellationToken cancellationToken = default)
        => _client.PutAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}",
            Body.Of(("doc", doc), ("expectedUpdatedAt", expectedUpdatedAt)),
            cancellationToken);

    /// <summary>Updates name, issues, or externalRef without replacing the document.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateMetadataAsync(
        string chartId,
        IDictionary<string, object?> fields,
        CancellationToken cancellationToken = default)
        => _client.PutAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}", fields, cancellationToken);

    /// <summary>Deletes a chart.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string chartId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync($"/v1/charts/{SeatLayerClient.Escape(chartId)}", cancellationToken);

    /// <summary>Copies a chart — the usual way to provision a venue from a template.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CopyAsync(
        string chartId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}/duplicate", null, idempotencyKey, cancellationToken);

    /// <summary>Copies a chart with name, reference, or destination-workspace overrides.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CopyWithOptionsAsync(
        string chartId, ChartCopyRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}/duplicate",
            Body.Of(
                ("name", request.Name), ("externalRef", request.ExternalRef),
                ("workspaceId", request.WorkspaceId)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Moves a chart to the archive.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ArchiveAsync(
        string chartId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}/archive", null, null, cancellationToken);

    /// <summary>Restores a chart from the archive.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UnarchiveAsync(
        string chartId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}/unarchive", null, null, cancellationToken);

    /// <summary>Publishes the draft. Events can only be created from a published chart.</summary>
    public Task<IReadOnlyDictionary<string, object?>> PublishAsync(
        string chartId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}/publish", null, null, cancellationToken);
}

/// <summary>Published catalog templates that can be materialized as workspace chart drafts.</summary>
public sealed class TemplatesService
{
    private readonly SeatLayerClient _client;

    internal TemplatesService(SeatLayerClient client) => _client = client;

    /// <summary>
    /// Instantiates a published template as a draft in the caller's workspace.
    /// </summary>
    /// <remarks>
    /// The API requires a JSON object even when no overrides are needed, so this sends
    /// <c>{}</c>. It uses the server's header-replay contract and retries with one stable
    /// idempotency key.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> InstantiateTemplateAsync(
        string templateId, CancellationToken cancellationToken = default)
        => InstantiateTemplateAsync(templateId, new TemplateInstantiateRequest(), cancellationToken);

    /// <summary>Instantiates with optional name, workspace, document, or version-pinning overrides.</summary>
    public Task<IReadOnlyDictionary<string, object?>> InstantiateTemplateAsync(
        string templateId,
        TemplateInstantiateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            $"/v1/templates/{SeatLayerClient.Escape(templateId)}/instantiate",
            Body.Of(
                ("name", request.Name), ("workspaceId", request.WorkspaceId),
                ("editedDoc", request.EditedDoc), ("version", request.Version), ("sha256", request.Sha256)),
            request.IdempotencyKey,
            cancellationToken);
}

/// <summary>Filters and paging for an event listing.</summary>
public sealed class EventListRequest
{
    /// <summary>Restrict to one workspace.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Find events by your own reference.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>Page size. Clamped server-side; asking for more is not an error.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page. Leave null to start.</summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Include live availability counts. Costs one server round-trip per event, so
    /// <see cref="EventsService.ListAllAsync"/> turns it off.
    /// </summary>
    public bool Counts { get; set; } = true;

    internal Dictionary<string, string?> ToQuery()
    {
        var query = new Dictionary<string, string?>
        {
            ["workspaceId"] = WorkspaceId,
            ["externalRef"] = ExternalRef,
            ["limit"] = Limit?.ToString(),
            ["cursor"] = Cursor,
        };
        if (!Counts)
        {
            query["counts"] = "0";
        }

        return query;
    }
}

/// <summary>Full public request for creating an event from a published chart.</summary>
public sealed class EventCreateRequest
{
    /// <summary>Published chart to instantiate.</summary>
    public required string ChartId { get; set; }
    /// <summary>Display name.</summary>
    public string? Name { get; set; }
    /// <summary>Public URL slug.</summary>
    public string? Slug { get; set; }
    /// <summary>Start epoch milliseconds.</summary>
    public long? StartsAt { get; set; }
    /// <summary>Venue label.</summary>
    public string? Venue { get; set; }
    /// <summary>Caller-owned stable reference.</summary>
    public string? ExternalRef { get; set; }
    /// <summary>ISO currency code.</summary>
    public string? Currency { get; set; }
    /// <summary>Public event description.</summary>
    public string? Description { get; set; }
    /// <summary>End epoch milliseconds.</summary>
    public long? EndsAt { get; set; }
    /// <summary>IANA timezone.</summary>
    public string? Timezone { get; set; }
    /// <summary>BCP 47 locale.</summary>
    public string? Locale { get; set; }
    /// <summary>Pre-uploaded poster asset id.</summary>
    public string? PosterAssetId { get; set; }
    /// <summary>live or test.</summary>
    public string? Mode { get; set; }
    /// <summary>Optional caller key for exact server replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// One input in a full ticket-release replacement. It deliberately excludes live response fields
/// such as position, sold-out time, consumption, and remaining quota.
/// </summary>
public sealed class TicketReleaseInput
{
    /// <summary>Existing server-issued release id to preserve while replacing; omit to create one.</summary>
    public string? Id { get; set; }
    /// <summary>Buyer-visible release name.</summary>
    public required string Name { get; set; }
    /// <summary>Category key, or null/omitted for every non-tiered category.</summary>
    public string? CategoryKey { get; set; }
    /// <summary>Integer price in major currency units.</summary>
    public required int Price { get; set; }
    /// <summary>Optional prior integer price in major currency units.</summary>
    public int? PreviousPrice { get; set; }
    /// <summary>Optional maximum quantity; null/omitted means unlimited.</summary>
    public int? Quota { get; set; }
    /// <summary>Optional inclusive start timestamp in epoch milliseconds.</summary>
    public long? StartsAt { get; set; }
    /// <summary>Optional exclusive end timestamp in epoch milliseconds.</summary>
    public long? EndsAt { get; set; }
    /// <summary>Optional action: buy, apply, or invoice; defaults to buy server-side.</summary>
    public string? Action { get; set; }
    /// <summary>Required HTTPS destination for apply or invoice actions; omit for buy.</summary>
    public string? ActionUrl { get; set; }
}

/// <summary>Whole-list ticket-release replacement request.</summary>
public sealed class TicketReleaseReplaceRequest
{
    /// <summary>Ordered replacement inputs. The server derives dense positions from this order.</summary>
    public required IReadOnlyList<TicketReleaseInput> Releases { get; set; }
}

/// <summary>Selects one immutable published Event configuration version.</summary>
/// <remarks>Configuration identity remains separate from the chart's venue geometry.</remarks>
public sealed class EventConfigurationReference
{
    /// <summary>Configuration library id.</summary>
    public required string Id { get; set; }

    /// <summary>Exact immutable version to attach.</summary>
    public required long Version { get; set; }
}

/// <summary>Compare-and-set request for an Event's configuration binding.</summary>
public sealed class EventConfigurationBindingUpdateRequest
{
    /// <summary>Revision returned by the latest binding read.</summary>
    public required long ExpectedRevision { get; set; }

    /// <summary>Published version to attach, or null to explicitly detach.</summary>
    public EventConfigurationReference? Configuration { get; set; }
}

/// <summary>Event lifecycle, metadata and reports.</summary>
public sealed class EventsService
{
    private readonly SeatLayerClient _client;

    internal EventsService(SeatLayerClient client) => _client = client;

    /// <summary>One page of events, including live counts unless turned off.</summary>
    public async Task<Page> ListAsync(
        EventListRequest? request = null, CancellationToken cancellationToken = default)
        => Page.From(
            await _client.GetAsync("/v1/events", (request ?? new()).ToQuery(), cancellationToken)
                .ConfigureAwait(false),
            "events");

    /// <summary>
    /// Every event, paging transparently. Counts default off — you are walking the whole
    /// list, so per-event availability is rarely what you want and always what it costs.
    /// </summary>
    public async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ListAllAsync(
        EventListRequest? request = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = request ?? new EventListRequest();
        query.Counts = false;
        string? cursor = null;

        do
        {
            query.Cursor = cursor;
            var page = await ListAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var seatEvent in page.Items)
            {
                yield return seatEvent;
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    /// <summary>Creates an event from a published chart.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        string chartId,
        string? name = null,
        string? slug = null,
        long? startsAt = null,
        string? venue = null,
        string? externalRef = null,
        string? currency = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            "/v1/events",
            Body.Of(
                ("chartId", chartId), ("name", name), ("slug", slug), ("startsAt", startsAt),
                ("venue", venue), ("externalRef", externalRef), ("currency", currency)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Creates an event with the complete public metadata request.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateAsync(
        EventCreateRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            "/v1/events",
            Body.Of(
                ("chartId", request.ChartId), ("name", request.Name), ("slug", request.Slug),
                ("startsAt", request.StartsAt), ("venue", request.Venue),
                ("externalRef", request.ExternalRef), ("currency", request.Currency),
                ("description", request.Description), ("endsAt", request.EndsAt),
                ("timezone", request.Timezone), ("locale", request.Locale),
                ("posterAssetId", request.PosterAssetId), ("mode", request.Mode)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Retrieves an event with live counts.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}", null, cancellationToken);

    /// <summary>Reads the Event's exact immutable configuration selection and audit trail.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveConfigurationBindingAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/event-configuration",
            null,
            cancellationToken);

    /// <summary>Attaches an exact published configuration version, or explicitly detaches it.</summary>
    /// <remarks>
    /// The expected revision prevents concurrent administrative changes from silently overwriting
    /// each other. This mutation remains single-attempt because it has no header-replay contract.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> UpdateConfigurationBindingAsync(
        string eventKey,
        EventConfigurationBindingUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Body.Of removes null optional values, but null is the explicit detach instruction here.
        var body = new Dictionary<string, object?>
        {
            ["expectedRevision"] = request.ExpectedRevision,
            ["configuration"] = request.Configuration,
        };
        return _client.PutAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/event-configuration",
            body,
            cancellationToken);
    }

    /// <summary>Updates event metadata.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        string eventKey, IDictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _client.PatchAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}", fields, cancellationToken);

    /// <summary>Soft-deletes an event.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.DeleteAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}", cancellationToken);

    /// <summary>Uploads raw PNG, JPEG, or WebP poster bytes (maximum 5 MiB).</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdatePosterAsync(
        string eventKey, byte[] bytes, string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
        => _client.PutBinaryAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/poster", bytes, contentType, cancellationToken);

    /// <summary>Deletes the event poster.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeletePosterAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/poster", cancellationToken);

    /// <summary>Moves a live event onto the latest published version of its chart.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateChartAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/update-chart", null, null, cancellationToken);

    /// <summary>Updates the chart while explicitly acknowledging assignment loss when required.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateChartAsync(
        string eventKey,
        bool? acknowledgeDroppedAssignments,
        string? reason,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/update-chart",
            Body.Of(
                ("acknowledgeDroppedAssignments", acknowledgeDroppedAssignments),
                ("reason", reason)),
            null,
            cancellationToken);

    /// <summary>Stops buyer sales. Existing holds keep their TTL.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CloseAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/close", null, null, cancellationToken);

    /// <summary>Resumes buyer sales.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ReopenAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/reopen", null, null, cancellationToken);

    /// <summary>Moves an event to the archive, preserving reporting.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ArchiveAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/archive", null, null, cancellationToken);

    /// <summary>Reads the checkout window buyers get for this event.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveHoldTtlAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}/hold-ttl", null, cancellationToken);

    /// <summary>Sets the checkout window, in milliseconds.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateHoldTtlAsync(
        string eventKey, long? holdTtlMs, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["holdTtlMs"] = holdTtlMs };
        return _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/hold-ttl", body, null, cancellationToken);
    }

    /// <summary>Lists ticket releases with current quota consumption for the event.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListTicketReleasesAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}/releases", null, cancellationToken);

    /// <summary>
    /// Replaces the event's complete, ordered ticket-release list.
    /// </summary>
    /// <remarks>
    /// This mutation is deliberately single-attempt because the public operation has no
    /// header-replay contract.
    /// </remarks>
    public Task<IReadOnlyDictionary<string, object?>> UpdateTicketReleasesAsync(
        string eventKey,
        TicketReleaseReplaceRequest request,
        CancellationToken cancellationToken = default)
        => _client.PutAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/releases",
            Body.Of(("releases", request.Releases)),
            cancellationToken);

    /// <summary>Ends one ticket release immediately. This mutation is intentionally single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CloseTicketReleaseAsync(
        string eventKey, string releaseId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/releases/{SeatLayerClient.Escape(releaseId)}/close",
            null,
            null,
            cancellationToken);

    /// <summary>Retrieves the event report.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveReportAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}/report", null, cancellationToken);

    /// <summary>Retrieves the event audit log.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveLogAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}/log", null, cancellationToken);

    /// <summary>Retrieves a bounded page of event audit entries.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveLogAsync(
        string eventKey, int? limit, long? before, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/log",
            new Dictionary<string, string?>
            {
                ["limit"] = limit?.ToString(),
                ["before"] = before?.ToString(),
            },
            cancellationToken);
}
