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
  buildTimelineOption,
  listSourceDays,
  sourceDay,
  sourceDayWindow,
} from "../../lib/session-timeline";

// ECharts is large; load it as its own chunk.
const EChart = lazy(() =>
  import("../../charts/EChart").then((m) => ({ default: m.EChart })),
);

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Please try again.";
}

function formatDay(day: string): string {
  return new Date(`${day}T00:00:00`).toLocaleDateString(undefined, {
    dateStyle: "medium",
  });
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
  const selectedDay = () => {
    const day = searchParams.day;
    return typeof day === "string" && /^\d{4}-\d{2}-\d{2}$/.test(day)
      ? day
      : undefined;
  };
  const chartWindow = createMemo(() => {
    const day = selectedDay();
    return day ? sourceDayWindow(day, offsetMinutes()) : undefined;
  });
  const option = createMemo(() =>
    buildTimelineOption(checks.data ?? [], chartWindow()),
  );
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
            <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
              <label class="select flex w-full min-w-0 items-center gap-2 sm:w-auto">
                <span
                  class="icon-[tabler--calendar] size-5 shrink-0 text-ap-muted"
                  aria-hidden="true"
                />
                <span class="sr-only">Filter by day</span>
                <select
                  class="min-w-0 grow"
                  value={selectedDay() ?? ""}
                  onChange={(event) =>
                    setSearchParams({
                      day: event.currentTarget.value || undefined,
                    })
                  }
                >
                  <option value="">Entire session</option>
                  <For each={days()}>
                    {(day) => (
                      <option value={day}>
                        {formatDay(day)} (
                        {(dayCounts().get(day) ?? 0).toLocaleString()} checks)
                      </option>
                    )}
                  </For>
                </select>
              </label>
              <div class="flex items-center gap-2 sm:ml-auto">
                <div class="join" role="group" aria-label="Chart drag mode">
                  <div class="tooltip" data-tip="Select a time range to zoom">
                    <button
                      type="button"
                      class={`btn btn-square btn-sm join-item ${dragMode() === "zoom" ? "btn-primary" : "btn-outline"}`}
                      aria-label="Select range to zoom"
                      aria-pressed={dragMode() === "zoom"}
                      onClick={() => setDragMode("zoom")}
                    >
                      <span
                        class="icon-[tabler--zoom-in] size-5"
                        aria-hidden="true"
                      />
                    </button>
                  </div>
                  <div class="tooltip" data-tip="Pan across time">
                    <button
                      type="button"
                      class={`btn btn-square btn-sm join-item ${dragMode() === "pan" ? "btn-primary" : "btn-outline"}`}
                      aria-label="Pan across time"
                      aria-pressed={dragMode() === "pan"}
                      onClick={() => setDragMode("pan")}
                    >
                      <span
                        class="icon-[tabler--hand-move] size-5"
                        aria-hidden="true"
                      />
                    </button>
                  </div>
                </div>
                <div class="tooltip" data-tip="Reset zoom">
                  <button
                    type="button"
                    class="btn btn-outline btn-square btn-sm"
                    aria-label="Reset zoom"
                    onClick={resetZoom}
                  >
                    <span
                      class="icon-[tabler--zoom-reset] size-5"
                      aria-hidden="true"
                    />
                  </button>
                </div>
              </div>
            </div>
          </Card>
          <Card
            title="Checks Over Time"
            subtitle={session.data!.name}
            icon="icon-[tabler--timeline]"
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
              <EChart
                class="h-[32rem] w-full"
                label={`Cumulative checks over time for ${session.data!.name}`}
                option={option()}
                onUpdate={handleChartUpdate}
                onEvents={{ globalcursortaken: handleGlobalCursor }}
              />
            </Show>
          </Card>
        </Match>
      </Switch>
    </div>
  );
}
