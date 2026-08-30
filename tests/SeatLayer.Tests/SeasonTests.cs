using System.Net;
using SeatLayer;
using Xunit;

namespace SeatLayer.Tests;

public class SeasonTests
{
    private static (SeatLayerClient Client, StubHandler Handler) Build(
        IEnumerable<(HttpStatusCode, string, IDictionary<string, string>)> responses,
        int maxRetries = 1)
    {
        var handler = new StubHandler(responses);
        var client = new SeatLayerClient("sk_test_abc", new SeatLayerClientOptions
        {
            HttpClient = new HttpClient(handler),
            MaxRetries = maxRetries,
        });
        return (client, handler);
    }

    private static (HttpStatusCode, string, IDictionary<string, string>) Ok()
        => (HttpStatusCode.OK, "{}", new Dictionary<string, string>());

    private static (HttpStatusCode, string, IDictionary<string, string>) RateLimited()
        => ((HttpStatusCode)429, "{\"error\":\"rate_limited\"}",
            new Dictionary<string, string> { ["Retry-After"] = "0" });

    [Fact]
    public async Task MapsAll48SeasonOperationsAndExactReplayClasses()
    {
        var (client, handler) = Build(Enumerable.Range(0, 48).Select(_ => Ok()));
        var seasons = client.Seasons;

        await seasons.ListSeasonsAsync(new SeasonListRequest
        {
            WorkspaceId = "ws 1", StructureState = "draft", Limit = 20, Cursor = "c/1",
        });
        await seasons.ValidateSeasonAsync(new SeasonSelectionRequest
        {
            SourcePerformanceGroupKeys = new[] { "pg_1" },
        });
        await seasons.CreateSeasonAsync(new SeasonCreateRequest
        {
            Name = "Series", EventKeys = new[] { "ev_1", "ev_2" }, IdempotencyKey = "create-1",
        });
        await seasons.RetrieveSeasonAsync("sea/a");
        await seasons.UpdateSeasonAsync("sea/a", new SeasonUpdateRequest
        {
            ExpectedRevision = 1, Name = "Series 2", IdempotencyKey = "update-1",
        });
        await seasons.DeleteSeasonAsync("sea/a", "delete-1");
        await seasons.ActivateSeasonAsync("sea/a", 1);
        await seasons.CloseSeasonAsync("sea/a", 2);
        await seasons.ArchiveSeasonAsync("sea/a", 3);
        await seasons.RetrieveSeasonLifecycleAsync("sea/a", "life/1");
        await seasons.CreateSeasonPlanAsync("sea/a", new SeasonPlanCreateRequest
        {
            Name = "Premium", EventKeys = new[] { "ev_1", "ev_2" }, IdempotencyKey = "plan-1",
        });
        await seasons.RetrieveSeasonPlanAsync("sea/a", "plan/1");
        await seasons.PublishSeasonPlanAsync("sea/a", "plan/1", 4);
        await seasons.SupersedeSeasonPlanAsync("sea/a", "plan/1", 5);
        await seasons.OpenSeasonSalesAsync("sea/a", 6);
        await seasons.PauseSeasonSalesAsync("sea/a", 7);
        await seasons.ResumeSeasonSalesAsync("sea/a", 8);
        await seasons.EndSeasonSalesAsync("sea/a", 9);
        await seasons.DuplicateSeasonToLiveAsync("sea/a", new SeasonDuplicateToLiveRequest
        {
            EventKeys = new[] { "live_1", "live_2" }, IdempotencyKey = "live-1",
        });
        await seasons.CreateSeasonBuyerAccessSessionAsync(
            "sea/a", new SeasonBuyerAccessSessionRequest
            {
                AllowedOrigin = "https://tickets.example", IncludePublic = true,
            });
        await seasons.ListSeasonBuyerAccessSessionsAsync("sea/a", 10);
        await seasons.RevokeSeasonBuyerAccessSessionAsync("sea/a", "session/1");
        await seasons.RetrieveSeasonHoldAsync("sea/a", "hold/1");
        await seasons.BookSeasonHoldAsync("sea/a", "hold/1", new SeasonBookHoldRequest
        {
            BookActionId = "book_1", BookingRef = "order_1",
        });
        await seasons.RetrieveSeasonBookingAsync("sea/a", "book/1");
        await seasons.CancelSeasonBookingAsync(
            "sea/a", "book/1", new SeasonCancelBookingRequest
            {
                CancelActionId = "cancel_1", BookingRef = "order_1",
                PlanActivationId = "activation_1", RightDisposition = "release",
            });
        await seasons.ValidateSeasonBuyerRehearsalAsync("sea/a");
        await seasons.CreateSeasonHolderImportAsync("sea/a", new SeasonHolderImportRequest
        {
            SuccessorPlanActivationId = "activation_2",
            Rows = new[]
            {
                new SeasonHolderImportRow
                {
                    RowId = "row_1", HolderRef = "holder_1",
                    PriorPlanActivationId = "activation_1", PriorContractRef = "contract_1",
                    Labels = new[] { "A-1" },
                },
            },
            IdempotencyKey = "import-1",
        });
        await seasons.RetrieveSeasonHolderImportAsync("sea/a", "import/1");
        await seasons.CreateSeasonRenewalOffersAsync(
            "sea/a", new SeasonRenewalOffersCreateRequest
            {
                SuccessorPlanActivationId = "activation_2", DeadlineAt = 1_800_000_000_000,
                IdempotencyKey = "offers-1",
            });
        await seasons.ListSeasonRenewalOffersAsync("sea/a");
        await seasons.RetrieveSeasonRenewalOfferAsync("sea/a", "offer/1");
        await seasons.ExtendSeasonRenewalOfferAsync("sea/a", "offer/1", 1_800_000_100_000);
        await seasons.InspectSeasonRenewalOfferAsync("sea/a", "offer/1");
        await seasons.CommitSeasonRenewalOfferAsync(
            "sea/a", "offer/1", new SeasonRenewalCommitRequest
            {
                CommitActionId = "commit_1", OrderRef = "order_2",
                BookingRef = "booking_2", PlanActivationId = "activation_2",
            });
        await seasons.DeclineSeasonRenewalOfferAsync("sea/a", "offer/1");
        await seasons.ReleaseSeasonRenewalOfferAsync("sea/a", "offer/1");
        await seasons.ListSeasonOccurrencesAsync("sea/a");
        await seasons.CreateSeasonAmendmentAsync("sea/a", new SeasonAmendmentCreateRequest
        {
            EventKey = "event/1", Kind = "reschedule", StartsAt = 1_800_000_200_000,
            IdempotencyKey = "amendment-1",
        });
        await seasons.ListSeasonAmendmentsAsync("sea/a");
        await seasons.RetrieveSeasonAmendmentAsync("sea/a", "amendment/1");
        await seasons.RetrieveSeasonReportAsync("sea/a");
        await seasons.ListSeasonOperationsAsync("sea/a");
        await seasons.RetrieveSeasonSupportLookupAsync(
            "sea/a", new SeasonSupportLookupRequest { HolderRef = "holder a/b" });
        await seasons.ListSeasonOutboxAsync("sea/a");
        await seasons.ReplaySeasonOutboxAsync("sea/a", "occurrence/1");
        await seasons.ListSeasonAuditAsync("sea/a");
        await seasons.ExportSeasonSupportSnapshotAsync("sea/a");

        var expected = new (HttpMethod Method, string Url)[]
        {
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons?workspaceId=ws+1&structureState=draft&limit=20&cursor=c%2f1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/validate"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa"),
            (HttpMethod.Patch, "https://api.seatlayer.io/v1/seasons/sea%2Fa"),
            (HttpMethod.Delete, "https://api.seatlayer.io/v1/seasons/sea%2Fa"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/activate"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/close"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/archive"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/lifecycle/life%2F1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1/publish"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1/supersede"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/sales/open"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/sales/pause"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/sales/resume"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/sales/end"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/duplicate-to-live"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/buyer-access-sessions"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/buyer-access-sessions?limit=10"),
            (HttpMethod.Delete, "https://api.seatlayer.io/v1/seasons/sea%2Fa/buyer-access-sessions/session%2F1"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/holds/hold%2F1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/holds/hold%2F1/book"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/bookings/book%2F1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/bookings/book%2F1/cancel"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/buyer-rehearsals/validate"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/imports"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/imports/import%2F1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/extend"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/inspect"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/commit"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/decline"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/release"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/occurrences"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/amendments"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/amendments"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/amendments/amendment%2F1"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/reports"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/operations"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/support-lookups?holderRef=holder+a%2fb"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/outbox"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/outbox/occurrence%2F1/replay"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/audit"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/export"),
        };

