import { Show } from "solid-js";

type HeroStatCardProps = {
  label: string;
  value: string | number;
  color: "ap-blue" | "ap-violet" | "ap-gold" | "ap-green";
  icon?: string;
  description?: string;
  class?: string;
};

export default function HeroStatCard(props: HeroStatCardProps) {
  return (
    <div
      class={`card min-w-0 border-2 shadow-sm ${props.class ?? ""}`}
      style={{
        "border-color": `var(--color-${props.color})`,
        background: `linear-gradient(135deg, color-mix(in srgb, var(--color-${props.color}) 16%, var(--color-base-100)), var(--color-base-100))`,
      }}
    >
      <div class="card-body justify-center gap-2">
        <div class="flex items-center gap-3" style={{ color: `var(--color-${props.color})` }}>
          <Show when={props.icon}><span class={`${props.icon} size-8 shrink-0`} aria-hidden="true" /></Show>
          <p class="font-display text-2xl font-extrabold break-words" aria-live="polite">{props.value}</p>
        </div>
        <p class="text-ap-muted font-semibold">{props.label}</p>
        <Show when={props.description}>
          <p class="text-xs text-ap-muted">{props.description}</p>
        </Show>
      </div>
    </div>
  );
}
