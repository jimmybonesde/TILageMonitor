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
}
