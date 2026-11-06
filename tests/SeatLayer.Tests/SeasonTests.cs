using System.Net;
using System.Text.Json;
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

    private static string Canonical(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    [Fact]
    public async Task MapsAll87SeasonOperationsAndExactReplayClasses()
    {
        var (client, handler) = Build(Enumerable.Range(0, 87).Select(_ => Ok()));
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

        await seasons.CreateSeasonHolderImportFromSeasonAsync(
            "sea/a", new SeasonHolderImportFromSeasonRequest
            {
                SourceSeasonId = "sea_2026", SuccessorPlanActivationId = "activation_3", DryRun = true,
                IdempotencyKey = "from-season-1",
            });
        await seasons.ListSeasonPerformancesAsync("sea/a");
        await seasons.CreateSeasonPerformancesAsync("sea/a", new SeasonPerformanceCreateRequest
        {
            TitlePattern = "Match", FirstStartsAt = 1_800_000_000_000, Timezone = "UTC",
            Repeat = "weekly", Count = 3, AttachToPlanKeys = new[] { "plan_1" },
            IdempotencyKey = "perf-create-1",
        });
        await seasons.AttachSeasonPerformancesAsync("sea/a", new SeasonPerformanceAttachRequest
        {
            EventKeys = new[] { "ev_3" }, IdempotencyKey = "perf-attach-1",
        });
        await seasons.DeleteSeasonPerformanceAsync("sea/a", "ev/3", "perf-delete-1");
        await seasons.UpdateSeasonPlanAsync("sea/a", "plan/1", new SeasonPlanUpdateRequest
        {
            Name = "Gold", IdempotencyKey = "plan-update-1",
        });
        await seasons.DeleteSeasonPlanAsync("sea/a", "plan/2", "plan-delete-1");
        await seasons.PublishSeasonAsync("sea/a");
        await seasons.CreateSeasonCheckoutTestAsync("sea/a");
        await seasons.ListSeasonCheckoutTestsAsync("sea/a");
        await seasons.ReplaceSeasonPackagePricesAsync("sea/a", "plan/1", new SeasonPackagePricesRequest
        {
            Prices = new[] { new SeasonPackagePrice { CategoryKey = "A", UnitPrice = 12000 } },
            Currency = "EUR", IdempotencyKey = "prices-1",
        });
        await seasons.ListSeasonPricesAsync("sea/a");
        await seasons.AttachSeasonPlanPerformanceAsync("sea/a", "plan/1", "ev_4", "plan-perf-1");
        await seasons.DeleteSeasonPlanPerformanceAsync("sea/a", "plan/1", "ev/4", "plan-perf-2");
        await seasons.MarkSeasonPerformancePastAsync("sea/a", "ev/4", true, "past-1");
        await seasons.ReturnSeasonOccurrenceInventoryAsync(
            "sea/a", "ev/5", new SeasonOccurrenceReturnRequest
            {
                ActionId = "return_1", BookingRef = "order_1", PlanActivationId = "activation_1",
                Labels = new[] { "A-1", "A-2" },
                Source = new SeasonOccurrenceInventorySource { BookingActionId = "book_1" },
            });
        await seasons.ReclaimSeasonOccurrenceInventoryAsync(
            "sea/a", "ev/5", new SeasonOccurrenceReclaimRequest
            {
                ActionId = "reclaim_1", ReturnActionId = "return_1", BookingRef = "order_1",
                PlanActivationId = "activation_1", Labels = new[] { "A-1" },
                Source = new SeasonOccurrenceInventorySource { RenewalOfferId = "offer_1" },
            });
        await seasons.ListSeasonHoldersAsync("sea/a", new SeasonHolderListRequest
        {
            Query = "smith jones", Status = "active", Cursor = "c/2", Limit = 25,
        });
        await seasons.RefundSeasonOrderAsync("sea/a", "ord/1", new OrderRefundRequest
        {
            AmountMinor = 500, IdempotencyKey = "refund-1",
        });
        await seasons.CancelSeasonOrderAsync("sea/a", "ord/1", new OrderCancelRequest
        {
            Reason = "other", Note = "Duplicate", RefundConfirmed = true,
            IdempotencyKey = "cancel-order-1",
        });
        await seasons.CancelSeasonOrderDateAsync(
            "sea/a", "ord/1", "ev/6", new SeasonOrderDateCancelRequest
            {
                ActionId = "date_cancel_1", IdempotencyKey = "cancel-date-1",
            });
        await seasons.ResendSeasonOrderTicketsAsync("sea/a", "ord/1", "fan@example.com");
        await seasons.RetrieveSeasonHostedRenewalsAsync("sea/a");
        await seasons.RotateSeasonRenewalOfferLinkAsync("sea/a", "offer/1");
        await seasons.UpdateSeasonRenewalOfferContactAsync(
            "sea/a", "offer/1", new SeasonRenewalContactRequest { Email = null, Name = "Ana" });
        await seasons.CreateSeasonRenewalNoticesAsync("sea/a", new[] { "offer_1", "offer_2" });
        await seasons.ResendSeasonRenewalOfferAsync("sea/a", "offer/1");
        await seasons.RetrieveSeasonSalesReportAsync("sea/a");
        await seasons.RetrieveSeasonSeatsReportAsync("sea/a");
        await seasons.ExportSeasonHoldersCsvAsync(
            "sea/a", new SeasonHolderFilterRequest { Query = "smith", Status = "active" });
        await seasons.ExportSeasonOrdersCsvAsync("sea/a");
        await seasons.RetrieveSeasonSeatCountsAsync("sea/a");
        await seasons.RefundSeasonPerformanceAsync("sea/a", "ev/7", new SeasonPerformanceRefundRequest
        {
            Amount = "per_date", Reason = "Storm", IdempotencyKey = "refund-date-1",
        });
        await seasons.ListSeasonBulkJobsAsync("sea/a");
        await seasons.RetrieveSeasonBulkJobAsync("sea/a", "job/1");
        await seasons.RetrieveSeasonRenewalQuoteAsync("sea/a", "offer/1");
        await seasons.CreateSeasonRenewalIntentAsync("sea/a", "offer/1");
        await seasons.CreateSeasonRenewalCheckoutAsync("sea/a", "offer/1", new SeasonRenewalCheckoutRequest
        {
            ReturnUrl = "https://tickets.example/renewed", BuyerEmail = "ana@example.com",
            BuyerName = "Ana Silva", Provider = "stripe", IdempotencyKey = "renew-1",
        });
        await seasons.ReleaseSeasonLapsedOffersAsync("sea/a");

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
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/imports/from-season"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/performances"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/performances"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/performances/attach"),
            (HttpMethod.Delete, "https://api.seatlayer.io/v1/seasons/sea%2Fa/performances/ev%2F3"),
            (HttpMethod.Patch, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1"),
            (HttpMethod.Delete, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F2"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/publish"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/checkout-tests"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/checkout-tests"),
            (HttpMethod.Put, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1/prices"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/prices"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1/performances"),
            (HttpMethod.Delete, "https://api.seatlayer.io/v1/seasons/sea%2Fa/plans/plan%2F1/performances/ev%2F4"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/performances/ev%2F4/past"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/occurrences/ev%2F5/returns"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/occurrences/ev%2F5/reclaims"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/holders?q=smith+jones&status=active&cursor=c%2f2&limit=25"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/orders/ord%2F1/refund"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/orders/ord%2F1/cancel"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/orders/ord%2F1/dates/ev%2F6/cancel"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/orders/ord%2F1/resend"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-hosted"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/link"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/contact"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-notices"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/resend"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/reports/sales"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/reports/seats"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/reports/holders.csv?q=smith&status=active"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/reports/orders.csv"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/seat-counts"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/performances/ev%2F7/refunds"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/jobs"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/jobs/job%2F1"),
            (HttpMethod.Get, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/quote"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/intent"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/checkout"),
            (HttpMethod.Post, "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/release-lapsed"),
        };

        Assert.Equal(87, handler.Calls.Count);
        Assert.Null(handler.Calls[26].Body);
        Assert.Equal(expected.Length, handler.Calls.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Method, handler.Calls[index].Method);
            Assert.Equal(expected[index].Url, handler.Calls[index].Url.AbsoluteUri);
        }

        // A null key means the caller passed none, so the SDK generated one.
        var replayKeys = new Dictionary<int, string?>
        {
            [2] = "create-1", [4] = "update-1", [5] = "delete-1", [10] = "plan-1",
            [18] = "live-1", [27] = "import-1", [29] = "offers-1", [38] = "amendment-1",
            [48] = "from-season-1", [50] = "perf-create-1", [51] = "perf-attach-1",
            [52] = "perf-delete-1", [53] = "plan-update-1", [54] = "plan-delete-1",
            [55] = null, [56] = null, [58] = "prices-1", [60] = "plan-perf-1",
            [61] = "plan-perf-2", [62] = "past-1", [66] = "refund-1", [67] = "cancel-order-1",
            [68] = "cancel-date-1", [80] = "refund-date-1", [85] = "renew-1", [86] = null,
        };
        Assert.Equal(26, replayKeys.Count);
        for (var index = 0; index < handler.Calls.Count; index++)
        {
            if (replayKeys.TryGetValue(index, out var key))
            {
                var sent = handler.Calls[index].Headers.GetValues("Idempotency-Key").Single();
                if (key is null)
                {
                    Assert.False(string.IsNullOrEmpty(sent));
                }
                else
                {
                    Assert.Equal(key, sent);
                }
            }
            else
            {
                Assert.False(handler.Calls[index].Headers.Contains("Idempotency-Key"));
            }
        }

        // Bodies of the 39 operations added after the first 48. Null means no body at all.
        var bodies = new Dictionary<int, string?>
        {
            [48] = "{\"sourceSeasonId\":\"sea_2026\",\"successorPlanActivationId\":\"activation_3\",\"dryRun\":true}",
            [49] = null,
            [50] = "{\"titlePattern\":\"Match\",\"firstStartsAt\":1800000000000,\"timezone\":\"UTC\",\"repeat\":\"weekly\",\"count\":3,\"attachToPlanKeys\":[\"plan_1\"]}",
            [51] = "{\"eventKeys\":[\"ev_3\"]}",
            [52] = null,
            [53] = "{\"name\":\"Gold\"}",
            [54] = null,
            [55] = null,
            [56] = "{}",
            [57] = null,
            [58] = "{\"prices\":[{\"categoryKey\":\"A\",\"unitPrice\":12000}],\"currency\":\"EUR\"}",
            [59] = null,
            [60] = "{\"eventKey\":\"ev_4\"}",
            [61] = null,
            [62] = "{\"force\":true}",
            [63] = "{\"actionId\":\"return_1\",\"bookingRef\":\"order_1\",\"planActivationId\":\"activation_1\",\"labels\":[\"A-1\",\"A-2\"],\"source\":{\"bookingActionId\":\"book_1\"}}",
            [64] = "{\"actionId\":\"reclaim_1\",\"returnActionId\":\"return_1\",\"bookingRef\":\"order_1\",\"planActivationId\":\"activation_1\",\"labels\":[\"A-1\"],\"source\":{\"renewalOfferId\":\"offer_1\"}}",
            [65] = null,
            [66] = "{\"amountMinor\":500}",
            [67] = "{\"reason\":\"other\",\"note\":\"Duplicate\",\"refundConfirmed\":true}",
            [68] = "{\"actionId\":\"date_cancel_1\"}",
            [69] = "{\"email\":\"fan@example.com\"}",
            [70] = null,
            [71] = null,
            [72] = "{\"email\":null,\"name\":\"Ana\"}",
            [73] = "{\"offerIds\":[\"offer_1\",\"offer_2\"]}",
            [74] = null,
            [75] = null,
            [76] = null,
            [77] = null,
            [78] = null,
            [79] = null,
            [80] = "{\"amount\":\"per_date\",\"reason\":\"Storm\"}",
            [81] = null,
            [82] = null,
            [83] = null,
            [84] = null,
            [85] = "{\"returnUrl\":\"https://tickets.example/renewed\",\"buyerEmail\":\"ana@example.com\",\"buyerName\":\"Ana Silva\",\"provider\":\"stripe\"}",
            [86] = null,
        };
        Assert.Equal(39, bodies.Count);
        foreach (var (index, body) in bodies)
        {
            if (body is null)
            {
                Assert.Null(handler.Calls[index].Body);
            }
            else
            {
                Assert.Equal(Canonical(body), Canonical(handler.Calls[index].Body!));
            }
        }
    }

    [Fact]
    public async Task NamesTheDateCancelVoidJobAndSizesAHolderImportCallByItsDates()
    {
        Assert.Equal("sbj_void_amd_1", SeasonsService.DateCancelVoidJobId("amd_1"));
        Assert.Equal(
            new[] { 100, 100, 95, 43 },
            new[] { 1, 20, 21, 50 }.Select(dates => SeasonsService.HolderImportRowsPerCall(dates)));

        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            ((HttpStatusCode)422,
                "{\"error\":\"invalid_rows\",\"code\":\"invalid_rows\","
                + "\"message\":\"At most 43 rows a call for this package.\",\"maxRows\":43}",
                new Dictionary<string, string>()),
            Ok(),
        }, maxRetries: 3);

        var error = await Assert.ThrowsAsync<SeatLayerValidationException>(
            () => client.Seasons.CreateSeasonHolderImportAsync("sea_1", new SeasonHolderImportRequest
            {
                SuccessorPlanActivationId = "spa_1",
                Rows = Array.Empty<SeasonHolderImportRow>(),
            }));

        Assert.Equal(422, error.Status);
        Assert.Equal("invalid_rows", error.Code);
        Assert.Equal(43L, error.Body["maxRows"]);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task CsvExportsReturnRawTextAndPlanDeleteReturnsNothing()
    {
        const string holders = "holder,email\nH-1,fan@example.com\n";
        const string orders = "order,total\nSO-1,12000\n";
        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            (HttpStatusCode.OK, holders, new Dictionary<string, string>()),
            (HttpStatusCode.OK, orders, new Dictionary<string, string>()),
            (HttpStatusCode.NoContent, "", new Dictionary<string, string>()),
        });

        Assert.Equal(holders, await client.Seasons.ExportSeasonHoldersCsvAsync("sea/a"));
        Assert.Equal(orders, await client.Seasons.ExportSeasonOrdersCsvAsync("sea/a"));
        var deletion = client.Seasons.DeleteSeasonPlanAsync("sea/a", "plan/2");
        await deletion;

        Assert.True(deletion.IsCompletedSuccessfully);
        Assert.Equal("text/csv", handler.Calls[0].Headers.Accept.Single().MediaType);
        Assert.Equal(
            "https://api.seatlayer.io/v1/seasons/sea%2Fa/reports/holders.csv",
            handler.Calls[0].Url.AbsoluteUri);
        Assert.Equal(HttpMethod.Delete, handler.Calls[2].Method);
        Assert.False(string.IsNullOrEmpty(
            handler.Calls[2].Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task PublishReplaysWithOneGeneratedKey()
    {
        var (client, handler) = Build(new[] { RateLimited(), Ok() }, maxRetries: 2);

        await client.Seasons.PublishSeasonAsync("sea_1");

        Assert.Equal(2, handler.Calls.Count);
        var keys = handler.Calls.Select(call => call.Headers.GetValues("Idempotency-Key").Single()).ToList();
        Assert.False(string.IsNullOrEmpty(keys[0]));
        Assert.Equal(keys[0], keys[1]);
    }

    [Fact]
    public async Task BulkRefundOfADateReplaysWithOneGeneratedKey()
    {
        var (client, handler) = Build(new[] { RateLimited(), Ok() }, maxRetries: 2);

        await client.Seasons.RefundSeasonPerformanceAsync(
            "sea_1", "ev_1", new SeasonPerformanceRefundRequest { Amount = "per_date" });

        Assert.Equal(2, handler.Calls.Count);
        var keys = handler.Calls.Select(call => call.Headers.GetValues("Idempotency-Key").Single()).ToList();
        Assert.False(string.IsNullOrEmpty(keys[0]));
        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(Canonical("{\"amount\":\"per_date\"}"), Canonical(handler.Calls[1].Body!));
    }

    [Fact]
    public async Task LapsedOfferReleaseReplaysWithOneKeyAndSendsNoBody()
    {
        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            RateLimited(),
            (HttpStatusCode.Accepted,
                "{\"job\":{\"jobId\":\"sbj_1\",\"kind\":\"lapsed_release\",\"state\":\"planning\"}}",
                new Dictionary<string, string>()),
            RateLimited(),
            Ok(),
        }, maxRetries: 2);

        var released = await client.Seasons.ReleaseSeasonLapsedOffersAsync("sea/a");
        await client.Seasons.ReleaseSeasonLapsedOffersAsync("sea_1", "release-lapsed-1");

        Assert.Equal(4, handler.Calls.Count);
        Assert.All(handler.Calls, call => Assert.Equal(HttpMethod.Post, call.Method));
        Assert.All(handler.Calls, call => Assert.Null(call.Body));
        Assert.Equal(
            "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/release-lapsed",
            handler.Calls[0].Url.AbsoluteUri);
        var keys = handler.Calls.Select(call => call.Headers.GetValues("Idempotency-Key").Single()).ToList();
        Assert.False(string.IsNullOrEmpty(keys[0]));
        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(new[] { "release-lapsed-1", "release-lapsed-1" }, keys.Skip(2));
        var job = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(released["job"]);
        Assert.Equal("lapsed_release", job["kind"]);
    }

    [Fact]
    public async Task RenewalOfferListAndHostedRenewalsSendTheirPageQuery()
    {
        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            (HttpStatusCode.OK, "{\"offers\":[],\"truncated\":true,\"nextCursor\":\"c/2\"}",
                new Dictionary<string, string>()),
            Ok(),
            (HttpStatusCode.OK, "{\"offers\":[],\"holdersWithoutEmail\":0,\"nextCursor\":null}",
                new Dictionary<string, string>()),
            Ok(),
        });
        var seasons = client.Seasons;

        var offers = await seasons.ListSeasonRenewalOffersAsync(
            "sea/a", new SeasonRenewalOfferPageRequest { Cursor = "c/1", Limit = 50 });
        await seasons.ListSeasonRenewalOffersAsync("sea/a", new SeasonRenewalOfferPageRequest { Limit = 100 });
        var hosted = await seasons.RetrieveSeasonHostedRenewalsAsync(
            "sea/a", new SeasonRenewalOfferPageRequest { Cursor = "c/1", Limit = 500 });
        await seasons.RetrieveSeasonHostedRenewalsAsync("sea/a", null);

        Assert.Equal(
            new[]
            {
                "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers?cursor=c%2f1&limit=50",
                "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers?limit=100",
                "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-hosted?cursor=c%2f1&limit=500",
                "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-hosted",
            },
            handler.Calls.Select(call => call.Url.AbsoluteUri));
        Assert.All(handler.Calls, call => Assert.Equal(HttpMethod.Get, call.Method));
        Assert.Equal("c/2", offers["nextCursor"]);
        Assert.True(hosted.ContainsKey("nextCursor"));
        Assert.Null(hosted["nextCursor"]);
    }

    [Fact]
    public async Task SingleTicketsReachTheWireOnCreateAndUpdate()
    {
        const string updated = "{\"season\":{\"key\":\"sea_1\",\"singleTickets\":true},\"singleTicketDates\":"
            + "{\"direction\":\"open\",\"opened\":[\"ev_1\"],\"closed\":[],\"notOpened\":[],\"jobId\":null}}";
        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            (HttpStatusCode.Created, "{\"season\":{\"key\":\"sea_1\",\"singleTickets\":true}}",
                new Dictionary<string, string>()),
            (HttpStatusCode.OK, updated, new Dictionary<string, string>()),
            Ok(),
        });
        var seasons = client.Seasons;

        await seasons.CreateSeasonAsync(new SeasonCreateRequest
        {
            Name = "Series", EventKeys = new[] { "ev_1", "ev_2" }, SingleTickets = true,
            IdempotencyKey = "create-1",
        });
        var answer = await seasons.UpdateSeasonAsync("sea/a", new SeasonUpdateRequest
        {
            ExpectedRevision = 5, SingleTickets = false, IdempotencyKey = "update-1",
        });
        await seasons.UpdateSeasonAsync("sea/a", new SeasonUpdateRequest { ExpectedRevision = 6, Name = "Series 2" });

        Assert.Equal(3, handler.Calls.Count);
        Assert.Equal(HttpMethod.Post, handler.Calls[0].Method);
        Assert.Equal(
            Canonical("{\"eventKeys\":[\"ev_1\",\"ev_2\"],\"name\":\"Series\",\"singleTickets\":true}"),
            Canonical(handler.Calls[0].Body!));
        Assert.Equal(HttpMethod.Patch, handler.Calls[1].Method);
        Assert.Equal("https://api.seatlayer.io/v1/seasons/sea%2Fa", handler.Calls[1].Url.AbsoluteUri);
        Assert.Equal(
            Canonical("{\"expectedRevision\":5,\"singleTickets\":false}"), Canonical(handler.Calls[1].Body!));
        Assert.Equal(
            Canonical("{\"expectedRevision\":6,\"name\":\"Series 2\"}"), Canonical(handler.Calls[2].Body!));
        var dates = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(answer["singleTicketDates"]);
        Assert.Equal("open", dates["direction"]);
    }

    [Fact]
    public async Task RenewalNoticesForOneOfferBatchSendItsBatchId()
    {
        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            (HttpStatusCode.OK, "{\"scheduled\":240,\"withoutEmail\":3}", new Dictionary<string, string>()),
        });

        var sent = await client.Seasons.CreateSeasonRenewalNoticesAsync("sea/a", null, "srb/1");

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-notices", call.Url.AbsoluteUri);
        Assert.Equal(Canonical("{\"batchId\":\"srb/1\"}"), Canonical(call.Body!));
        Assert.False(call.Headers.Contains("Idempotency-Key"));
        Assert.Equal(240L, sent["scheduled"]);
    }

    [Fact]
    public async Task RenewalOffersAndBringAcrossAnswerAJobAboveOneHundred()
    {
        const string job = "{\"jobId\":\"sbj_1\",\"kind\":\"renewal_run\",\"state\":\"planning\","
            + "\"counts\":{\"total\":250,\"done\":0,\"skipped\":0,\"failed\":0}}";
        var (client, handler) = Build(new (HttpStatusCode, string, IDictionary<string, string>)[]
        {
            (HttpStatusCode.Accepted, "{\"job\":" + job + ",\"batchId\":\"srb_1\"}",
                new Dictionary<string, string>()),
            (HttpStatusCode.Created, "{\"offers\":[{\"offerId\":\"sro_1\"}]}", new Dictionary<string, string>()),
            (HttpStatusCode.Accepted,
                "{\"job\":{\"jobId\":\"sbj_2\",\"kind\":\"holder_import\",\"state\":\"planning\"}}",
                new Dictionary<string, string>()),
        });
        var seasons = client.Seasons;

        var queued = await seasons.CreateSeasonRenewalOffersAsync("sea_1", new SeasonRenewalOffersCreateRequest
        {
            DeadlineAt = 1_800_000_000_000, IdempotencyKey = "offers-big",
        });
        var made = await seasons.CreateSeasonRenewalOffersAsync("sea_1", new SeasonRenewalOffersCreateRequest
        {
            DeadlineAt = 1_800_000_000_000, IdempotencyKey = "offers-small",
        });
        var imported = await seasons.CreateSeasonHolderImportFromSeasonAsync(
            "sea_1", new SeasonHolderImportFromSeasonRequest
            {
                SourceSeasonId = "sea_0", SuccessorPlanActivationId = "pa_1", IdempotencyKey = "bring-across",
            });

        var renewalRun = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(queued["job"]);
        Assert.Equal("renewal_run", renewalRun["kind"]);
        var counts = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(renewalRun["counts"]);
        Assert.Equal(250L, counts["total"]);
        Assert.Equal("srb_1", queued["batchId"]);
        Assert.False(queued.ContainsKey("offers"));
        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(made["offers"]));
        Assert.False(made.ContainsKey("job"));
        var holderImport = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(imported["job"]);
        Assert.Equal("holder_import", holderImport["kind"]);
        Assert.Equal(
            new[] { "offers-big", "offers-small", "bring-across" },
            handler.Calls.Select(call => call.Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task RenewalCheckoutReplaysWithOneGeneratedKey()
    {
        var (client, handler) = Build(new[] { RateLimited(), Ok() }, maxRetries: 2);

        await client.Seasons.CreateSeasonRenewalCheckoutAsync(
            "sea_1", "offer_1", new SeasonRenewalCheckoutRequest { ReturnUrl = "https://tickets.example/r" });

        Assert.Equal(2, handler.Calls.Count);
        var keys = handler.Calls.Select(call => call.Headers.GetValues("Idempotency-Key").Single()).ToList();
        Assert.False(string.IsNullOrEmpty(keys[0]));
        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(
            Canonical("{\"returnUrl\":\"https://tickets.example/r\"}"), Canonical(handler.Calls[1].Body!));
    }

    [Fact]
    public async Task RenewalIntentAndDeclineAreSingleAttemptAndDeclineSendsTheReason()
    {
        var (client, handler) = Build(new[] { RateLimited(), Ok(), Ok() }, maxRetries: 3);

        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Seasons.CreateSeasonRenewalIntentAsync("sea_1", "offer_1"));
        await client.Seasons.DeclineSeasonRenewalOfferAsync("sea/a", "offer/1", "Moving away");
        await client.Seasons.DeclineSeasonRenewalOfferAsync("sea_1", "offer_1");

        Assert.Equal(3, handler.Calls.Count);
        Assert.All(handler.Calls, call => Assert.False(call.Headers.Contains("Idempotency-Key")));
        Assert.Null(handler.Calls[0].Body);
        Assert.Equal(
            "https://api.seatlayer.io/v1/seasons/sea%2Fa/renewal-offers/offer%2F1/decline",
            handler.Calls[1].Url.AbsoluteUri);
        Assert.Equal("{\"reason\":\"Moving away\"}", handler.Calls[1].Body);
        Assert.Equal("{}", handler.Calls[2].Body);
    }

    [Fact]
    public async Task PriceTypesSaleSettingsHolderContactsAndDateCancelsReachTheWire()
    {
        var (client, handler) = Build(Enumerable.Range(0, 6).Select(_ => Ok()));
        var seasons = client.Seasons;

        await seasons.ReplaceSeasonPackagePricesAsync("sea_1", "plan_1", new SeasonPackagePricesRequest
        {
            Prices = new[]
            {
                new SeasonPackagePrice { CategoryKey = "stalls", UnitPrice = 18000 },
                new SeasonPackagePrice { CategoryKey = "stalls", PriceTypeKey = "child", UnitPrice = 9000 },
            },
        });
        await seasons.UpdateSeasonAsync("sea_1", new SeasonUpdateRequest
        {
            ExpectedRevision = 3, CheckoutMode = "seatlayer", Audience = "public",
            TransferCutoffHours = 24, ReturnCutoffHours = null,
        });
        await seasons.UpdateSeasonAsync("sea_1", new SeasonUpdateRequest { ExpectedRevision = 4, Name = "Renamed" });
        await seasons.CreateSeasonHolderImportAsync("sea_1", new SeasonHolderImportRequest
        {
            SuccessorPlanActivationId = "pa_1",
            Rows = new[]
            {
                new SeasonHolderImportRow
                {
                    RowId = "r1", HolderRef = "h1", PriorPlanActivationId = "pa_0", PriorContractRef = "c1",
                    Labels = new[] { "A-1" }, ExistingBookingRef = "b1",
                    HolderEmail = "ana@example.com", HolderName = "Ana Silva",
                },
            },
        });
        await seasons.CreateSeasonAmendmentAsync("sea_1", new SeasonAmendmentCreateRequest
        {
            EventKey = "ev_night_3", Kind = "cancel", Refund = "per_date", Reason = "Storm",
            IdempotencyKey = "night-3-storm",
        });
        await seasons.CreateSeasonAmendmentAsync(
            "sea_1", new SeasonAmendmentCreateRequest { EventKey = "ev_1", Kind = "postpone" });

        var bodies = new[]
        {
            "{\"prices\":[{\"categoryKey\":\"stalls\",\"unitPrice\":18000},"
                + "{\"categoryKey\":\"stalls\",\"priceTypeKey\":\"child\",\"unitPrice\":9000}]}",
            "{\"expectedRevision\":3,\"checkoutMode\":\"seatlayer\",\"audience\":\"public\","
                + "\"transferCutoffHours\":24,\"returnCutoffHours\":null}",
            "{\"expectedRevision\":4,\"name\":\"Renamed\"}",
            "{\"successorPlanActivationId\":\"pa_1\",\"rows\":[{\"rowId\":\"r1\",\"holderRef\":\"h1\","
                + "\"priorPlanActivationId\":\"pa_0\",\"priorContractRef\":\"c1\",\"labels\":[\"A-1\"],"
                + "\"existingBookingRef\":\"b1\",\"holderEmail\":\"ana@example.com\",\"holderName\":\"Ana Silva\"}]}",
            "{\"eventKey\":\"ev_night_3\",\"kind\":\"cancel\",\"refund\":\"per_date\",\"reason\":\"Storm\"}",
            "{\"eventKey\":\"ev_1\",\"kind\":\"postpone\"}",
        };
        Assert.Equal(bodies.Length, handler.Calls.Count);
        for (var index = 0; index < bodies.Length; index++)
        {
            Assert.Equal(Canonical(bodies[index]), Canonical(handler.Calls[index].Body!));
        }

        Assert.Equal("night-3-storm", handler.Calls[4].Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task UnsupportedSeasonMutationsAreNeverRetried()
    {
        var (client, handler) = Build(
            new[] { RateLimited(), RateLimited(), RateLimited() }, maxRetries: 3);

        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Seasons.RotateSeasonRenewalOfferLinkAsync("sea_1", "offer_1"));
        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Seasons.ResendSeasonOrderTicketsAsync("sea_1", "ord_1"));
        await Assert.ThrowsAsync<SeatLayerRateLimitException>(
            () => client.Seasons.ResendSeasonRenewalOfferAsync("sea_1", "offer_1"));

        Assert.Equal(3, handler.Calls.Count);
        Assert.All(handler.Calls, call => Assert.False(call.Headers.Contains("Idempotency-Key")));
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
