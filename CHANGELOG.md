# Changelog

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
