using Afterpelago.Services;

namespace Afterpelago.Statistics;

/// <summary>Shared arithmetic so every statistic measures time and days the same way.</summary>
internal static class StatMath
{
    public static double Hours(IEnumerable<PlayEpisode> episodes) => episodes.Sum(e => (e.End - e.Start).TotalHours);

    public static DateOnly SourceDay(DateTimeOffset utc, int offsetMinutes) => DateOnly.FromDateTime(utc.UtcDateTime.AddMinutes(offsetMinutes));

    public static StatValue CheckValue(CheckInfo check) => new()
    {
        SlotId = check.SenderSlotId, OtherSlotId = check.ReceiverSlotId, ItemReceiverSlotId = check.ReceiverSlotId,
        Item = check.Item, Location = check.Location, Timestamp = check.Timestamp,
    };

    public const string NoPlaytime = "No gameplay connections were recorded, so time-based rates cannot be calculated.";
}

public sealed class NaturalChecksStat() : SessionStat("naturalChecks")
{
    public override StatResult Calculate(SessionStatsContext c) => Ok(new() { Number = c.NaturalChecks.Count, Unit = "checks" });
}

public sealed class AutoResolvedChecksStat() : SessionStat("autoResolvedChecks")
{
    public override StatResult Calculate(SessionStatsContext c) =>
        Ok(new() { Number = c.Checks.Count(x => x.Resolution != CheckResolution.Natural), Unit = "checks" });
}

public sealed class FirstCheckStat() : SessionStat("firstCheck")
{
    public override StatResult Calculate(SessionStatsContext c) =>
        c.NaturalChecks.Count == 0 ? Missing("No checks were found through gameplay.") : Ok(StatMath.CheckValue(c.NaturalChecks[0]));
}

public sealed class LastCheckStat() : SessionStat("lastCheck")
{
    public override StatResult Calculate(SessionStatsContext c) =>
        c.NaturalChecks.Count == 0 ? Missing("No checks were found through gameplay.") : Ok(StatMath.CheckValue(c.NaturalChecks[^1]));
}

public sealed class FirstClearStat() : SessionStat("firstClear")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        var cleared = c.Slots.Where(s => s.GoalCompletedUtc is not null).OrderBy(s => s.GoalCompletedUtc).ThenBy(s => s.Id).FirstOrDefault();
        return cleared is null ? Missing("No slot completed its goal.") : Ok(new() { SlotId = cleared.Id, Timestamp = cleared.GoalCompletedUtc });
    }
}

public sealed class LastClearStat() : SessionStat("lastClear")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        var cleared = c.Slots.Where(s => s.GoalCompletedUtc is not null).OrderByDescending(s => s.GoalCompletedUtc).ThenByDescending(s => s.Id).FirstOrDefault();
        return cleared is null ? Missing("No slot completed its goal.") : Ok(new() { SlotId = cleared.Id, Timestamp = cleared.GoalCompletedUtc });
    }
}

/// <summary>Natural checks per hour that at least one player was connected; offline gaps do not count against the rate.</summary>
public sealed class ChecksPerHourStat() : SessionStat("checksPerHour")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        var hours = StatMath.Hours(c.Episodes);
        return hours <= 0 ? Missing(StatMath.NoPlaytime) : Ok(new() { Number = c.NaturalChecks.Count / hours, Unit = "checks/hour" });
    }
}

/// <summary>Natural checks divided by the source-local calendar days from the first to the last natural check, inclusive.</summary>
public sealed class ChecksPerDayStat() : SessionStat("checksPerDay")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        if (c.NaturalChecks.Count == 0) return Missing("No checks were found through gameplay.");
        var first = StatMath.SourceDay(c.NaturalChecks[0].Timestamp, c.SourceUtcOffsetMinutes);
        var last = StatMath.SourceDay(c.NaturalChecks[^1].Timestamp, c.SourceUtcOffsetMinutes);
        var days = last.DayNumber - first.DayNumber + 1;
        return Ok(new() { Number = (double)c.NaturalChecks.Count / days, Unit = "checks/day" });
    }
}

