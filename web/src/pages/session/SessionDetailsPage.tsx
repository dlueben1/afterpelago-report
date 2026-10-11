import { A, useNavigate, useParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createMemo, lazy } from "solid-js";
import {
  getGetSessionChecksDailyQueryKey,
  getGetSessionChecksDailyQueryOptions,
  getGetSessionDetailsQueryKey,
  getGetSessionDetailsQueryOptions,
  useGetSession,
} from "../../api/generated/afterpelago";
import type {
  DailyCheckCountResponse,
  SessionDetailsResponse,
} from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";
import HeroStatCard from "../../components/cards/HeroStatCard";
import SessionOverviewScenery from "../../components/cards/SessionOverviewScenery";
import GameBox from "../../components/games/GameBox";
import {
  formatSessionDate,
  formatSessionTime,
} from "../../lib/session-overview";
import { buildDailyOption } from "../../lib/session-timeline";

// ECharts is large; load it as its own chunk.
const EChart = lazy(() =>
  import("../../charts/EChart").then((m) => ({ default: m.EChart })),
);

function formatPlaytime(value: number | string | null): string {
  if (value === null) return "Unavailable";
  const seconds = Math.max(0, Math.floor(Number(value)));
  const parts = [
    [Math.floor(seconds / 86_400), "day"],
    [Math.floor((seconds % 86_400) / 3_600), "hour"],
    [Math.floor((seconds % 3_600) / 60), "minute"],
    [seconds % 60, "second"],
  ] as const;
  return parts
    .map(
      ([count, unit]) =>
        `${count.toLocaleString()} ${unit}${count === 1 ? "" : "s"}`,
    )
    .join(", ");
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Please try again.";
}

function statusClass(status: string): string {
  return status === "Live"
    ? "badge-success"
    : status === "Abandoned"
      ? "badge-warning"
      : "badge-info";
}

