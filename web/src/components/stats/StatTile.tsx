import { A } from "@solidjs/router";
import { Show, type JSX } from "solid-js";
import type { StatResponse, StatSlotResponse } from "../../api/generated/model";
import Card from "../cards/Card";

export interface StatDefinition {
  id: string;
  label: string;
  icon: string;
  color: string;
  /** Short explanation of how the number is calculated. */
  help?: string;
  /** Describes the roles of the slots/item for statistics that carry more than a number. */
  detail?: (stat: StatResponse, ctx: StatContext) => JSX.Element;
}

export interface StatContext {
  sessionId: number;
}

export function formatNumber(value: number | string | null, digits = 1) {
  if (value === null) return "";
  const n = Number(value);
  return Number.isInteger(n)
    ? n.toLocaleString()
    : n.toLocaleString(undefined, { maximumFractionDigits: digits });
}

export function formatDuration(seconds: number | string | null) {
  if (seconds === null) return "";
  const total = Math.max(0, Math.round(Number(seconds)));
  const parts = [
    [Math.floor(total / 86_400), "d"],
    [Math.floor((total % 86_400) / 3_600), "h"],
    [Math.floor((total % 3_600) / 60), "m"],
    [total % 60, "s"],
  ] as const;
  const shown = parts.filter(([n]) => n > 0).slice(0, 2);
  return shown.length === 0
    ? "0s"
    : shown.map(([n, u]) => `${n}${u}`).join(" ");
}

export function formatTimestamp(value: string | null) {
  return value
    ? new Date(value).toLocaleString(undefined, {
        dateStyle: "medium",
        timeStyle: "short",
      })
    : "";
}

function formatDay(value: string) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, {
    dateStyle: "medium",
  });
}

function SlotLink(props: { slot: StatSlotResponse | null; sessionId: number }) {
  return (
    <Show when={props.slot} fallback={<span>someone</span>}>
      {(slot) => (
        <A
          href={`/sessions/${props.sessionId}/slot/${slot().id}`}
          class="link link-hover font-semibold"
        >
          {slot().playerName}
        </A>
      )}
    </Show>
  );
}

function ItemBlock(props: { stat: StatResponse; children: JSX.Element }) {
  return (
    <div class="mt-2 flex items-start gap-3">
      <Show when={props.stat.imageUrl}>
        <img
          src={props.stat.imageUrl!}
          alt=""
          width="48"
          height="48"
          class="size-12 shrink-0 object-contain"
          style={{ "image-rendering": "pixelated" }}
        />
      </Show>
      <div class="min-w-0 text-sm">{props.children}</div>
    </div>
  );
}

const when = (stat: StatResponse) => (
  <Show when={stat.timestamp}>
    <p class="text-xs text-ap-muted">{formatTimestamp(stat.timestamp)}</p>
  </Show>
);

