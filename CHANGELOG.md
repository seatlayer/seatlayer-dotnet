# Changelog

## Unreleased

- `Seasons` grows from 48 to 80 operations: performances, package updates and prices, one-call
  publishing, checkout tests, occurrence returns and reclaims, holders, Season order refunds and
  cancellations, hosted renewal links, contacts and notices, sales and seat reports, and holder
  and order CSV exports.
- Added `Holders` (12 operations: customers, holder details and sessions, tickets, ticket
  transfers, and returns), `Orders` (9 operations: orders across events and Seasons, and an
  event's orders with refunds, cancellations, ticket emails, and CSV export), and `EventSeries`
  (5 operations).
- Added `Holders.CreateHolderReturnAsync`: give back a holder's seat on one date, one per ticket.
- `Events` gains 14 sales setup methods: buyer checkout choice, payment gateway, sales readiness,
  preflight and transitions, booking fee, and discount codes.
- 23 of the new mutations use exact header replay, so 37 client mutations now retry with one
  stable `Idempotency-Key`. Unsupported operations, including the show-once renewal link rotation,
  stay single-attempt.
- CSV exports return the raw text. `DeleteSeasonPlanAsync` and `DeleteEventDiscountCodeAsync`
  return a plain `Task` for the API's 204.

## 0.8.0 (2026-09-10)

- Added Event hosting regions: `Region` on the event create request, `defaultRegion` on workspace create, `UpdateDefaultRegionAsync`, and the `EventHostingRegions` constants. Events created without a region run in Western Europe.

## 0.7.0 — 2026-08-30

- Added `Seasons`, covering all 48 frozen secret-key Season operations: compatibility and
  catalogue, lifecycle and sales, trusted buyer handoff, incumbent imports and renewals,
  amendments, operations, outbox recovery, audit, and support export.
- Header-replay retries are limited to the eight Season operations whose API contract guarantees
  exact response replay. Show-once credentials and domain-exact lifecycle, booking, cancellation,
  and renewal commits remain single-attempt.
- Season allocations are identity-only and the API response declares host pricing authority.
  Buyer rehearsal validation sends no evidence body because SeatLayer discovers the retained
  hold, booking, cancellation, and delivered webhook chain automatically.

## 0.6.1

- Documentation only. Refreshes the README, adds frequently asked
  questions, and aligns package metadata. No API or behaviour changes.

## 0.6.0 — 2026-08-23

- Added exact immutable Event configuration binding reads and compare-and-set
  attach/detach through `Events.RetrieveConfigurationBindingAsync` and
  `Events.UpdateConfigurationBindingAsync`. Updates remain deliberately single-attempt.

## 0.5.0 — 2026-08-21

- Added `PerformanceGroups`, the trusted server resource for fixed two-to-eight
  performance runs. It creates and activates groups, mints one-time browser
  access, retrieves authoritative group holds, and confirms bookings with
  stable action and order references. Browser-only group routes remain outside
  this secret-key SDK.

- **Security/reliability:** Mutations now default to a single attempt. Automatic header-replay
  retries are limited to chart create/copy, template instantiation, event create, and workspace
  create, preventing transient failures from duplicating holds or best-available results and from
  issuing extra show-once credentials.

- Added `Templates.InstantiateTemplateAsync` for materializing published catalog templates as drafts,
  with header-replay idempotency.
- Added typed ticket-release list, full-list replacement, and close methods to `Events`.

## 0.2.0 — 2026-08-12

- Added typed channel allocation management and origin-bound buyer access sessions.
- Added channel-aware hold and booking controls, including explicit privileged override reasons.
- Added paginated booking lifecycle reads and encoded booking retrieval.
- Booking and cancellation calls now reject missing or blank stable booking references.
- Expanded the README with private-sale guidance and direct links across the SeatLayer SDK family.

## 0.1.0 — unreleased

First release of the SeatLayer .NET server SDK.

- `SeatLayerClient` with secret-key auth, per-attempt timeouts, and a `SendAsync` escape hatch.
- Services: `Charts`, `Events`, `Inventory`, `Sessions`, `Webhooks`, `Workspaces`.
- Every call is async and takes a `CancellationToken`; cancelling stops retries immediately
  rather than being treated as a transient fault.
- Automatic `Idempotency-Key` on every mutation, reused across retries so a retried booking
  cannot become two bookings.
- Retries on 429/408/5xx with exponential backoff and full jitter; honours `Retry-After`.
  4xx is never retried.
- Typed exceptions: `SeatLayerAuthException` (with `IsModeMismatch`),
  `SeatLayerConflictException` (with `Conflicts` and `IsSoldOut`), `SeatLayerRateLimitException`,
  `SeatLayerValidationException`, `SeatLayerNotFoundException`, `SeatLayerConnectionException`.
- `Webhook.Verify` — raw-body HMAC-SHA256 via `CryptographicOperations.FixedTimeEquals`.
- `CreateManageSessionAsync` requires explicit capabilities; the API's default grants
  `event:cancel`, which reverses paid bookings.
- `ListAllAsync` returns an `IAsyncEnumerable`, paging as you consume it.
- **No package dependencies** — `HttpClient`, `System.Text.Json` and `HMACSHA256` ship with
  the framework, so the SDK forces no version on your application.
- Supply your own `HttpClient` (from `IHttpClientFactory`) and the SDK will not dispose it,
  because it does not own its lifetime.

Requires .NET 8.
