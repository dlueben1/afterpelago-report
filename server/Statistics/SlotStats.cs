namespace Afterpelago.Statistics;

public sealed class SlotNaturalChecksStat() : SlotStat("naturalChecks")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot) =>
        Ok(new() { Number = c.NaturalChecks.Count(x => x.SenderSlotId == slot.Id), Unit = "checks" });
}

/// <summary>Checks of this slot's world that were delivered in bulk by a release rather than found by playing.</summary>
public sealed class SlotAutoResolvedChecksStat() : SlotStat("autoResolvedChecks")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot) =>
        Ok(new() { Number = c.Checks.Count(x => x.SenderSlotId == slot.Id && x.Resolution != CheckResolution.Natural), Unit = "checks" });
}

public sealed class SlotGameplayHoursStat() : SlotStat("gameplayHours")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot)
    {
        var hours = StatMath.Hours(slot.GameplayEpisodes);
        return hours <= 0 ? Missing("This slot never had a gameplay connection.") : Ok(new() { Number = hours, Unit = "hours" });
    }
}

/// <summary>Natural checks per hour this slot's gameplay client was connected.</summary>
public sealed class SlotChecksPerHourStat() : SlotStat("checksPerHour")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot)
    {
        var hours = StatMath.Hours(slot.GameplayEpisodes);
        if (hours <= 0) return Missing("This slot never had a gameplay connection.");
        return Ok(new() { Number = c.NaturalChecks.Count(x => x.SenderSlotId == slot.Id) / hours, Unit = "checks/hour" });
    }
}

/// <summary>Share of the slot's checks found by playing before it cleared its game or released its world.</summary>
public sealed class SlotNaturalBeforeClearStat() : SlotStat("naturalBeforeClearPct")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot)
    {
        var cutoff = slot.GoalCompletedUtc ?? slot.ReleasedUtc;
        if (cutoff is null) return Missing("This slot neither completed its goal nor released its remaining items.");
        var sent = c.Checks.Where(x => x.SenderSlotId == slot.Id).ToList();
        if (sent.Count == 0) return Missing("This slot has no checks.");
        var before = sent.Count(x => x.Resolution == CheckResolution.Natural && x.Timestamp <= cutoff);
        return Ok(new() { Number = 100.0 * before / sent.Count, Unit = "%" });
    }
}

public sealed class SlotFirstItemReceivedStat() : SlotStat("firstItemReceived")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot)
    {
        var check = c.Checks.FirstOrDefault(x => x.ReceiverSlotId == slot.Id);
        if (check is null) return Missing("This slot received no items from checks.");
        return Ok(new()
        {
            SlotId = slot.Id, OtherSlotId = check.SenderSlotId, ItemReceiverSlotId = slot.Id,
            Item = check.Item, Location = check.Location, Timestamp = check.Timestamp, Text = check.Resolution.ToString(),
        });
    }
}

public sealed class SlotItemsReceivedNaturalStat() : SlotStat("itemsReceivedNatural")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot) =>
        Ok(new() { Number = c.Checks.Count(x => x.ReceiverSlotId == slot.Id && x.Resolution == CheckResolution.Natural), Unit = "items" });
}

public sealed class SlotItemsReceivedAutoStat() : SlotStat("itemsReceivedAuto")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot) =>
        Ok(new() { Number = c.Checks.Count(x => x.ReceiverSlotId == slot.Id && x.Resolution != CheckResolution.Natural), Unit = "items" });
}

public sealed class SlotHintsReceivedStat() : SlotStat("hintsReceived")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot) =>
        Ok(new() { Number = c.DistinctHints.Count(h => h.ReceiverSlotId == slot.Id), Unit = "hints" });
}

public sealed class SlotClearedAtStat() : SlotStat("clearedAt")
{
    public override StatResult Calculate(SessionStatsContext c, SlotInfo slot) =>
        slot.GoalCompletedUtc is null ? Missing("This slot did not complete its goal.") : Ok(new() { SlotId = slot.Id, Timestamp = slot.GoalCompletedUtc });
}

public sealed class UserSessionsPlayedStat() : UserStat("sessionsPlayed")
{
    public override StatResult Calculate(UserStatsContext c) =>
        Ok(new() { Number = c.Slots.Select(s => s.SessionId).Distinct().Count(), Unit = "sessions" });
}

public sealed class UserSlotsPlayedStat() : UserStat("slotsPlayed")
{
    public override StatResult Calculate(UserStatsContext c) => Ok(new() { Number = c.Slots.Count, Unit = "slots" });
}
