# SeatLayer .NET SDK: seat booking, reserved seating and season ticket API

[![CI](https://github.com/seatlayer/seatlayer-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/seatlayer/seatlayer-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SeatLayer.svg)](https://www.nuget.org/packages/SeatLayer)
[![License: MIT](https://img.shields.io/badge/license-MIT-111827.svg)](LICENSE)

SeatLayer's official .NET SDK gives ASP.NET Core and other C# backends on .NET 8 and newer one
async, thread-safe `SeatLayerClient` for reserved seating: register it as a singleton, read a
buyer's held seats with their real prices, book them under your order id, and run season tickets and
box office sales, with no package dependencies.

## What SeatLayer is

SeatLayer is reserved-seat ticketing and seating software: organizers sell with hosted checkout,
a box office, door check-in and season tickets, and platforms add the same seat maps, with 3D
seat views, through the SeatLayer SDK and API. One chart drives the 2D map, the 3D venue and the
view from each seat (3D for venues up to 60,000 seats; the 2D map benchmarked at 200,000 seats).

[Website](https://seatlayer.io/) · [For developers](https://seatlayer.io/developers/) ·
[Season tickets](https://seatlayer.io/season-tickets/) · [Pricing](https://seatlayer.io/pricing/) ·
[Live demo](https://seatlayer.io/demo/)

[SeatLayer package on NuGet](https://www.nuget.org/packages/SeatLayer) ·
[.NET server SDK guide](https://docs.seatlayer.io/server-sdk/dotnet/) ·
[SeatLayer developer platform](https://seatlayer.io/developers/) ·
[SeatLayer JavaScript seat map SDK](https://www.npmjs.com/package/@seatlayer/js) ·
[Server API reference](https://docs.seatlayer.io/server-api/events/)

> **Server-side only.** This library authenticates with your secret key. Never ship it in a client
> application. Browser surfaces get short-lived, origin-bound tokens that you mint here.

**How it works:** the buyer picks and holds seats in your website or app with your public key. Your server then reads the hold, takes the payment through your own provider, and books the seats with this SDK and your secret key.

**Start here:** [Quickstart](https://docs.seatlayer.io/start/quickstart/) · [Holds and checkout](https://docs.seatlayer.io/buyer-sdk/holds-and-checkout/) · [.NET server SDK guide](https://docs.seatlayer.io/server-sdk/dotnet/) · [SDK catalog](https://docs.seatlayer.io/sdk-catalog.json) · [Pricing](https://seatlayer.io/pricing/): 100 sold seats free every month, then 10¢ down to 5¢ per sold seat. No subscription, and purchased credits never expire. Test mode is free.

## Install the .NET seat booking SDK

```bash
dotnet add package SeatLayer
```

Or pin it in your project file:

```xml
<PackageReference Include="SeatLayer" Version="0.9.0" />
```

`SeatLayer` is published on NuGet. Requires .NET 8 or newer. **No package dependencies**: `HttpClient`, `System.Text.Json` and
`HMACSHA256` all ship with the framework, so the SDK forces no version on your application.

## Quick start

```csharp
using SeatLayer;

var client = new SeatLayerClient(Environment.GetEnvironmentVariable("SEATLAYER_SECRET_KEY")!);

// 1. Materialize a published catalog template as a draft for this organiser.
// Replace this placeholder with a template id from your catalog.
var chart = (IReadOnlyDictionary<string, object?>)(
    await client.Templates.InstantiateTemplateAsync("your-published-template"))["meta"]!;
await client.Charts.PublishAsync((string)chart["id"]!);

// 2. Create an event on it.
var created = await client.Events.CreateAsync(new EventCreateRequest
{
    ChartId = (string)chart["id"]!,
    Name = "Spring Gala",
    Currency = "EUR", // leave unset to inherit the workspace currency
    Region = EventHostingRegions.WesternEurope, // India: AsiaPacific
});
var meta = (IReadOnlyDictionary<string, object?>)created["meta"]!;
var eventKey = (string)meta["key"]!;

// 3. Sell four seats over the phone.
var held = await client.Inventory.HoldBestAvailableAsync(eventKey, new BestAvailableRequest { Qty = 4 });
// … take payment against held["items"], which carry authoritative prices …
await client.Inventory.BookAsync(eventKey, (string)held["holdId"]!, bookingRef: "order-8842");
```

## Event hosting region

Set `EventCreateRequest.Region` based on the **event venue**, not your API server or office. It
controls the initial placement of the Event's live inventory; an existing Event
cannot be moved later. Leave it unset to inherit the workspace default (`western-europe` for new accounts).
Set that default with the named `defaultRegion` argument to `Workspaces.CreateAsync` or
`Workspaces.UpdateDefaultRegionAsync`; changing it affects only future Events.

- `western-europe`, `eastern-europe`, `north-america-east`, `north-america-west`, `south-america`
- `asia-pacific`, `northeast-asia`, `southeast-asia`, `oceania`, `africa`, `middle-east`

The hint is best effort, not a data-residency guarantee. See the
[full Event region guide](https://docs.seatlayer.io/server-api/event-regions/).

Register the client as a **singleton**. It is thread-safe, and its `HttpClient` is meant to be
long-lived: constructing one per request exhausts sockets.

```csharp
builder.Services.AddSingleton(_ =>
    new SeatLayerClient(builder.Configuration["SeatLayer:SecretKey"]!));
```

Using `IHttpClientFactory`? Pass the client in and the SDK will not dispose it, because it does not
own its lifetime:

```csharp
new SeatLayerClient(secretKey, new SeatLayerClientOptions { HttpClient = factory.CreateClient() });
```

## Season tickets (Fixed Renewable Seasons)

`client.Seasons` has all 90 season operations: seasons and their packages, dates and package prices
(per price type, set with `SeasonPackagePrice.PriceTypeKey`: `adult`, `child`, `concession`,
`senior` or `student`), sales windows, checkout tests, buyer sessions and booking, holder imports
and the holder list, season orders, renewals (offers, quotes, intents, checkouts and hosted renewal
links), date changes and cancellations, season seats, bulk refunds with their jobs, sales and seat
reports, CSV exports, audit and support export. The bulk refund of a cancelled date is
`RefundSeasonPerformanceAsync`, the release of lapsed renewal offers is
`ReleaseSeasonLapsedOffersAsync`, and their jobs are read with `ListSeasonBulkJobsAsync` and
`RetrieveSeasonBulkJobAsync`.
Validate compatible events before creation, then keep the secret key on your server and give the
browser `SeasonPicker` only the show-once, origin-bound buyer token.

Before you open sales, run one test hold, booking and cancellation with their webhooks, then call
`ValidateSeasonBuyerRehearsalAsync(seasonKey)`. It finds those test records itself, so it takes no
body. With your own checkout, until a test like this or a checkout test has passed, opening sales answers with a
`checkout_test_missing` warning. A season hold lists each date's seat, category and quantity but no
price: when you run your own checkout, you set the package price and take the payment, and you
issue the tickets.

```csharp
var validation = await client.Seasons.ValidateSeasonAsync(new SeasonSelectionRequest
{
    EventKeys = new[] { "show-friday", "show-saturday" },
});

var created = await client.Seasons.CreateSeasonAsync(new SeasonCreateRequest
{
    Name = "2027 subscription",
    EventKeys = new[] { "show-friday", "show-saturday" },
    IdempotencyKey = "season-2027-create",
});
```

A `202` answer means the work was accepted and is still running: keep the returned operation id and
poll `RetrieveSeasonLifecycleAsync` until it finishes. Bookings, cancellations and renewal commits
are sent once and never retried by the SDK, so give each one your own stable id; sending the same
id again is safe. A buyer session is sent once too, and repeating it is not safe: if its answer is
lost, revoke what was created and create one new session.

When SeatLayer takes the Season's payments, renew a holder with no hosted page: read the price,
start a checkout, and send the holder to `payment.url`. For a renewal taken by phone or at the box
office, `CreateSeasonRenewalIntentAsync` records the holder's intent before you commit the offer.
A decline can carry the holder's reason.

```csharp
var quote = await client.Seasons.RetrieveSeasonRenewalQuoteAsync(seasonKey, offerId);
if (quote["problem"] is null)
{
    var started = await client.Seasons.CreateSeasonRenewalCheckoutAsync(
        seasonKey, offerId, new SeasonRenewalCheckoutRequest
        {
            ReturnUrl = "https://tickets.yourclub.example/renewed",
            IdempotencyKey = $"renew-{offerId}",
        });
    // Send the holder to the "url" under started["payment"].
}

await client.Holders.DeclineHolderRenewalOfferAsync("holder_123", offerId, "Moving away");
```

Renewal works at scale. Above 100 contracts, `CreateSeasonRenewalOffersAsync` answers
`202 {job, batchId}` instead of `201 {offers}`, and a commit of more than 100 holders from
`CreateSeasonHolderImportFromSeasonAsync` answers `202 {job}`: follow the job with
`RetrieveSeasonBulkJobAsync`. `ListSeasonRenewalOffersAsync` and
`RetrieveSeasonHostedRenewalsAsync` take a `SeasonRenewalOfferPageRequest` and answer a
`nextCursor`, null on the last page.

The three CSV exports (`Seasons.ExportSeasonHoldersCsvAsync`, `Seasons.ExportSeasonOrdersCsvAsync`
and `Orders.ExportEventOrdersCsvAsync`) return the CSV as a raw string. `Seasons.DeleteSeasonPlanAsync`
and `Events.DeleteEventDiscountCodeAsync` return a plain `Task`, because the API answers 204 with
no body. On request types where a field can be cleared (`SeasonRenewalContactRequest`, `HolderUpdateRequest`,
`EventPaymentGatewayReplaceRequest`, `EventDiscountCodeUpdateRequest`, and the cutoffs on
`SeasonUpdateRequest`), assigning `null` sends
`null`, and leaving the property unassigned leaves the field out.

### Season seats

Season seats are the seats a season sets aside on every date before its sales open. A season
buyer's hold then doesn't have to ask each date, and single-ticket buyers see those seats as
unavailable. They are off for every season until you set `InventoryMode = "block"` on
`SeasonCreateRequest` or `SeasonUpdateRequest`. The season needs one package, and its sales must
not have opened. Turning them on keeps the season's revision.

```csharp
await client.Seasons.UpdateSeasonAsync(seasonKey,
    new SeasonUpdateRequest { ExpectedRevision = revision, InventoryMode = "block" });

var answer = await client.Seasons.SetAsideSeasonBlockAsync(seasonKey, new SeasonBlockSetAsideRequest
{
    ExpectedRevision = revision,
    KeepForSingleTickets = new SeasonSeatSelection { SectionIds = new[] { "sec_family" } },
});
var job = (IReadOnlyDictionary<string, object?>)answer["job"]!;
while (job["state"] is "planning" or "running")
{
    await Task.Delay(TimeSpan.FromSeconds(2));
    var read = await client.Seasons.RetrieveSeasonBulkJobAsync(seasonKey, (string)job["jobId"]!);
    job = (IReadOnlyDictionary<string, object?>)read["job"]!;
}
var block = await client.Seasons.RetrieveSeasonBlockAsync(seasonKey);

// Release unsold season seats to single sale. Leave At null to release now.
await client.Seasons.ReleaseSeasonBlockAsync(seasonKey, new SeasonBlockReleaseRequest
{
    ExpectedRevision = revision,
    Scope = new SeasonBlockReleaseScope { All = true },
    At = "2026-12-01T09:00:00Z",
});
```

- On an active season, block mode turns on by itself once every date has the seats, and
  `season.block.ready` is sent once. On a draft, `PublishSeasonAsync` turns it on.
  `season.block.released` is sent once a release has run.
- In block mode, a date cancel (`CreateSeasonAmendmentAsync`, kind `cancel`) or
  `MarkSeasonPerformancePastAsync` first takes the date off every season seat. Until that finishes
  the answer is a `202`: check it with `SeasonsService.IsDateLeavePending(answer)`, then send the
  same request again after `Retry-After`.
- Refusals come through `SeatLayerException` with `Code`, and nothing changes. For example
  `409 season_block_packages` (more than one package), `409 season_block_on_sale` (sales have
  opened), or `409 block_not_ready` from `OpenSeasonSalesAsync` before block mode is on.
- A partner's seat on a date (one a sales channel sells there) stays out of the block. It is
  listed in the block's `notFreeOnEveryDate` with reason `channel`.

The [Season seats docs](https://docs.seatlayer.io/server-api/seasons/#season-seats) list every refusal.

## Test vs live

Keys carry their own mode. `sk_test_…` keys can only touch test-mode events and `sk_live_…` only
live ones; crossing them returns `403 mode_mismatch`, surfaced as `SeatLayerAuthException` with
`IsModeMismatch`.

```csharp
if (env.IsProduction() && client.Mode != "live")
{
    throw new InvalidOperationException("Refusing to boot production against test-mode seating data.");
}
```

A publishable `pk_` key is rejected at construction with a message naming the mistake, rather than
failing as a `401` three round-trips later.

## Book reserved seats from .NET

**Buyer picks seats in the browser.** Your frontend holds them; your backend confirms the price and
books. Never price from what the browser sent you: `RetrieveHoldAsync` is authoritative.

```csharp
var hold = await client.Inventory.RetrieveHoldAsync(eventKey, holdId);
var items = ((IEnumerable<object?>)hold["items"]!)
    .Cast<IReadOnlyDictionary<string, object?>>()
    .ToList();
var currencies = items.Select(item => (string)item["currency"]!).Distinct().ToArray();
if (currencies.Length != 1)
{
    throw new InvalidOperationException("A hold must use one currency.");
}
var currency = currencies[0];
var total = items.Sum(item =>
    Convert.ToDecimal(item["unitPrice"]) *
    Convert.ToDecimal(item.TryGetValue("quantity", out var quantity) ? quantity : 1));
// … charge `total` in `currency` …
await client.Inventory.BookAsync(eventKey, holdId, bookingRef: charge.Id);
```

**Your backend picks the seats.** Phone orders, box office, comps.

```csharp
// Payment already taken: book outright, so nothing is stranded if a second call fails.
await client.Inventory.BookBestAvailableAsync(eventKey,
    new BestAvailableRequest { Qty = 2, BookingRef = "phone-1183" });

// Or name the seats yourself.
await client.Inventory.BoxOfficeBookAsync(eventKey, new[] { "A-1", "A-2" }, "comp-14");
```

## Private and partner sales

Channels reserve inventory for a partner, member group, presale, or other private allocation. A
buyer access session is short-lived and origin-bound, so the browser receives only the allocation
it is allowed to sell; your secret key remains on your server.

```csharp
var channel = await client.Channels.CreateAsync(eventKey, new CreateChannelRequest
{
    Name = "Venue members",
    AccessIntent = "private",
});

await client.Channels.UpdateAssignmentsAsync(
    eventKey,
    new[] { "A-1", "A-2" },
    assignmentVersion: 1,
    targetChannelId: "ch_members");

var access = await client.Channels.CreateBuyerAccessSessionAsync(
    eventKey,
    new BuyerAccessSessionRequest
    {
        IncludePublic = false,
        AllowedOrigin = "https://members.example",
        ChannelIds = new[] { "ch_members" },
        MaxQuantity = 2,
    });
```

Pass the returned token to the buyer SDK. For trusted backend sales, provide `ChannelIds` on a
`BestAvailableRequest`, or use the named `channelIds` argument on `HoldAsync`, `BookAsync`, or
`BookLabelsAsync`. `IgnoreChannelRestrictions = true` is an explicit privileged override and
should be accompanied by an audit `Reason`.

## Listing and pagination

`ListAsync` returns one `Page` plus a cursor. `ListAllAsync` is an async stream that pages as you
consume it. It is deliberately not a `List`, because the point of paginating is to *not* hold an
unbounded result set in memory.

```csharp
// One page, your own paging.
var page = await client.Events.ListAsync(new EventListRequest { Limit = 50 });
page.Items;
page.NextCursor;   // null once exhausted

// Or let the SDK walk it.
await foreach (var seatEvent in client.Events.ListAllAsync())
{
    await SyncAsync(seatEvent);
}
```

Listing events includes live availability counts by default, which costs the server one round-trip
**per event**. `ListAllAsync` drops them automatically, since walking a whole catalogue is exactly when
you don't want that, and you can control it explicitly:

```csharp
await client.Events.ListAsync(new EventListRequest { Limit = 50, Counts = false });
```

Set `SeriesKey` to list only the dates of one event series.

## Keeping a hold alive

When an order takes longer than the checkout window (an invoice, a phone sale), extend rather than
release and re-hold. Releasing first hands the seats to whoever is racing for them in between.

```csharp
try
{
    await client.Inventory.ExtendHoldAsync(eventKey, holdId, ttlMs: 10 * 60_000);
}
catch (SeatLayerConflictException)
{
    // Gone, expired, or at its renewal cap: the buyer has to re-pick.
}
```

## Embedding the control room

Your secret key never reaches a browser. Mint a scoped token instead.

```csharp
var session = await client.Sessions.CreateManageSessionAsync(
    eventKey,
    "https://box-office.yourplatform.com",
    new[] { SessionsService.CapabilityView, SessionsService.CapabilityBlock },
    expiresInSeconds: 3600);
```

`capabilities` is **required** by this SDK even though the raw API safely defaults an omitted list
to view-only (`event:view`). Keeping the argument required makes browser authority visible at every
call site. Grant the smallest set the page needs. `event:cancel` puts a booking back on sale; it
does not refund anyone. To refund an order that SeatLayer's checkout took, grant `event:refund`.

## Webhooks

Verify every delivery against the **raw** body. Model binding and re-serialising changes the bytes,
so verification will fail.

```csharp
app.MapPost("/webhooks/seatlayer", async (HttpRequest request) =>
{
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer);          // raw bytes, never a bound model

    IReadOnlyDictionary<string, object?> seatEvent;
    try
    {
        seatEvent = Webhook.Verify(
            buffer.ToArray(),
            request.Headers["X-SeatLayer-Signature"],
            Environment.GetEnvironmentVariable("SEATLAYER_WEBHOOK_SECRET")!);
    }
    catch (SeatLayerWebhookVerificationException)
    {
        return Results.BadRequest();
    }

    // The signed body carries "at", but nothing enforces a freshness window, so a
    // captured delivery stays valid indefinitely. Deduplicate on occurrenceId:
    // this is your replay protection, not an optimisation.
    if (await AlreadyProcessedAsync((string)seatEvent["occurrenceId"]!))
    {
        return Results.Ok();
    }

    await ProcessAsync(seatEvent);
    return Results.Ok();
});
```

## Errors

```csharp
try
{
    await client.Inventory.HoldBestAvailableAsync(eventKey, new BestAvailableRequest { Qty = 6 });
}
catch (SeatLayerConflictException e) when (e.IsSoldOut)
{
    return OfferAlternativeDates();          // a business outcome, not a bug
}
catch (SeatLayerRateLimitException e)
{
    return RetryAfter(e.RetryAfterSeconds);
}
catch (SeatLayerAuthException e) when (e.IsModeMismatch)
{
    throw new InvalidOperationException("Test key pointed at a live event, or the reverse.");
}
```

`when` filters read especially well here: a sold-out result and a genuine conflict are the same
exception type but different outcomes.

| Type | Status | Means |
|---|---|---|
| `SeatLayerAuthException` | 401, 403 | Bad, revoked, or wrong-mode key |
| `SeatLayerNotFoundException` | 404 | No such resource *for this organisation* |
| `SeatLayerConflictException` | 409 | Inventory moved, or a guard rejected the change |
| `SeatLayerValidationException` | 422 | Understood and rejected |
| `SeatLayerRateLimitException` | 429 | Over budget; carries `RetryAfterSeconds` |
| `SeatLayerConnectionException` | none | No answer: DNS, TLS, socket, timeout |

Every API exception carries `Status`, `Code`, `Body` and `RequestId`. Quote the request id in
support requests.

## Reliability

**Retries.** Reads (`GET`/`HEAD`) retry 429, 408 and 5xx with exponential backoff and full jitter;
`Retry-After` wins when the server sends it. 41 mutations use exact header replay:

- `Charts.CreateAsync`, `Charts.CopyAsync`, `Templates.InstantiateTemplateAsync`,
  `Events.CreateAsync`, `Workspaces.CreateAsync`, `PerformanceGroups.CreateAsync`
- 26 on `Seasons`: `CreateSeasonAsync`, `UpdateSeasonAsync`, `DeleteSeasonAsync`,
  `CreateSeasonPlanAsync`, `DuplicateSeasonToLiveAsync`, `CreateSeasonHolderImportAsync`,
  `CreateSeasonRenewalOffersAsync`, `CreateSeasonAmendmentAsync`,
  `CreateSeasonHolderImportFromSeasonAsync`, `CreateSeasonPerformancesAsync`,
  `AttachSeasonPerformancesAsync`, `DeleteSeasonPerformanceAsync`, `UpdateSeasonPlanAsync`,
  `DeleteSeasonPlanAsync`, `PublishSeasonAsync`, `CreateSeasonCheckoutTestAsync`,
  `ReplaceSeasonPackagePricesAsync`, `AttachSeasonPlanPerformanceAsync`,
  `DeleteSeasonPlanPerformanceAsync`, `MarkSeasonPerformancePastAsync`, `RefundSeasonOrderAsync`,
  `CancelSeasonOrderAsync`, `CancelSeasonOrderDateAsync`, `RefundSeasonPerformanceAsync`,
  `CreateSeasonRenewalCheckoutAsync`, and `ReleaseSeasonLapsedOffersAsync`
- `Holders.UpdateHolderAsync`, `Holders.CreateHolderTransferAsync`,
  `Holders.CreateHolderReturnAsync`, `Holders.CreateHolderRenewalCheckoutAsync`,
  `Orders.RefundEventOrderAsync`, and `Orders.CancelEventOrderAsync`
- `EventSeries.CreateEventSeriesAsync`, `EventSeries.AddEventSeriesDatesAsync`, and
  `EventSeries.UpdateEventSeriesAsync`

Other 4xx responses are never retried. A cancelled `CancellationToken` stops the loop immediately
rather than being treated as a transient fault.

**Idempotency.** Those 41 replay-backed operations carry an `Idempotency-Key`, generated when you
do not supply one and reused across attempts. All remaining SDK mutations are single-attempt. Some
have a server-side domain idempotency contract, but the SDK does not retry them automatically. This
includes inventory holds and bookings, show-once credential or secret creation, unsupported
operations, and raw `SendAsync` mutations. Keep the booking reference in the booking body for
reconciliation, but handle an unknown network outcome explicitly instead of automatically
repeating the sale.

```csharp
new SeatLayerClient(secretKey, new SeatLayerClientOptions
{
    MaxRetries = 3,                       // total attempts
    Timeout = TimeSpan.FromSeconds(30),   // per attempt
});
```

## Escape hatch

For surface this SDK does not wrap yet, `SendAsync` keeps auth and error mapping. Raw reads retain
the read retry policy; raw mutations are always single-attempt because their replay contract is unknown:

```csharp
await client.SendAsync(HttpMethod.Post, "/v1/events/ev_1/some-new-route",
    body: new Dictionary<string, object?> { ["qty"] = 2 });
```

## API surface

The client exposes these services. Performance Groups cover runs, sessions, holds, and bookings;
Seasons cover catalogue, plan, sales, buyer-session, booking, renewal, occurrence, reporting,
outbox, and support operations. `Events` also carries event sales setup: who takes payment, the
payment gateway, sales readiness and state, the booking fee, and discount codes.

| Service | Methods |
| --- | --- |
| `Charts` | `ListAsync` `ListAllAsync` `CreateAsync` `RetrieveAsync` `UpdateAsync` `DeleteAsync` `CopyAsync` `ArchiveAsync` `UnarchiveAsync` `PublishAsync` |
| `Templates` | `InstantiateTemplateAsync` |
| `Events` | `ListAsync` `ListAllAsync` `CreateAsync` `RetrieveAsync` `RetrieveConfigurationBindingAsync` `UpdateConfigurationBindingAsync` `UpdateAsync` `DeleteAsync` `UpdateChartAsync` `CloseAsync` `ReopenAsync` `ArchiveAsync` `RetrieveHoldTtlAsync` `UpdateHoldTtlAsync` `ListTicketReleasesAsync` `UpdateTicketReleasesAsync` `CloseTicketReleaseAsync` `RetrieveReportAsync` `RetrieveLogAsync` `RetrieveEventBuyerCheckoutAsync` `ReplaceEventBuyerCheckoutAsync` `RetrieveEventPaymentGatewayAsync` `ReplaceEventPaymentGatewayAsync` `RetrieveEventSalesReadinessAsync` `PreflightEventSalesAsync` `TransitionEventSalesAsync` `RetrieveEventBookingFeeAsync` `ReplaceEventBookingFeeAsync` `ListEventDiscountCodesAsync` `CreateEventDiscountCodeAsync` `UpdateEventDiscountCodeAsync` `EndEventDiscountCodeAsync` `DeleteEventDiscountCodeAsync` |
| `EventSeries` | `CreateEventSeriesAsync` `ListEventSeriesAsync` `RetrieveEventSeriesAsync` `AddEventSeriesDatesAsync` `UpdateEventSeriesAsync` |
| `Channels` | `ListAsync` `CreateAsync` `UpdateAsync` `UpdateAssignmentsAsync` `ListAllocationAsync` `RetrieveAccessPreviewAsync` `RetrieveReportAsync` `PauseAsync` `UnpauseAsync` `ArchiveAsync` `CreateBuyerAccessSessionAsync` `ListBuyerAccessSessionsAsync` `RevokeBuyerAccessSessionAsync` |
| `Holders` | `ListCustomersAsync` `RetrieveHolderAsync` `UpdateHolderAsync` `RevokeHolderSessionsAsync` `ListHolderSeasonsAsync` `ListHolderTicketsAsync` `RetrieveHolderTicketAsync` `CreateHolderTransferAsync` `ListHolderTransfersAsync` `CancelHolderTransferAsync` `AcceptHolderTransferAsync` `CreateHolderReturnAsync` `CreateHolderRenewalCheckoutAsync` `DeclineHolderRenewalOfferAsync` |
| `Inventory` | `HoldAsync` `HoldBestAvailableAsync` `BookBestAvailableAsync` `ExtendHoldAsync` `RetrieveHoldAsync` `ReleaseAsync` `BookAsync` `BookLabelsAsync` `BoxOfficeBookAsync` `UnbookAsync` `BlockAsync` `UnblockAsync` `UnblockAllAsync` `RetrieveAvailabilityAsync` `UpdateAvailabilityAsync` `ListBookingsAsync` `RetrieveBookingAsync` |
| `Orders` | `ListOrdersAsync` `RetrieveOrderAsync` `ListOrderTicketsAsync` `ListEventOrdersAsync` `RetrieveEventOrderAsync` `ExportEventOrdersCsvAsync` `RefundEventOrderAsync` `CancelEventOrderAsync` `ResendEventOrderTicketsAsync` |
| `Sessions` | `CreateManageSessionAsync` `RevokeManageSessionAsync` `CreateDesignerSessionAsync` `RevokeDesignerSessionAsync` |
| `Webhooks` | `ListAsync` `CreateAsync` `UpdateAsync` `DeleteAsync` `ListDeliveriesAsync` |
| `Workspaces` | `ListAsync` `CreateAsync` `RetrieveAsync` `UpdateAsync` |
| `PerformanceGroups` | `ListAsync` `CreateAsync` `RetrieveAsync` `DeleteAsync` `ActivateAsync` `CloseAsync` `RetrieveLifecycleAsync` `CreateBuyerAccessSessionAsync` `ListBuyerAccessSessionsAsync` `RevokeBuyerAccessSessionAsync` `RetrieveHoldAsync` `BookHoldAsync` `RetrieveBookingAsync` |
| `Seasons` | 90 operations for catalogue and Plan lifecycle, performances and package prices, publishing and checkout tests, sales windows, buyer access and booking, holder imports, holders and Season orders, renewals and hosted renewal links, occurrence amendments, returns and reclaims, bulk refunds and jobs, reports and CSV exports, audit, outbox, and support export |

Full reference: [SeatLayer .NET server SDK guide](https://docs.seatlayer.io/server-sdk/dotnet/)

## Frequently asked questions

### What does the SeatLayer API do for a ticketing platform?

SeatLayer gives your .NET backend one API for seating charts, live seat holds, booking, box office
sales and season tickets, with official SDKs in Node.js, Python, PHP, Java, Go, Ruby and .NET.
Buyers pick seats on the same SeatLayer map on the web, iOS, Android, Flutter and React Native, and
your server books what they hold with your own order id, so a retried booking never sells a seat
twice. You pay only for sold seats: 100 a month are free, then 10¢ down to 5¢ a seat, with no
subscription, and holds and test mode cost nothing. Its measured results are under Scale evidence
below.

### How much does the SeatLayer API cost?

Building and testing is free: test mode has no time limit, needs no card and uses no credits. Live,
with your own checkout, you pay one credit per confirmed sold seat: the first 100 sold seats each
month are free, then 10¢ down to 5¢ a seat, with no subscription, and purchased credits never
expire. Designing charts, showing the map, holds and releases never use a credit. An event that
sells through SeatLayer's checkout costs $0.50 per ticket instead, and your first 25 tickets are
free.

### How do I book seats from .NET?

Create a client with your secret key, read the buyer's hold with `Inventory.RetrieveHoldAsync` to
get the real seats and prices, take the payment, then call
`Inventory.BookAsync(eventKey, holdId, bookingRef: ...)` with your own order id as `bookingRef`. For
phone orders, the box office and comps, `Inventory.BookBestAvailableAsync` picks and books the best
seats in one call, with no browser involved. Keep that `bookingRef` for the order: it finds the
booking again in `Inventory.ListBookingsAsync` and cancels it with `Inventory.UnbookAsync`.

### How do I stop two buyers taking the same seat?

In the SeatLayer picker, a buyer's seats are held when they go to checkout; with the bare chart,
your code calls hold. The hold keeps those seats for that buyer for a set time, which you can change
per event, and every other buyer sees them taken at once. Your server reads the hold with
`Inventory.RetrieveHoldAsync`, charges from the prices it returns rather than anything the browser
sent, and books it; `Inventory.ExtendHoldAsync` gives a slow checkout more time without letting the
seats go. If a booking call times out, send it again with the same hold and the same `bookingRef`:
seats already booked under that reference are never sold twice.

### Can I use my own payment provider?

Yes. Nothing in this SDK takes a payment: your server charges the hold's total through the provider
you already use, then books the seats with your order id as `bookingRef`, and SeatLayer never
touches the money. If you would rather SeatLayer run the checkout for an event
(`Events.ReplaceEventBuyerCheckoutAsync`), buyers pay through one of 14 payment gateways, including
Stripe, PayPal and Square, straight into your own account.

### How do I sell season tickets and renewals through an API?

The season API is in this SDK: `client.Seasons` has all 90 season operations. You create a season
from its dates with `CreateSeasonAsync`, the buyer chooses one seat in the browser `SeasonPicker`
and the season hold takes it on every date at once, and your server books it with
`BookSeasonHoldAsync`; for the next season, `CreateSeasonRenewalOffersAsync` offers each holder the
same seat. `SetAsideSeasonBlockAsync` sets season seats aside on every date before sales open, and
with your own checkout each season seat uses one SDK credit per date, like any other sold seat.

### Does it work with ASP.NET Core?

Yes. Register one `SeatLayerClient` as a singleton: it is thread-safe and keeps one long-lived
`HttpClient`, or pass your own from `IHttpClientFactory` and the SDK will not dispose it. For
webhooks, copy `Request.Body` into a buffer and pass those raw bytes to `Webhook.Verify`, never a
model-bound object. Every call is async and takes a `CancellationToken`, which stops retries as soon
as it is cancelled.

## Continue your .NET integration

- [Follow the .NET server SDK guide](https://docs.seatlayer.io/server-sdk/dotnet/)
  for installation, authentication, and the full hold-to-booking flow.
- [Handle errors, retries, and safe booking repeats](https://docs.seatlayer.io/server-sdk/reliability/)
  before connecting a production order flow.
- [Verify SeatLayer webhooks](https://docs.seatlayer.io/server-sdk/webhooks/)
  to react to holds, expiry, and bookings on your server.
- [Browse the SeatLayer server API reference](https://docs.seatlayer.io/server-api/events/)
  for every endpoint behind this SDK.
- [Generate clients from the SeatLayer OpenAPI description](https://docs.seatlayer.io/openapi.json)
  or explore the raw API surface.
- [Point AI coding agents at the SeatLayer docs index](https://docs.seatlayer.io/llms.txt)
  (`llms.txt`) for an agent-readable map of the documentation.
- [Explore every SeatLayer SDK on GitHub](https://github.com/seatlayer)
  across web, mobile, and server.

## SeatLayer SDK ecosystem

| Surface | Package or source |
|---|---|
| JavaScript | [`@seatlayer/js`](https://www.npmjs.com/package/@seatlayer/js) |
| React | [`@seatlayer/react`](https://www.npmjs.com/package/@seatlayer/react) |
| React Native | [`@seatlayer/react-native`](https://www.npmjs.com/package/@seatlayer/react-native) |
| iOS | [`seatlayer-ios`](https://github.com/seatlayer/seatlayer-ios) |
| Flutter | [`seatlayer`](https://pub.dev/packages/seatlayer) |
| Android | [`seatlayer-android`](https://github.com/seatlayer/seatlayer-android) |
| Server SDKs | [Node.js, Python, PHP, Ruby, .NET, Java, and Go](https://docs.seatlayer.io/server-sdk/install/) |
| Node.js (server) | [`@seatlayer/server`](https://www.npmjs.com/package/@seatlayer/server) |
| Python (server) | [`seatlayer`](https://pypi.org/project/seatlayer/) |
| PHP (server) | [`seatlayer/seatlayer-php`](https://packagist.org/packages/seatlayer/seatlayer-php) |
| Ruby (server) | [`seatlayer`](https://rubygems.org/gems/seatlayer) |
| .NET (server) | [`SeatLayer`](https://www.nuget.org/packages/SeatLayer) (this package) |
| Java (server) | [`io.seatlayer:seatlayer-java`](https://central.sonatype.com/artifact/io.seatlayer/seatlayer-java) |
| Go (server) | [`github.com/seatlayer/seatlayer-go`](https://pkg.go.dev/github.com/seatlayer/seatlayer-go) |

## Scale evidence

Benchmarked on public 100,000-, 150,000- and 200,000-seat venue fixtures on 15 September 2026: 200,000 seats chart-ready in 1.95 s, desktop, local production build. Tested at 10,000 simulated concurrent buyers on one 12,000-seat event (18 September 2026): engine-side hold latency p99 9 ms, zero server errors. Method, fixtures and run files: https://github.com/seatlayer/seatlayer-performance. Live 200,000-seat stadium demo: https://app.seatlayer.io/demo/play/century-stadium-200k.

## Development

```bash
dotnet build     # warnings are errors
dotnet test
dotnet pack -c Release
```

## License

MIT
