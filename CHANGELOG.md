# Changelog

## Unreleased

- `Seasons` grows from 48 to 87 operations: performances, package updates and prices, one-call
  publishing, checkout tests, occurrence returns and reclaims, holders, Season order refunds and
  cancellations, hosted renewal links, contacts and notices, renewal quotes, intents and
  checkouts, sales and seat reports, holder and
  order CSV exports, bulk refunds and the release of lapsed renewal offers, with their jobs.
- Added `Holders` (14 operations: customers, holder details and sessions, tickets, ticket
  transfers, returns, and renewals), `Orders` (9 operations: orders across events and Seasons, and an
  event's orders with refunds, cancellations, ticket emails, and CSV export), and `EventSeries`
  (5 operations).
- Added `Holders.CreateHolderReturnAsync`: give back a holder's seat on one date, one per ticket.
- `Events` gains 14 sales setup methods: buyer checkout choice, payment gateway, sales readiness,
  preflight and transitions, booking fee, and discount codes.
- 27 of the new mutations use exact header replay, so 41 client mutations now retry with one
  stable `Idempotency-Key`. Unsupported operations, including the show-once renewal link rotation,
  stay single-attempt.
- CSV exports return the raw text. `DeleteSeasonPlanAsync` and `DeleteEventDiscountCodeAsync`
  return a plain `Task` for the API's 204.
- Added `Seasons.RefundSeasonPerformanceAsync` (refund every order of a cancelled date as a job,
  with header replay; `SeasonPerformanceRefundRequest`), `Seasons.ListSeasonBulkJobsAsync`, and
  `Seasons.RetrieveSeasonBulkJobAsync`.
- `SeasonPackagePrice` takes `PriceTypeKey` (`adult`, `child`, `concession`, `senior`,
  `student`), left out when null.
- `SeasonAmendmentCreateRequest` takes `Refund` and `Reason`, for the new `postpone` and `cancel`
  kinds.
- `SeasonUpdateRequest` takes `CheckoutMode`, `TransferCutoffHours`, and `ReturnCutoffHours`; an
  assigned null cutoff is sent as null.
- `SeasonHolderImportRow` takes `HolderEmail` and `HolderName`, left out when null.
- `EventListRequest` takes `SeriesKey` to list one event series' dates.
- Added `Seasons.RetrieveSeasonRenewalQuoteAsync`: what renewing an offer costs, and whether SeatLayer checkout can take it now.
- Added `Seasons.CreateSeasonRenewalIntentAsync`: record a holder's intent for a renewal taken by phone or at the box office, so your own checkout can commit.
- Added `Seasons.CreateSeasonRenewalCheckoutAsync`: a SeatLayer checkout that renews an offer with no hosted page, with header replay (`SeasonRenewalCheckoutRequest`).
- Added `Holders.CreateHolderRenewalCheckoutAsync`: the same checkout for one of a holder's offers, with header replay.
- Added `Holders.DeclineHolderRenewalOfferAsync`: decline one of a holder's offers, with an optional reason.
- Added a `Seasons.DeclineSeasonRenewalOfferAsync` overload that takes a `reason`, kept on the offer as `declineReason`.
- `SeasonUpdateRequest.Audience` takes `embed_only` or `public`.
- `OpenSeasonSalesAsync` answers `warnings` (`checkout_test_missing` until a checkout test or rehearsal has passed) and can refuse with `409 plan_not_published` or `409 checkout_mode_not_chosen`.
- A cancelled date refunds per date only: an amendment's `Refund` is `none` or `per_date`, and `SeasonPerformanceRefundRequest.Amount` is `per_date` (`full` answers `422 date_refund_per_date_only`; refund a whole order with the order refund).
- `EventSeriesDatesRequest.RetryFailed` makes the failed dates again (`409 series_busy`, `422 nothing_to_retry`).
- `OrderListRequest.HolderRef` lists one holder's orders; orders carry `holderRef` and `customerId`, and `buyer.email` can be null (a box office sale with no email).
- Season holds, give-backs and date cancels can refuse with `performance_cancelled`, `refund_not_started` (503), `missing_manage_capability` (403) or `invalid_refund`; a holder's status can be `reserved`; hosted renewal readiness carries `missingPrices`.
- `EventSeriesCreateRequest` takes `FromEventKey`: an event you already set up becomes the first date under its own key, and every other date is a copy of it. `Name` and `ChartKey` are no longer required; give them, or `FromEventKey` (an `ArgumentException` otherwise). It refuses with `404 event_not_found`, or `422 event_has_no_date`, `event_in_season`, `event_in_performance_group`, `event_in_series` or `invalid_request`.
- Every date of an event series carries the series' plain name (`Hamlet`, not `Hamlet · 3 Nov`); tell dates apart by `startsAt`. `UpdateEventSeriesAsync` renames each date that carries the series name, and a series name cannot hold `{n}`, `{date}` or `{time}`.
- Added `Seasons.ReleaseSeasonLapsedOffersAsync`: release the seats of every lapsed renewal offer as a job (`202 {job}`), with header replay. An offer extended before its turn is skipped and keeps its seat.
- `Seasons.RetrieveSeasonHostedRenewalsAsync` and `Seasons.ListSeasonRenewalOffersAsync` page: new overloads take a `SeasonRenewalOfferPageRequest` (`Cursor` from the page's `nextCursor`, and `Limit`, 1-500 and 1-100). Both answers carry `nextCursor`, null on the last page, and a bad cursor or limit answers `400 invalid_cursor` or `invalid_limit`.
- `CreateSeasonRenewalOffersAsync` answers `201 {offers}` for 100 contracts or fewer, and `202 {job, batchId}` above that: a `renewal_run` job makes the offers. Follow it with `RetrieveSeasonBulkJobAsync`, then page the offers with `ListSeasonRenewalOffersAsync`. It can refuse with `409 job_mismatch` or `503 season_not_configured`.
- `CreateSeasonHolderImportFromSeasonAsync` answers `202 {job}` when a commit brings across more than 100 holders: a `holder_import` job, whose `summary` has the same counts when it completes. It can refuse with `409 job_mismatch`, `503 job_busy` or `503 season_not_configured`. A retrieved bulk job carries `summary`, empty until it completes.
- `CreateSeasonRenewalNoticesAsync` takes up to 20,000 offer ids; more answers `422 too_many_offer_ids`.

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
