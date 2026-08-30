namespace SeatLayer;

/// <summary>Events or activated Performance Groups selected for a Season.</summary>
public class SeasonSelectionRequest
{
    /// <summary>Compatible assigned-seat event keys.</summary>
    public IEnumerable<string>? EventKeys { get; set; }

    /// <summary>Activated Performance Groups whose immutable members should be included.</summary>
    public IEnumerable<string>? SourcePerformanceGroupKeys { get; set; }
}

/// <summary>Filters and cursor paging for Seasons.</summary>
public sealed class SeasonListRequest
{
    /// <summary>Restrict results to one workspace.</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Restrict results to one structure lifecycle state.</summary>
    public string? StructureState { get; set; }

    /// <summary>Page size, capped by the API.</summary>
    public int? Limit { get; set; }

    /// <summary>Continues a previous page.</summary>
    public string? Cursor { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["workspaceId"] = WorkspaceId,
        ["structureState"] = StructureState,
        ["limit"] = Limit?.ToString(),
        ["cursor"] = Cursor,
    };
}

/// <summary>Creates a draft fixed renewable Season.</summary>
public sealed class SeasonCreateRequest : SeasonSelectionRequest
{
    /// <summary>Operator-facing Season name.</summary>
    public required string Name { get; set; }

    /// <summary>Optional edition label, such as 2027.</summary>
    public string? Edition { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Compare-and-set changes to Season identity fields.</summary>
public sealed class SeasonUpdateRequest
{
    /// <summary>Revision returned by the latest Season read.</summary>
    public required long ExpectedRevision { get; set; }

    /// <summary>Replacement operator-facing name, when supplied.</summary>
    public string? Name { get; set; }

    /// <summary>Replacement edition, when supplied.</summary>
    public string? Edition { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Creates a publishable plan within a Season.</summary>
public sealed class SeasonPlanCreateRequest : SeasonSelectionRequest
{
    /// <summary>Operator-facing plan name.</summary>
    public required string Name { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Copies a tested Season onto compatible live-mode events.</summary>
public sealed class SeasonDuplicateToLiveRequest
{
    /// <summary>Live event keys corresponding to the tested occurrences.</summary>
    public required IEnumerable<string> EventKeys { get; set; }

    /// <summary>Optional name for the live copy.</summary>
    public string? Name { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Security boundaries for a one-time Season browser bearer.</summary>
public sealed class SeasonBuyerAccessSessionRequest
{
    /// <summary>Exact browser origin allowed to consume the token.</summary>
    public required string AllowedOrigin { get; set; }

    /// <summary>Whether public inventory is visible.</summary>
    public bool IncludePublic { get; set; }

    /// <summary>Requested token lifetime in seconds.</summary>
    public int? ExpiresInSeconds { get; set; }

    /// <summary>Maximum quantity this buyer may select.</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>Your buyer reference.</summary>
    public string? BuyerRef { get; set; }
}

/// <summary>Stable identifiers confirming external payment for a Season hold.</summary>
public sealed class SeasonBookHoldRequest
{
    /// <summary>Stable action id for this booking attempt.</summary>
    public required string BookActionId { get; set; }

    /// <summary>Stable merchant booking reference.</summary>
    public required string BookingRef { get; set; }
}

/// <summary>Cancels a Season booking and chooses how its renewable right is handled.</summary>
public sealed class SeasonCancelBookingRequest
{
    /// <summary>Stable action id for this cancellation attempt.</summary>
    public required string CancelActionId { get; set; }

    /// <summary>Original merchant booking reference.</summary>
    public required string BookingRef { get; set; }

    /// <summary>Plan activation that owns the renewable right.</summary>
    public required string PlanActivationId { get; set; }

    /// <summary><c>preserve</c> or <c>release</c>.</summary>
    public required string RightDisposition { get; set; }
}

/// <summary>One incumbent holder row imported into a successor plan.</summary>
public sealed class SeasonHolderImportRow
{
    /// <summary>Caller-stable row id.</summary>
    public required string RowId { get; set; }

    /// <summary>Stable holder reference.</summary>
    public required string HolderRef { get; set; }

    /// <summary>Prior plan activation.</summary>
    public required string PriorPlanActivationId { get; set; }

    /// <summary>Prior contract reference.</summary>
    public required string PriorContractRef { get; set; }

    /// <summary>Seats retained by the holder.</summary>
    public required IEnumerable<string> Labels { get; set; }

    /// <summary>Existing booking reference, when one exists.</summary>
    public string? ExistingBookingRef { get; set; }
}

/// <summary>Imports incumbent holders into a successor plan.</summary>
public sealed class SeasonHolderImportRequest
{
    /// <summary>Successor plan activation receiving the renewable rights.</summary>
    public required string SuccessorPlanActivationId { get; set; }

    /// <summary>When true, validates without committing.</summary>
    public bool? DryRun { get; set; }

    /// <summary>Holder rows to validate or import.</summary>
    public required IEnumerable<SeasonHolderImportRow> Rows { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Creates renewal offers for eligible incumbent contracts.</summary>
public sealed class SeasonRenewalOffersCreateRequest
{
    /// <summary>Successor plan activation, or the current successor when omitted.</summary>
    public string? SuccessorPlanActivationId { get; set; }

    /// <summary>Offer deadline in epoch milliseconds.</summary>
    public required long DeadlineAt { get; set; }

    /// <summary>Optional subset of contract ids.</summary>
    public IEnumerable<string>? ContractIds { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Stable external references for committing one accepted renewal.</summary>
public sealed class SeasonRenewalCommitRequest
{
    /// <summary>Stable action id for this commit attempt.</summary>
    public required string CommitActionId { get; set; }

    /// <summary>Merchant order reference.</summary>
    public required string OrderRef { get; set; }

    /// <summary>Merchant booking reference.</summary>
    public required string BookingRef { get; set; }

    /// <summary>Successor plan activation being booked.</summary>
    public required string PlanActivationId { get; set; }
}

/// <summary>Creates an audited occurrence amendment.</summary>
public sealed class SeasonAmendmentCreateRequest
{
    /// <summary>Occurrence event key.</summary>
    public required string EventKey { get; set; }

    /// <summary><c>reschedule</c>, <c>replace</c>, or <c>cancel_exception</c>.</summary>
    public required string Kind { get; set; }

    /// <summary>Replacement start time in epoch milliseconds.</summary>
    public long? StartsAt { get; set; }

    /// <summary>Replacement occurrence name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Booking or holder identifier for support lookup.</summary>
public sealed class SeasonSupportLookupRequest
{
    /// <summary>Merchant booking reference.</summary>
    public string? BookingRef { get; set; }

    /// <summary>Stable holder reference.</summary>
    public string? HolderRef { get; set; }
}

/// <summary>
/// Fixed renewable Season catalogue, sales, buyer handoff, renewals, and support operations.
/// </summary>
/// <remarks>
/// This is a server-only service. Browser selection belongs in <c>SeasonPicker</c>; give it
/// only the show-once token returned by <see cref="CreateSeasonBuyerAccessSessionAsync"/>.
/// </remarks>
public sealed class SeasonsService
{
    private readonly SeatLayerClient _client;

    internal SeasonsService(SeatLayerClient client) => _client = client;

    private static string Path(string seasonKey, string suffix = "")
        => $"/v1/seasons/{SeatLayerClient.Escape(seasonKey)}{suffix}";

    private static Dictionary<string, object?> Selection(SeasonSelectionRequest request)
        => Body.Of(
            ("eventKeys", request.EventKeys?.ToList()),
            ("sourcePerformanceGroupKeys", request.SourcePerformanceGroupKeys?.ToList()));

    /// <summary>Lists Seasons.</summary>
    public async Task<Page> ListSeasonsAsync(
        SeasonListRequest? request = null, CancellationToken cancellationToken = default)
        => Page.From(
            await _client.GetAsync(
                "/v1/seasons", (request ?? new()).ToQuery(), cancellationToken).ConfigureAwait(false),
            "seasons");

    /// <summary>Runs a read-only compatibility preflight without creating a Season.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ValidateSeasonAsync(
        SeasonSelectionRequest request, CancellationToken cancellationToken = default)
        => _client.PostAsync("/v1/seasons/validate", Selection(request), null, cancellationToken);

    /// <summary>Creates a draft Season with exact header-replay idempotency.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonAsync(
        SeasonCreateRequest request, CancellationToken cancellationToken = default)
    {
        var body = Selection(request);
        body["name"] = request.Name;
        if (request.Edition is not null)
        {
            body["edition"] = request.Edition;
        }

        return _client.PostHeaderReplayAsync(
            "/v1/seasons", body, request.IdempotencyKey, cancellationToken);
    }

    /// <summary>Retrieves one Season and its plans.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey), null, cancellationToken);

    /// <summary>Updates Season identity fields with compare-and-set and header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateSeasonAsync(
        string seasonKey, SeasonUpdateRequest request, CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Patch,
            Path(seasonKey),
            Body.Of(
                ("expectedRevision", request.ExpectedRevision),
                ("name", request.Name),
                ("edition", request.Edition)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Deletes an unused draft Season with exact header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteSeasonAsync(
        string seasonKey, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Delete, Path(seasonKey), null, idempotencyKey, cancellationToken);

    /// <summary>Activates a draft Season.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ActivateSeasonAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => LifecycleAsync(seasonKey, "activate", expectedRevision, cancellationToken);

    /// <summary>Closes a Season.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CloseSeasonAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => LifecycleAsync(seasonKey, "close", expectedRevision, cancellationToken);

    /// <summary>Archives a closed Season.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ArchiveSeasonAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => LifecycleAsync(seasonKey, "archive", expectedRevision, cancellationToken);

    /// <summary>Retrieves a Season lifecycle operation.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonLifecycleAsync(
        string seasonKey, string operationId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/lifecycle/{SeatLayerClient.Escape(operationId)}"),
            null,
            cancellationToken);

    /// <summary>Creates a draft plan with exact header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonPlanAsync(
        string seasonKey, SeasonPlanCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = Selection(request);
        body["name"] = request.Name;
        return _client.PostHeaderReplayAsync(
            Path(seasonKey, "/plans"), body, request.IdempotencyKey, cancellationToken);
    }

    /// <summary>Retrieves one Season plan.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonPlanAsync(
        string seasonKey, string planKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}"), null, cancellationToken);

    /// <summary>Publishes a Season plan.</summary>
    public Task<IReadOnlyDictionary<string, object?>> PublishSeasonPlanAsync(
        string seasonKey, string planKey, long expectedRevision,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}/publish"),
            Body.Of(("expectedRevision", expectedRevision)),
            null,
            cancellationToken);

    /// <summary>Supersedes a published Season plan.</summary>
    public Task<IReadOnlyDictionary<string, object?>> SupersedeSeasonPlanAsync(
        string seasonKey, string planKey, long expectedRevision,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}/supersede"),
            Body.Of(("expectedRevision", expectedRevision)),
            null,
            cancellationToken);

    /// <summary>Opens Season sales.</summary>
    public Task<IReadOnlyDictionary<string, object?>> OpenSeasonSalesAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => SalesAsync(seasonKey, "open", expectedRevision, cancellationToken);

    /// <summary>Pauses Season sales.</summary>
    public Task<IReadOnlyDictionary<string, object?>> PauseSeasonSalesAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => SalesAsync(seasonKey, "pause", expectedRevision, cancellationToken);

    /// <summary>Resumes Season sales.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ResumeSeasonSalesAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => SalesAsync(seasonKey, "resume", expectedRevision, cancellationToken);

    /// <summary>Ends Season sales.</summary>
    public Task<IReadOnlyDictionary<string, object?>> EndSeasonSalesAsync(
        string seasonKey, long expectedRevision, CancellationToken cancellationToken = default)
        => SalesAsync(seasonKey, "end", expectedRevision, cancellationToken);

    /// <summary>Duplicates a tested Season onto compatible live-mode events with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DuplicateSeasonToLiveAsync(
        string seasonKey, SeasonDuplicateToLiveRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/duplicate-to-live"),
            Body.Of(("eventKeys", request.EventKeys.ToList()), ("name", request.Name)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Creates and reveals a show-once, origin-bound Season browser bearer.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonBuyerAccessSessionAsync(
        string seasonKey, SeasonBuyerAccessSessionRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, "/buyer-access-sessions"),
            Body.Of(
                ("allowedOrigin", request.AllowedOrigin),
                ("includePublic", request.IncludePublic),
                ("expiresInSeconds", request.ExpiresInSeconds),
                ("maxQuantity", request.MaxQuantity),
                ("buyerRef", request.BuyerRef)),
            null,
            cancellationToken);

    /// <summary>Lists Season buyer-session metadata without revealing bearer values.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonBuyerAccessSessionsAsync(
        string seasonKey, int? limit = null, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, "/buyer-access-sessions"),
            new Dictionary<string, string?> { ["limit"] = limit?.ToString() },
            cancellationToken);

    /// <summary>Revokes a Season browser bearer before it expires.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RevokeSeasonBuyerAccessSessionAsync(
        string seasonKey, string sessionId, CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            Path(seasonKey, $"/buyer-access-sessions/{SeatLayerClient.Escape(sessionId)}"),
            cancellationToken);

    /// <summary>Retrieves the trusted server projection of a buyer-created Season hold.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonHoldAsync(
        string seasonKey, string operationId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/holds/{SeatLayerClient.Escape(operationId)}"),
            null,
            cancellationToken);

    /// <summary>Confirms external payment for a Season hold.</summary>
    public Task<IReadOnlyDictionary<string, object?>> BookSeasonHoldAsync(
        string seasonKey, string operationId, SeasonBookHoldRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/holds/{SeatLayerClient.Escape(operationId)}/book"),
            Body.Of(("bookActionId", request.BookActionId), ("bookingRef", request.BookingRef)),
            null,
            cancellationToken);

    /// <summary>Retrieves a Season booking operation.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonBookingAsync(
        string seasonKey, string actionId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/bookings/{SeatLayerClient.Escape(actionId)}"),
            null,
            cancellationToken);

    /// <summary>Cancels a Season booking and preserves or releases its renewable right.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CancelSeasonBookingAsync(
        string seasonKey, string actionId, SeasonCancelBookingRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/bookings/{SeatLayerClient.Escape(actionId)}/cancel"),
            Body.Of(
                ("cancelActionId", request.CancelActionId),
                ("bookingRef", request.BookingRef),
                ("planActivationId", request.PlanActivationId),
                ("rightDisposition", request.RightDisposition)),
            null,
            cancellationToken);

    /// <summary>Discovers and validates retained hold, booking, cancellation, and webhook evidence.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ValidateSeasonBuyerRehearsalAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, "/buyer-rehearsals/validate"),
            cancellationToken: cancellationToken);

