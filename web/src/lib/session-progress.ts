import type { EChartsCoreOption } from "echarts/core";
import type {
  CheckResponse,
  EpisodeResponse,
  SessionSlotResponse,
} from "../api/generated/model";
import { sourceDay, sourceDayWindow } from "./session-timeline";

const DAY_MS = 86_400_000;

export interface ProgressBucket {
  label: string;
  /** Exclusive UTC end (ms); checks before this count toward the bucket. */
  endMs: number;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

/** One bucket per source-local calendar day between the first and last check. */
export function buildDayBuckets(
  checks: readonly CheckResponse[],
  offsetMinutes: number,
): ProgressBucket[] {
  if (checks.length === 0) return [];
  const days = checks
    .map((check) => sourceDay(check.timestampUtc, offsetMinutes))
    .sort();
  const last = Date.parse(`${days[days.length - 1]}T00:00:00.000Z`);
  const buckets: ProgressBucket[] = [];
  for (let t = Date.parse(`${days[0]}T00:00:00.000Z`); t <= last; t += DAY_MS) {
    const day = new Date(t).toISOString().slice(0, 10);
    buckets.push({
      label: new Date(`${day}T00:00:00`).toLocaleDateString(undefined, {
        month: "short",
        day: "numeric",
      }),
      endMs: sourceDayWindow(day, offsetMinutes).end,
    });
  }
  return buckets;
}

/** One bucket per listed episode; the final bucket also absorbs any later checks. */
export function buildEpisodeBuckets(
  episodes: readonly EpisodeResponse[],
): ProgressBucket[] {
  return episodes.map((episode, index) => ({
    label: `Episode ${episode.index}`,
    endMs:
      index === episodes.length - 1
        ? Number.POSITIVE_INFINITY
        : Date.parse(episode.endUtc) + 1,
  }));
}

/**
 * Bump chart: at each bucket, slots are ranked by the share of their own checks
 * found so far. A slot's total is every check it sent by the end of the session.
 */
export function buildProgressOption(
  checks: readonly CheckResponse[],
  slots: readonly SessionSlotResponse[],
  slotColors: ReadonlyMap<number, string>,
  buckets: readonly ProgressBucket[],
  focusSlotId?: number,
): EChartsCoreOption {
  const totals = new Map<number, number>();
  const sorted = [...checks].sort(
    (a, b) => Date.parse(a.timestampUtc) - Date.parse(b.timestampUtc),
  );
  for (const check of sorted) {
    const id = Number(check.senderSlotId);
    totals.set(id, (totals.get(id) ?? 0) + 1);
  }

  const found = new Map<number, number>();
  const goalMs = new Map(
    slots.flatMap((slot) =>
      slot.goalCompletedUtc
        ? [[Number(slot.id), Date.parse(slot.goalCompletedUtc)] as const]
        : [],
    ),
  );
  const ranked = slots.map((slot) => ({
    slot,
    points: [] as { rank: number; pct: number; found: number; total: number }[],
  }));

  let cursor = 0;
  for (const bucket of buckets) {
    while (
      cursor < sorted.length &&
      Date.parse(sorted[cursor].timestampUtc) < bucket.endMs
    ) {
      const id = Number(sorted[cursor].senderSlotId);
      found.set(id, (found.get(id) ?? 0) + 1);
      cursor++;
    }
    const standings = ranked.map((entry) => {
      const id = Number(entry.slot.id);
      const total = totals.get(id) ?? 0;
      const count = found.get(id) ?? 0;
      const cleared = (goalMs.get(id) ?? Infinity) < bucket.endMs;
      const pct = total > 0 ? count / total : cleared ? 1 : 0;
      return { entry, pct, count, total };
    });
    standings.sort(
      (a, b) =>
        b.pct - a.pct ||
        a.entry.slot.playerName.localeCompare(b.entry.slot.playerName),
    );
    standings.forEach(({ entry, pct, count, total }, index) => {
      entry.points.push({ rank: index + 1, pct, found: count, total });
    });
  }

  const dimmed = (id: number) =>
    focusSlotId !== undefined && focusSlotId !== id;

  return {
    animation: false,
    grid: { left: 8, right: 160, top: 24, bottom: 8, containLabel: true },
    tooltip: {
      renderMode: "html",
      className: "ap-chart-tooltip",
      transitionDuration: 0,
      textStyle: { color: "#10374a" },
      trigger: "item",
      confine: true,
      formatter: (params: unknown) => {
        const { seriesName, name, data } = params as {
          seriesName?: string;
          name?: string;
          data?: {
            rank: number;
            pct: number;
            found: number;
            total: number;
            color: string;
          };
        };
        if (!data) return "";
        return `<b style="color:${escapeHtml(data.color)}">${escapeHtml(seriesName ?? "")}</b><br/>Rank <b>${data.rank}</b> &middot; <b>${Math.round(data.pct * 100)}%</b> (${data.found.toLocaleString()} of ${data.total.toLocaleString()} checks)<br/><span style="opacity:.75">${escapeHtml(name ?? "")}</span>`;
      },
    },
    xAxis: {
      type: "category",
      boundaryGap: false,
      data: buckets.map((bucket) => bucket.label),
    },
    yAxis: {
      type: "value",
      inverse: true,
      min: 1,
      max: Math.max(1, slots.length),
      interval: 1,
      axisLabel: { formatter: (value: number) => `#${value}` },
      splitLine: { show: false },
    },
    series: ranked.map(({ slot, points }) => {
      const id = Number(slot.id);
      const color = slotColors.get(id) ?? "#94a3b8";
      const opacity = dimmed(id) ? 0.15 : 1;
      return {
        type: "line",
        name: slot.playerName,
        data: points.map((point) => ({ value: point.rank, ...point, color })),
        symbol: "circle",
        symbolSize: 14,
        lineStyle: { width: dimmed(id) ? 2 : 4, color, opacity },
        itemStyle: { color, opacity },
        endLabel: {
          show: true,
          formatter: "{a}",
          color,
          opacity,
          fontWeight: "bold",
        },
        emphasis: { focus: "series" },
      };
    }),
  };
}
