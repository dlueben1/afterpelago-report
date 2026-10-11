import type { SessionSlotResponse } from "../api/generated/model";

/** FNV-1a 32-bit hash. */
function hash(text: string): number {
  let h = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    h ^= text.charCodeAt(i);
    h = Math.imul(h, 0x01000193) >>> 0;
  }
  return h;
}

// Alternating tones keep hue-adjacent slots distinguishable when there are many slots.
const TONES: ReadonlyArray<readonly [number, number]> = [
  [72, 50],
  [60, 36],
  [85, 66],
];

/**
 * Unique, well-separated color per slot. The hash of "slot name + game name"
 * fixes each slot's position in the ordering; hues are then spread evenly
 * around the wheel so no two slots collide.
 */
export function buildSlotColors(
  slots: ReadonlyArray<
    Pick<SessionSlotResponse, "id" | "playerName" | "gameName">
  >,
): Map<number, string> {
  const ordered = slots
    .map((slot) => ({
      id: Number(slot.id),
      key: hash(`${slot.playerName}\u0000${slot.gameName ?? ""}`),
    }))
    .sort((a, b) => a.key - b.key || a.id - b.id);

  const colors = new Map<number, string>();
  ordered.forEach(({ id }, index) => {
    const hue = (index * 360) / ordered.length;
    const [saturation, lightness] = TONES[index % TONES.length];
    // Comma syntax: zrender's color parser (used for hover emphasis) rejects space-separated hsl().
    colors.set(id, `hsl(${hue.toFixed(1)}, ${saturation}%, ${lightness}%)`);
  });
  return colors;
}
