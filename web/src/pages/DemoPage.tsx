import { useQueryClient } from "@tanstack/solid-query";
import {
  createMemo,
  createSignal,
  lazy,
  Match,
  Show,
  Suspense,
  Switch,
} from "solid-js";
import {
  getGetCounterQueryKey,
  useGetCounter,
  useGetServerData,
  useIncrementCounter,
} from "../api/generated/afterpelago";

// ECharts is large; load it as its own chunk.
const EChart = lazy(() =>
  import("../charts/EChart").then((m) => ({ default: m.EChart })),
);

const timeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "medium",
});

export default function DemoPage() {
  const queryClient = useQueryClient();

  const serverData = useGetServerData();
  const counter = useGetCounter();
  const increment = useIncrementCounter({
    mutation: {
      onSuccess: (saved) =>
        queryClient.setQueryData(getGetCounterQueryKey(), saved),
    },
  });

  // Local-only Solid state; independent of the backend.
  const [clicks, setClicks] = createSignal(0);

  const chartOption = createMemo(() => ({
    grid: { left: 40, right: 16, top: 24, bottom: 32 },
    tooltip: { trigger: "axis" },
    xAxis: {
      type: "category",
      data: serverData.data?.samples.map((s) => s.label) ?? [],
    },
    yAxis: { type: "value" },
    series: [
      {
        type: "bar",
        data: serverData.data?.samples.map((s) => s.value) ?? [],
        itemStyle: { color: "#2a8f84", borderRadius: [4, 4, 0, 0] },
      },
    ],
  }));

  return (
    <div class="grid gap-6 md:grid-cols-3">
      <section
        class="rounded-box border border-base-300 bg-base-100 p-5"
        aria-labelledby="server-heading"
      >
        <h2
          id="server-heading"
          class="text-sm font-semibold uppercase tracking-wide text-base-content/60"
        >
          Server data
        </h2>
        <Switch>
          <Match when={serverData.isPending}>
            <p
              class="mt-3 flex items-center gap-2"
              data-testid="server-loading"
            >
              <span class="loading loading-spinner loading-sm" /> Loading…
            </p>
          </Match>
          <Match when={serverData.isError}>
            <p class="mt-3 text-error" role="alert" data-testid="server-error">
              Could not load server data: {serverData.error?.message}
            </p>
          </Match>
          <Match when={serverData.data}>
            {(data) => (
              <div class="mt-3 space-y-1">
                <p class="text-xl font-semibold" data-testid="server-greeting">
                  {data().greeting}
                </p>
                <p
                  class="text-sm text-base-content/70"
                  data-testid="server-time"
                >
                  {timeFormat.format(new Date(data().serverTime))}
                </p>
                <p
                  class="text-sm text-base-content/70"
                  data-testid="server-random"
                >
                  Random value: {data().randomValue}
                </p>
              </div>
            )}
          </Match>
        </Switch>
        <button
          class="btn btn-sm btn-outline mt-4"
          disabled={serverData.isFetching}
          onClick={() => void serverData.refetch()}
          data-testid="server-refresh"
        >
          {serverData.isFetching ? "Refreshing…" : "Refresh"}
        </button>
      </section>

      <section
        class="rounded-box border border-base-300 bg-base-100 p-5"
        aria-labelledby="client-heading"
      >
        <h2
          id="client-heading"
          class="text-sm font-semibold uppercase tracking-wide text-base-content/60"
        >
          Browser state
        </h2>
        <p
          class="mt-3 text-3xl font-semibold tabular-nums"
          data-testid="local-count"
        >
          Count: {clicks()}
        </p>
        <button
          class="btn btn-sm btn-secondary mt-4"
          onClick={() => setClicks((c) => c + 1)}
          data-testid="local-increment"
        >
          Increment
        </button>
        <span class="icon-[tabler--beach] text-3xl text-red-500" />
      </section>

      <section
        class="rounded-box border border-base-300 bg-base-100 p-5"
        aria-labelledby="saved-heading"
      >
        <h2
          id="saved-heading"
          class="text-sm font-semibold uppercase tracking-wide text-base-content/60"
        >
          Saved in SQLite
        </h2>
        <Show
          when={counter.data}
          fallback={
            <p class="mt-3 text-base-content/60">
              {counter.isError ? "Unavailable" : "Loading…"}
            </p>
          }
        >
          {(saved) => (
            <>
              <p
                class="mt-3 text-3xl font-semibold tabular-nums"
                data-testid="saved-count"
              >
                Saved: {saved().value}
              </p>
              <p class="text-xs text-base-content/60">
                Updated {timeFormat.format(new Date(saved().updatedAt))}
              </p>
            </>
          )}
        </Show>
        <button
          class="btn btn-sm btn-primary mt-4"
          disabled={increment.isPending}
          onClick={() => increment.mutate()}
          data-testid="saved-increment"
        >
          {increment.isPending ? "Saving…" : "Increment"}
        </button>
        <Show when={increment.isError}>
          <p class="mt-2 text-sm text-error" role="alert">
            Could not save: {increment.error?.message}
          </p>
        </Show>
      </section>

      <section
        class="rounded-box border border-base-300 bg-base-100 p-5 md:col-span-3"
        aria-labelledby="chart-heading"
      >
        <h2
          id="chart-heading"
          class="text-sm font-semibold uppercase tracking-wide text-base-content/60"
        >
          Sample series
        </h2>
        <Suspense
          fallback={
            <div class="mt-3 h-72 w-full animate-pulse rounded-box bg-base-200" />
          }
        >
          <EChart
            class="mt-3 h-72 w-full"
            option={chartOption()}
            label="Bar chart of sample values from the server"
          />
        </Suspense>
      </section>
    </div>
  );
}
