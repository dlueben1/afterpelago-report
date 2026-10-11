import { A, useParams, useSearchParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import type { ECharts } from "echarts/core";
import {
  For,
  Match,
  Show,
  Switch,
  createEffect,
  createMemo,
  createSignal,
  lazy,
} from "solid-js";
import {
  getGetSessionChecksQueryKey,
  getGetSessionChecksQueryOptions,
  getGetSessionDetailsQueryKey,
  getGetSessionDetailsQueryOptions,
  useGetSession,
} from "../../api/generated/afterpelago";
import type {
  CheckResponse,
  SessionDetailsResponse,
} from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";
import {
  buildCalendarOption,
  calendarHeightPx,
} from "../../lib/session-calendar";
import {
  buildDayBuckets,
  buildEpisodeBuckets,
  buildProgressOption,
} from "../../lib/session-progress";
import {
  buildGapBreaks,
  buildTimelineOption,
  listEpisodesWithChecks,
  listSourceDays,
  sourceDay,
  sourceDayWindow,
} from "../../lib/session-timeline";
import { buildSlotColors } from "../../lib/slot-colors";
import {
  OverTimeOptions,
  PerDayOptions,
  ProgressOptions,
} from "./TimelineOptions";

// ECharts is large; load it as its own chunk.
const EChart = lazy(() =>
  import("../../charts/EChart").then((m) => ({ default: m.EChart })),
);

type ChartTab = "over-time" | "per-day" | "progress";

const TABS: { id: ChartTab; label: string; title: string; icon: string }[] = [
  {
    id: "over-time",
    label: "Over Time",
    title: "Checks Over Time",
    icon: "icon-[tabler--timeline]",
  },
  {
    id: "per-day",
    label: "Per Day",
    title: "Checks per Day",
    icon: "icon-[tabler--calendar-stats]",
  },
  {
    id: "progress",
    label: "Leaderboard",
    title: "Daily Leaderboard",
    icon: "icon-[tabler--chart-line]",
  },
];

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Please try again.";
}

