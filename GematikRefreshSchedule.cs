namespace TILageMonitor;

public static class GematikRefreshSchedule
{
    public static TimeSpan GetDelayUntilNextSlot(DateTime nowLocal)
    {
        var next = new DateTime(
            nowLocal.Year, nowLocal.Month, nowLocal.Day,
            nowLocal.Hour, nowLocal.Minute, 0, nowLocal.Kind);

        var minuteModulo = next.Minute % 5;
        if (minuteModulo == 1)
        {
            if (nowLocal > next)
                next = next.AddMinutes(5);
        }
        else
        {
            next = next.AddMinutes((1 - minuteModulo + 5) % 5);
        }

        var delay = next - nowLocal;
        return delay < TimeSpan.FromSeconds(5)
            ? TimeSpan.FromSeconds(5)
            : delay;
    }
}
