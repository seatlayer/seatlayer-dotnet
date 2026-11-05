namespace SeatLayer;

/// <summary>The schedule an event series makes its dates from.</summary>
public sealed class EventSeriesSchedule
{
    /// <summary>
    /// <c>none</c>, <c>daily</c>, <c>weekly</c>, <c>biweekly</c>, or <c>dates</c>. The
    /// <c>dates</c> rule takes the days in <c>Dates</c>; the others step from <c>From</c>.
    /// </summary>
    public required string Rule { get; set; }

    /// <summary>The first day, <c>YYYY-MM-DD</c> in the series' zone. Required unless the rule is <c>dates</c>.</summary>
    public string? From { get; set; }

    /// <summary>The last day, inclusive, <c>YYYY-MM-DD</c>. Give this or <c>Count</c>, not both.</summary>
    public string? Until { get; set; }

    /// <summary>How many days. With neither this nor <c>Until</c>, one day.</summary>
    public int? Count { get; set; }

    /// <summary>Start times on each day, <c>HH:MM</c>: one date per day and time.</summary>
    public required IEnumerable<string> Times { get; set; }

    /// <summary>Moments to leave out, <c>YYYY-MM-DDTHH:MM</c>.</summary>
    public IEnumerable<string>? Skip { get; set; }

    /// <summary>For the <c>dates</c> rule: the days, <c>YYYY-MM-DD</c>.</summary>
    public IEnumerable<string>? Dates { get; set; }

    internal Dictionary<string, object?> ToBody()
        => Body.Of(
            ("rule", Rule),
            ("from", From),
            ("until", Until),
            ("count", Count),
            ("times", Times.ToList()),
            ("skip", Skip?.ToList()),
            ("dates", Dates?.ToList()));
}

/// <summary>Plans an event series from a schedule, or makes it as a job.</summary>
public sealed class EventSeriesCreateRequest
{
    /// <summary>The series name; each date is named from it.</summary>
    public required string Name { get; set; }

    /// <summary>A published floor plan of this organization.</summary>
    public required string ChartKey { get; set; }

    /// <summary>The schedule the dates are made from.</summary>
    public required EventSeriesSchedule Schedule { get; set; }

    /// <summary>IANA zone the schedule is read in.</summary>
    public string? Timezone { get; set; }

    /// <summary>Venue label for each date.</summary>
    public string? Venue { get; set; }

    /// <summary>How long each date runs, in minutes; sets each date's end.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>When true, answers the dates the schedule makes and creates nothing.</summary>
    public bool? DryRun { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Cursor paging for event series.</summary>
public sealed class EventSeriesListRequest
{
    /// <summary>Page size, capped by the API.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["limit"] = Limit?.ToString(),
        ["cursor"] = Cursor,
    };
}

/// <summary>
/// Dates to add to an event series, copied from its first date. Set one of
/// <see cref="Schedule"/>, <see cref="Dates"/> or <see cref="RetryFailed"/>.
/// </summary>
public sealed class EventSeriesDatesRequest
{
    /// <summary>A schedule that makes the new dates.</summary>
    public EventSeriesSchedule? Schedule { get; set; }

    /// <summary>Exact moments, <c>YYYY-MM-DDTHH:MM</c> in the series' zone.</summary>
    public IEnumerable<string>? Dates { get; set; }

    /// <summary>
    /// True to make again every date that failed to be made (up to 100, as in
    /// <c>job.failedDates</c>): <c>422 nothing_to_retry</c> when none did, <c>409 series_busy</c>
    /// while another change runs.
    /// </summary>
    public bool RetryFailed { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Changes the name, prices or sales of every upcoming date in a series.</summary>
public sealed class EventSeriesUpdateRequest
{
    /// <summary>A new series name, when supplied.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// New prices as the API takes them: a <c>categories</c> list covering every category of
    /// the floor plan. New orders pay these.
    /// </summary>
    public IDictionary<string, object?>? Prices { get; set; }

    /// <summary><c>on</c> or <c>off</c>: opens or pauses sales on every upcoming date.</summary>
    public string? Sales { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Event series: many dated events made from one floor plan and a schedule, then changed
/// together.
/// </summary>
/// <remarks>
/// Creating a series and adding dates run as a job; poll
/// <see cref="RetrieveEventSeriesAsync(string, CancellationToken)"/> for its state.
/// </remarks>
public sealed class EventSeriesService
{
    private readonly SeatLayerClient _client;

    internal EventSeriesService(SeatLayerClient client) => _client = client;

    private static string Path(string eventKey, string suffix = "")
        => $"/v1/event-series/{SeatLayerClient.Escape(eventKey)}{suffix}";

    /// <summary>Plans an event series from a schedule, or makes it as a job, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateEventSeriesAsync(
        EventSeriesCreateRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            "/v1/event-series",
            Body.Of(
                ("name", request.Name),
                ("chartKey", request.ChartKey),
                ("schedule", request.Schedule.ToBody()),
                ("timezone", request.Timezone),
                ("venue", request.Venue),
                ("durationMinutes", request.DurationMinutes),
                ("dryRun", request.DryRun)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists event series.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListEventSeriesAsync(
        EventSeriesListRequest? request = null, CancellationToken cancellationToken = default)
        => _client.GetAsync("/v1/event-series", (request ?? new()).ToQuery(), cancellationToken);

    /// <summary>Retrieves an event series, its dates and its job.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveEventSeriesAsync(
        string eventKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(eventKey), null, cancellationToken);

    /// <summary>Adds dates to an event series, copied from its first date, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> AddEventSeriesDatesAsync(
        string eventKey, EventSeriesDatesRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(eventKey, "/dates"),
            Body.Of(
                ("schedule", request.Schedule?.ToBody()),
                ("dates", request.Dates?.ToList()),
                ("retryFailed", request.RetryFailed ? (object)true : null)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Changes the name, prices or sales of every upcoming date, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateEventSeriesAsync(
        string eventKey, EventSeriesUpdateRequest request, CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Patch,
            Path(eventKey),
            Body.Of(("name", request.Name), ("prices", request.Prices), ("sales", request.Sales)),
            request.IdempotencyKey,
            cancellationToken);
}
