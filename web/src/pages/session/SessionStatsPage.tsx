import { A, useParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createMemo } from "solid-js";
import { getGetSessionStatisticsQueryOptions } from "../../api/generated/afterpelago";
import type { SessionStatisticsResponse } from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";
import {
  SESSION_STATS,
  StatTile,
  formatTimestamp,
} from "../../components/stats/StatTile";

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Please try again.";
}

export default function SessionStatsPage() {
  const params = useParams<{ id: string }>();
  const sessionId = createMemo(() => {
    const value = Number(params.id);
    return /^\d+$/.test(params.id) &&
      Number.isInteger(value) &&
      value > 0 &&
      value <= 2_147_483_647
      ? value
      : undefined;
  });
  const stats = useQuery<SessionStatisticsResponse, ApiError>(() => {
    const id = sessionId() ?? 0;
    return getGetSessionStatisticsQueryOptions(id, {
      query: { enabled: sessionId() !== undefined },
    });
  });
  const byId = createMemo(
    () => new Map((stats.data?.stats ?? []).map((stat) => [stat.id, stat])),
  );
  const unavailable = () =>
    sessionId() === undefined ||
    (stats.error instanceof ApiError && stats.error.status === 404);

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
        <Match when={stats.isPending}>
          <div
            class="flex min-h-64 items-center justify-center gap-3"
            role="status"
          >
            <span
              class="loading loading-spinner text-primary"
              aria-hidden="true"
            />
            <span>Loading statistics…</span>
          </div>
        </Match>
        <Match when={stats.isError}>
          <div class="alert alert-error" role="alert">
            <span>Could not load statistics: {errorMessage(stats.error)}</span>
            <button
              type="button"
              class="btn btn-sm"
              onClick={() => stats.refetch()}
            >
              Retry
            </button>
          </div>
        </Match>
        <Match when={stats.data}>
          {(data) => (
            <>
              <Card
                title="Session Statistics"
                subtitle={data().sessionName}
                icon="icon-[tabler--chart-bar]"
                color="ap-orange"
              >
                <Show
                  when={data().computedAt}
                  fallback={
                    <p class="mt-2 text-ap-muted">
                      Statistics for this session have not been calculated yet.
                    </p>
                  }
                >
                  <p class="mt-2 text-sm text-ap-muted">
                    Calculated {formatTimestamp(data().computedAt)}. Only checks
                    found by playing count toward rates and records. Items
                    delivered by a release are counted separately.
                  </p>
                </Show>
              </Card>
              <div class="grid min-w-0 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                <For each={SESSION_STATS}>
                  {(definition) => (
                    <StatTile
                      definition={definition}
                      stat={byId().get(definition.id)}
                      context={{ sessionId: Number(data().sessionId) }}
                    />
                  )}
                </For>
              </div>
            </>
          )}
        </Match>
      </Switch>
    </div>
  );
}
