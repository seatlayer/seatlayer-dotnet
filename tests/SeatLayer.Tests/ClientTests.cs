using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SeatLayer;
using Xunit;

namespace SeatLayer.Tests;

/// <summary>
/// Replays a queue of responses and records every request, so the retry loop, headers and
/// error mapping are exercised without a network.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body, IDictionary<string, string> Headers)> _responses;

    internal StubHandler(
        IEnumerable<(HttpStatusCode Status, string Body, IDictionary<string, string> Headers)> responses)
        => _responses = new Queue<(HttpStatusCode, string, IDictionary<string, string>)>(responses);

    internal List<(
        HttpMethod Method,
        Uri Url,
        HttpRequestHeaders Headers,
        string? Body,
        string? ContentType)> Calls { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Calls.Add((
            request.Method,
            request.RequestUri!,
            request.Headers,
            body,
            request.Content?.Headers.ContentType?.MediaType));

        Assert.True(_responses.Count > 0, "more requests than queued responses");
        var (status, content, headers) = _responses.Dequeue();

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
        foreach (var header in headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }
}

public class ClientTests
{
    private static (SeatLayerClient Client, StubHandler Handler) Build(
        IEnumerable<(HttpStatusCode, string, IDictionary<string, string>)> responses, int maxRetries = 3)
    {
        var handler = new StubHandler(responses);
        var client = new SeatLayerClient("sk_test_abc", new SeatLayerClientOptions
        {
            HttpClient = new HttpClient(handler),
            MaxRetries = maxRetries,
        });
        return (client, handler);
    }

    private static (HttpStatusCode, string, IDictionary<string, string>) Ok(string body)
        => (HttpStatusCode.OK, body, new Dictionary<string, string>());

    private static (HttpStatusCode, string, IDictionary<string, string>) Status(
        HttpStatusCode status, string body, IDictionary<string, string>? headers = null)
        => (status, body, headers ?? new Dictionary<string, string>());

    // ---------- construction ----------

    [Fact]
    public void RejectsPublishableKeyByName()
    {
        // The pk_/sk_ mix-up is the most common first-run failure; a 401 three
        // round-trips later teaches nothing.
        var error = Assert.Throws<ArgumentException>(() => new SeatLayerClient("pk_test_abc"));
        Assert.Contains("publishable key", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    public void RejectsNonSecretKey(string key)
        => Assert.Throws<ArgumentException>(() => new SeatLayerClient(key));

    [Fact]
    public void ReportsKeyMode()
    {
        Assert.Equal("test", new SeatLayerClient("sk_test_abc").Mode);
        Assert.Equal("live", new SeatLayerClient("sk_live_abc").Mode);
    }

    // ---------- requests ----------

    [Fact]
    public async Task SendsBearerAuthAndParsesBody()
    {
        var (client, handler) = Build(new[] { Ok("{\"meta\":{\"key\":\"ev_1\"}}") });

        var result = await client.Events.RetrieveAsync("ev_1");

        var meta = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result["meta"]);
        Assert.Equal("ev_1", meta["key"]);
        Assert.Equal("Bearer sk_test_abc", handler.Calls[0].Headers.Authorization!.ToString());
        Assert.Equal("https://api.seatlayer.io/v1/events/ev_1", handler.Calls[0].Url.ToString());
    }

    [Fact]
    public async Task EscapesPathParameters()
    {
        var (client, handler) = Build(new[] { Ok("{}") });
        await client.Events.RetrieveAsync("ev/../admin");
        Assert.Contains("%2F", handler.Calls[0].Url.ToString());
    }

    [Fact]
    public async Task OnlyGeneratesIdempotencyKeyForHeaderReplayMutations()
    {
        var (client, handler) = Build(new[]
        {
            Ok("{\"events\":[]}"),
            Status(HttpStatusCode.Created, "{}"),
            Status(HttpStatusCode.Created, "{}"),
        });

        await client.Events.ListAsync();
        await client.Events.CreateAsync("c_1");
        await client.Inventory.HoldAsync("ev_1", new[] { "A-1" });

        Assert.False(handler.Calls[0].Headers.Contains("Idempotency-Key"));
        Assert.True(handler.Calls[1].Headers.Contains("Idempotency-Key"));
        Assert.False(handler.Calls[2].Headers.Contains("Idempotency-Key"));
    }

    [Fact]
    public async Task TemplateInstantiationUsesObjectBodyEscapedPathAndHeaderReplay()
    {
        var retryNow = new Dictionary<string, string> { ["Retry-After"] = "0" };
        var (client, handler) = Build(new[]
        {
            Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}", retryNow),
            Status(HttpStatusCode.Created, "{\"meta\":{}}"),
        });

        await client.Templates.InstantiateTemplateAsync("tpl / main");

        Assert.Equal(2, handler.Calls.Count);
        Assert.All(handler.Calls, call =>
        {
            Assert.Equal(HttpMethod.Post, call.Method);
            Assert.Equal("https://api.seatlayer.io/v1/templates/tpl%20%2F%20main/instantiate", call.Url.AbsoluteUri);
            Assert.Equal("{}", call.Body);
        });
        Assert.Equal(
            handler.Calls[0].Headers.GetValues("Idempotency-Key").Single(),
            handler.Calls[1].Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task TicketReleaseMethodsUseWholeListBodyEscapedPathsAndSingleAttempts()
    {
        var retryNow = new Dictionary<string, string> { ["Retry-After"] = "0" };
        var (client, handler) = Build(new[]
        {
            Ok("{\"releases\":[]}"),
            Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}", retryNow),
            Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}", retryNow),
        });

        await client.Events.ListTicketReleasesAsync("ev / main");
        await Assert.ThrowsAsync<SeatLayerRateLimitException>(() => client.Events.UpdateTicketReleasesAsync(
            "ev / main",
            new TicketReleaseReplaceRequest
            {
                Releases = new[] { new TicketReleaseInput { Name = "Early bird", Price = 18 } },
            }));
        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Events.CloseTicketReleaseAsync("ev / main", "rel / one"));