public sealed class BusiestDayStat() : SessionStat("busiestDay")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        if (c.NaturalChecks.Count == 0) return Missing("No checks were found through gameplay.");
        var busiest = c.NaturalChecks.GroupBy(x => StatMath.SourceDay(x.Timestamp, c.SourceUtcOffsetMinutes))
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
        return Ok(new() { Number = busiest.Count(), Unit = "checks", Text = busiest.Key.ToString("yyyy-MM-dd") });
    }
}

/// <summary>The slot whose items were hinted most. Ties go to the slot hinted first, then the lowest slot id.</summary>
public sealed class MostHintsReceivedStat() : SessionStat("mostHintsReceived")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        var best = c.DistinctHints.GroupBy(h => h.ReceiverSlotId)
            .Select(g => new { SlotId = g.Key, Count = g.Count(), First = g.Min(h => h.Timestamp) })
            .OrderByDescending(x => x.Count).ThenBy(x => x.First).ThenBy(x => x.SlotId).FirstOrDefault();
        return best is null ? Missing("No hints were recorded.") : Ok(new() { SlotId = best.SlotId, Number = best.Count, Unit = "hints" });
    }
}

public sealed class FirstHintStat() : SessionStat("firstHint")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        if (c.Hints.Count == 0) return Missing("No hints were recorded.");
        var hint = c.Hints[0];
        return Ok(new()
        {
            SlotId = hint.ReceiverSlotId, OtherSlotId = hint.SenderSlotId, ItemReceiverSlotId = hint.ReceiverSlotId,
            Item = hint.Item, Location = hint.Location, Timestamp = hint.Timestamp,
        });
    }
}

/// <summary>
/// Hint-to-check durations. A hint pays off when its location is later checked naturally.
/// Hints already marked found, checked before the hint, or only ever bulk-delivered have no payoff and are ignored.
/// </summary>
internal static class HintPayoff
{
    public static IEnumerable<(HintInfo Hint, CheckInfo Check, TimeSpan Delay)> Payoffs(SessionStatsContext c)
    {
        var checks = c.NaturalChecks.GroupBy(x => (x.SenderSlotId, x.Location)).ToDictionary(g => g.Key, g => g.First());
        foreach (var hint in c.DistinctHints)
        {
            if (string.Equals(hint.Status, "found", StringComparison.OrdinalIgnoreCase)) continue;
            if (!checks.TryGetValue((hint.SenderSlotId, hint.Location), out var check)) continue;
            if (check.ReceiverSlotId != hint.ReceiverSlotId || check.Item != hint.Item) continue;
            if (check.Timestamp < hint.Timestamp || (check.Timestamp == hint.Timestamp && check.Line < hint.Line)) continue;
            yield return (hint, check, check.Timestamp - hint.Timestamp);
        }
    }

    public static StatValue ToValue((HintInfo Hint, CheckInfo Check, TimeSpan Delay) p) => new()
    {
        SlotId = p.Hint.ReceiverSlotId, OtherSlotId = p.Hint.SenderSlotId, ItemReceiverSlotId = p.Hint.ReceiverSlotId,
        Item = p.Hint.Item, Location = p.Hint.Location, Timestamp = p.Hint.Timestamp, DurationSeconds = p.Delay.TotalSeconds,
    };
}

public sealed class MostHelpfulHintStat() : SessionStat("mostHelpfulHint")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        var payoffs = HintPayoff.Payoffs(c).ToList();
        if (payoffs.Count == 0) return Missing("No hint was followed by a check of its location.");
        return Ok(HintPayoff.ToValue(payoffs.OrderBy(p => p.Delay).ThenBy(p => p.Hint.Timestamp).ThenBy(p => p.Hint.Id).First()));
    }
}

public sealed class LeastHelpfulHintStat() : SessionStat("leastHelpfulHint")
{
    public override StatResult Calculate(SessionStatsContext c)
    {
        var payoffs = HintPayoff.Payoffs(c).ToList();
        if (payoffs.Count == 0) return Missing("No hint was followed by a check of its location.");
        return Ok(HintPayoff.ToValue(payoffs.OrderByDescending(p => p.Delay).ThenBy(p => p.Hint.Timestamp).ThenBy(p => p.Hint.Id).First()));
    }
}
