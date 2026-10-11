import type { EChartsCoreOption } from "echarts/core";
import type {
  CheckResponse,
  DailyCheckCountResponse,
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

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function formatCheckTooltip(params: unknown, checks: CheckResponse[]): string {
  const dataIndex = (params as { dataIndex?: number }).dataIndex;
  const check = dataIndex === undefined ? undefined : checks[dataIndex];
  if (!check) return "";
  const image = check.imageUrl
    ? `<img src="${escapeHtml(check.imageUrl)}" alt="" width="32" height="32" style="image-rendering:pixelated;vertical-align:middle;margin-right:6px" />`
    : "";
  const summary = `<b>${escapeHtml(check.senderName)}</b> found <b>${escapeHtml(check.itemName)}</b> for <b>${escapeHtml(check.receiverName)}</b>`;
  const location = `(Found at ${escapeHtml(check.locationName)})`;
  const when = `${new Date(check.timestampUtc).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" })}`;
  return `<div style="max-width:280px">${image}<span>${summary}</span><br/><span style="opacity:.75">${location}</span><br/><span style="opacity:.75">${when}</span></div>`;
}

/**
 * Cumulative checks-over-time chart: one point per check, item-image symbols,
 * drag-select X-range zoom via the toolbox, wheel zoom via inside dataZoom.
 */
export function buildTimelineOption(
  checks: CheckResponse[],
  window?: { start: number; end: number },
): EChartsCoreOption {
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
    },
    yAxis: { type: "value", min: 0, minInterval: 1 },
    toolbox: {
      feature: {
        dataZoom: { yAxisIndex: "none", filterMode: "none" },
        restore: {},
      },
    },
    dataZoom: [{ type: "inside", xAxisIndex: 0, filterMode: "none" }],
    tooltip: {
      trigger: "item",
      confine: true,
      formatter: (params: unknown) => formatCheckTooltip(params, checks),
    },
    series: [
      {
        type: "line",
        name: "Checks",
        data: checks.map((check, index) => ({
          value: [Date.parse(check.timestampUtc), index + 1],
          symbol: check.imageUrl ? `image://${check.imageUrl}` : "circle",
        })),
        showSymbol: true,
        symbolSize: 22,
        symbolKeepAspect: true,
        lineStyle: { width: 2 },
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
