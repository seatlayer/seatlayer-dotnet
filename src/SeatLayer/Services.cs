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

/// <summary>
/// Seat-map definitions that events are created from.
/// </summary>
/// <remarks>
/// Even when organisers draw their own venues in the embedded Designer you need this:
/// <see cref="SessionsService.CreateDesignerSessionAsync"/> requires a chart id that
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
        => _client.PostAsync(
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

    /// <summary>Deletes a chart.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string chartId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync($"/v1/charts/{SeatLayerClient.Escape(chartId)}", cancellationToken);

    /// <summary>Copies a chart — the usual way to provision a venue from a template.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CopyAsync(
        string chartId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/charts/{SeatLayerClient.Escape(chartId)}/duplicate", null, idempotencyKey, cancellationToken);

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
        => _client.PostAsync(
            "/v1/events",
            Body.Of(
                ("chartId", chartId), ("name", name), ("slug", slug), ("startsAt", startsAt),
                ("venue", venue), ("externalRef", externalRef), ("currency", currency)),
            idempotencyKey,
            cancellationToken);

    /// <summary>Retrieves an event with live counts.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}", null, cancellationToken);

    /// <summary>Updates event metadata.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateAsync(
        string eventKey, IDictionary<string, object?> fields, CancellationToken cancellationToken = default)
        => _client.PatchAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}", fields, cancellationToken);

    /// <summary>Soft-deletes an event.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.DeleteAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}", cancellationToken);

    /// <summary>Moves a live event onto the latest published version of its chart.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateChartAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/update-chart", null, null, cancellationToken);

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
        string eventKey, long holdTtlMs, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            $"/v1/events/{SeatLayerClient.Escape(eventKey)}/hold-ttl",
            Body.Of(("holdTtlMs", holdTtlMs)), null, cancellationToken);

    /// <summary>Retrieves the event report.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveReportAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}/report", null, cancellationToken);

    /// <summary>Retrieves the event audit log.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveLogAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync($"/v1/events/{SeatLayerClient.Escape(eventKey)}/log", null, cancellationToken);
}
