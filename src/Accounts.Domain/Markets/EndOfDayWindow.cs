namespace Atlas.Accounts.Domain.Markets;

/// <summary>
/// Core banking is closed for end-of-day processing from 22:00 to 06:00 in each market's own time (CBS §5). Calls
/// in the window are refused, so none are made: work due in it waits for the market to reopen.
/// </summary>
public static class EndOfDayWindow
{
    private static readonly TimeOnly Starts = new(22, 0);
    private static readonly TimeOnly Ends = new(6, 0);

    public static bool IsClosed(DateTimeOffset now, TimeZoneInfo market) =>
        TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTime(now, market).TimeOfDay) is var local
        && (local >= Starts || local < Ends);

    /// <summary>When the market next reopens: 06:00 market time, today or tomorrow. <paramref name="now"/> if open.</summary>
    public static DateTimeOffset NextOpening(DateTimeOffset now, TimeZoneInfo market)
    {
        if (!IsClosed(now, market))
        {
            return now;
        }

        var local = TimeZoneInfo.ConvertTime(now, market);
        var day = local.TimeOfDay < Ends.ToTimeSpan() ? local.Date : local.Date.AddDays(1);
        var opening = day + Ends.ToTimeSpan();

        // 06:00 always exists and is unambiguous: daylight saving changes happen in the small hours, before 06:00.
        return new DateTimeOffset(opening, market.GetUtcOffset(opening));
    }
}