        Assert.Equal(3, handler.Calls.Count);
        Assert.Equal("https://api.seatlayer.io/v1/events/ev%20%2F%20main/releases", handler.Calls[0].Url.AbsoluteUri);
        Assert.Equal(HttpMethod.Put, handler.Calls[1].Method);
        using var update = JsonDocument.Parse(handler.Calls[1].Body!);
        var release = update.RootElement.GetProperty("releases")[0];
        Assert.Equal("Early bird", release.GetProperty("name").GetString());
        Assert.Equal(18, release.GetProperty("price").GetInt32());
        Assert.False(release.TryGetProperty("position", out _));
        Assert.False(handler.Calls[1].Headers.Contains("Idempotency-Key"));
        Assert.Equal(
            "https://api.seatlayer.io/v1/events/ev%20%2F%20main/releases/rel%20%2F%20one/close",
            handler.Calls[2].Url.AbsoluteUri);
        Assert.False(handler.Calls[2].Headers.Contains("Idempotency-Key"));
    }

    [Fact]
    public async Task EventConfigurationBindingUsesExactVersionsExplicitDetachAndSingleAttempts()
    {
        const string binding = "{\"configuration\":{\"id\":\"ec_touring\",\"version\":3}," +
            "\"revision\":7,\"changedBy\":\"api-key:key_1\",\"changedAt\":123,\"audit\":[" +
            "{\"id\":\"eca_1\",\"from\":null,\"to\":{\"id\":\"ec_touring\",\"version\":3}," +
            "\"revision\":7,\"actor\":\"api-key:key_1\",\"createdAt\":123}]}";
        var retryNow = new Dictionary<string, string> { ["Retry-After"] = "0" };
        var (client, handler) = Build(new[]
        {
            Ok(binding),
            Ok(binding),
            Ok("{\"configuration\":null,\"revision\":8,\"changedBy\":null,\"changedAt\":null,\"audit\":[]}"),
            Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}", retryNow),
        });

        var retrieved = await client.Events.RetrieveConfigurationBindingAsync("ev / main");
        var attached = await client.Events.UpdateConfigurationBindingAsync(
            "ev / main",
            new EventConfigurationBindingUpdateRequest
            {
                ExpectedRevision = 6,
                Configuration = new EventConfigurationReference { Id = "ec_touring", Version = 3 },
            });
        var detached = await client.Events.UpdateConfigurationBindingAsync(
            "ev / main",
            new EventConfigurationBindingUpdateRequest { ExpectedRevision = 7, Configuration = null });
        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Events.UpdateConfigurationBindingAsync(
                "ev_1",
                new EventConfigurationBindingUpdateRequest { ExpectedRevision = 8, Configuration = null }));

        var configuration = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            retrieved["configuration"]);
        Assert.Equal("ec_touring", configuration["id"]);
        var audit = Assert.IsAssignableFrom<IReadOnlyList<object?>>(retrieved["audit"]);
        var firstAudit = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(audit[0]);
        Assert.Null(firstAudit["from"]);
        Assert.NotNull(attached["configuration"]);
        Assert.Null(detached["configuration"]);

        Assert.Equal(4, handler.Calls.Count);
        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(
                "https://api.seatlayer.io/v1/events/ev%20%2F%20main/event-configuration",
                handler.Calls[index].Url.AbsoluteUri);
        }
        Assert.Equal(HttpMethod.Get, handler.Calls[0].Method);
        Assert.Equal(HttpMethod.Put, handler.Calls[1].Method);
        Assert.Equal(HttpMethod.Put, handler.Calls[2].Method);
        using var attachBody = JsonDocument.Parse(handler.Calls[1].Body!);
        Assert.Equal(6, attachBody.RootElement.GetProperty("expectedRevision").GetInt64());
        Assert.Equal("ec_touring", attachBody.RootElement.GetProperty("configuration")
            .GetProperty("id").GetString());
        using var detachBody = JsonDocument.Parse(handler.Calls[2].Body!);
        Assert.Equal(JsonValueKind.Null, detachBody.RootElement.GetProperty("configuration").ValueKind);
        Assert.False(handler.Calls[1].Headers.Contains("Idempotency-Key"));
        Assert.False(handler.Calls[2].Headers.Contains("Idempotency-Key"));
        Assert.False(handler.Calls[3].Headers.Contains("Idempotency-Key"));
    }

    [Fact]
    public async Task HonoursCallerIdempotencyKey()
    {
        var (client, handler) = Build(new[] { Status(HttpStatusCode.Created, "{}") });
        await client.Events.CreateAsync("c_1", idempotencyKey: "order-42");

        Assert.Equal("order-42", handler.Calls[0].Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task RejectsInvalidIdempotencyKey()
    {
        var (client, _) = Build(Array.Empty<(HttpStatusCode, string, IDictionary<string, string>)>());
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Events.CreateAsync("c_1", idempotencyKey: "has spaces"));
        Assert.Contains("Invalid Idempotency-Key", error.Message);
    }

    [Fact]
    public async Task DropsNullQueryParameters()
    {
        var (client, handler) = Build(new[] { Ok("{\"charts\":[]}") });
        await client.Charts.ListAsync(new ChartListRequest { WorkspaceId = "ws_1" });

        Assert.Equal("https://api.seatlayer.io/v1/charts?workspaceId=ws_1", handler.Calls[0].Url.ToString());
    }

    [Fact]
    public async Task OmitsEmptyOptionalFieldsRatherThanNulling()
    {
        // Sending "name": null is not the same as omitting it; some fields treat an
        // explicit null as "clear this".
        var (client, handler) = Build(new[] { Status(HttpStatusCode.Created, "{}") });
        await client.Events.CreateAsync("c_1");

        var body = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Single(body.RootElement.EnumerateObject());
        Assert.True(body.RootElement.TryGetProperty("chartId", out _));
    }

    // ---------- errors ----------

    [Fact]
    public async Task ModeMismatchIsTyped()
    {
        var (client, _) = Build(new[]
        {
            Status(HttpStatusCode.Forbidden, "{\"error\":\"mode_mismatch\"}"),
        });

        var error = await Assert.ThrowsAsync<SeatLayerAuthException>(
            () => client.Events.RetrieveAsync("ev_1"));
        Assert.True(error.IsModeMismatch);
    }

    [Fact]
    public async Task ConflictsExposedPerSeat()
    {
        var (client, _) = Build(new[]
        {
            Status(HttpStatusCode.Conflict,
                "{\"error\":\"conflict\",\"conflicts\":[{\"label\":\"A-1\",\"status\":\"booked\"}]}"),
        });

        var error = await Assert.ThrowsAsync<SeatLayerConflictException>(
            () => client.Inventory.HoldAsync("ev_1", new[] { "A-1" }));
        Assert.Single(error.Conflicts);
        Assert.Equal("A-1", error.Conflicts[0]["label"]);
    }

    [Fact]
    public async Task SoldOutIsABusinessOutcome()
    {
        var (client, _) = Build(new[]
        {
            Status(HttpStatusCode.Conflict, "{\"error\":\"conflict\",\"reason\":\"sold_out\"}"),
        });

        var error = await Assert.ThrowsAsync<SeatLayerConflictException>(
            () => client.Inventory.HoldBestAvailableAsync("ev_1", new BestAvailableRequest { Qty = 4 }));
        Assert.True(error.IsSoldOut);
    }

    [Fact]
    public async Task NotFoundIsTyped()
    {
        var (client, _) = Build(new[] { Status(HttpStatusCode.NotFound, "{\"error\":\"not_found\"}") });
        await Assert.ThrowsAsync<SeatLayerNotFoundException>(() => client.Events.RetrieveAsync("ev_1"));
    }

    [Fact]
    public async Task SurfacesRequestId()
    {
        var (client, _) = Build(
            new[]
            {
                Status(HttpStatusCode.InternalServerError, "{\"error\":\"internal\"}",
                    new Dictionary<string, string> { ["X-Request-ID"] = "req_9" }),
            },
            maxRetries: 1);

        var error = await Assert.ThrowsAsync<SeatLayerException>(() => client.Events.RetrieveAsync("ev_1"));
        Assert.Equal("req_9", error.RequestId);
    }

    [Fact]
    public async Task StableErrorContractPrefersCodeAndPreservesEvidence()
    {
        var (client, _) = Build(
            new[]
            {
                Status(
                    HttpStatusCode.UnprocessableEntity,
                    "{\"error\":\"validation_failed\",\"code\":\"invalid_expiry\",\"field\":\"expiresAt\"}",
                    new Dictionary<string, string> { ["X-Request-ID"] = "req_contract" }),
            },
            maxRetries: 1);

        var error = await Assert.ThrowsAsync<SeatLayerValidationException>(
            () => client.Channels.CreateAccessLinkAsync(
                "ev_1", "ch_1", new AccessLinkCreateRequest { ExpiresAt = 1 }));
        Assert.Equal(422, error.Status);
        Assert.Equal("invalid_expiry", error.Code);
        Assert.Equal("expiresAt", error.Body["field"]);
        Assert.Equal("req_contract", error.RequestId);
    }

    [Fact]
    public async Task SurvivesNonJsonErrorBody()
    {
        // A proxy or WAF can answer with HTML; that must not become a parse crash that
        // hides the real status from the caller.
        var (client, _) = Build(
            new[] { Status(HttpStatusCode.BadGateway, "<html>bad gateway</html>") }, maxRetries: 1);

        var error = await Assert.ThrowsAsync<SeatLayerException>(() => client.Events.RetrieveAsync("ev_1"));
        Assert.Equal(502, error.Status);
    }

    // ---------- retry ----------

    [Fact]
    public async Task HeaderReplayMutationsRetry429AndReuseIdempotencyKey()
    {
        var operations = new (string Name, Func<SeatLayerClient, Task> Run)[]
        {
            ("create chart", async client => await client.Charts.CreateAsync("Main")),
            ("copy chart", async client => await client.Charts.CopyAsync("c_1")),
            ("create event", async client => await client.Events.CreateAsync("c_1")),
            ("create workspace", async client => await client.Workspaces.CreateAsync("Tenant")),
        };

        foreach (var operation in operations)
        {
            var (client, handler) = Build(new[]
            {
                Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}",
                    new Dictionary<string, string> { ["Retry-After"] = "0" }),
                Status(HttpStatusCode.Created, "{\"ok\":true}"),
            });

            await operation.Run(client);

            Assert.True(handler.Calls.Count == 2, operation.Name);
            Assert.Equal(
                handler.Calls[0].Headers.GetValues("Idempotency-Key").Single(),
                handler.Calls[1].Headers.GetValues("Idempotency-Key").Single());
        }
    }

    [Fact]
    public async Task BookingWithCallerKeyRemainsSingleAttempt()
    {
        var (client, handler) = Build(new[]
        {
            Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}",
                new Dictionary<string, string> { ["Retry-After"] = "0" }),
        });

        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Inventory.BookLabelsAsync(
                "ev_1", new[] { "A-1" }, "order-42", idempotencyKey: "request-42"));

        Assert.Single(handler.Calls);
        Assert.Equal("request-42", handler.Calls[0].Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task RawMutationIsFailClosedForRetries()
    {
        var (client, handler) = Build(new[]
        {
            Status((HttpStatusCode)429, "{\"error\":\"rate_limited\"}",
                new Dictionary<string, string> { ["Retry-After"] = "0" }),
        });

        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.SendAsync(
                HttpMethod.Post, "/v1/events", body: new { chartId = "c_1" }, idempotencyKey: "raw-42"));

        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task ReadRetriesTransientFailure()
    {
        var (client, handler) = Build(new[]
        {
            Status((HttpStatusCode)429, "{}", new Dictionary<string, string> { ["Retry-After"] = "0" }),
            Ok("{\"meta\":{\"key\":\"ev_1\"}}"),
        });

        await client.Events.RetrieveAsync("ev_1");

        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task DoesNotRetry4xx()
    {
        var (client, handler) = Build(new[]
        {
            Status(HttpStatusCode.UnprocessableEntity, "{\"error\":\"invalid_slug\"}"),
        });

        await Assert.ThrowsAsync<SeatLayerValidationException>(() => client.Events.CreateAsync("c_1"));
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task GivesUpAfterMaxRetries()
    {
        var retryNow = new Dictionary<string, string> { ["Retry-After"] = "0" };
        var (client, handler) = Build(
            new[] { Status((HttpStatusCode)429, "{}", retryNow), Status((HttpStatusCode)429, "{}", retryNow) },
            maxRetries: 2);

        await Assert.ThrowsAsync<SeatLayerRateLimitException>(() => client.Events.CreateAsync("c_1"));
        Assert.Equal(2, handler.Calls.Count);
    }

    [Fact]
    public async Task RetryAfterHeaderWins()
    {
        var (client, _) = Build(
            new[]
            {
                Status((HttpStatusCode)429, "{\"error\":\"rate_limited\",\"retryAfterSeconds\":99}",
                    new Dictionary<string, string> { ["Retry-After"] = "0" }),
            },
            maxRetries: 1);

        var error = await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Events.RetrieveAsync("ev_1"));
        Assert.Equal(0, error.RetryAfterSeconds);
    }

    [Fact]
    public async Task CancellationStopsImmediately()
    {
        // A cancelled token is the caller's decision, not a transient fault to retry through.
        var (client, _) = Build(new[] { Ok("{}") });
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Events.RetrieveAsync("ev_1", cts.Token));
    }

    // ---------- pagination ----------

    [Fact]
    public async Task ListAllWalksPagesAndStops()
    {
        var (client, handler) = Build(new[]
        {
            Ok("{\"charts\":[{\"id\":\"c_1\"},{\"id\":\"c_2\"}],\"nextCursor\":\"cur_1\"}"),
            Ok("{\"charts\":[{\"id\":\"c_3\"}]}"),
        });

        var seen = new List<object?>();
        await foreach (var chart in client.Charts.ListAllAsync())
        {
            seen.Add(chart["id"]);
        }

        Assert.Equal(new object?[] { "c_1", "c_2", "c_3" }, seen);
        Assert.Equal(2, handler.Calls.Count);
        // Absent nextCursor terminates — a caller looping cannot spin forever.
        Assert.Contains("cursor=cur_1", handler.Calls[1].Url.ToString());
    }

    [Fact]
    public async Task ListAllEventsSkipsCountsFanout()
    {
        // Counts cost a server round-trip PER EVENT, which is exactly the cost pagination
        // was added to avoid.
        var (client, handler) = Build(new[] { Ok("{\"events\":[]}") });
        await foreach (var _ in client.Events.ListAllAsync())
        {
            // drain
        }

        Assert.Contains("counts=0", handler.Calls[0].Url.ToString());
    }

    [Fact]
    public async Task SinglePageKeepsCounts()
    {
        var (client, handler) = Build(new[] { Ok("{\"events\":[]}") });
        await client.Events.ListAsync(new EventListRequest { Limit = 10 });
        Assert.DoesNotContain("counts=0", handler.Calls[0].Url.ToString());
    }

    // ---------- guards ----------

    [Fact]
    public async Task ManageSessionRequiresCapabilities()
    {
        var (client, _) = Build(Array.Empty<(HttpStatusCode, string, IDictionary<string, string>)>());
        // The API defaults omission to view-only, but the SDK requires an explicit grant
        // so browser authority stays reviewable at the call site.
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Sessions.CreateManageSessionAsync("ev_1", "https://box.example", Array.Empty<string>()));
        Assert.Contains("capabilities is required", error.Message);
    }

    [Fact]
    public async Task ManageSessionSendsCapabilities()
    {
        var (client, handler) = Build(new[] { Status(HttpStatusCode.Created, "{\"token\":\"mse_x\"}") });
        await client.Sessions.CreateManageSessionAsync(
            "ev_1", "https://box.example", new[] { SessionsService.CapabilityView });

        var body = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal("event:view", body.RootElement.GetProperty("capabilities")[0].GetString());
    }

    [Fact]
    public async Task BookBestAvailableRequiresBookingRef()
    {
        var (client, _) = Build(Array.Empty<(HttpStatusCode, string, IDictionary<string, string>)>());
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Inventory.BookBestAvailableAsync("ev_1", new BestAvailableRequest { Qty = 2 }));
        Assert.Contains("BookingRef is required", error.Message);
    }

    [Fact]
    public async Task ChartUpdateSendsExpectedUpdatedAt()
    {
        var (client, handler) = Build(new[] { Ok("{\"meta\":{}}") });
        await client.Charts.UpdateAsync("c_1", new Dictionary<string, object?> { ["version"] = 1 }, 1234);

        var body = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal(1234, body.RootElement.GetProperty("expectedUpdatedAt").GetInt64());
    }

    [Fact]
    public async Task ExtendHoldPostsHoldId()
    {
        var (client, handler) = Build(new[] { Ok("{\"ok\":true,\"expiresAt\":123}") });
        await client.Inventory.ExtendHoldAsync("ev_1", "h_9", 600000);

        Assert.Equal("https://api.seatlayer.io/v1/events/ev_1/extend", handler.Calls[0].Url.ToString());
        var body = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal("h_9", body.RootElement.GetProperty("holdId").GetString());
        Assert.Equal(600000, body.RootElement.GetProperty("ttlMs").GetInt64());
    }

    [Fact]
    public async Task HoldCarriesChannelAuthority()
    {
        var (client, handler) = Build(new[] { Ok("{\"holdId\":\"h_1\"}") });
        await client.Inventory.HoldAsync(
            "ev_1",
            new[] { "A-1" },
            channelIds: new[] { "ch_partner" },
            ignoreChannelRestrictions: false,
            reason: "partner checkout");

        var body = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal("ch_partner", body.RootElement.GetProperty("channelIds")[0].GetString());
        Assert.False(body.RootElement.GetProperty("ignoreChannelRestrictions").GetBoolean());
        Assert.Equal("partner checkout", body.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ExtendedInventoryContractsAreSentExactly()
    {
        var (client, handler) = Build(new[]
        {
            Ok("{\"ok\":true}"),
            Ok("{\"ok\":true}"),
            Ok("{\"ok\":true,\"holdTtlMs\":null}"),
        });

        await client.Inventory.ExtendHoldAsync(
            "ev_1", "h_1",
            channelIds: new[] { "ch_partner" },
            ignoreChannelRestrictions: true,
            reason: "staff override");
        await client.Inventory.BlockAsync("ev_1", new[] { "A-1" }, releaseAt: 1_800_000_000_000);
        await client.Events.UpdateHoldTtlAsync("ev_1", null);

        using var extend = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal("ch_partner", extend.RootElement.GetProperty("channelIds")[0].GetString());
        Assert.True(extend.RootElement.GetProperty("ignoreChannelRestrictions").GetBoolean());
        using var block = JsonDocument.Parse(handler.Calls[1].Body!);
        Assert.Equal(1_800_000_000_000, block.RootElement.GetProperty("releaseAt").GetInt64());
        using var ttl = JsonDocument.Parse(handler.Calls[2].Body!);
        Assert.Equal(JsonValueKind.Null, ttl.RootElement.GetProperty("holdTtlMs").ValueKind);
    }

    [Fact]
    public async Task ExtendedPublicRequestContractsMatchTheServer()
    {
        var (client, handler) = Build(new[]
        {
            Status(HttpStatusCode.Created, "{\"meta\":{}}"),
            Ok("{\"ok\":true,\"updated\":true,\"meta\":{}}"),
            Ok("{\"meta\":{}}"),
            Status(HttpStatusCode.Created, "{\"link\":{},\"capability\":\"x\"}"),
            Status(HttpStatusCode.Created, "{\"session\":{\"id\":\"dse_1\"}}"),
            Ok("{\"deliveries\":[]}"),
            Ok("{\"sessions\":[]}"),
            Ok("{\"ok\":true,\"channel\":{}}"),
        });

        await client.Events.CreateAsync(new EventCreateRequest
        {
            ChartId = "c_1",
            Description = "Gala",
            EndsAt = 1_800_000_000_000,
            Timezone = "Europe/London",
            Locale = "en-GB",
            Mode = "test",
        });
        await client.Events.UpdateChartAsync("ev_1", true, "accept allocation drop");
        await client.Events.UpdatePosterAsync("ev_1", new byte[] { 0x89, 0x50, 0x4e, 0x47 }, "image/png");
        var linkResult = await client.Channels.CreateAccessLinkAsync(
            "ev/1", "ch/1", new AccessLinkCreateRequest
            {
                Label = "Partner",
                IncludePublic = false,
                MaxRedemptions = 50,
                SessionTtlSeconds = 900,
            });
        var designerResult = await client.Sessions.CreateDesignerSessionAsync(new DesignerSessionRequest
        {
            WorkspaceId = "ws_1",
            ChartId = "c_1",
            AllowedOrigin = "https://designer.example",
            Authority = "publish",
            CanPublish = true,
            Mode = "safe",
            SafeModeOptions = new Dictionary<string, bool> { ["allowDeletingObjects"] = false },
            Features = new Dictionary<string, object?> { ["tables"] = true },
        });
        await client.Webhooks.ListDeliveriesAsync("wh_1", 25, "failed", 1_800_000_000_000);
        await client.Channels.ListBuyerAccessSessionsAsync(
            "ev_1", new BuyerAccessSessionListRequest { Limit = 20 });
        await client.Channels.ArchiveAsync("ev_1", "ch_1", null, "return to public");

        using var create = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.Equal("test", create.RootElement.GetProperty("mode").GetString());
        using var update = JsonDocument.Parse(handler.Calls[1].Body!);
        Assert.True(update.RootElement.GetProperty("acknowledgeDroppedAssignments").GetBoolean());
        Assert.Equal("image/png", handler.Calls[2].ContentType);
        Assert.Contains("/events/ev%2F1/channels/ch%2F1/access-links", handler.Calls[3].Url.ToString());
        using var designer = JsonDocument.Parse(handler.Calls[4].Body!);
        Assert.True(designer.RootElement.GetProperty("canPublish").GetBoolean());
        Assert.Equal("x", linkResult["capability"]);
        var session = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(designerResult["session"]);
        Assert.Equal("dse_1", session["id"]);
        Assert.Contains("status=failed", handler.Calls[5].Url.ToString());
        Assert.Equal(
            "https://api.seatlayer.io/v1/events/ev_1/buyer-access-sessions?limit=20",
            handler.Calls[6].Url.ToString());
        using var archive = JsonDocument.Parse(handler.Calls[7].Body!);
        Assert.Equal(JsonValueKind.Null, archive.RootElement.GetProperty("destination").ValueKind);
    }

    [Fact]
    public async Task CreatesOriginBoundBuyerAccessSession()
    {
        var (client, handler) = Build(new[] { Status(HttpStatusCode.Created, "{\"token\":\"bas_x\"}") });
        await client.Channels.CreateBuyerAccessSessionAsync(
            "ev/1",
            new BuyerAccessSessionRequest
            {
                IncludePublic = false,
                AllowedOrigin = "https://partner.example",
                ChannelIds = new[] { "ch_1" },
                MaxQuantity = 4,
                IdempotencyKey = "partner-order-42",
            });

        Assert.Equal(
            "https://api.seatlayer.io/v1/events/ev%2F1/buyer-access-sessions",
            handler.Calls[0].Url.ToString());
        Assert.Equal(
            "partner-order-42",
            handler.Calls[0].Headers.GetValues("Idempotency-Key").Single());
        var body = JsonDocument.Parse(handler.Calls[0].Body!);
        Assert.False(body.RootElement.GetProperty("includePublic").GetBoolean());
        Assert.Equal("https://partner.example", body.RootElement.GetProperty("allowedOrigin").GetString());
        Assert.Equal("ch_1", body.RootElement.GetProperty("channelIds")[0].GetString());
    }

    [Fact]
    public async Task ReadsBookingByTrimmedEncodedReference()
    {
        var (client, handler) = Build(new[] { Ok("{\"bookingRef\":\"order / 42\"}") });
        await client.Inventory.RetrieveBookingAsync("ev_1", "  order / 42  ");

        Assert.Equal(
            "https://api.seatlayer.io/v1/events/ev_1/bookings/order%20%2F%2042",
            handler.Calls[0].Url.AbsoluteUri);
    }

    [Fact]
    public async Task RejectsBlankBookingReferenceBeforeRequest()
    {
        var (client, _) = Build(Array.Empty<(HttpStatusCode, string, IDictionary<string, string>)>());
        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Inventory.UnbookAsync("ev_1", new[] { "A-1" }, "   "));
        Assert.Contains("bookingRef is required", error.Message);
    }

    [Fact]
    public async Task SpentHoldIsAConflict()
    {
        var (client, _) = Build(new[]
        {
            Status(HttpStatusCode.Conflict, "{\"error\":\"cannot_extend\",\"reason\":\"expired\"}"),
        });

        var error = await Assert.ThrowsAsync<SeatLayerConflictException>(
            () => client.Inventory.ExtendHoldAsync("ev_1", "h_9"));
        Assert.Equal("cannot_extend", error.Code);
    }

    [Fact]
    public async Task FallsBackToRetryAfterJsonFieldWhenNoHeader()
    {
        // An integral retryAfterSeconds decodes as long, so a check that only matched
        // double would silently fall through to the 1-second default.
        var (client, _) = Build(
            new[] { Status((HttpStatusCode)429, "{\"error\":\"rate_limited\",\"retryAfterSeconds\":7}") },
            maxRetries: 1);

        var error = await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Events.RetrieveAsync("ev_1"));
        Assert.Equal(7, error.RetryAfterSeconds);
    }

    [Fact]
    public async Task EpochMillisStaysIntegral()
    {
        // A double would render 1754006400000 as 1.7540064E+12 in any string
        // interpolation the caller does.
        var (client, _) = Build(new[] { Ok("{\"expiresAt\":1754006400000}") });
        var result = await client.Events.RetrieveAsync("ev_1");
        Assert.IsType<long>(result["expiresAt"]);
    }
}
