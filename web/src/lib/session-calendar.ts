import type { EChartsCoreOption } from "echarts/core";
import type {
  CheckResponse,
  SessionSlotResponse,
} from "../api/generated/model";
import { sourceDay } from "./session-timeline";

const DAY_MS = 86_400_000;
const CELL_HEIGHT = 88;
const CALENDAR_LEFT = 44;
const CALENDAR_RIGHT = 8;
const CALENDAR_TOP = 36;
const CALENDAR_BOTTOM = 8;
const FULL_TURN = Math.PI * 2;

interface RenderApi {
  value(dimension: number): unknown;
  coord(value: unknown[]): number[];
  style(extra?: object): object;
}
interface RenderParams {
  coordSys: { cellWidth: number; cellHeight: number };
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

/** Every calendar day from first to last, inclusive (YYYY-MM-DD). */
function enumerateDays(first: string, last: string): string[] {
  const days: string[] = [];
  const end = Date.parse(`${last}T00:00:00.000Z`);
  for (let t = Date.parse(`${first}T00:00:00.000Z`); t <= end; t += DAY_MS) {
    days.push(new Date(t).toISOString().slice(0, 10));
  }
  return days;
}

/** Chart height in px for the vertical calendar: one row per week spanned. */
export function calendarHeightPx(
  checks: readonly CheckResponse[],
  offsetMinutes: number,
): number {
  if (checks.length === 0) return 0;
  const days = checks
    .map((c) => sourceDay(c.timestampUtc, offsetMinutes))
    .sort();
  const first = days[0];
  const spanned = enumerateDays(first, days[days.length - 1]).length;
  const weekday = new Date(`${first}T00:00:00.000Z`).getUTCDay();
  const weeks = Math.ceil((weekday + spanned) / 7);
  return weeks * CELL_HEIGHT + CALENDAR_TOP + CALENDAR_BOTTOM;
}

/** Pie-per-day calendar of checks found, one slice per sending slot. */
export function buildCalendarOption(
  checks: readonly CheckResponse[],
  slots: readonly SessionSlotResponse[],
  slotColors: ReadonlyMap<number, string>,
  offsetMinutes: number,
): EChartsCoreOption {
  const slotById = new Map(slots.map((slot) => [Number(slot.id), slot]));
  const perDay = new Map<string, Map<number, number>>();
  for (const check of checks) {
    const day = sourceDay(check.timestampUtc, offsetMinutes);
    const counts = perDay.get(day) ?? new Map<number, number>();
    const slotId = Number(check.senderSlotId);
    counts.set(slotId, (counts.get(slotId) ?? 0) + 1);
    perDay.set(day, counts);
  }

  const dayKeys = [...perDay.keys()].sort();
  const first = dayKeys[0];
  const last = dayKeys[dayKeys.length - 1];
  const allDays = first ? enumerateDays(first, last) : [];

  // [day, startAngle, endAngle, count, dayTotal, slotId]
  const slices = dayKeys.flatMap((day) => {
    const counts = [...perDay.get(day)!.entries()].sort(
      (a, b) => b[1] - a[1] || a[0] - b[0],
    );
    const total = counts.reduce((sum, [, n]) => sum + n, 0);
    let angle = -Math.PI / 2;
    return counts.map(([slotId, count]) => {
      const start = angle;
      angle += (count / total) * FULL_TURN;
      return {
        value: [day, start, angle, count, total, slotId],
        itemStyle: { color: slotColors.get(slotId) ?? "#94a3b8" },
      };
    });
  });

  const dayTotals = new Map(
    [...perDay].map(([day, counts]) => [
      day,
      [...counts.values()].reduce((sum, n) => sum + n, 0),
    ]),
  );

  return {
    animation: false,
    tooltip: {
      renderMode: "html",
      className: "ap-chart-tooltip",
      transitionDuration: 0,
      textStyle: { color: "#10374a" },
      trigger: "item",
      confine: true,
      formatter: (params: unknown) => {
        const value = (params as { value?: unknown[] }).value;
        if (!value || value[0] === undefined) return "";
        const slot = slotById.get(Number(value[5]));
        const count = Number(value[3]);
        const total = Number(value[4]);
        const color = slotColors.get(Number(value[5])) ?? "#94a3b8";
        const day = new Date(`${value[0]}T00:00:00`).toLocaleDateString(
          undefined,
          { dateStyle: "medium" },
        );
        return `<b style="color:${escapeHtml(color)}">${escapeHtml(slot?.playerName ?? "Unknown")}</b>${slot?.gameName ? ` <span style="opacity:.75">(${escapeHtml(slot.gameName)})</span>` : ""}<br/><b>${count.toLocaleString()}</b> of ${total.toLocaleString()} checks (${Math.round((count / total) * 100)}%)<br/><span style="opacity:.75">${day}</span>`;
      },
    },
    calendar: {
      range: first ? [first, last] : undefined,
      orient: "vertical",
      top: CALENDAR_TOP,
      bottom: CALENDAR_BOTTOM,
      left: CALENDAR_LEFT,
      right: CALENDAR_RIGHT,
      cellSize: ["auto", CELL_HEIGHT],
      yearLabel: { show: false },
      dayLabel: { firstDay: 0, nameMap: "en", color: "#6b7a89" },
      monthLabel: { nameMap: "en", color: "#6b7a89" },
      splitLine: { lineStyle: { color: "#cbd5e1" } },
      itemStyle: {
        color: "transparent",
        borderColor: "#e2e8f0",
        borderWidth: 1,
      },
    },
    series: [
      {
        type: "custom",
        coordinateSystem: "calendar",
        z: 2,
        data: slices,
        renderItem: (params: RenderParams, api: RenderApi) => {
          const [cx, cy] = api.coord([api.value(0)]);
          const { cellWidth, cellHeight } = params.coordSys;
          const radius = Math.min(cellWidth, cellHeight) / 2 - 17;
          return {
            type: "sector",
            shape: {
              cx,
              cy,
              r0: 0,
              r: radius,
              startAngle: Number(api.value(1)),
              endAngle: Number(api.value(2)),
            },
            style: api.style({ stroke: "#ffffff", lineWidth: 1 }),
            styleEmphasis: { stroke: "#10374a", lineWidth: 2 },
          };
        },
      },
      {
        type: "scatter",
        coordinateSystem: "calendar",
        silent: true,
        tooltip: { show: false },
        symbol: "circle",
        symbolSize: 1,
        itemStyle: { color: "transparent", borderWidth: 0 },
        z: 3,
        data: allDays.map((day) => [day, 0]),
        label: {
          show: true,
          position: "top",
          distance: 30,
          color: "#94a3b8",
          fontSize: 10,
          formatter: (params: { value: unknown[] }) =>
            String(Number(String(params.value[0]).slice(8))),
        },
      },
      {
        type: "scatter",
        coordinateSystem: "calendar",
        silent: true,
        tooltip: { show: false },
        symbol: "circle",
        symbolSize: 1,
        itemStyle: { color: "transparent", borderWidth: 0 },
        z: 3,
        data: allDays.map((day) => [day, dayTotals.get(day) ?? 0]),
        label: {
          show: true,
          position: "bottom",
          distance: 30,
          color: "#526777",
          fontSize: 10,
          fontWeight: 600,
          formatter: (params: { value: unknown[] }) => {
            const total = Number(params.value[1]);
            return `${total.toLocaleString()} ${total === 1 ? "check" : "checks"}`;
          },
        },
      },
    ],
  };
}
