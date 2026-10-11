import type { EChartsCoreOption } from "echarts/core";
import type {
  CheckResponse,
  DailyCheckCountResponse,
  EpisodeResponse,
  SessionSlotResponse,
} from "../api/generated/model";

const DAY_MS = 86_400_000;
const MINUTE_MS = 60_000;

/** Source-local day (YYYY-MM-DD) for a UTC instant, using the session's published UTC offset. */
export function sourceDay(isoUtc: string, offsetMinutes: number): string {
  return new Date(Date.parse(isoUtc) + offsetMinutes * MINUTE_MS)
    .toISOString()
    .slice(0, 10);
}

/** UTC-millisecond window covering one source-local day. */
export function sourceDayWindow(
  day: string,
  offsetMinutes: number,
): { start: number; end: number } {
  const start = Date.parse(`${day}T00:00:00.000Z`) - offsetMinutes * MINUTE_MS;
  return { start, end: start + DAY_MS };
}

/** Distinct source-local days covered by the checks, in order. */
export function listSourceDays(
  checks: CheckResponse[],
  offsetMinutes: number,
): string[] {
  return [
    ...new Set(
      checks.map((check) => sourceDay(check.timestampUtc, offsetMinutes)),
    ),
  ].sort();
}

/** Episodes worth listing: ones where at least one check was found. */
export function listEpisodesWithChecks(
  episodes: readonly EpisodeResponse[],
): EpisodeResponse[] {
  return episodes.filter((episode) => Number(episode.checkCount) > 0);
}

export interface AxisBreakRange {
  start: number;
  end: number;
  gap: string;
}

/**
 * Time spans where nobody was connected, for X-axis breaks. A gap that contains
 * a check is left intact so no check is ever squeezed out of view.
 */