        Assert.Equal(48, handler.Calls.Count);
        Assert.Null(handler.Calls[26].Body);
        Assert.Equal(expected.Length, handler.Calls.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Method, handler.Calls[index].Method);
            Assert.Equal(expected[index].Url, handler.Calls[index].Url.AbsoluteUri);
        }

        var replayKeys = new Dictionary<int, string>
        {
            [2] = "create-1", [4] = "update-1", [5] = "delete-1", [10] = "plan-1",
            [18] = "live-1", [27] = "import-1", [29] = "offers-1", [38] = "amendment-1",
        };
        for (var index = 0; index < handler.Calls.Count; index++)
        {
            if (replayKeys.TryGetValue(index, out var key))
            {
                Assert.Equal(key, handler.Calls[index].Headers.GetValues("Idempotency-Key").Single());
            }
            else
            {
                Assert.False(handler.Calls[index].Headers.Contains("Idempotency-Key"));
            }
        }
    }

    [Fact]
    public async Task PatchAndDeleteReplayWithTheSameCallerKey()
    {
        var (client, handler) = Build(
            new[] { RateLimited(), Ok(), RateLimited(), Ok() }, maxRetries: 2);

        await client.Seasons.UpdateSeasonAsync("sea_1", new SeasonUpdateRequest
        {
            ExpectedRevision = 1, Name = "Retry", IdempotencyKey = "stable-update",
        });
        await client.Seasons.DeleteSeasonAsync("sea_1", "stable-delete");

        Assert.Equal(4, handler.Calls.Count);
        Assert.Equal(new[] { HttpMethod.Patch, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Delete },
            handler.Calls.Select(call => call.Method));
        Assert.Equal(
            new[] { "stable-update", "stable-update", "stable-delete", "stable-delete" },
            handler.Calls.Select(call => call.Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task ShowOnceBuyerSessionMintIsNeverRetried()
    {
        var (client, handler) = Build(new[] { RateLimited() }, maxRetries: 3);

        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Seasons.CreateSeasonBuyerAccessSessionAsync(
                "sea_1", new SeasonBuyerAccessSessionRequest
                {
                    AllowedOrigin = "https://tickets.example", IncludePublic = true,
                }));

        Assert.Single(handler.Calls);
        Assert.False(handler.Calls[0].Headers.Contains("Idempotency-Key"));
    }
}
