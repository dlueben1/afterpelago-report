import { A, useParams, useSearchParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createMemo, lazy } from "solid-js";
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
              <p class="text-sm text-ap-muted sm:ml-auto">
                Drag across the chart to zoom into a time range. Use the toolbox
                restore button to reset.
              </p>
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
              />
            </Show>
          </Card>
        </Match>
      </Switch>
    </div>
  );
}
