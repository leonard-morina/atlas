using Atlas.Accounts.Domain.Markets;

namespace Atlas.Accounts.UnitTests;

/// <summary>CBS §5: closed 22:00 to 06:00 in the market's own time.</summary>
[TestClass]
public sealed class EndOfDayWindowTests
{
    private static readonly TimeZoneInfo Belgrade = TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    [TestMethod]
    [DataRow("2026-10-04T05:59:00+02:00", true)]
    [DataRow("2026-10-04T06:00:00+02:00", false)]
    [DataRow("2026-10-04T21:59:00+02:00", false)]
    [DataRow("2026-10-04T22:00:00+02:00", true)]
    public void The_window_is_in_market_time(string at, bool closed) =>
        Assert.AreEqual(closed, EndOfDayWindow.IsClosed(DateTimeOffset.Parse(at), Belgrade));

    [TestMethod]
    public void The_same_moment_can_be_open_in_one_market_and_closed_in_another()
    {
        var at = DateTimeOffset.Parse("2026-10-04T19:30:00Z"); // 21:30 in Belgrade, 22:30 in Istanbul

        Assert.IsFalse(EndOfDayWindow.IsClosed(at, Belgrade));
        Assert.IsTrue(EndOfDayWindow.IsClosed(at, Istanbul));
    }

    [TestMethod]
    public void Before_midnight_the_market_reopens_the_next_morning() =>
        Assert.AreEqual(
            DateTimeOffset.Parse("2026-10-05T06:00:00+02:00"),
            EndOfDayWindow.NextOpening(DateTimeOffset.Parse("2026-10-04T23:15:00+02:00"), Belgrade));

    [TestMethod]
    public void After_midnight_the_market_reopens_the_same_morning() =>
        Assert.AreEqual(
            DateTimeOffset.Parse("2026-10-05T06:00:00+02:00"),
            EndOfDayWindow.NextOpening(DateTimeOffset.Parse("2026-10-05T01:15:00+02:00"), Belgrade));

    [TestMethod]
    public void Reopening_follows_the_daylight_saving_change() =>
        // Clocks go back on 25 October 2026: the window opens at UTC+2 and closes at UTC+1.
        Assert.AreEqual(
            DateTimeOffset.Parse("2026-10-25T06:00:00+01:00"),
            EndOfDayWindow.NextOpening(DateTimeOffset.Parse("2026-10-24T22:30:00+02:00"), Belgrade));

    [TestMethod]
    public void An_open_market_is_open_now()
    {
        var now = DateTimeOffset.Parse("2026-10-04T12:00:00+02:00");

        Assert.AreEqual(now, EndOfDayWindow.NextOpening(now, Belgrade));
    }
}