export const SESSION_STATS: StatDefinition[] = [
  {
    id: "naturalChecks",
    label: "Checks Found",
    icon: "icon-[tabler--cube]",
    color: "ap-violet",
    help: "Checks found by playing, not delivered in bulk by a release.",
  },
  {
    id: "autoResolvedChecks",
    label: "Checks Released",
    icon: "icon-[tabler--package-export]",
    color: "ap-blue",
    help: "Checks delivered automatically when players completed their goal or released their remaining items.",
  },
  {
    id: "checksPerHour",
    label: "Checks per Hour",
    icon: "icon-[tabler--gauge]",
    color: "ap-green",
    help: "Checks found by playing divided by the hours at least one player was connected. Time with nobody online is ignored.",
  },
  {
    id: "checksPerDay",
    label: "Checks per Day",
    icon: "icon-[tabler--calendar-stats]",
    color: "ap-green",
    help: "Checks found by playing divided by the calendar days from the first to the last check.",
  },
  {
    id: "busiestDay",
    label: "Busiest Day",
    icon: "icon-[tabler--flame]",
    color: "ap-orange",
    detail: (s) => (
      <>
        <p class="text-sm">{s.text ? formatDay(s.text) : ""}</p>
      </>
    ),
  },
  {
    id: "firstCheck",
    label: "First Check",
    icon: "icon-[tabler--flag]",
    color: "ap-pink",
    detail: (s, c) => (
      <ItemBlock stat={s}>
        <p>
          <SlotLink slot={s.slot} sessionId={c.sessionId} /> found{" "}
          <b>{s.item}</b> for{" "}
          <SlotLink slot={s.otherSlot} sessionId={c.sessionId} />
        </p>
        <p class="text-ap-muted">at {s.location}</p>
        {when(s)}
      </ItemBlock>
    ),
  },
  {
    id: "lastCheck",
    label: "Last Check",
    icon: "icon-[tabler--flag-filled]",
    color: "ap-pink",
    detail: (s, c) => (
      <ItemBlock stat={s}>
        <p>
          <SlotLink slot={s.slot} sessionId={c.sessionId} /> found{" "}
          <b>{s.item}</b> for{" "}
          <SlotLink slot={s.otherSlot} sessionId={c.sessionId} />
        </p>
        <p class="text-ap-muted">at {s.location}</p>
        {when(s)}
      </ItemBlock>
    ),
  },
  {
    id: "firstClear",
    label: "First to Clear",
    icon: "icon-[tabler--trophy]",
    color: "ap-gold",
    detail: (s, c) => (
      <div class="text-sm">
        <SlotLink slot={s.slot} sessionId={c.sessionId} />
        {when(s)}
      </div>
    ),
  },
  {
    id: "lastClear",
    label: "Last to Clear",
    icon: "icon-[tabler--hourglass]",
    color: "ap-gold",
    detail: (s, c) => (
      <div class="text-sm">
        <SlotLink slot={s.slot} sessionId={c.sessionId} />
        {when(s)}
      </div>
    ),
  },
  {
    id: "mostHintsReceived",
    label: "Most Hints Received",
    icon: "icon-[tabler--bulb]",
    color: "ap-gold",
    help: "The slot with the most distinct hints about items it needs.",
    detail: (s, c) => (
      <p class="text-sm">
        <SlotLink slot={s.slot} sessionId={c.sessionId} />
      </p>
    ),
  },
  {
    id: "firstHint",
    label: "First Hint",
    icon: "icon-[tabler--bulb-filled]",
    color: "ap-gold",
    detail: (s, c) => (
      <ItemBlock stat={s}>
        <p>
          Hint for <SlotLink slot={s.slot} sessionId={c.sessionId} />
          's <b>{s.item}</b>
        </p>
        <p class="text-ap-muted">
          at {s.location} in{" "}
          <SlotLink slot={s.otherSlot} sessionId={c.sessionId} />
          's world
        </p>
        {when(s)}
      </ItemBlock>
    ),
  },
  {
    id: "mostHelpfulHint",
    label: "Most Helpful Hint",
    icon: "icon-[tabler--thumb-up]",
    color: "ap-green",
    help: "The hint whose location was checked soonest afterwards.",
    detail: (s, c) => (
      <ItemBlock stat={s}>
        <p>
          <b>{s.item}</b> for <SlotLink slot={s.slot} sessionId={c.sessionId} />
        </p>
        <p class="text-ap-muted">
          at {s.location} in{" "}
          <SlotLink slot={s.otherSlot} sessionId={c.sessionId} />
          's world
        </p>
        <p class="text-xs font-semibold">
          Found {formatDuration(s.durationSeconds)} after the hint
        </p>
      </ItemBlock>
    ),
  },
  {
    id: "leastHelpfulHint",
    label: "Least Helpful Hint",
    icon: "icon-[tabler--thumb-down]",
    color: "ap-orange",
    help: "The hint whose location took longest to check afterwards. Hints that were never followed up are not counted.",
    detail: (s, c) => (
      <ItemBlock stat={s}>
        <p>
          <b>{s.item}</b> for <SlotLink slot={s.slot} sessionId={c.sessionId} />
        </p>
        <p class="text-ap-muted">
          at {s.location} in{" "}
          <SlotLink slot={s.otherSlot} sessionId={c.sessionId} />
          's world
        </p>
        <p class="text-xs font-semibold">
          Found {formatDuration(s.durationSeconds)} after the hint
        </p>
      </ItemBlock>
    ),
  },
];