    /// <summary>Creates a dry-run or committed incumbent holder import with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonHolderImportAsync(
        string seasonKey, SeasonHolderImportRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/imports"),
            Body.Of(
                ("successorPlanActivationId", request.SuccessorPlanActivationId),
                ("dryRun", request.DryRun),
                ("rows", request.Rows.ToList())),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Retrieves a holder import and per-row decisions.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonHolderImportAsync(
        string seasonKey, string importId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/imports/{SeatLayerClient.Escape(importId)}"), null, cancellationToken);

    /// <summary>Creates renewal offers with exact header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonRenewalOffersAsync(
        string seasonKey, SeasonRenewalOffersCreateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/renewal-offers"),
            Body.Of(
                ("successorPlanActivationId", request.SuccessorPlanActivationId),
                ("deadlineAt", request.DeadlineAt),
                ("contractIds", request.ContractIds?.ToList())),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists renewal offers.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonRenewalOffersAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/renewal-offers"), null, cancellationToken);

    /// <summary>Retrieves one renewal offer.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonRenewalOfferAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}"),
            null,
            cancellationToken);

    /// <summary>Extends one renewal offer deadline.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ExtendSeasonRenewalOfferAsync(
        string seasonKey, string offerId, long deadlineAt,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/extend"),
            Body.Of(("deadlineAt", deadlineAt)),
            null,
            cancellationToken);

    /// <summary>Inspects authoritative renewal pricing and availability.</summary>
    public Task<IReadOnlyDictionary<string, object?>> InspectSeasonRenewalOfferAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/inspect"),
            null,
            cancellationToken);

    /// <summary>Commits an accepted renewal using stable external references.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CommitSeasonRenewalOfferAsync(
        string seasonKey, string offerId, SeasonRenewalCommitRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/commit"),
            Body.Of(
                ("commitActionId", request.CommitActionId),
                ("orderRef", request.OrderRef),
                ("bookingRef", request.BookingRef),
                ("planActivationId", request.PlanActivationId)),
            null,
            cancellationToken);

    /// <summary>Declines a renewal offer.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeclineSeasonRenewalOfferAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/decline"),
            new Dictionary<string, object?>(),
            null,
            cancellationToken);

    /// <summary>Releases the seat right attached to a renewal offer.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ReleaseSeasonRenewalOfferAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/release"),
            new Dictionary<string, object?>(),
            null,
            cancellationToken);

    /// <summary>Lists ordered Season occurrences.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonOccurrencesAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/occurrences"), null, cancellationToken);

    /// <summary>Creates an audited occurrence amendment with exact header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonAmendmentAsync(
        string seasonKey, SeasonAmendmentCreateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/amendments"),
            Body.Of(
                ("eventKey", request.EventKey),
                ("kind", request.Kind),
                ("startsAt", request.StartsAt),
                ("name", request.Name)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists occurrence amendments.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonAmendmentsAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/amendments"), null, cancellationToken);

    /// <summary>Retrieves one occurrence amendment.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonAmendmentAsync(
        string seasonKey, string amendmentId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/amendments/{SeatLayerClient.Escape(amendmentId)}"),
            null,
            cancellationToken);

    /// <summary>Retrieves the Season operations and delivery report.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonReportAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/reports"), null, cancellationToken);

    /// <summary>Lists buyer and renewal operations.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonOperationsAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/operations"), null, cancellationToken);

    /// <summary>Finds Season support records by booking or holder reference.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonSupportLookupAsync(
        string seasonKey, SeasonSupportLookupRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, "/support-lookups"),
            new Dictionary<string, string?>
            {
                ["bookingRef"] = request?.BookingRef,
                ["holderRef"] = request?.HolderRef,
            },
            cancellationToken);

    /// <summary>Lists Season webhook outbox occurrences.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonOutboxAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/outbox"), null, cancellationToken);

    /// <summary>Replays one Season webhook outbox occurrence.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ReplaySeasonOutboxAsync(
        string seasonKey, string occurrenceId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/outbox/{SeatLayerClient.Escape(occurrenceId)}/replay"),
            new Dictionary<string, object?>(),
            null,
            cancellationToken);

    /// <summary>Lists the Season audit trail.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonAuditAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/audit"), null, cancellationToken);

    /// <summary>Exports a bounded support snapshot.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ExportSeasonSupportSnapshotAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/export"), null, cancellationToken);

    private Task<IReadOnlyDictionary<string, object?>> LifecycleAsync(
        string seasonKey, string action, long expectedRevision, CancellationToken cancellationToken)
        => _client.PostAsync(
            Path(seasonKey, $"/{action}"),
            Body.Of(("expectedRevision", expectedRevision)),
            null,
            cancellationToken);

    private Task<IReadOnlyDictionary<string, object?>> SalesAsync(
        string seasonKey, string action, long expectedRevision, CancellationToken cancellationToken)
        => _client.PostAsync(
            Path(seasonKey, $"/sales/{action}"),
            Body.Of(("expectedRevision", expectedRevision)),
            null,
            cancellationToken);
}