export default function SessionDetailsPage() {
  const params = useParams<{ id: string }>();
  const navigate = useNavigate();
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
  const dailyChecks = useQuery<DailyCheckCountResponse[], ApiError>(() => {
    const id = sessionId() ?? 0;
    const userId = account.data?.user?.discordUserId;
    return getGetSessionChecksDailyQueryOptions(id, {
      query: {
        queryKey: [...getGetSessionChecksDailyQueryKey(id), userId],
        enabled: sessionId() !== undefined && !!userId,
      },
    });
  });
  const dailyOption = createMemo(() =>
    buildDailyOption(dailyChecks.data ?? []),
  );
  function openTimeline(day?: string) {
    const id = sessionId();
    if (id === undefined) return;
    navigate(
      day ? `/sessions/${id}/timeline?day=${day}` : `/sessions/${id}/timeline`,
    );
  }
  function onDailyChartClick(params: unknown) {
    const point = params as {
      componentType?: string;
      name?: string;
      event?: { event?: Event };
    };
    if (point.componentType !== "series" || !point.name) return;
    // Keep the click from also triggering the whole-card navigation below.
    point.event?.event?.stopPropagation();
    openTimeline(point.name);
  }
  const hasMultipleTeams = createMemo(
    () =>
      new Set((session.data?.slots ?? []).map((slot) => slot.teamNumber)).size >
      1,
  );
  const unavailable = () =>
    sessionId() === undefined ||
    (session.error instanceof ApiError && session.error.status === 404);

  return (
    <div class="flex min-w-0 flex-col gap-4">
      <A
        href="/sessions"
        class="link link-primary inline-flex items-center gap-2 self-start font-semibold"
      >
        <span class="icon-[tabler--arrow-left] size-4" aria-hidden="true" />
        Back to Sessions
      </A>
      <Switch>
        <Match when={unavailable()}>
          <Card
            title="Session unavailable"
            subtitle="This session does not exist or is no longer available."
            color="ap-blue"
          />
        </Match>
        <Match when={session.isPending}>
          <div
            class="flex min-h-64 items-center justify-center gap-3"
            role="status"
          >
            <span
              class="loading loading-spinner text-primary"
              aria-hidden="true"
            />
            <span>Loading session…</span>
          </div>
        </Match>
        <Match when={session.isError && !session.data}>
          <div class="alert alert-error" role="alert">
            <span>
              Could not load the session: {errorMessage(session.error)}
            </span>
            <button
              type="button"
              class="btn btn-sm"
              disabled={session.isFetching}
              onClick={() => session.refetch()}
            >
              Retry
            </button>
          </div>
        </Match>
        <Match when={session.data}>
          {(details) => (
            <>
              <Show when={session.isError}>
                <div class="alert alert-error" role="alert">
                  <span>
                    Could not refresh this session:{" "}
                    {errorMessage(session.error)}
                  </span>
                  <button
                    type="button"
                    class="btn btn-sm"
                    disabled={session.isFetching}
                    onClick={() => session.refetch()}
                  >
                    Retry
                  </button>
                </div>
              </Show>
              <div class="grid min-w-0 gap-4 lg:grid-cols-12">
                <Card
                  title={details().name}
                  icon="icon-[tabler--device-gamepad-filled]"
                  color="ap-blue"
                  class="lg:col-span-5"
                  backdrop={<SessionOverviewScenery />}
                >
                  <div class="flex flex-col gap-4">
                    <div class="flex flex-wrap items-center gap-3">
                      <span
                        class={`badge badge-soft shrink-0 ${statusClass(details().status)}`}
                      >
                        {details().status}
                      </span>
                    </div>
                    <p class="text-sm text-ap-muted">
                      Worlds and players — Coming soon
                    </p>
                    <dl class="grid gap-3 border-t border-ap-grid pt-4 text-sm sm:grid-cols-2">
                      <div>
                        <dt class="font-semibold text-ap-muted">Started</dt>
                        <dd class="mt-1">
                          <time datetime={details().startUtc}>
                            {formatSessionDate(details().startUtc)}
                            <span class="block">
                              {formatSessionTime(details().startUtc)}
                            </span>
                          </time>
                        </dd>
                      </div>
                      <div>
                        <dt class="font-semibold text-ap-muted">Ended</dt>
                        <dd class="mt-1">
                          <time datetime={details().endUtc}>
                            {formatSessionDate(details().endUtc)}
                            <span class="block">
                              {formatSessionTime(details().endUtc)}
                            </span>
                          </time>
                        </dd>
                      </div>
                    </dl>
                  </div>
                </Card>
                <div class="grid min-w-0 gap-4 sm:grid-cols-2 lg:col-span-7 lg:grid-cols-3 lg:self-start">
                  <HeroStatCard
                    label="Total Checks"
                    value={Number(details().checkCount).toLocaleString()}
                    color="ap-violet"
                    icon="icon-[tabler--cube]"
                    class="min-h-32"
                  />
                  <HeroStatCard
                    label="Hints Used"
                    value={Number(details().hintCount).toLocaleString()}
                    color="ap-gold"
                    icon="icon-[tabler--bulb]"
                    class="min-h-32"
                  />
                  <HeroStatCard
                    label="Total Playtime"
                    value={formatPlaytime(details().totalPlaytimeSeconds)}
                    color="ap-green"
                    icon="icon-[tabler--clock]"
                    description="Time with at least one player connected to a game."
                    class="min-h-32"
                  />
                </div>
              </div>
              <div class="grid min-w-0 gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
                <div
                  class="cursor-pointer rounded-box transition-shadow focus-visible:outline-2 focus-visible:outline-primary hover:shadow-md"
                  role="link"
                  tabindex="0"
                  aria-label={`Open the Timeline Explorer for ${details().name}`}
                  onClick={() => openTimeline()}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") openTimeline();
                  }}
                >
                  <Card
                    title="Timeline Explorer"
                    subtitle="Explore the flow of checks, hints, and key events throughout the session."
                    icon="icon-[tabler--timeline]"
                    color="ap-pink"
                    class="min-h-72"
                  >
                    <Switch>
                      <Match when={dailyChecks.isPending}>
                        <div
                          class="flex min-h-40 items-center justify-center gap-3"
                          role="status"
                        >
                          <span
                            class="loading loading-spinner text-primary"
                            aria-hidden="true"
                          />
                          <span>Loading timeline…</span>
                        </div>
                      </Match>
                      <Match when={dailyChecks.isError}>
                        <p class="mt-4 text-ap-muted" role="alert">
                          Could not load the daily summary:{" "}
                          {errorMessage(dailyChecks.error)}
                        </p>
                      </Match>
                      <Match when={!dailyChecks.data?.length}>
                        <p class="mt-4 text-ap-muted">
                          No checks were recorded for this session.
                        </p>
                      </Match>
                      <Match when={dailyChecks.data}>
                        <EChart
                          class="h-40 w-full"
                          label={`Checks sent per day for ${details().name}`}
                          option={dailyOption()}
                          onEvents={{ click: onDailyChartClick }}
                        />
                        <p class="mt-2 text-sm">
                          <span class="link link-primary font-semibold">
                            Open Timeline Explorer
                          </span>{" "}
                          — select a day to zoom in.
                        </p>
                      </Match>
                    </Switch>
                  </Card>
                </div>
                <Card
                  title="Session Statistics"
                  icon="icon-[tabler--chart-bar]"
                  color="ap-orange"
                  class="min-h-72"
                >
                  <p class="mt-4 text-ap-muted">Coming soon</p>
                </Card>
              </div>
              <div class="grid min-w-0 gap-4 lg:grid-cols-2">
                <Card
                  title="Sphere Viewer"
                  icon="icon-[tabler--circles]"
                  color="ap-orange"
                  class="min-h-56"
                >
                  <p class="mt-4 text-ap-muted">Coming soon</p>
                </Card>
                <Card
                  title="Worlds"
                  icon="icon-[tabler--world]"
                  color="ap-violet"
                  class="min-h-56"
                >
                  <Show
                    when={details().slots.length > 0}
                    fallback={
                      <p class="mt-4 text-ap-muted">No worlds available.</p>
                    }
                  >
                    <div class="game-box-gallery mt-4">
                      <For each={details().slots}>
                        {(slot) => (
                          <GameBox
                            playerName={slot.playerName}
                            gameName={slot.gameName}
                            coverUrl={slot.coverUrl}
                            teamNumber={
                              hasMultipleTeams()
                                ? Number(slot.teamNumber)
                                : undefined
                            }
                          />
                        )}
                      </For>
                    </div>
                  </Show>
                </Card>
              </div>
            </>
          )}
        </Match>
      </Switch>
    </div>
  );
}
