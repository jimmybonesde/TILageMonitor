using Xunit;

namespace TILageMonitor.Tests;

public sealed class StatusAndScheduleTests
{
    [Theory]
    [InlineData("none", false, false, "none")]
    [InlineData("partial", false, false, "partial")]
    [InlineData("full", false, false, "full")]
    [InlineData("none", true, false, "maintenance")]
    [InlineData("none", false, true, "maintenance")]
    public void Classify_returns_expected_service_status(
        string outage,
        bool maintenance,
        bool subComponentMaintenance,
        string expected)
    {
        var status = new AppStatus
        {
            Outage = outage,
            HasMaintenance = maintenance,
            HasSubComponentMaintenance = subComponentMaintenance
        };

        Assert.Equal(expected, TiStatusClassifier.Classify(status));
    }

    [Fact]
    public void ClassifyOverall_prioritizes_full_outage()
    {
        var result = TiStatusClassifier.ClassifyOverall(
        [
            new AppStatus { Outage = "partial" },
            new AppStatus { Outage = "full" },
            new AppStatus { HasMaintenance = true }
        ]);

        Assert.Equal(TiOverallStatus.Full, result);
    }

    [Theory]
    [InlineData(10, 0, 60)]
    [InlineData(11, 0, 300)]
    [InlineData(10, 5, 55)]
    public void Refresh_schedule_targets_next_gematik_slot(
        int minute,
        int second,
        int expectedSeconds)
    {
        var now = new DateTime(2026, 9, 11, 10, minute, second, DateTimeKind.Local);

        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            GematikRefreshSchedule.GetDelayUntilNextSlot(now));
    }
    [Theory]
    [InlineData("https://github.com/jimmybonesde/TILageMonitor/releases/tag/v2.0.7", false)]
    [InlineData("https://github.com/jimmybonesde/TILageMonitor/releases/download/v2.0.7/TILageMonitor-2.0.7-Setup.exe", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset-2e65be/123/TILageMonitor-2.0.7-Setup.exe", true)]
    [InlineData("http://github.com/jimmybonesde/TILageMonitor/releases/download/v2.0.7/TILageMonitor-2.0.7-Setup.exe", false)]
    [InlineData("https://evil.example/TILageMonitor-2.0.7-Setup.exe", false)]
    public void Update_service_accepts_only_direct_setup_assets(string url, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsSetupDownloadUrl(url));
    }

    [Theory]
    [InlineData("https://github.com/jimmybonesde/TILageMonitor/releases/download/v2.0.7/checksums.txt", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset-2e65be/123/checksums.txt", true)]
    [InlineData("https://evil.example/checksums.txt", false)]
    [InlineData("http://github.com/checksums.txt", false)]
    public void Update_service_allows_only_https_github_checksum_hosts(string url, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsAllowedUpdateUrl(url));
    }

    [Fact]
    public void Active_incident_tracker_seeds_without_toast_then_toasts_new_and_prunes()
    {
        var known = new HashSet<string>(StringComparer.Ordinal);

        var first = ActiveIncidentTracker.Sync(known, ["1", "2"], seedWithoutToast: true);
        Assert.Empty(first);
        Assert.Equal(new HashSet<string> { "1", "2" }, known);

        var second = ActiveIncidentTracker.Sync(known, ["2", "3"], seedWithoutToast: false);
        Assert.Equal(new[] { "3" }, second);
        Assert.Equal(new HashSet<string> { "2", "3" }, known);

        // Resolved IDs are pruned so a later reopen can toast again.
        var third = ActiveIncidentTracker.Sync(known, ["1"], seedWithoutToast: false);
        Assert.Equal(new[] { "1" }, third);
        Assert.Equal(new HashSet<string> { "1" }, known);
    }

    [Fact]
    public void Incident_seed_gate_survives_first_api_fail_without_cache_then_toasts_only_new()
    {
        // Mirrors MainWindow: _incidentSeedDone stays false when Refresh catch runs
        // without Render (no cache). Next successful Sync must still seedWithoutToast.
        var known = new HashSet<string>(StringComparer.Ordinal);
        var incidentSeedDone = false;

        // First API fail path: no Sync / no seed — only _firstLoad would flip in catch.
        // Seed readiness must remain false so recover does not toast pre-existing IDs.

        var afterRecover = ActiveIncidentTracker.Sync(
            known,
            ["10", "20"],
            seedWithoutToast: !incidentSeedDone);
        Assert.Empty(afterRecover);
        Assert.Equal(new HashSet<string> { "10", "20" }, known);
        incidentSeedDone = true;

        var sameSet = ActiveIncidentTracker.Sync(
            known,
            ["10", "20"],
            seedWithoutToast: !incidentSeedDone);
        Assert.Empty(sameSet);

        var withNew = ActiveIncidentTracker.Sync(
            known,
            ["10", "20", "30"],
            seedWithoutToast: !incidentSeedDone);
        Assert.Equal(new[] { "30" }, withNew);
        Assert.Equal(new HashSet<string> { "10", "20", "30" }, known);
    }

    [Fact]
    public void Automatic_update_retry_waits_for_same_failed_release()
    {
        var now = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);
        var retryAfter = AutoUpdateRetryPolicy.GetNextRetryUtc(now);

        Assert.False(AutoUpdateRetryPolicy.ShouldAttempt("2.0.7", "2.0.7", retryAfter, now.AddHours(1)));
        Assert.True(AutoUpdateRetryPolicy.ShouldAttempt("2.0.8", "2.0.7", retryAfter, now.AddHours(1)));
        Assert.True(AutoUpdateRetryPolicy.ShouldAttempt("2.0.7", "2.0.7", retryAfter, retryAfter));
    }

    [Fact]
    public void Automatic_update_installation_is_opt_in_by_default()
    {
        Assert.False(new AppSettings().AutoInstallUpdates);
    }

    [Fact]
    public void Silent_update_helper_waits_and_reopens_app_when_installer_fails()
    {
        var script = UpdateInstallerLauncher.BuildScript(
            4242,
            @"C:\Temp\TILageMonitor-2.0.9-Setup.exe",
            @"C:\Users\Randy\AppData\Local\Programs\TILageMonitor\TILageMonitor.exe",
            @"C:\Users\Randy\AppData\Roaming\TILageMonitor\update-install.log",
            @"C:\Temp\TILageMonitor-Update-Backup-test",
            "2.0.15");

        Assert.Contains("start \"\" /wait", script, StringComparison.Ordinal);
        Assert.Contains("/VERYSILENT", script, StringComparison.Ordinal);
        Assert.Contains("/LOG=", script, StringComparison.Ordinal);
        Assert.Contains("--update-failed", script, StringComparison.Ordinal);
        Assert.Contains("robocopy", script, StringComparison.Ordinal);
        Assert.Contains("--failed-update-version", script, StringComparison.Ordinal);
    }


    [Fact]
    public void Unsigned_update_artifact_uses_checksum_fallback()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tilage-monitor-test-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        try
        {
            var result = AuthenticodeVerifier.Check(path);
            Assert.True(result.IsValid);
            Assert.False(result.IsSigned);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Installer_download_limit_is_bounded()
    {
        Assert.Equal(500L * 1024 * 1024, UpdateService.MaxInstallerBytes);
    }

    [Fact]
    public void History_missing_service_in_partial_api_response_stays_unknown()
    {
        var hour = new HistoryHourSnapshot();
        var lage = new LageV2
        {
            AppStatus = new Dictionary<string, AppStatus>(StringComparer.OrdinalIgnoreCase)
            {
                ["erezept"] = new AppStatus { Outage = "none" }
            }
        };

        HistoryStore.ApplySnapshot(hour, lage);

        Assert.Equal("none", hour.Services["erezept"]);
        Assert.False(hour.Services.ContainsKey("epa"));
    }

    [Fact]
    public void History_hour_keys_keep_both_dst_fallback_hours_distinct()
    {
        var summerHour = new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.FromHours(2));
        var winterHour = new DateTimeOffset(2026, 10, 25, 2, 0, 0, TimeSpan.FromHours(1));

        Assert.NotEqual(HistoryStore.GetHourKey(summerHour), HistoryStore.GetHourKey(winterHour));
    }

    [Fact]
    public void History_uses_incident_timeline_when_no_local_snapshot_exists()
    {
        var now = DateTime.Now;
        var incident = new Incident
        {
            App = ["E-Rezept"],
            Steps =
            [
                new IncidentStep
                {
                    Status = 4,
                    Timestamp = now.Date.AddHours(10).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 3,
                    Timestamp = now.Date.AddHours(11).ToUniversalTime()
                }
            ]
        };

        var rows = HistoryStore.BuildRows(
            new HistoryFile(),
            new IncidentResponse { Data = [incident] });

        var eRezept = Assert.Single(rows, row => row.ServiceKey == "erezept");
        var today = Assert.Single(eRezept.Days, day => day.Date == DateTime.Today);

        Assert.Equal("partial", today.Hours[10].Status);
        Assert.Null(today.Hours[11].Status);
    }

    [Fact]
    public void History_marks_unaffected_hours_ok_when_incident_api_is_available()
    {
        var rows = HistoryStore.BuildRows(
            new HistoryFile(),
            new IncidentResponse { Success = true });

        var eRezept = Assert.Single(rows, row => row.ServiceKey == "erezept");
        var today = Assert.Single(eRezept.Days, day => day.Date == DateTime.Today);
        var now = DateTime.Now;

        var pastOrCurrent = today.Hours.Where(h => today.Date.AddHours(h.Hour) <= now).ToList();
        var future = today.Hours.Where(h => today.Date.AddHours(h.Hour) > now).ToList();

        Assert.NotEmpty(pastOrCurrent);
        Assert.All(pastOrCurrent, hour => Assert.Equal("none", hour.Status));
        Assert.All(future, hour => Assert.Null(hour.Status));
        // Sanity: not all 24 hours are green-filled as none
        Assert.False(today.Hours.All(h => h.Status == "none"));
    }

    [Fact]
    public void History_incident_status1_beats_maintenance_flag()
    {
        var now = DateTime.Now;
        var hour = Math.Clamp(now.Hour - 2, 0, 21);
        var incident = new Incident
        {
            App = ["E-Rezept"],
            Steps =
            [
                new IncidentStep
                {
                    Status = 1,
                    HasMaintenance = true,
                    Timestamp = now.Date.AddHours(hour).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 3,
                    Timestamp = now.Date.AddHours(hour + 1).ToUniversalTime()
                }
            ]
        };

        var rows = HistoryStore.BuildRows(
            new HistoryFile(),
            new IncidentResponse { Data = [incident] });

        var eRezept = Assert.Single(rows, row => row.ServiceKey == "erezept");
        var today = Assert.Single(eRezept.Days, day => day.Date == DateTime.Today);
        Assert.Equal("full", today.Hours[hour].Status);
    }

    [Fact]
    public void History_carries_severity_across_status_2_update_steps()
    {
        var now = DateTime.Now;
        var hour = Math.Clamp(now.Hour - 3, 0, 20);
        var incident = new Incident
        {
            App = ["E-Rezept"],
            Steps =
            [
                new IncidentStep
                {
                    Status = 4,
                    Timestamp = now.Date.AddHours(hour).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 2,
                    Timestamp = now.Date.AddHours(hour + 1).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 3,
                    Timestamp = now.Date.AddHours(hour + 2).ToUniversalTime()
                }
            ]
        };

        var rows = HistoryStore.BuildRows(
            new HistoryFile(),
            new IncidentResponse { Data = [incident] });

        var eRezept = Assert.Single(rows, row => row.ServiceKey == "erezept");
        var today = Assert.Single(eRezept.Days, day => day.Date == DateTime.Today);
        Assert.Equal("partial", today.Hours[hour].Status);
        Assert.Equal("partial", today.Hours[hour + 1].Status);
        Assert.Null(today.Hours[hour + 2].Status);
    }

    [Fact]
    public void SoftFail_resolve_keeps_last_good_when_fresh_is_null()
    {
        var last = new IncidentResponse
        {
            Success = true,
            Data = [new Incident { Id = 42, Title = "keep-me" }]
        };

        var resolved = CacheStore.ResolveIncidents(null, last);
        Assert.Same(last, resolved);
        Assert.Equal(42, Assert.Single(resolved.Data).Id);

        var outagesLast = new OutageResponse
        {
            Success = true,
            Data = [new Outage { Service = "eRezept" }]
        };
        var outages = CacheStore.ResolveOutages(null, outagesLast);
        Assert.Same(outagesLast, outages);

        var empty = CacheStore.ResolveIncidents(null, null);
        Assert.NotNull(empty);
        Assert.Empty(empty.Data);
    }

    [Fact]
    public void SoftFail_resolve_keeps_last_good_when_fresh_is_empty_failure_shell()
    {
        var last = new IncidentResponse
        {
            Success = true,
            Data = [new Incident { Id = 7, Title = "keep-me" }]
        };
        var fresh = new IncidentResponse { Success = false, Data = [] };

        var resolved = CacheStore.ResolveIncidents(fresh, last);
        Assert.Same(last, resolved);

        var outagesLast = new OutageResponse
        {
            Success = true,
            Data = [new Outage { Service = "eRezept" }]
        };
        var outagesFresh = new OutageResponse { Success = false, Data = [] };
        Assert.Same(outagesLast, CacheStore.ResolveOutages(outagesFresh, outagesLast));
    }

    [Theory]
    [InlineData("E-Rezept", "erezept")]
    [InlineData("eRezept", "erezept")]
    [InlineData("ÖGD", "ogd")]
    [InlineData("OGD", "ogd")]
    [InlineData("TI-Anschluss", "tianschluss")]
    [InlineData("tianschluss", "tianschluss")]
    public void TryMapServiceKey_maps_common_api_labels(string label, string expectedKey)
    {
        Assert.Equal(expectedKey, HistoryStore.TryMapServiceKey(label));
    }

    [Fact]
    public void Timeline_merges_adjacent_same_kind_after_status2_carry()
    {
        var now = DateTime.Now;
        var hour = Math.Clamp(now.Hour - 4, 0, 19);
        var incident = new Incident
        {
            App = ["E-Rezept"],
            Steps =
            [
                new IncidentStep
                {
                    Status = 4,
                    Timestamp = now.Date.AddHours(hour).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 2,
                    Timestamp = now.Date.AddHours(hour + 1).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 2,
                    Timestamp = now.Date.AddHours(hour + 2).ToUniversalTime()
                },
                new IncidentStep
                {
                    Status = 3,
                    Timestamp = now.Date.AddHours(hour + 3).ToUniversalTime()
                }
            ]
        };

        var events = HistoryTimelineBuilder.Build(
            new IncidentResponse { Data = [incident] },
            new OutageResponse(),
            serviceFilterKey: "erezept",
            maxEvents: 40);

        var partial = events.Where(e => e.KindLabel == "Teilausfall").ToList();
        var merged = Assert.Single(partial);
        Assert.Equal(now.Date.AddHours(hour), merged.StartLocal);
        Assert.Equal(now.Date.AddHours(hour + 3), merged.EndLocal);
    }

    [Fact]
    public void Incident_seed_stays_open_on_soft_fail_empty_then_seeds_real_ids_without_toast_storm()
    {
        // Mirrors MainWindow: soft-fail/null/empty ResolveIncidents must NOT flip
        // _incidentSeedDone; a later trusted active set seeds silently (no toast storm).
        var known = new HashSet<string>(StringComparer.Ordinal);
        var incidentSeedDone = false;

        IncidentResponse? incidentsFresh = null; // soft-fail
        var resolved = CacheStore.ResolveIncidents(incidentsFresh, lastGood: null);
        Assert.True(CacheStore.IsEmptyFailure(resolved));
        Assert.False(CacheStore.IsTrustedIncidents(incidentsFresh));

        var activeIds = resolved.Data
            .Where(x => x.Status is 1 or 4)
            .Select(x => x.Id.ToString());

        var afterSoftFail = ActiveIncidentTracker.Sync(
            known,
            activeIds,
            seedWithoutToast: !incidentSeedDone);
        Assert.Empty(afterSoftFail);
        Assert.Empty(known);
        // Soft-fail empty → do NOT mark seed done
        if (CacheStore.IsTrustedIncidents(incidentsFresh))
            incidentSeedDone = true;
        Assert.False(incidentSeedDone);

        // Later trusted response with real active IDs
        incidentsFresh = new IncidentResponse
        {
            Success = true,
            Data =
            [
                new Incident { Id = 101, Status = 1, Title = "A" },
                new Incident { Id = 102, Status = 4, Title = "B" }
            ]
        };
        Assert.True(CacheStore.IsTrustedIncidents(incidentsFresh));
        resolved = CacheStore.ResolveIncidents(incidentsFresh, lastGood: null);
        activeIds = resolved.Data
            .Where(x => x.Status is 1 or 4)
            .Select(x => x.Id.ToString());

        var afterTrusted = ActiveIncidentTracker.Sync(
            known,
            activeIds,
            seedWithoutToast: !incidentSeedDone);
        Assert.Empty(afterTrusted); // seeded, not toasted
        Assert.Equal(new HashSet<string> { "101", "102" }, known);
        incidentSeedDone = true;

        // A brand-new ID after seed should toast
        var withNew = ActiveIncidentTracker.Sync(
            known,
            ["101", "102", "103"],
            seedWithoutToast: !incidentSeedDone);
        Assert.Equal(new[] { "103" }, withNew);
    }

    [Fact]
    public void App_mutex_names_prefer_global_namespace()
    {
        Assert.Equal(@"Global\TILageMonitor_SingleInstance", App.PreferredMutexName);
        Assert.Equal(@"Local\TILageMonitor_SingleInstance", App.FallbackMutexName);
        Assert.StartsWith(@"Global\", App.PreferredShowWindowEventName);
        Assert.Equal(@"Global\TILageMonitor_ShowWindow", App.PreferredShowWindowEventName);
        Assert.Equal(@"Local\TILageMonitor_ShowWindow", App.FallbackShowWindowEventName);
    }

    [Fact]
    public void App_sync_namespace_choice_couples_mutex_and_event()
    {
        var ns = App.ChooseSyncObjectNamespace();
        Assert.True(ns is @"Global\" or @"Local\");

        var mutexName = ns + "TILageMonitor_SingleInstance";
        var eventName = ns + "TILageMonitor_ShowWindow";
        Assert.StartsWith(ns, mutexName);
        Assert.StartsWith(ns, eventName);

        // Preferred constants stay Global-primary for Inno AppMutex alignment.
        Assert.StartsWith(@"Global\", App.PreferredMutexName);
        Assert.StartsWith(@"Global\", App.PreferredShowWindowEventName);
    }

    [Fact]
    public void History_day_label_uses_compact_culture_form()
    {
        LocalizationService.Configure("de");
        var deGroup = new HistoryDayGroup(
            new DateTime(2026, 9, 15),
            [HistoryDayCell.From(new DateTime(2026, 9, 15), 10, "none")]);
        Assert.Equal("15.09", deGroup.DateLabel);

        LocalizationService.Configure("en");
        var enGroup = new HistoryDayGroup(
            new DateTime(2026, 9, 15),
            [HistoryDayCell.From(new DateTime(2026, 9, 15), 10, "none")]);
        Assert.Equal("9/15", enGroup.DateLabel);

        // Hour cell tooltip composed from fragments (not whole-string Translate)
        Assert.Contains("no data point", HistoryDayCell.From(new DateTime(2026, 9, 15), 8, null).Tooltip);

        LocalizationService.Configure("system");
    }

    [Fact]
    public void Local_availability_coverage_rejects_thin_sample()
    {
        Assert.False(HistoryStore.IsLocalAvailabilityCoverageSufficient(0, "erezept"));
        Assert.False(HistoryStore.IsLocalAvailabilityCoverageSufficient(2, "erezept"));
        Assert.False(HistoryStore.IsLocalAvailabilityCoverageSufficient(48, "erezept"));

        var expected = HistoryStore.ExpectedHoursInWindow;
        var barelyEnough = (int)Math.Ceiling(expected * HistoryStore.MinLocalAvailabilityCoverageRatio);
        Assert.True(HistoryStore.IsLocalAvailabilityCoverageSufficient(barelyEnough, "erezept"));
        Assert.False(HistoryStore.IsLocalAvailabilityCoverageSufficient(barelyEnough - 1, "erezept"));

        // Alle: threshold scales by service count
        var allExpected = expected * AppSettings.ServiceKeys.Length;
        var allBarely = (int)Math.Ceiling(allExpected * HistoryStore.MinLocalAvailabilityCoverageRatio);
        Assert.True(HistoryStore.IsLocalAvailabilityCoverageSufficient(allBarely, null));
        Assert.False(HistoryStore.IsLocalAvailabilityCoverageSufficient(allBarely - 1, null));
    }

    [Fact]
    public void DayTooltip_uses_wall_tile_count_consistent_with_hours_list()
    {
        var date = new DateTime(2026, 3, 29); // EU spring-forward candidate
        var hours = Enumerable.Range(0, 24)
            .Select(h => HistoryDayCell.From(date, h, "none"))
            .ToList();
        var day = new HistoryDayGroup(date, hours);

        Assert.Equal(24, day.Hours.Count);
        Assert.Contains("24/24", day.DayTooltip, StringComparison.Ordinal);
        // When physical hours differ (DST), tooltip must not mix wall known vs physical total.
        if (day.PhysicalHourCount != 24)
            Assert.DoesNotContain($"24/{day.PhysicalHourCount}", day.DayTooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void Hour_cell_cursor_hand_only_when_navigable()
    {
        var ok = HistoryDayCell.From(DateTime.Today, 10, "none");
        var unknown = HistoryDayCell.From(DateTime.Today, 11, null);
        var partial = HistoryDayCell.From(DateTime.Today, 12, "partial");
        var full = HistoryDayCell.From(DateTime.Today, 13, "full");
        var maint = HistoryDayCell.From(DateTime.Today, 14, "maintenance");

        Assert.False(ok.IsNavigable);
        Assert.False(unknown.IsNavigable);
        Assert.True(partial.IsNavigable);
        Assert.True(full.IsNavigable);
        Assert.True(maint.IsNavigable);
        Assert.Equal(System.Windows.Input.Cursors.Arrow, ok.CellCursor);
        Assert.Equal(System.Windows.Input.Cursors.Hand, partial.CellCursor);
    }

}
