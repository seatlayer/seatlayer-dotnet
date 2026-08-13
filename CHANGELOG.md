# Changelog

## Unreleased

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
