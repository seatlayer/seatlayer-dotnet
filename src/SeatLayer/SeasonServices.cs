using System.Text.Json.Serialization;

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

    /// <summary>
    /// True: each date also sells its own single tickets once the Season is on sale. Left out when
    /// null; the default is Season only.
    /// </summary>
    public bool? SingleTickets { get; set; }

    /// <summary>
    /// <c>block</c> turns season seats on for this Season. Left out when null; the default is
    /// <c>per_hold</c>, and anything else answers <c>422 invalid_inventory_mode</c>. Every Season
    /// answer carries <c>inventoryMode</c>: how the season's seats are decided, <c>block</c> once
    /// season seats are turned on for it (<c>inventoryMode: "block"</c> on create or update) or it
    /// sells from its block, else <c>per_hold</c>.
    /// </summary>
    public string? InventoryMode { get; set; }

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

    /// <summary>
    /// Who takes the money: <c>seatlayer</c> or <c>server</c>. Left out when null; it cannot go
    /// back to undecided, and it is fixed once a live booking exists.
    /// </summary>
    public string? CheckoutMode { get; set; }

    /// <summary>
    /// <c>embed_only</c> or <c>public</c> (listed on your SeatLayer website; an ended Season cannot
    /// become public, <c>409 sales_ended</c>). Left out when null.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Turned on while the Season is on sale, each upcoming date's own sales open through its
    /// readiness checks; turned off, they stop. The answer's <c>singleTicketDates</c> says which.
    /// Left out when null.
    /// </summary>
    public bool? SingleTickets { get; set; }

    /// <summary>
    /// <c>block</c> turns season seats on for this Season; <c>per_hold</c> turns them off again,
    /// refused with <c>409 season_block_exists</c> once seats are set aside. It does not change the
    /// Season's <c>revision</c>. Refused with <c>409 season_block_packages</c> (more than one
    /// package), <c>409 season_block_on_sale</c> (sales already opened without season seats),
    /// <c>409 season_not_draft_or_active</c>, or <c>422 invalid_inventory_mode</c>. Left out when
    /// null.
    /// </summary>
    public string? InventoryMode { get; set; }

    /// <summary>
    /// Hours (0 to 720) before a date starts that its tickets stop being sent to others. Left out
    /// when unassigned; assign null to send null, which means the club default (Settings ›
    /// Ticket site), or until the date starts when the club has none.
    /// </summary>
    public int? TransferCutoffHours
    {
        get => _transferCutoffHours;
        set
        {
            _transferCutoffHours = value;
            _transferCutoffHoursSet = true;
        }
    }

    /// <summary>
    /// Hours (0 to 720) before a date starts that a holder can no longer give a seat back. Left out
    /// when unassigned; assign null to send null, which means the club default.
    /// </summary>
    public int? ReturnCutoffHours
    {
        get => _returnCutoffHours;
        set
        {
            _returnCutoffHours = value;
            _returnCutoffHoursSet = true;
        }
    }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }

    private int? _transferCutoffHours;
    private int? _returnCutoffHours;
    private bool _transferCutoffHoursSet;
    private bool _returnCutoffHoursSet;

    internal Dictionary<string, object?> ToBody()
    {
        var body = Body.Of(
            ("expectedRevision", ExpectedRevision),
            ("name", Name),
            ("edition", Edition),
            ("checkoutMode", CheckoutMode),
            ("audience", Audience),
            ("singleTickets", SingleTickets),
            ("inventoryMode", InventoryMode));
        // Body.Of drops nulls, but an assigned null cutoff means "until the date starts".
        if (_transferCutoffHoursSet)
        {
            body["transferCutoffHours"] = _transferCutoffHours;
        }

        if (_returnCutoffHoursSet)
        {
            body["returnCutoffHours"] = _returnCutoffHours;
        }

        return body;
    }
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

    /// <summary>Where the holder's renewal emails go. Left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HolderEmail { get; set; }

    /// <summary>The holder's name on renewal emails. Left out when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HolderName { get; set; }
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

/// <summary>Starts a SeatLayer checkout that renews an offer, with no hosted page.</summary>
public sealed class SeasonRenewalCheckoutRequest
{
    /// <summary>
    /// Where the buyer comes back to, on one of the organization's allowed domains.
    /// <c>seatlayer_checkout=success</c> or <c>seatlayer_checkout=cancelled</c> is added.
    /// </summary>
    public required string ReturnUrl { get; set; }

    /// <summary>
    /// Where the tickets go when no email is on file for the holder; the one on file wins.
    /// </summary>
    public string? BuyerEmail { get; set; }

    /// <summary>The buyer's name.</summary>
    public string? BuyerName { get; set; }

    /// <summary>Which connected gateway to pay with, when the organization has more than one.</summary>
    public string? Provider { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }

    internal Dictionary<string, object?> ToBody() => Body.Of(
        ("returnUrl", ReturnUrl),
        ("buyerEmail", BuyerEmail),
        ("buyerName", BuyerName),
        ("provider", Provider));
}

/// <summary>Seats kept for single tickets, or released: by section, category or label.</summary>
public sealed class SeasonSeatSelection
{
    /// <summary>Whole sections.</summary>
    public IEnumerable<string>? SectionIds { get; set; }

