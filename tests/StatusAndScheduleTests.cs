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

        var eRezept = Assert.Single(rows.Where(row => row.ServiceKey == "erezept"));
        var today = Assert.Single(eRezept.Days.Where(day => day.Date == DateTime.Today));

        Assert.Equal("partial", today.Hours[10].Status);
        Assert.Null(today.Hours[11].Status);
    }
}
