import { A, useParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createMemo } from "solid-js";
import { getGetSlotStatisticsQueryOptions } from "../../api/generated/afterpelago";
import type { SlotStatisticsResponse } from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";
import GameBox from "../../components/games/GameBox";
import {
  SLOT_STATS,
  StatTile,
  formatTimestamp,
} from "../../components/stats/StatTile";
import OwnerChip from "../../components/user/OwnerChip";

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Please try again.";
}

const parseId = (text: string) => {
  const value = Number(text);
  return /^\d+$/.test(text) &&
    Number.isInteger(value) &&
    value > 0 &&
    value <= 2_147_483_647
    ? value
    : undefined;
};

export default function SlotStatsPage() {
  const params = useParams<{ id: string; slotId: string }>();
  const sessionId = createMemo(() => parseId(params.id));
  const slotId = createMemo(() => parseId(params.slotId));
  const stats = useQuery<SlotStatisticsResponse, ApiError>(() =>
    getGetSlotStatisticsQueryOptions(sessionId() ?? 0, slotId() ?? 0, {
      query: { enabled: sessionId() !== undefined && slotId() !== undefined },
    }),
  );
  const byId = createMemo(
    () => new Map((stats.data?.stats ?? []).map((stat) => [stat.id, stat])),
  );
  const unavailable = () =>
    sessionId() === undefined ||
    slotId() === undefined ||
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
            title="Slot unavailable"
            subtitle="This slot does not exist or is no longer available."
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
              <Card color="ap-violet">
                <div class="flex flex-wrap items-center gap-6">
                  <div class="w-28 shrink-0">
                    <GameBox
                      playerName={data().slot.playerName}
                      gameName={data().slot.gameName}
                      coverUrl={data().slot.coverUrl}
                    />
                  </div>
                  <div class="min-w-0">
                    <p class="text-sm font-semibold text-ap-muted">
                      Slot Statistics · {data().sessionName}
                    </p>
                    <h1 class="font-display text-3xl font-bold break-words">
                      {data().slot.playerName}
                    </h1>
                    <p class="text-ap-muted">
                      {data().slot.gameName ?? "Unknown game"}
                    </p>
                    <div class="mt-2">
                      <OwnerChip owner={data().slot} />
                    </div>
                    <Show when={data().computedAt}>
                      <p class="mt-2 text-xs text-ap-muted">
                        Calculated {formatTimestamp(data().computedAt)}
                      </p>
                    </Show>
                  </div>
                </div>
              </Card>
              <div class="grid min-w-0 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                <For each={SLOT_STATS}>
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