    /// <summary>Every seat of these categories.</summary>
    public IEnumerable<string>? CategoryKeys { get; set; }

    /// <summary>Single seats, by label.</summary>
    public IEnumerable<string>? Labels { get; set; }

    internal Dictionary<string, object?> ToBody() => Body.Of(
        ("sectionIds", SectionIds?.ToList()),
        ("categoryKeys", CategoryKeys?.ToList()),
        ("labels", Labels?.ToList()));
}

/// <summary>Sets a Season's seats aside on every date.</summary>
public sealed class SeasonBlockSetAsideRequest
{
    /// <summary>The Season revision you read; another answers <c>409 revision_mismatch</c>.</summary>
    public required long ExpectedRevision { get; set; }

    /// <summary>
    /// Left out of the block: kept for single tickets. Null keeps nothing back, so the block is every
    /// seat the packages price.
    /// </summary>
    public SeasonSeatSelection? KeepForSingleTickets { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>What a release covers: every unsold seat (<see cref="All"/>), or one of the lists.</summary>
public sealed class SeasonBlockReleaseScope
{
    /// <summary>True: every unsold seat.</summary>
    public bool? All { get; set; }

    /// <summary>Whole sections.</summary>
    public IEnumerable<string>? SectionIds { get; set; }

    /// <summary>Every seat of these categories.</summary>
    public IEnumerable<string>? CategoryKeys { get; set; }

    /// <summary>Single seats, by label.</summary>
    public IEnumerable<string>? Labels { get; set; }

    internal Dictionary<string, object?> ToBody() => Body.Of(
        ("all", All),
        ("sectionIds", SectionIds?.ToList()),
        ("categoryKeys", CategoryKeys?.ToList()),
        ("labels", Labels?.ToList()));
}

/// <summary>Releases season seats to single sale.</summary>
public sealed class SeasonBlockReleaseRequest
{
    /// <summary>The Season revision you read; another answers <c>409 revision_mismatch</c>.</summary>
    public required long ExpectedRevision { get; set; }

    /// <summary>What the release covers.</summary>
    public required SeasonBlockReleaseScope Scope { get; set; }

    /// <summary>Release on these dates only. Null: every date still to come.</summary>
    public IEnumerable<string>? EventKeys { get; set; }

    /// <summary>An ISO time to release at. Null: now.</summary>
    public string? At { get; set; }

    /// <summary>Kept on the release.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Creates an audited occurrence amendment.</summary>
public sealed class SeasonAmendmentCreateRequest
{
    /// <summary>Occurrence event key.</summary>
    public required string EventKey { get; set; }

    /// <summary>
    /// <c>reschedule</c>, <c>replace</c>, <c>cancel_exception</c>, <c>postpone</c> (the date stays
    /// with no new time yet), or <c>cancel</c> (the date will not happen).
    /// </summary>
    public required string Kind { get; set; }

    /// <summary>Replacement start time in epoch milliseconds.</summary>
    public long? StartsAt { get; set; }

    /// <summary>Replacement occurrence name.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// With <c>cancel</c> only: refund each SeatLayer order's share of the date as a job.
    /// <c>none</c> (the default) or <c>per_date</c>; a whole-order refund is the order refund.
    /// </summary>
    public string? Refund { get; set; }

    /// <summary>Kept on the refund job.</summary>
    public string? Reason { get; set; }

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

/// <summary>Derives renewal rights for a successor plan from a previous Season's holders.</summary>
public sealed class SeasonHolderImportFromSeasonRequest
{
    /// <summary>The previous Season whose holders carry over.</summary>
    public required string SourceSeasonId { get; set; }

    /// <summary>Successor plan activation receiving the renewable rights.</summary>
    public required string SuccessorPlanActivationId { get; set; }

    /// <summary>When true, validates without committing.</summary>
    public bool? DryRun { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Creates performances inside a draft Season from a repeat rule.</summary>
public sealed class SeasonPerformanceCreateRequest
{
    /// <summary>Pattern each performance title is made from.</summary>
    public required string TitlePattern { get; set; }

    /// <summary>First start time in epoch milliseconds.</summary>
    public long? FirstStartsAt { get; set; }

    /// <summary>IANA time zone the repeat rule is read in.</summary>
    public required string Timezone { get; set; }

    /// <summary><c>none</c>, <c>daily</c>, <c>weekly</c>, <c>biweekly</c>, or <c>dates</c>.</summary>
    public required string Repeat { get; set; }

    /// <summary>How many performances to create, from 1 to 20.</summary>
    public int? Count { get; set; }

    /// <summary>Start times in epoch milliseconds, for the <c>dates</c> rule.</summary>
    public IEnumerable<long>? Dates { get; set; }

    /// <summary>Venue label for each performance.</summary>
    public string? Venue { get; set; }

    /// <summary>Length of each performance in minutes.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>Live-inventory region; one of the <see cref="EventHostingRegions"/> values.</summary>
    public string? Region { get; set; }

    /// <summary>Packages that should include the new performances.</summary>
    public IEnumerable<string>? AttachToPlanKeys { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Attaches existing events to a draft Season and its packages.</summary>
public sealed class SeasonPerformanceAttachRequest
{
    /// <summary>Event keys to attach.</summary>
    public required IEnumerable<string> EventKeys { get; set; }

    /// <summary>Packages that should include the attached performances.</summary>
    public IEnumerable<string>? AttachToPlanKeys { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Renames a draft package or changes the performances it covers.</summary>
public sealed class SeasonPlanUpdateRequest
{
    /// <summary>Replacement package name, when supplied.</summary>
    public string? Name { get; set; }

    /// <summary>Replacement performance event keys, when supplied.</summary>
    public IEnumerable<string>? EventKeys { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Options for a server-side Season checkout test.</summary>
public sealed class SeasonCheckoutTestRequest
{
    /// <summary>Package to test.</summary>
    public string? PlanKey { get; set; }

    /// <summary>Seats to hold, from 1 to 4.</summary>
    public int? Quantity { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>One chart category price in a package price table.</summary>
public sealed class SeasonPackagePrice
{
    /// <summary>Chart category key.</summary>
    public required string CategoryKey { get; set; }

    /// <summary>
    /// <c>adult</c>, <c>child</c>, <c>concession</c>, <c>senior</c>, or <c>student</c>. Left out
    /// when null, which prices the category for adults. Each category takes each type once.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PriceTypeKey { get; set; }

    /// <summary>Package price in minor currency units.</summary>
    public required long UnitPrice { get; set; }
}

/// <summary>Replaces a package price table.</summary>
public sealed class SeasonPackagePricesRequest
{
    /// <summary>The whole price table, one entry per chart category.</summary>
    public required IEnumerable<SeasonPackagePrice> Prices { get; set; }

    /// <summary>Three-letter ISO currency code.</summary>
    public string? Currency { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// The sale an occurrence allocation came from: a Season booking action or a renewal offer.
/// Set exactly one.
/// </summary>
public sealed class SeasonOccurrenceInventorySource
{
    /// <summary>Season booking action that sold the seats.</summary>
    public string? BookingActionId { get; set; }

    /// <summary>Renewal offer that sold the seats.</summary>
    public string? RenewalOfferId { get; set; }

    internal Dictionary<string, object?> ToBody()
        => Body.Of(("bookingActionId", BookingActionId), ("renewalOfferId", RenewalOfferId));
}

/// <summary>Returns one Season occurrence allocation to ordinary event inventory.</summary>
public sealed class SeasonOccurrenceReturnRequest
{
    /// <summary>Stable action id for this return.</summary>
    public required string ActionId { get; set; }

    /// <summary>Merchant booking reference that holds the seats.</summary>
    public required string BookingRef { get; set; }

    /// <summary>Plan activation that owns the seats.</summary>
    public required string PlanActivationId { get; set; }

    /// <summary>Seats being returned.</summary>
    public required IEnumerable<string> Labels { get; set; }

    /// <summary>The booking action or renewal offer that sold the seats.</summary>
    public required SeasonOccurrenceInventorySource Source { get; set; }
}

/// <summary>Reclaims returned occurrence inventory while it is still free.</summary>
public sealed class SeasonOccurrenceReclaimRequest
{
    /// <summary>Stable action id for this reclaim.</summary>
    public required string ActionId { get; set; }

    /// <summary>The return action being reversed.</summary>
    public required string ReturnActionId { get; set; }

    /// <summary>Merchant booking reference that held the seats.</summary>
    public required string BookingRef { get; set; }

    /// <summary>Plan activation that owns the seats.</summary>
    public required string PlanActivationId { get; set; }

    /// <summary>Seats being reclaimed.</summary>
    public required IEnumerable<string> Labels { get; set; }

    /// <summary>The booking action or renewal offer that sold the seats.</summary>
    public required SeasonOccurrenceInventorySource Source { get; set; }
}

/// <summary>Search filters for a Season's holders.</summary>
public class SeasonHolderFilterRequest
{
    /// <summary>Search text, sent as <c>q</c>: holder name, email, reference, order number or seat.</summary>
    public string? Query { get; set; }

    /// <summary>Restrict results to one holder status.</summary>
    public string? Status { get; set; }

    internal Dictionary<string, string?> ToFilterQuery() => new()
    {
        ["q"] = Query,
        ["status"] = Status,
    };
}

/// <summary>Search filters and cursor paging for a Season's holders.</summary>
public sealed class SeasonHolderListRequest : SeasonHolderFilterRequest
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

/// <summary>
/// Cursor paging for a Season's renewal offers, newest first: the offer list and the hosted
/// renewal state.
/// </summary>
public sealed class SeasonRenewalOfferPageRequest
{
    /// <summary>The <c>nextCursor</c> of the page before.</summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Offers a page: 1-100 for the offer list (default 100), 1-500 for the hosted renewal state
    /// (default 500).
    /// </summary>
    public int? Limit { get; set; }

    internal Dictionary<string, string?> ToQuery() => new()
    {
        ["cursor"] = Cursor,
        ["limit"] = Limit?.ToString(),
    };
}

/// <summary>Options for taking back one date of a Season order.</summary>
public sealed class SeasonOrderDateCancelRequest
{
    /// <summary>Stable action id for this cancellation.</summary>
    public string? ActionId { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>Refunds every SeatLayer order holding a cancelled date, as a job.</summary>
public sealed class SeasonPerformanceRefundRequest
{
    /// <summary>
    /// <c>per_date</c>: the date's share of each order, rounded down. <c>full</c> is refused
    /// (<c>422 date_refund_per_date_only</c>); refund a whole order with the order refund.
    /// </summary>
    public required string Amount { get; set; }

    /// <summary>Kept on the job.</summary>
    public string? Reason { get; set; }

    /// <summary>Optional key for exact server response replay.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// The name and email a renewal offer is sent to. Assign null to clear a field; leave a field
/// unassigned to keep its current value.
/// </summary>
public sealed class SeasonRenewalContactRequest
{
    private string? _email;
    private string? _name;
    private bool _emailSet;
    private bool _nameSet;

    /// <summary>Contact email, or null to clear it.</summary>
    public string? Email
    {
        get => _email;
        set
        {
            _email = value;
            _emailSet = true;
        }
    }

    /// <summary>Contact name, or null to clear it.</summary>
    public string? Name
    {
        get => _name;
        set
        {
            _name = value;
            _nameSet = true;
        }
    }

    internal Dictionary<string, object?> ToBody()
    {
        // Body.Of drops nulls, but an assigned null is the clear instruction here.
        var body = new Dictionary<string, object?>();
        if (_emailSet)
        {
            body["email"] = _email;
        }

        if (_nameSet)
        {
            body["name"] = _name;
        }

        return body;
    }
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

    /// <summary>
    /// The id of the job voiding a cancelled date's tickets: <c>sbj_void_</c> and the cancel's
    /// <c>amendmentId</c>. Cancelling a date starts it (a repeated cancel joins it); follow it with
    /// <see cref="RetrieveSeasonBulkJobAsync"/>. Each Season ticket it voids is told by
    /// <c>season.ticket.voided</c> with reason <c>date_cancelled</c>.
    /// </summary>
    public static string DateCancelVoidJobId(string amendmentId) => "sbj_void_" + amendmentId;

    /// <summary>
    /// The most rows one <see cref="CreateSeasonHolderImportAsync"/> call takes for a package of
    /// <paramref name="dates"/> dates. Each row is fenced on every date: 100 up to 20 dates, then
    /// <c>floor(2300 / (dates + 3))</c>, 43 at 50. A call with more answers
    /// <c>422 invalid_rows</c> with <c>maxRows</c>.
    /// </summary>
    public static int HolderImportRowsPerCall(int dates)
        => (int)Math.Max(1L, Math.Min(100L, 2300L / (Math.Max(1L, dates) + 3)));

    /// <summary>
    /// Whether an answer is the <c>202</c> a season-seats season's date cancel
    /// (<see cref="CreateSeasonAmendmentAsync"/>) or past date
    /// (<see cref="MarkSeasonPerformancePastAsync"/>) sends while the date is still leaving every
    /// season seat: <c>state</c> <c>finishing</c>, <c>code</c> <c>block_date_leave_pending</c>, with
    /// <c>eventKey</c>, <c>shardsLeft</c> and <c>shardCount</c>. It finishes by itself: send the same
    /// request again after <c>Retry-After</c> to read how it ended.
    /// </summary>
    public static bool IsDateLeavePending(IReadOnlyDictionary<string, object?>? answer)
        => answer is not null
            && answer.TryGetValue("state", out var state) && state is "finishing"
            && answer.TryGetValue("code", out var code) && code is "block_date_leave_pending";

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

        if (request.SingleTickets is not null)
        {
            body["singleTickets"] = request.SingleTickets;
        }

        if (request.InventoryMode is not null)
        {
            body["inventoryMode"] = request.InventoryMode;
        }

        return _client.PostHeaderReplayAsync(
            "/v1/seasons", body, request.IdempotencyKey, cancellationToken);
    }

    /// <summary>Retrieves one Season and its plans.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey), null, cancellationToken);

    /// <summary>
    /// Updates Season identity fields, checkout mode, transfer and return cutoffs, and single
    /// tickets with compare-and-set and header replay. A change to single tickets names the dates
    /// it opened or stopped in <c>singleTicketDates</c>; its <c>jobRefused</c> says why the
    /// <c>season_singles</c> job could not start. The change still stands, and sending
    /// <c>SingleTickets = true</c> again opens any date still not open. An exact retry with the
    /// same <c>ExpectedRevision</c> and idempotency key is not applied twice.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateSeasonAsync(
        string seasonKey, SeasonUpdateRequest request, CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Patch,
            Path(seasonKey),
            request.ToBody(),
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

    /// <summary>
    /// Opens Season sales. The answer's <c>warnings</c> say what is advised but not required, such
    /// as <c>checkout_test_missing</c> until a checkout test or rehearsal has passed. Refused with
    /// <c>409 plan_not_published</c> or <c>409 checkout_mode_not_chosen</c> until a package is
    /// published and a checkout mode is chosen, and with <c>409 block_not_ready</c> when season
    /// seats are turned on (<c>InventoryMode = "block"</c>) but the seats are not set aside yet.
    /// </summary>
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

    /// <summary>
    /// Creates a dry-run or committed incumbent holder import with header replay. A call takes at
    /// most <see cref="HolderImportRowsPerCall"/> rows for the package's dates (100 up to 20
    /// dates); more answers <c>422 invalid_rows</c> with <c>maxRows</c> in the exception's
    /// <c>Body</c>.
    /// </summary>
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

    /// <summary>
    /// Creates renewal offers with exact header replay. For 100 contracts or fewer the offers are
    /// made in this call: <c>201 {offers}</c>. Above 100, a <c>renewal_run</c> job makes them:
    /// <c>202 {job, batchId}</c>. Follow the job with <see cref="RetrieveSeasonBulkJobAsync"/>, then
    /// read the offers with <c>ListSeasonRenewalOffersAsync</c>; the same key answers the same job.
    /// Refuses with <c>409 job_mismatch</c> or <c>503 season_not_configured</c>.
    /// </summary>
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

    /// <summary>Lists the first page of renewal offers, newest first (100 offers).</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonRenewalOffersAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => ListSeasonRenewalOffersAsync(seasonKey, null, cancellationToken);

    /// <summary>
    /// Lists one page of renewal offers, newest first. Pass the page's <c>nextCursor</c> as
    /// <c>Cursor</c> for the next; it is null on the last. <c>Limit</c> is 1-100 (default 100). A
    /// cursor this list did not give out or a limit out of range answers
    /// <c>400 invalid_cursor</c> or <c>400 invalid_limit</c>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonRenewalOffersAsync(
        string seasonKey, SeasonRenewalOfferPageRequest? request,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, "/renewal-offers"), request?.ToQuery(), cancellationToken);

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

    /// <summary>Declines a renewal offer and releases its seats at once.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeclineSeasonRenewalOfferAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => DeclineSeasonRenewalOfferAsync(seasonKey, offerId, null, cancellationToken);

    /// <summary>
    /// Declines a renewal offer and releases its seats at once, keeping the holder's reason
    /// (1 to 200 characters) on the offer as <c>declineReason</c>. A repeated decline keeps the
    /// first reason. A null reason sends none.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> DeclineSeasonRenewalOfferAsync(
        string seasonKey, string offerId, string? reason,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/decline"),
            Body.Of(("reason", reason)),
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

    /// <summary>
    /// Creates an audited occurrence amendment with exact header replay. A <c>cancel</c> also
    /// voids every live ticket for the date as a <c>date_cancel_void</c> job,
    /// <see cref="DateCancelVoidJobId"/> of the answer's <c>amendmentId</c>; follow it with
    /// <see cref="RetrieveSeasonBulkJobAsync"/>. A cancelled date can't be moved
    /// (<c>reschedule</c>), postponed or replaced: <c>409 performance_cancelled</c>. On a
    /// season-seats season, a <c>cancel</c> first takes the date off every season seat: while that
    /// finishes the answer is a <c>202</c> (check with <see cref="IsDateLeavePending"/>); send the
    /// same request again to read the amendment.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonAmendmentAsync(
        string seasonKey, SeasonAmendmentCreateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/amendments"),
            Body.Of(
                ("eventKey", request.EventKey),
                ("kind", request.Kind),
                ("startsAt", request.StartsAt),
                ("name", request.Name),
                ("refund", request.Refund),
                ("reason", request.Reason)),
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

    /// <summary>
    /// Dry-runs or commits renewal rights derived from a previous Season's holders, with header replay.
    /// A dry run answers <c>200 {import}</c>; a commit of 100 holders or fewer <c>201 {import}</c>.
    /// A commit of more than 100 holders answers <c>202 {job}</c>: a <c>holder_import</c> job brings
    /// them across. Follow it with <see cref="RetrieveSeasonBulkJobAsync"/>; its <c>summary</c> has
    /// the same counts when it completes, and the same key answers the same job. Refuses with
    /// <c>409 job_mismatch</c>, <c>503 job_busy</c> or <c>503 season_not_configured</c>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonHolderImportFromSeasonAsync(
        string seasonKey, SeasonHolderImportFromSeasonRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/imports/from-season"),
            Body.Of(
                ("sourceSeasonId", request.SourceSeasonId),
                ("successorPlanActivationId", request.SuccessorPlanActivationId),
                ("dryRun", request.DryRun)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists Season performances and package membership in every lifecycle state.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonPerformancesAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/performances"), null, cancellationToken);

    /// <summary>Creates performances inside a draft Season from a repeat rule, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonPerformancesAsync(
        string seasonKey, SeasonPerformanceCreateRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/performances"),
            Body.Of(
                ("titlePattern", request.TitlePattern),
                ("firstStartsAt", request.FirstStartsAt),
                ("timezone", request.Timezone),
                ("repeat", request.Repeat),
                ("count", request.Count),
                ("dates", request.Dates?.ToList()),
                ("venue", request.Venue),
                ("durationMinutes", request.DurationMinutes),
                ("region", request.Region),
                ("attachToPlanKeys", request.AttachToPlanKeys?.ToList())),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Attaches existing events to a draft Season and its packages, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> AttachSeasonPerformancesAsync(
        string seasonKey, SeasonPerformanceAttachRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/performances/attach"),
            Body.Of(
                ("eventKeys", request.EventKeys.ToList()),
                ("attachToPlanKeys", request.AttachToPlanKeys?.ToList())),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Removes a performance from a draft Season and every package, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteSeasonPerformanceAsync(
        string seasonKey, string eventKey, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Delete,
            Path(seasonKey, $"/performances/{SeatLayerClient.Escape(eventKey)}"),
            null,
            idempotencyKey,
            cancellationToken);

    /// <summary>Renames a draft package or changes the performances it covers, with header replay.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateSeasonPlanAsync(
        string seasonKey, string planKey, SeasonPlanUpdateRequest request,
        CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Patch,
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}"),
            Body.Of(("name", request.Name), ("eventKeys", request.EventKeys?.ToList())),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Deletes a draft package that is not the last one, with header replay. The API answers
    /// 204 with no body, so this returns nothing.
    /// </summary>
    public async Task DeleteSeasonPlanAsync(
        string seasonKey, string planKey, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => await _client.MutationWithHeaderReplayAsync(
            HttpMethod.Delete,
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}"),
            null,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Activates the Season structure and publishes every draft package in one call, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> PublishSeasonAsync(
        string seasonKey, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/publish"), null, idempotencyKey, cancellationToken);

    /// <summary>
    /// Runs a server-side hold and release to prove the Season buyer path, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonCheckoutTestAsync(
        string seasonKey, SeasonCheckoutTestRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/checkout-tests"),
            Body.Of(("planKey", request?.PlanKey), ("quantity", request?.Quantity)),
            request?.IdempotencyKey,
            cancellationToken);

    /// <summary>Lists recent Season checkout tests, newest first.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonCheckoutTestsAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/checkout-tests"), null, cancellationToken);

    /// <summary>
    /// Replaces a package price table, per chart category in minor units, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReplaceSeasonPackagePricesAsync(
        string seasonKey, string planKey, SeasonPackagePricesRequest request,
        CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Put,
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}/prices"),
            Body.Of(("prices", request.Prices.ToList()), ("currency", request.Currency)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Lists every package price table for a Season. Each price carries its <c>priceTypeKey</c>
    /// and <c>priceTypeLabel</c>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonPricesAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/prices"), null, cancellationToken);

    /// <summary>
    /// Adds a performance to a published package and extends its allocations, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> AttachSeasonPlanPerformanceAsync(
        string seasonKey, string planKey, string eventKey, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, $"/plans/{SeatLayerClient.Escape(planKey)}/performances"),
            Body.Of(("eventKey", eventKey)),
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Removes a performance from a published package and releases its allocations, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> DeleteSeasonPlanPerformanceAsync(
        string seasonKey, string planKey, string eventKey, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.MutationWithHeaderReplayAsync(
            HttpMethod.Delete,
            Path(
                seasonKey,
                $"/plans/{SeatLayerClient.Escape(planKey)}/performances/{SeatLayerClient.Escape(eventKey)}"),
            null,
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Marks a performance past so it stops blocking new Season sales, with header replay.
    /// A cancelled date answers <c>409 performance_cancelled</c>. On a season-seats season the date
    /// first leaves every season seat: while that finishes the answer is a <c>202</c> (check with
    /// <see cref="IsDateLeavePending"/>).
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> MarkSeasonPerformancePastAsync(
        string seasonKey, string eventKey, bool? force = null, string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, $"/performances/{SeatLayerClient.Escape(eventKey)}/past"),
            Body.Of(("force", force)),
            idempotencyKey,
            cancellationToken);

    /// <summary>
    /// Returns one Season occurrence allocation to ordinary event inventory. Domain-idempotent by
    /// action id, and single-attempt.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReturnSeasonOccurrenceInventoryAsync(
        string seasonKey, string eventKey, SeasonOccurrenceReturnRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/occurrences/{SeatLayerClient.Escape(eventKey)}/returns"),
            Body.Of(
                ("actionId", request.ActionId),
                ("bookingRef", request.BookingRef),
                ("planActivationId", request.PlanActivationId),
                ("labels", request.Labels.ToList()),
                ("source", request.Source.ToBody())),
            null,
            cancellationToken);

    /// <summary>
    /// Reclaims returned occurrence inventory while it is still free. Domain-idempotent by action
    /// id, and single-attempt.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReclaimSeasonOccurrenceInventoryAsync(
        string seasonKey, string eventKey, SeasonOccurrenceReclaimRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/occurrences/{SeatLayerClient.Escape(eventKey)}/reclaims"),
            Body.Of(
                ("actionId", request.ActionId),
                ("returnActionId", request.ReturnActionId),
                ("bookingRef", request.BookingRef),
                ("planActivationId", request.PlanActivationId),
                ("labels", request.Labels.ToList()),
                ("source", request.Source.ToBody())),
            null,
            cancellationToken);

    /// <summary>Lists Season ticket holders, one page at a time.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonHoldersAsync(
        string seasonKey, SeasonHolderListRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, "/holders"), (request ?? new()).ToQuery(), cancellationToken);

    /// <summary>
    /// Refunds a Season order in full or in part through its payment provider, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RefundSeasonOrderAsync(
        string seasonKey, string orderId, OrderRefundRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, $"/orders/{SeatLayerClient.Escape(orderId)}/refund"),
            Body.Of(("amountMinor", request?.AmountMinor)),
            request?.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Cancels a Season order without moving money and releases every date, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CancelSeasonOrderAsync(
        string seasonKey, string orderId, OrderCancelRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, $"/orders/{SeatLayerClient.Escape(orderId)}/cancel"),
            request.ToBody(),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Takes back one date of a Season order and puts its seats back on sale, with header replay.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CancelSeasonOrderDateAsync(
        string seasonKey, string orderId, string eventKey,
        SeasonOrderDateCancelRequest? request = null, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(
                seasonKey,
                $"/orders/{SeatLayerClient.Escape(orderId)}/dates/{SeatLayerClient.Escape(eventKey)}/cancel"),
            Body.Of(("actionId", request?.ActionId)),
            request?.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Emails a Season order's tickets again. Single-attempt: the API has no replay contract
    /// for it.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ResendSeasonOrderTicketsAsync(
        string seasonKey, string orderId, string? email = null,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/orders/{SeatLayerClient.Escape(orderId)}/resend"),
            Body.Of(("email", email)),
            null,
            cancellationToken);

    /// <summary>Retrieves renewal links, payments and emails for the first page of offers (500).</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonHostedRenewalsAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => RetrieveSeasonHostedRenewalsAsync(seasonKey, null, cancellationToken);

    /// <summary>
    /// Retrieves renewal links, payments and emails for one page of offers, newest first. Pass the
    /// page's <c>nextCursor</c> as <c>Cursor</c> for the next; it is null on the last. <c>Limit</c>
    /// is 1-500 (default 500). A cursor this list did not give out or a limit out of range answers
    /// <c>400 invalid_cursor</c> or <c>400 invalid_limit</c>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonHostedRenewalsAsync(
        string seasonKey, SeasonRenewalOfferPageRequest? request,
        CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, "/renewal-hosted"), request?.ToQuery(), cancellationToken);

    /// <summary>
    /// Makes a new renewal link for one holder; the old link stops working. Never retried: the
    /// new link is revealed once.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RotateSeasonRenewalOfferLinkAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/link"),
            null,
            null,
            cancellationToken);

    /// <summary>Sets the name and email a renewal offer is sent to.</summary>
    public Task<IReadOnlyDictionary<string, object?>> UpdateSeasonRenewalOfferContactAsync(
        string seasonKey, string offerId, SeasonRenewalContactRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/contact"),
            request.ToBody(),
            null,
            cancellationToken);

    /// <summary>
    /// Emails renewal invitations now and reminders before the deadline, for every offer or only
    /// the listed ones. At most 20,000 offer ids; more answers <c>422 too_many_offer_ids</c>. Leave
    /// them out to email every open offer.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonRenewalNoticesAsync(
        string seasonKey, IEnumerable<string>? offerIds = null,
        CancellationToken cancellationToken = default)
        => CreateSeasonRenewalNoticesAsync(seasonKey, offerIds, null, cancellationToken);

    /// <summary>
    /// Emails renewal invitations now and reminders before the deadline. <paramref name="batchId"/>,
    /// instead of offer ids, emails every open offer one <c>CreateSeasonRenewalOffersAsync</c> batch
    /// made (its <c>batchId</c>). A null <paramref name="offerIds"/> or <paramref name="batchId"/>
    /// sends none.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonRenewalNoticesAsync(
        string seasonKey, IEnumerable<string>? offerIds, string? batchId,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, "/renewal-notices"),
            Body.Of(("offerIds", offerIds?.ToList()), ("batchId", batchId)),
            null,
            cancellationToken);

    /// <summary>Sends one holder a new renewal invitation. Single-attempt.</summary>
    public Task<IReadOnlyDictionary<string, object?>> ResendSeasonRenewalOfferAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/resend"),
            null,
            null,
            cancellationToken);

    /// <summary>
    /// Retrieves what renewing an offer costs, priced as the SeatLayer checkout will charge it.
    /// <c>problem</c> is null when SeatLayer checkout can take the renewal now; otherwise it says
    /// why not.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonRenewalQuoteAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/quote"),
            null,
            cancellationToken);

    /// <summary>
    /// Records that the holder means to renew, for a renewal taken by phone or at the box office,
    /// so your own checkout can commit the offer. Single-attempt; a repeat answers the same offer.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonRenewalIntentAsync(
        string seasonKey, string offerId, CancellationToken cancellationToken = default)
        => _client.PostAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/intent"),
            null,
            null,
            cancellationToken);

    /// <summary>
    /// Starts a SeatLayer checkout that renews an offer, with no hosted page, with header replay.
    /// Send the buyer to <c>payment.url</c>; paying renews the offer. A repeat with the same key
    /// answers the same order.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> CreateSeasonRenewalCheckoutAsync(
        string seasonKey, string offerId, SeasonRenewalCheckoutRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, $"/renewal-offers/{SeatLayerClient.Escape(offerId)}/checkout"),
            request.ToBody(),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>Retrieves Season money, seats, holders and renewals as totals.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonSalesReportAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/reports/sales"), null, cancellationToken);

    /// <summary>Retrieves seats still free per package and per date.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonSeatsReportAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/reports/seats"), null, cancellationToken);

    /// <summary>Downloads every Season ticket holder as CSV, returned as raw text.</summary>
    public Task<string> ExportSeasonHoldersCsvAsync(
        string seasonKey, SeasonHolderFilterRequest? request = null,
        CancellationToken cancellationToken = default)
        => _client.GetCsvAsync(
            Path(seasonKey, "/reports/holders.csv"), request?.ToFilterQuery(), cancellationToken);

    /// <summary>Downloads every Season order SeatLayer took as CSV, returned as raw text.</summary>
    public Task<string> ExportSeasonOrdersCsvAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetCsvAsync(Path(seasonKey, "/reports/orders.csv"), null, cancellationToken);

    /// <summary>Retrieves Season seats sold per package and per date.</summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonSeatCountsAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/seat-counts"), null, cancellationToken);

    /// <summary>
    /// Refunds every SeatLayer order holding a cancelled date, as a job, with header replay: a
    /// retry with the same key replays the same job. Follow it with
    /// <see cref="RetrieveSeasonBulkJobAsync"/>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RefundSeasonPerformanceAsync(
        string seasonKey, string eventKey, SeasonPerformanceRefundRequest request,
        CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, $"/performances/{SeatLayerClient.Escape(eventKey)}/refunds"),
            Body.Of(("amount", request.Amount), ("reason", request.Reason)),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Releases the seats of every lapsed renewal offer, as a job, with header replay: a retry with
    /// the same key replays the same job. An offer extended before its turn is skipped and keeps its
    /// seat. Answers <c>202 {job}</c>; follow it with <see cref="RetrieveSeasonBulkJobAsync"/>.
    /// <c>503 job_busy</c> means another job holds the Season: send the same request again.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReleaseSeasonLapsedOffersAsync(
        string seasonKey, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/renewal-offers/release-lapsed"), null, idempotencyKey, cancellationToken);

    /// <summary>Lists a Season's bulk jobs, newest first (100 at most).</summary>
    public Task<IReadOnlyDictionary<string, object?>> ListSeasonBulkJobsAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/jobs"), null, cancellationToken);

    /// <summary>
    /// Retrieves one bulk job with its counts, up to 500 failed items, and the <c>summary</c> it
    /// ended with (empty until it completes). Job kinds include <c>holder_import</c> and
    /// <c>date_cancel_void</c>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonBulkJobAsync(
        string seasonKey, string jobId, CancellationToken cancellationToken = default)
        => _client.GetAsync(
            Path(seasonKey, $"/jobs/{SeatLayerClient.Escape(jobId)}"), null, cancellationToken);