export function buildGapBreaks(
  episodes: readonly EpisodeResponse[],
  checks: readonly CheckResponse[],
  window?: { start: number; end: number },
): AxisBreakRange[] {
  const ordered = episodes
    .map((e) => ({ start: Date.parse(e.startUtc), end: Date.parse(e.endUtc) }))
    .sort((a, b) => a.start - b.start);
  const times = checks
    .map((c) => Date.parse(c.timestampUtc))
    .sort((a, b) => a - b);
  const breaks: AxisBreakRange[] = [];
  let cursor = 0;
  for (let i = 1; i < ordered.length; i++) {
    const gapStart = Math.max(ordered[i - 1].end, ordered[0].start);
    const gapEnd = ordered[i].start;
    if (gapEnd <= gapStart) continue;
    while (cursor < times.length && times[cursor] < gapStart) cursor++;
    if (cursor < times.length && times[cursor] <= gapEnd) continue;
    const start = window ? Math.max(gapStart, window.start) : gapStart;
    const end = window ? Math.min(gapEnd, window.end) : gapEnd;
    if (end > start) breaks.push({ start, end, gap: "1.2%" });
  }
  return breaks;
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function formatCheckTooltip(
  params: unknown,
  checks: CheckResponse[],
  slotColors?: ReadonlyMap<number, string>,
): string {
  const dataIndex = (params as { dataIndex?: number }).dataIndex;
  const check = dataIndex === undefined ? undefined : checks[dataIndex];
  if (!check) return "";
  const image = check.imageUrl
    ? `<img src="${escapeHtml(check.imageUrl)}" alt="" width="64" height="64" style="flex:none;width:64px;height:64px;object-fit:contain;image-rendering:pixelated" />`
    : "";
  const playerName = (name: string, slotId: number | string) => {
    const color = slotColors?.get(Number(slotId));
    const style = color ? ` style="color:${escapeHtml(color)}"` : "";
    return `<b${style}>${escapeHtml(name)}</b>`;
  };
  const summary = `${playerName(check.senderName, check.senderSlotId)} found <b>${escapeHtml(check.itemName)}</b> for ${playerName(check.receiverName, check.receiverSlotId)}`;
  const location = `(Found at ${escapeHtml(check.locationName)})`;
  const when = `${new Date(check.timestampUtc).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}`;
  return `<div style="display:flex;align-items:flex-start;gap:12px;width:360px;max-width:calc(100vw - 96px);white-space:normal">${image}<div style="flex:1;min-width:0;overflow-wrap:anywhere"><div>${summary}</div><div style="margin-top:4px">${location}</div><div style="margin-top:4px">${when}</div></div></div>`;
}

/**
 * Cumulative checks-over-time chart: one point per check, item-image symbols,
 * drag-select X-range zoom via the toolbox, wheel zoom via inside dataZoom.
 */
export function buildTimelineOption(
  checks: CheckResponse[],
  window?: { start: number; end: number },
  slotColors?: ReadonlyMap<number, string>,
  slots: readonly SessionSlotResponse[] = [],
  breaks: readonly AxisBreakRange[] = [],
): EChartsCoreOption {
  const goalMarkers = slots.flatMap((slot) => {
    if (!slot.goalCompletedUtc) return [];
    const color = slotColors?.get(Number(slot.id)) ?? "#facc15";
    return [
      {
        xAxis: Date.parse(slot.goalCompletedUtc),
        label: {
          formatter: `${slot.playerName} Cleared!`,
          color: "#10374a",
          backgroundColor: "#ffffff",
          borderColor: color,
          borderWidth: 2,
          padding: [2, 5],
          position: "insideEndTop",
        },
        lineStyle: { color, width: 2, type: "solid" },
      },
    ];
  });
  return {
    animation: false,
    grid: { left: 8, right: 24, top: 40, bottom: 8, containLabel: true },
    xAxis: {
      type: "time",
      name: "Time Obtained",
      nameLocation: "middle",
      nameGap: 28,
      min: window?.start,
      max: window?.end,
      breaks: breaks.length > 0 ? breaks : undefined,
      breakArea: { expandOnClick: false },
    },
    // scale + filtered zoom lets the Y range refit to the visible X window.
    yAxis: { type: "value", scale: true, minInterval: 1 },
    toolbox: {
      feature: {
        dataZoom: { yAxisIndex: "none", filterMode: "filter" },
        restore: {},
      },
    },
    dataZoom: [
      {
        id: "timeline-inside",
        type: "inside",
        xAxisIndex: 0,
        filterMode: "filter",
      },
    ],
    tooltip: {
      renderMode: "html",
      className: "ap-chart-tooltip",
      transitionDuration: 0,
      textStyle: { color: "#10374a" },
      trigger: "item",
      confine: true,
      formatter: (params: unknown) =>
        formatCheckTooltip(params, checks, slotColors),
    },
    series: [
      {
        type: "line",
        name: "Checks",
        data: checks.map((check, index) => ({
          value: [Date.parse(check.timestampUtc), index + 1],
          symbol: check.imageUrl ? `image://${check.imageUrl}` : "circle",
          itemStyle: check.imageUrl
            ? undefined
            : { color: slotColors?.get(Number(check.senderSlotId)) },
        })),
        showSymbol: true,
        symbolSize: 22,
        symbolKeepAspect: true,
        lineStyle: { width: 2 },
        // Single gradient-filled path; cheap compared to per-point rendering.
        areaStyle: {
          color: {
            type: "linear",
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: "rgba(84, 112, 198, 0.35)" },
              { offset: 1, color: "rgba(84, 112, 198, 0.02)" },
            ],
          },
        },
        markLine: {
          symbol: "none",
          silent: true,
          animation: false,
          data: goalMarkers,
        },
        progressive: 1000,
        progressiveThreshold: 2000,
        emphasis: { scale: 1.5 },
      },
    ],
  };
}

/** Simplified one-point-per-day chart for the Session Details card. */
export function buildDailyOption(
  days: DailyCheckCountResponse[],
): EChartsCoreOption {
  return {
    animation: false,
    grid: { left: 8, right: 16, top: 16, bottom: 8, containLabel: true },
    xAxis: {
      type: "category",
      data: days.map((day) => day.date),
      axisLabel: {
        formatter: (value: string) =>
          new Date(`${value}T00:00:00`).toLocaleDateString(undefined, {
            month: "short",
            day: "numeric",
          }),
      },
    },
    yAxis: { type: "value", min: 0, minInterval: 1 },
    tooltip: {
      renderMode: "html",
      className: "ap-chart-tooltip",
      transitionDuration: 0,
      textStyle: { color: "#10374a" },
      trigger: "item",
      confine: true,
      formatter: (params: unknown) => {
        const { name, value } = params as { name?: string; value?: number };
        const day = name
          ? new Date(`${name}T00:00:00`).toLocaleDateString(undefined, {
              dateStyle: "medium",
            })
          : "";
        return `<b>${Number(value ?? 0).toLocaleString()}</b> checks<br/><span style="opacity:.75">${day}</span>`;
      },
    },
    series: [
      {
        type: "line",
        name: "Checks per day",
        data: days.map((day) => Number(day.count)),
        showSymbol: true,
        symbolSize: 9,
        lineStyle: { width: 2 },
        areaStyle: { opacity: 0.15 },
      },
    ],
  };
}
