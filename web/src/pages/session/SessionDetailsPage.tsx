import { A, useNavigate, useParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import {
  For,
  Match,
  Show,
  Switch,
  createMemo,
  createSignal,
  lazy,
} from "solid-js";
import {
  getGetSessionChecksDailyQueryKey,
  getGetSessionChecksDailyQueryOptions,
  getGetSessionDetailsQueryKey,
  getGetSessionDetailsQueryOptions,
  getGetSessionStatisticsQueryKey,
  getGetSessionStatisticsQueryOptions,
  useGetSession,
} from "../../api/generated/afterpelago";
import type {
  DailyCheckCountResponse,
  SessionDetailsResponse,
  SessionStatisticsResponse,
} from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";
import HeroStatCard from "../../components/cards/HeroStatCard";
import SessionOverviewScenery from "../../components/cards/SessionOverviewScenery";
import GameBox from "../../components/games/GameBox";
import ClaimSlotsDialog from "../../components/sessions/ClaimSlotsDialog";
import SlotOwnerPicker from "../../components/sessions/SlotOwnerPicker";
import { formatDuration, formatNumber } from "../../components/stats/StatTile";
import OwnerChip from "../../components/user/OwnerChip";
import { isAdmin } from "../../lib/auth";
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
  const hours = Math.floor(Math.max(0, Number(value)) / 3_600);
  return `${hours.toLocaleString()} ${hours === 1 ? "hour" : "hours"}`;
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
  const statistics = useQuery<SessionStatisticsResponse, ApiError>(() => {
    const id = sessionId() ?? 0;
    return getGetSessionStatisticsQueryOptions(id, {
      query: {
        queryKey: getGetSessionStatisticsQueryKey(id),
        enabled: sessionId() !== undefined,
      },
    });
  });
  const stat = (id: string) => statistics.data?.stats.find((s) => s.id === id);
  const [claiming, setClaiming] = createSignal<number>();
  let manageDialog!: HTMLDialogElement;
  const claimedCount = () =>
    (session.data?.slots ?? []).filter((slot) => slot.ownership !== "Unclaimed")
      .length;
  async function refreshSession() {
    const id = sessionId();
    if (id === undefined) return;
    await Promise.all([session.refetch(), statistics.refetch()]);
  }
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
                    <div class="flex flex-wrap items-center gap-2 text-sm">
                      <span class="text-ap-muted">
                        {claimedCount()} of {details().slots.length} slots
                        claimed
                      </span>
                      <button
                        type="button"
                        class="btn btn-sm btn-outline"
                        onClick={() => setClaiming(sessionId())}
                      >
                        Claim my slots
                      </button>
                      <Show when={isAdmin(account.data?.user?.role)}>
                        <button
                          type="button"
                          class="btn btn-sm btn-ghost"
                          onClick={() => manageDialog.showModal()}
                        >
                          Manage owners
                        </button>
                      </Show>
                    </div>
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
                  <Switch>
                    <Match when={statistics.isPending}>
                      <div
                        class="mt-4 flex items-center gap-2 text-ap-muted"
                        role="status"
                      >
                        <span class="loading loading-spinner loading-sm" />
                        Loading…
                      </div>
                    </Match>
                    <Match
                      when={statistics.isError || !statistics.data?.computedAt}
                    >
                      <p class="mt-4 text-ap-muted">
                        Statistics are not available for this session yet.
                      </p>
                    </Match>
                    <Match when={statistics.data}>
                      <dl class="mt-3 flex flex-col gap-2 text-sm">
                        <div class="flex justify-between gap-3">
                          <dt class="text-ap-muted">Checks per hour</dt>
                          <dd class="font-semibold">
                            {stat("checksPerHour")?.available
                              ? formatNumber(stat("checksPerHour")!.number)
                              : "Unavailable"}
                          </dd>
                        </div>
                        <div class="flex justify-between gap-3">
                          <dt class="text-ap-muted">First to clear</dt>
                          <dd class="truncate font-semibold">
                            {stat("firstClear")?.slot?.playerName ??
                              "Nobody yet"}
                          </dd>
                        </div>
                        <div class="flex justify-between gap-3">
                          <dt class="text-ap-muted">Most helpful hint</dt>
                          <dd class="font-semibold">
                            {stat("mostHelpfulHint")?.available
                              ? formatDuration(
                                  stat("mostHelpfulHint")!.durationSeconds,
                                )
                              : "Unavailable"}
                          </dd>
                        </div>
                      </dl>
                      <A
                        href={`/sessions/${details().id}/stats`}
                        class="btn btn-sm btn-primary mt-4"
                      >
                        View all statistics
                      </A>
                    </Match>
                  </Switch>
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
                            footer={<OwnerChip owner={slot} />}
                            onClick={() =>
                              navigate(
                                `/sessions/${details().id}/slot/${slot.id}`,
                              )
                            }
                          />
                        )}
                      </For>
                    </div>
                  </Show>
                </Card>
              </div>
              <ClaimSlotsDialog
                sessionId={claiming()}
                onClose={() => setClaiming(undefined)}
              />
              <dialog
                ref={manageDialog}
                class="modal"
                aria-labelledby="manage-owners-title"
              >
                <div class="modal-box max-w-3xl">
                  <h2
                    id="manage-owners-title"
                    class="font-display text-xl font-bold"
                  >
                    Manage slot owners
                  </h2>
                  <div class="mt-4">
                    <SlotOwnerPicker
                      sessionId={Number(details().id)}
                      slots={details().slots}
                      onChanged={refreshSession}
                    />
                  </div>
                  <div class="modal-action">
                    <form method="dialog">
                      <button class="btn">Close</button>
                    </form>
                  </div>
                </div>
                <form method="dialog" class="modal-backdrop">
                  <button>close</button>
                </form>
              </dialog>
            </>
          )}
        </Match>
      </Switch>
    </div>
  );
}
