import type {
  CheckResponse,
  SessionSlotResponse,
} from "../api/generated/model";
import { buildSlotColors } from "./slot-colors";

export type GroupMode = "slot" | "player";

export interface SlotGrouping {
  mode: GroupMode;
  /** One entry per chart series: every slot in "slot" mode, one per owning player in "player" mode. */
  entries: SessionSlotResponse[];
  /** Color per entry id. */
  entryColors: Map<number, string>;
  /** Color per real slot id; slots of one player share a color in "player" mode. */
  slotColors: Map<number, string>;
  /** Maps checks so their sender is the entry that represents the sending slot. */
  remapChecks: (checks: readonly CheckResponse[]) => CheckResponse[];
}

/** True when the slot belongs to an account. Shared, non-user, and unclaimed slots never group with others. */
function ownerKey(slot: SessionSlotResponse): string | undefined {
  return slot.ownership === "User" && slot.ownerDiscordUserId
    ? slot.ownerDiscordUserId
    : undefined;
}

export function hasGroupableSlots(slots: readonly SessionSlotResponse[]) {
  return slots.some((slot) => ownerKey(slot) !== undefined);
}

/**
 * Builds the series for the Timeline Explorer. "By Player" folds all slots owned by the same account
 * into one entry (so their checks and percentages combine); everything else stays separate.
 */
export function buildGrouping(
  slots: readonly SessionSlotResponse[],
  mode: GroupMode,
): SlotGrouping {
  const entryIdBySlot = new Map<number, number>();
  const entries: SessionSlotResponse[] = [];

  if (mode === "slot") {
    for (const slot of slots) {
      entryIdBySlot.set(Number(slot.id), Number(slot.id));
      entries.push(slot);
    }
  } else {
    const byOwner = new Map<string, SessionSlotResponse[]>();
    for (const slot of slots) {
      const key = ownerKey(slot);
      if (key === undefined) {
        entryIdBySlot.set(Number(slot.id), Number(slot.id));
        entries.push(slot);
        continue;
      }
      byOwner.set(key, [...(byOwner.get(key) ?? []), slot]);
    }
    for (const members of byOwner.values()) {
      const representative = members.reduce((a, b) =>
        Number(b.id) < Number(a.id) ? b : a,
      );
      const everyoneCleared = members.every((m) => m.goalCompletedUtc);
      for (const member of members)
        entryIdBySlot.set(Number(member.id), Number(representative.id));
      entries.push({
        ...representative,
        playerName:
          representative.ownerDisplayName ?? representative.playerName,
        gameName:
          members.length === 1
            ? representative.gameName
            : `${members.length} slots`,
        goalCompletedUtc: everyoneCleared
          ? members
              .map((m) => m.goalCompletedUtc!)
              .reduce((a, b) => (Date.parse(b) > Date.parse(a) ? b : a))
          : null,
      });
    }
  }

  const entryColors = buildSlotColors(entries);
  const slotColors = new Map(
    slots.map((slot) => [
      Number(slot.id),
      entryColors.get(entryIdBySlot.get(Number(slot.id))!) ?? "#94a3b8",
    ]),
  );
  return {
    mode,
    entries,
    entryColors,
    slotColors,
    remapChecks: (checks) =>
      mode === "slot"
        ? [...checks]
        : checks.map((check) => ({
            ...check,
            senderSlotId:
              entryIdBySlot.get(Number(check.senderSlotId)) ??
              check.senderSlotId,
          })),
  };
}