export default function TimelinePage() {
  const params = useParams<{ id: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const account = useGetSession();
  const [dragMode, setDragMode] = createSignal<"zoom" | "pan">("zoom");
  const [chart, setChart] = createSignal<ECharts>();

  function applyDragMode(instance: ECharts, mode: "zoom" | "pan") {
    instance.setOption({
      dataZoom: [{ id: "timeline-inside", moveOnMouseMove: mode === "pan" }],
    });
    instance.dispatchAction({
      type: "takeGlobalCursor",
      key: "dataZoomSelect",
      dataZoomSelectActive: mode === "zoom",
    });
    instance.getZr().setCursorStyle(mode === "zoom" ? "crosshair" : "grab");
  }

  createEffect(() => {
    const instance = chart();
    const mode = dragMode();
    if (instance && !instance.isDisposed()) applyDragMode(instance, mode);
  });

  function handleChartUpdate(instance: ECharts) {
    setChart(instance);
    applyDragMode(instance, dragMode());
  }

  function handleGlobalCursor(params: unknown) {
    const payload = params as { key?: string; dataZoomSelectActive?: boolean };
    setDragMode(
      payload.key === "dataZoomSelect" && payload.dataZoomSelectActive
        ? "zoom"
        : "pan",
    );
  }

  function resetZoom() {
    const instance = chart();
    if (!instance || instance.isDisposed()) return;
    instance.dispatchAction({ type: "restore" });
    applyDragMode(instance, dragMode());
  }

  const sessionId = createMemo(() => {
    const value = Number(params.id);
    return /^\d+$/.test(params.id) &&
      Number.isInteger(value) &&
      value > 0 &&
      value <= 2_147_483_647
      ? value
      : undefined;
  });
  const session = useQuery<SessionDetailsResponse, ApiError>(() => {
    const id = sessionId() ?? 0;
    const userId = account.data?.user?.discordUserId;
    return getGetSessionDetailsQueryOptions(id, {
      query: {
        queryKey: [...getGetSessionDetailsQueryKey(id), userId],
        enabled: sessionId() !== undefined && !!userId,
      },
    });
  });
  const checks = useQuery<CheckResponse[], ApiError>(() => {
    const id = sessionId() ?? 0;
    const userId = account.data?.user?.discordUserId;
    return getGetSessionChecksQueryOptions(id, {
      query: {
        queryKey: [...getGetSessionChecksQueryKey(id), userId],
        enabled: sessionId() !== undefined && !!userId,
      },
    });
  });
  const offsetMinutes = () => Number(session.data?.sourceUtcOffsetMinutes ?? 0);
  const slots = () => session.data?.slots ?? [];
  const allEpisodes = () => session.data?.episodes ?? [];
  const listedEpisodes = createMemo(() =>
    listEpisodesWithChecks(allEpisodes()),
  );
  const days = createMemo(() =>
    listSourceDays(checks.data ?? [], offsetMinutes()),
  );
  const dayCounts = createMemo(() => {
    const counts = new Map<string, number>();
    for (const check of checks.data ?? []) {
      const day = sourceDay(check.timestampUtc, offsetMinutes());
      counts.set(day, (counts.get(day) ?? 0) + 1);
    }
    return counts;
  });
  const slotColors = createMemo(() => buildSlotColors(slots()));

  const tab = (): ChartTab =>
    searchParams.chart === "per-day" || searchParams.chart === "progress"
      ? searchParams.chart
      : "over-time";
  const activeTab = () => TABS.find((entry) => entry.id === tab())!;

  // Legacy ?day= links still resolve to a day range.
  const selectedRange = createMemo(() => {
    const raw =
      typeof searchParams.range === "string"
        ? searchParams.range
        : typeof searchParams.day === "string"
          ? `day:${searchParams.day}`
          : "";
    const day = /^day:(\d{4}-\d{2}-\d{2})$/.exec(raw)?.[1];
    if (day)
      return { value: raw, window: sourceDayWindow(day, offsetMinutes()) };
    const index = /^episode:(\d+)$/.exec(raw)?.[1];
    const episode = listedEpisodes().find((e) => String(e.index) === index);
    return episode
      ? {
          value: raw,
          window: {
            start: Date.parse(episode.startUtc),
            end: Date.parse(episode.endUtc),
          },
        }
      : undefined;
  });
  const chartWindow = () => selectedRange()?.window;

  const skipGaps = () => searchParams.skipGaps === "1";
  const canSkipGaps = createMemo(
    () =>
      buildGapBreaks(allEpisodes(), checks.data ?? [], chartWindow()).length >
      0,
  );
  const overTimeOption = createMemo(() =>
    buildTimelineOption(
      checks.data ?? [],
      chartWindow(),
      slotColors(),
      slots(),
      skipGaps()
        ? buildGapBreaks(allEpisodes(), checks.data ?? [], chartWindow())
        : [],
    ),
  );
  const calendarOption = createMemo(() =>
    buildCalendarOption(
      checks.data ?? [],
      slots(),
      slotColors(),
      offsetMinutes(),
    ),
  );

  const granularity = () =>
    searchParams.granularity === "episode" && listedEpisodes().length > 0
      ? "episode"
      : "day";
  const focusSlot = () => {
    const value =
      typeof searchParams.focus === "string" ? searchParams.focus : "";
    return slots().some((slot) => String(slot.id) === value) ? value : "";
  };
  const progressOption = createMemo(() =>
    buildProgressOption(
      checks.data ?? [],
      slots(),
      slotColors(),
      granularity() === "episode"
        ? buildEpisodeBuckets(listedEpisodes())
        : buildDayBuckets(checks.data ?? [], offsetMinutes()),
      focusSlot() ? Number(focusSlot()) : undefined,
    ),
  );
  const progressHeight = () => `${Math.max(420, slots().length * 34 + 140)}px`;

  const unavailable = () =>
    sessionId() === undefined ||
    (session.error instanceof ApiError && session.error.status === 404) ||
    (checks.error instanceof ApiError && checks.error.status === 404);

  return (
    <div class="flex min-w-0 flex-col gap-4">
      <A
        href={`/sessions/${params.id}`}
        class="link link-primary inline-flex items-center gap-2 self-start font-semibold"
      >
        <span class="icon-[tabler--arrow-left] size-4" aria-hidden="true" />
        Back to Session
      </A>
      <Switch>
        <Match when={unavailable()}>
          <Card
            title="Session unavailable"
            subtitle="This session does not exist or is no longer available."
            color="ap-blue"
          />
        </Match>
        <Match when={session.isPending || checks.isPending}>
          <div
            class="flex min-h-64 items-center justify-center gap-3"
            role="status"
          >
            <span
              class="loading loading-spinner text-primary"
              aria-hidden="true"
            />
            <span>Loading timeline…</span>
          </div>
        </Match>
        <Match
          when={
            (session.isError && !session.data) ||
            (checks.isError && !checks.data)
          }
        >
          <div class="alert alert-error" role="alert">
            <span>
              Could not load the timeline:{" "}
              {errorMessage(checks.error ?? session.error)}
            </span>
            <button
              type="button"
              class="btn btn-sm"
              onClick={() => {
                void session.refetch();
                void checks.refetch();
              }}
            >
              Retry
            </button>
          </div>
        </Match>
        <Match when={session.data && checks.data}>
          <Card color="ap-grid">
            <Switch>
              <Match when={tab() === "over-time"}>
                <OverTimeOptions
                  days={days()}
                  dayCounts={dayCounts()}
                  episodes={listedEpisodes()}
                  range={selectedRange()?.value ?? ""}
                  onRangeChange={(value) =>
                    setSearchParams({
                      range: value || undefined,
                      day: undefined,
                    })
                  }
                  skipGaps={skipGaps()}
                  canSkipGaps={canSkipGaps()}
                  onSkipGapsChange={(value) =>
                    setSearchParams({ skipGaps: value ? "1" : undefined })
                  }
                  dragMode={dragMode()}
                  onDragModeChange={setDragMode}
                  onResetZoom={resetZoom}
                />
              </Match>
              <Match when={tab() === "per-day"}>
                <PerDayOptions />
              </Match>
              <Match when={tab() === "progress"}>
                <ProgressOptions
                  slots={slots()}
                  focus={focusSlot()}
                  onFocusChange={(value) =>
                    setSearchParams({ focus: value || undefined })
                  }
                  granularity={granularity()}
                  canUseEpisodes={listedEpisodes().length > 0}
                  onGranularityChange={(value) =>
                    setSearchParams({
                      granularity: value === "episode" ? value : undefined,
                    })
                  }
                />
              </Match>
            </Switch>
          </Card>
          <Card
            title={activeTab().title}
            subtitle={session.data!.name}
            icon={activeTab().icon}
            color="ap-pink"
            class="min-h-96"
          >
            <Show
              when={(checks.data ?? []).length > 0}
              fallback={
                <p class="py-16 text-center text-ap-muted" role="status">
                  No checks were recorded for this session.
                </p>
              }
            >
              <div role="tablist" class="tabs tabs-box">
                <For each={TABS}>
                  {(entry) => (
                    <>
                      <input
                        type="radio"
                        name="timeline-chart"
                        class="tab"
                        aria-label={entry.label}
                        checked={tab() === entry.id}
                        onChange={() =>
                          setSearchParams({
                            chart:
                              entry.id === "over-time" ? undefined : entry.id,
                          })
                        }
                      />
                      <div class="tab-content bg-base-100 border-base-300 p-4">
                        <Show when={tab() === entry.id}>
                          <Switch>
                            <Match when={entry.id === "over-time"}>
                              <EChart
                                class="h-[32rem] w-full"
                                label={`Cumulative checks over time for ${session.data!.name}`}
                                option={overTimeOption()}
                                onUpdate={handleChartUpdate}
                                onEvents={{
                                  globalcursortaken: handleGlobalCursor,
                                }}
                              />
                            </Match>
                            <Match when={entry.id === "per-day"}>
                              <EChart
                                class="w-full"
                                style={{
                                  height: `${calendarHeightPx(checks.data ?? [], offsetMinutes())}px`,
                                }}
                                label={`Checks found per day, by slot, for ${session.data!.name}`}
                                option={calendarOption()}
                              />
                              <ul class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm">
                                <For each={slots()}>
                                  {(slot) => (
                                    <li class="flex items-center gap-2">
                                      <span
                                        class="inline-block size-3 rounded-full"
                                        style={{
                                          "background-color": slotColors().get(
                                            Number(slot.id),
                                          ),
                                        }}
                                        aria-hidden="true"
                                      />
                                      {slot.playerName}
                                    </li>
                                  )}
                                </For>
                              </ul>
                            </Match>
                            <Match when={entry.id === "progress"}>
                              <EChart
                                class="w-full"
                                style={{ height: progressHeight() }}
                                label={`Share of each slot's checks found over time for ${session.data!.name}`}
                                option={progressOption()}
                              />
                            </Match>
                          </Switch>
                        </Show>
                      </div>
                    </>
                  )}
                </For>
              </div>
            </Show>
          </Card>
        </Match>
      </Switch>
    </div>
  );
}
