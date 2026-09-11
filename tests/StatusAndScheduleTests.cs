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
    [InlineData(10, 1, 300)]
    [InlineData(10, 5, 60)]
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
}
