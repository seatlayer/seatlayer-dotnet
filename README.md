# SeatLayer .NET Server SDK for Reserved Seating

[![CI](https://github.com/seatlayer/seatlayer-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/seatlayer/seatlayer-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/SeatLayer.svg)](https://www.nuget.org/packages/SeatLayer)
[![License: MIT](https://img.shields.io/badge/license-MIT-111827.svg)](LICENSE)

The official SeatLayer .NET server SDK is the **trusted side** of a reserved-seating
integration: inspect the holds a buyer created, price from server data, and book with a
stable `bookingRef`. From C# you manage seating charts, events, sales channels, and live
seat inventory through one typed ticketing API client.

[SeatLayer package on NuGet](https://www.nuget.org/packages/SeatLayer) ·
[SeatLayer server SDK documentation](https://docs.seatlayer.io/server-sdk/install/) ·
[SeatLayer reserved-seating platform](https://seatlayer.io/) ·
[SeatLayer JavaScript seat map SDK](https://www.npmjs.com/package/@seatlayer/js) ·
[SeatLayer AI Toolkit](https://github.com/seatlayer/seatlayer-ai-toolkit)

> **Server-side only.** This library authenticates with your secret key. Never ship it in a client
> application — browser surfaces get short-lived, origin-bound tokens that you mint here.

## Install

```bash
dotnet add package SeatLayer
```

Or pin it in your project file:

```xml
<PackageReference Include="SeatLayer" Version="0.6.0" />
```

`SeatLayer` is published on NuGet; `0.6.0` is the current release. Requires .NET 8 or newer. **No package dependencies** — `HttpClient`, `System.Text.Json` and
`HMACSHA256` all ship with the framework, so the SDK forces no version on your application.

## Quick start

```csharp
using SeatLayer;

var client = new SeatLayerClient(Environment.GetEnvironmentVariable("SEATLAYER_SECRET_KEY")!);

// 1. Materialize a published catalog template as a draft for this organiser.
var chart = (IReadOnlyDictionary<string, object?>)(
    await client.Templates.InstantiateTemplateAsync("your-published-template"))["meta"]!;
await client.Charts.PublishAsync((string)chart["id"]!);

// 2. Create an event on it.
var created = await client.Events.CreateAsync((string)chart["id"]!, name: "Spring Gala");
var meta = (IReadOnlyDictionary<string, object?>)created["meta"]!;
var eventKey = (string)meta["key"]!;

// 3. Sell four seats over the phone.
var held = await client.Inventory.HoldBestAvailableAsync(eventKey, new BestAvailableRequest { Qty = 4 });
// … take payment against held["items"], which carry authoritative prices …
await client.Inventory.BookAsync(eventKey, (string)held["holdId"]!, bookingRef: "order-8842");
```

Register the client as a **singleton**. It is thread-safe, and its `HttpClient` is meant to be
long-lived — constructing one per request exhausts sockets.

```csharp
builder.Services.AddSingleton(_ =>
    new SeatLayerClient(builder.Configuration["SeatLayer:SecretKey"]!));
```

Using `IHttpClientFactory`? Pass the client in and the SDK will not dispose it, because it does not
own its lifetime:

```csharp
new SeatLayerClient(secretKey, new SeatLayerClientOptions { HttpClient = factory.CreateClient() });
```

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

## The two selling flows

**Buyer picks seats in the browser.** Your frontend holds them; your backend confirms the price and
books. Never price from what the browser sent you — `RetrieveHoldAsync` is authoritative.

```csharp
var hold = await client.Inventory.RetrieveHoldAsync(eventKey, holdId);
// … charge the total of hold["items"] in hold["currency"] …
await client.Inventory.BookAsync(eventKey, holdId, bookingRef: charge.Id);
```

**Your backend picks the seats.** Phone orders, box office, comps.

```csharp
// Payment already taken — book outright, so nothing is stranded if a second call fails.
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
consume it — deliberately not a `List`, because the point of paginating is to *not* hold an
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
**per event**. `ListAllAsync` drops them automatically — walking a whole catalogue is exactly when
you don't want that — and you can control it explicitly:

```csharp
await client.Events.ListAsync(new EventListRequest { Limit = 50, Counts = false });
```

## Keeping a hold alive

When an order takes longer than the checkout window — an invoice, a phone sale — extend rather than
release and re-hold. Releasing first hands the seats to whoever is racing for them in between.

```csharp
try
{
    await client.Inventory.ExtendHoldAsync(eventKey, holdId, ttlMs: 10 * 60_000);
}
catch (SeatLayerConflictException)
{
    // Gone, expired, or at its renewal cap — the buyer has to re-pick.
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

`capabilities` is **required** by this SDK even though the API defaults it. That default grants all
four including `event:cancel`, which reverses paid bookings — not something that should arrive by
forgetting an argument. Grant the smallest set the page needs.

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
    // captured delivery stays valid indefinitely. Deduplicate on occurrenceId —
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

`when` filters read especially well here — a sold-out result and a genuine conflict are the same
exception type but different outcomes.

| Type | Status | Means |
|---|---|---|
| `SeatLayerAuthException` | 401, 403 | Bad, revoked, or wrong-mode key |
| `SeatLayerNotFoundException` | 404 | No such resource *for this organisation* |
| `SeatLayerConflictException` | 409 | Inventory moved, or a guard rejected the change |
| `SeatLayerValidationException` | 422 | Understood and rejected |
| `SeatLayerRateLimitException` | 429 | Over budget; carries `RetryAfterSeconds` |
| `SeatLayerConnectionException` | — | No answer: DNS, TLS, socket, timeout |

Every API exception carries `Status`, `Code`, `Body` and `RequestId` — quote the request id in
support requests.

## Reliability

**Retries.** Reads (`GET`/`HEAD`) retry 429, 408 and 5xx with exponential backoff and full jitter;
`Retry-After` wins when the server sends it. Automatic mutation retries are limited to the five
operations backed by exact response replay: `Charts.CreateAsync`, `Charts.CopyAsync`,
`Templates.InstantiateTemplateAsync`, `Events.CreateAsync`, and `Workspaces.CreateAsync`. Other 4xx
responses are never retried. A cancelled `CancellationToken` stops the loop immediately rather than
being treated as a transient fault.

**Idempotency.** Those five replay-backed operations carry an `Idempotency-Key`, generated when you
do not supply one and reused across attempts. Other mutations are single-attempt and receive no
automatic key. A caller-supplied key is forwarded but does not enable retries. This includes
inventory holds and bookings, show-once credential or secret creation, unsupported operations, and
raw `SendAsync` mutations. Keep the booking reference in the booking body for reconciliation, but
handle an unknown network outcome explicitly instead of automatically repeating the sale.

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

| Service | Methods |
| --- | --- |
| `Charts` | `ListAsync` `ListAllAsync` `CreateAsync` `RetrieveAsync` `UpdateAsync` `DeleteAsync` `CopyAsync` `ArchiveAsync` `UnarchiveAsync` `PublishAsync` |
| `Templates` | `InstantiateTemplateAsync` |
| `Events` | `ListAsync` `ListAllAsync` `CreateAsync` `RetrieveAsync` `RetrieveConfigurationBindingAsync` `UpdateConfigurationBindingAsync` `UpdateAsync` `DeleteAsync` `UpdateChartAsync` `CloseAsync` `ReopenAsync` `ArchiveAsync` `RetrieveHoldTtlAsync` `UpdateHoldTtlAsync` `ListTicketReleasesAsync` `UpdateTicketReleasesAsync` `CloseTicketReleaseAsync` `RetrieveReportAsync` `RetrieveLogAsync` |
| `Channels` | `ListAsync` `CreateAsync` `UpdateAsync` `UpdateAssignmentsAsync` `ListAllocationAsync` `RetrieveAccessPreviewAsync` `RetrieveReportAsync` `PauseAsync` `UnpauseAsync` `ArchiveAsync` `CreateBuyerAccessSessionAsync` `ListBuyerAccessSessionsAsync` `RevokeBuyerAccessSessionAsync` |
| `Inventory` | `HoldAsync` `HoldBestAvailableAsync` `BookBestAvailableAsync` `ExtendHoldAsync` `RetrieveHoldAsync` `ReleaseAsync` `BookAsync` `BookLabelsAsync` `BoxOfficeBookAsync` `UnbookAsync` `BlockAsync` `UnblockAsync` `UnblockAllAsync` `RetrieveAvailabilityAsync` `UpdateAvailabilityAsync` `ListBookingsAsync` `RetrieveBookingAsync` |
| `Sessions` | `CreateManageSessionAsync` `RevokeManageSessionAsync` `CreateDesignerSessionAsync` `RevokeDesignerSessionAsync` |
| `Webhooks` | `ListAsync` `CreateAsync` `UpdateAsync` `DeleteAsync` `ListDeliveriesAsync` |
| `Workspaces` | `ListAsync` `CreateAsync` `RetrieveAsync` `UpdateAsync` |

Full reference: [docs.seatlayer.io/server-sdk](https://docs.seatlayer.io/server-sdk/install/)

## Frequently asked questions

### How do I book seats from a .NET application?

Install the [`SeatLayer` NuGet package](https://www.nuget.org/packages/SeatLayer), construct a
`SeatLayerClient` with your secret key, and call `Inventory.BookAsync` with the hold id and a
stable `bookingRef`. When your own backend picks the seats — phone orders, box office, comps —
`Inventory.BookBestAvailableAsync` and `Inventory.BoxOfficeBookAsync` book outright with no prior
hold. Every booking method requires a booking reference, so each sale is tied to an immutable
order id you can reconcile against later.

### What does the server SDK do that the buyer SDK does not?

The buyer SDK runs in the browser or mobile app and only **selects and holds** seats. This .NET
SDK runs on your trusted server and **inspects and books** them. Your secret key never reaches a
buyer surface: browsers receive short-lived, origin-bound tokens minted here through
`Sessions.CreateManageSessionAsync` or `Channels.CreateBuyerAccessSessionAsync`. Always price a
sale from `Inventory.RetrieveHoldAsync`, never from values the browser sent you.

### How do temporary seat holds work server-side?

A hold reserves seats against concurrent buyers for a limited checkout window. From .NET you
retrieve it with `Inventory.RetrieveHoldAsync`, whose `items` and `currency` are authoritative for
pricing, and confirm it with `Inventory.BookAsync`. Use `Inventory.ExtendHoldAsync` for a long
checkout instead of releasing and re-holding, which would hand the seats to whoever is racing for
them. Booking is a single automatic attempt: after an unknown network outcome you may reconcile
and repeat the exact same event, hold, and `bookingRef` — seats already booked under that
reference are not sold again.

### Can I use my own payment provider?

Yes. SeatLayer never processes payment. Charge through Stripe, Adyen, Braintree, or any provider
you already use, calculating the total from the server-inspected hold items rather than from
client input, then call `Inventory.BookAsync` with your charge or order id as the `bookingRef`.
The [holds and checkout guide](https://docs.seatlayer.io/buyer-sdk/holds-and-checkout/) walks
through the full handoff.

## Related resources

- [Server SDK guide](https://docs.seatlayer.io/server-sdk/install/)
- [Errors, retries and idempotency](https://docs.seatlayer.io/server-sdk/reliability/)
- [Webhook verification](https://docs.seatlayer.io/server-sdk/webhooks/)
- [Server API reference](https://docs.seatlayer.io/server-api/events/)
- [OpenAPI description](https://docs.seatlayer.io/openapi.json)
- [Agent-readable documentation](https://docs.seatlayer.io/llms.txt)
- [SeatLayer GitHub organization](https://github.com/seatlayer)

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

## Development

```bash
dotnet build     # warnings are errors
dotnet test
dotnet pack -c Release
```

## License

MIT