export const SLOT_STATS: StatDefinition[] = [
  {
    id: "checksPerHour",
    label: "Checks per Hour",
    icon: "icon-[tabler--gauge]",
    color: "ap-green",
    help: "Checks found by playing divided by the hours this slot's game was connected.",
  },
  {
    id: "gameplayHours",
    label: "Time Connected",
    icon: "icon-[tabler--clock]",
    color: "ap-blue",
  },
  {
    id: "naturalChecks",
    label: "Checks Found",
    icon: "icon-[tabler--cube]",
    color: "ap-violet",
  },
  {
    id: "autoResolvedChecks",
    label: "Checks Released",
    icon: "icon-[tabler--package-export]",
    color: "ap-blue",
    help: "Locations in this world that were delivered in bulk by a release rather than found.",
  },
  {
    id: "naturalBeforeClearPct",
    label: "Found Before Clearing",
    icon: "icon-[tabler--percentage]",
    color: "ap-pink",
    help: "Share of this world's checks found by playing before the slot cleared its game or released its items.",
  },
  {
    id: "firstItemReceived",
    label: "First Item Received",
    icon: "icon-[tabler--gift]",
    color: "ap-gold",
    detail: (s, c) => (
      <ItemBlock stat={s}>
        <p>
          <b>{s.item}</b>
        </p>
        <p class="text-ap-muted">
          from <SlotLink slot={s.otherSlot} sessionId={c.sessionId} /> at{" "}
          {s.location}
        </p>
        {when(s)}
        <Show when={s.text && s.text !== "Natural"}>
          <p class="text-xs">Delivered automatically ({s.text})</p>
        </Show>
      </ItemBlock>
    ),
  },
  {
    id: "itemsReceivedNatural",
    label: "Items Received",
    icon: "icon-[tabler--inbox]",
    color: "ap-violet",
    help: "Items received because someone found them.",
  },
  {
    id: "itemsReceivedAuto",
    label: "Items Delivered by Release",
    icon: "icon-[tabler--package-import]",
    color: "ap-blue",
  },
  {
    id: "hintsReceived",
    label: "Hints Received",
    icon: "icon-[tabler--bulb]",
    color: "ap-gold",
  },
  {
    id: "clearedAt",
    label: "Cleared Their Game",
    icon: "icon-[tabler--trophy]",
    color: "ap-gold",
    detail: (s) => <p class="text-sm">{formatTimestamp(s.timestamp)}</p>,
  },
];

/** One statistic. Missing source data is shown as unavailable, never as zero. */
export function StatTile(props: {
  definition: StatDefinition;
  stat: StatResponse | undefined;
  context: StatContext;
}) {
  const def = () => props.definition;
  return (
    <Card
      title={def().label}
      icon={def().icon}
      color={def().color}
      class="min-h-32"
    >
      <Show
        when={props.stat?.available}
        fallback={
          <p class="mt-2 text-sm text-ap-muted" role="status">
            Unavailable
            {props.stat?.reason
              ? `: ${props.stat.reason}`
              : " until statistics are calculated."}
          </p>
        }
      >
        <Show when={props.stat!.number !== null}>
          <p class="mt-1 font-display text-3xl font-bold">
            {formatNumber(props.stat!.number)}
            <Show when={props.stat!.unit}>
              <span class="ml-1 text-base font-semibold text-ap-muted">
                {props.stat!.unit}
              </span>
            </Show>
          </p>
        </Show>
        <Show when={def().detail}>
          {(detail) => (
            <div class="mt-1">{detail()(props.stat!, props.context)}</div>
          )}
        </Show>
        <Show when={def().help}>
          <p class="mt-2 text-xs text-ap-muted">{def().help}</p>
        </Show>
      </Show>
    </Card>
  );
}