    /// <summary>
    /// Sets the Season's seats aside on every date: every seat its packages price, minus
    /// <see cref="SeasonBlockSetAsideRequest.KeepForSingleTickets"/>, with header replay: a retry
    /// with the same key replays the same answer. Answers <c>202 {job, block}</c>: a
    /// <c>season_block_set_aside</c> job to follow with <see cref="RetrieveSeasonBulkJobAsync"/>. On
    /// an active Season block mode turns on by itself once every date has the seats
    /// (<c>season.block.ready</c>); on a draft, Publish turns it on. Season seats must be on for the
    /// Season (<c>409 season_block_not_enabled</c> otherwise).
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> SetAsideSeasonBlockAsync(
        string seasonKey, SeasonBlockSetAsideRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/block"),
            Body.Of(
                ("expectedRevision", request.ExpectedRevision),
                ("keepForSingleTickets", request.KeepForSingleTickets?.ToBody())),
            request.IdempotencyKey,
            cancellationToken);

    /// <summary>
    /// Retrieves the Season's seats set aside on every date: <c>block</c> with its <c>mode</c>
    /// (<c>block</c> once on, else <c>per_hold</c>), <c>state</c>, <c>counts</c> and each date's
    /// <c>state</c>.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> RetrieveSeasonBlockAsync(
        string seasonKey, CancellationToken cancellationToken = default)
        => _client.GetAsync(Path(seasonKey, "/block"), null, cancellationToken);

    /// <summary>
    /// Releases season seats to single sale: every unsold seat, or the sections, categories or seats
    /// named, with header replay: a retry with the same key replays the same release. Answers
    /// <c>202 {job, block}</c>: a <c>season_block_release</c> job to follow with
    /// <see cref="RetrieveSeasonBulkJobAsync"/>. <c>season.block.released</c> is sent once it has run
    /// on every date.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> ReleaseSeasonBlockAsync(
        string seasonKey, SeasonBlockReleaseRequest request, CancellationToken cancellationToken = default)
        => _client.PostHeaderReplayAsync(
            Path(seasonKey, "/block/release"),
            Body.Of(
                ("expectedRevision", request.ExpectedRevision),
                ("scope", request.Scope.ToBody()),
                ("eventKeys", request.EventKeys?.ToList()),
                ("at", request.At),
                ("reason", request.Reason)),
            request.IdempotencyKey,
            cancellationToken);

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
