import { For, Show, type JSX } from "solid-js";
import type {
  EpisodeResponse,
  SessionSlotResponse,
} from "../../api/generated/model";

type DragMode = "zoom" | "pan";

function formatDay(day: string): string {
  return new Date(`${day}T00:00:00`).toLocaleDateString(undefined, {
    dateStyle: "medium",
  });
}

function formatEpisode(episode: EpisodeResponse): string {
  const start = new Date(episode.startUtc);
  const end = new Date(episode.endUtc);
  const date = start.toLocaleDateString(undefined, { dateStyle: "medium" });
  const time = (value: Date) =>
    value.toLocaleTimeString(undefined, { timeStyle: "short" });
  return `Episode ${episode.index}: ${date}, ${time(start)} – ${time(end)} (${Number(episode.checkCount).toLocaleString()} checks)`;
}

function SelectBox(props: {
  icon: string;
  label: string;
  children: JSX.Element;
}) {
  return (
    <label class="select flex w-full min-w-0 items-center gap-2 sm:w-auto">
      <span
        class={`${props.icon} size-5 shrink-0 text-ap-muted`}
        aria-hidden="true"
      />
      <span class="sr-only">{props.label}</span>
      {props.children}
    </label>
  );
}

export function OverTimeOptions(props: {
  days: string[];
  dayCounts: ReadonlyMap<string, number>;
  episodes: EpisodeResponse[];
  range: string;
  onRangeChange: (value: string) => void;
  skipGaps: boolean;
  canSkipGaps: boolean;
  onSkipGapsChange: (value: boolean) => void;
  dragMode: DragMode;
  onDragModeChange: (mode: DragMode) => void;
  onResetZoom: () => void;
}) {
  return (
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
      <SelectBox icon="icon-[tabler--calendar]" label="Select time range">
        <select
          class="min-w-0 grow"
          value={props.range}
          onChange={(event) => props.onRangeChange(event.currentTarget.value)}
        >
          <option value="">Entire session</option>
          <optgroup label="By Day">
            <For each={props.days}>
              {(day) => (
                <option value={`day:${day}`}>
                  {formatDay(day)} (
                  {(props.dayCounts.get(day) ?? 0).toLocaleString()} checks)
                </option>
              )}
            </For>
          </optgroup>
          <Show when={props.episodes.length > 0}>
            <optgroup label="By Episode">
              <For each={props.episodes}>
                {(episode) => (
                  <option value={`episode:${episode.index}`}>
                    {formatEpisode(episode)}
                  </option>
                )}
              </For>
            </optgroup>
          </Show>
        </select>
      </SelectBox>
      <label
        class="flex items-center gap-2"
        classList={{ "opacity-60": !props.canSkipGaps }}
      >
        <input
          type="checkbox"
          class="checkbox checkbox-sm checkbox-primary"
          checked={props.skipGaps}
          disabled={!props.canSkipGaps}
          onChange={(event) =>
            props.onSkipGapsChange(event.currentTarget.checked)
          }
        />
        <span>Skip gaps when no one was connected</span>
      </label>
      <div class="flex items-center gap-2 sm:ml-auto">
        <div class="join" role="group" aria-label="Chart drag mode">
          <div class="tooltip" data-tip="Select a time range to zoom">
            <button
              type="button"
              class={`btn btn-square btn-sm join-item ${props.dragMode === "zoom" ? "btn-primary" : "btn-outline"}`}
              aria-label="Select range to zoom"
              aria-pressed={props.dragMode === "zoom"}
              onClick={() => props.onDragModeChange("zoom")}
            >
              <span class="icon-[tabler--zoom-in] size-5" aria-hidden="true" />
            </button>
          </div>
          <div class="tooltip" data-tip="Pan across time">
            <button
              type="button"
              class={`btn btn-square btn-sm join-item ${props.dragMode === "pan" ? "btn-primary" : "btn-outline"}`}
              aria-label="Pan across time"
              aria-pressed={props.dragMode === "pan"}
              onClick={() => props.onDragModeChange("pan")}
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
            onClick={props.onResetZoom}
          >
            <span class="icon-[tabler--zoom-reset] size-5" aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
  );
}

export function PerDayOptions() {
  return <p class="text-ap-muted">This chart has no options yet.</p>;
}

export function ProgressOptions(props: {
  slots: readonly SessionSlotResponse[];
  focus: string;
  onFocusChange: (value: string) => void;
  granularity: "day" | "episode";
  canUseEpisodes: boolean;
  onGranularityChange: (value: "day" | "episode") => void;
}) {
  return (
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
      <SelectBox icon="icon-[tabler--user-star]" label="Highlight a slot">
        <select
          class="min-w-0 grow"
          value={props.focus}
          onChange={(event) => props.onFocusChange(event.currentTarget.value)}
        >
          <option value="">Highlight: everyone</option>
          <For each={props.slots}>
            {(slot) => (
              <option value={String(slot.id)}>{slot.playerName}</option>
            )}
          </For>
        </select>
      </SelectBox>
      <SelectBox
        icon="icon-[tabler--ruler-measure]"
        label="Compare progress by"
      >
        <select
          class="min-w-0 grow"
          value={props.granularity}
          onChange={(event) =>
            props.onGranularityChange(
              event.currentTarget.value === "episode" ? "episode" : "day",
            )
          }
        >
          <option value="day">Compare by day</option>
          <option value="episode" disabled={!props.canUseEpisodes}>
            Compare by episode
          </option>
        </select>
      </SelectBox>
    </div>
  );
}
