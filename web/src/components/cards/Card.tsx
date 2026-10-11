import { Show, children, type JSX, type ParentProps } from "solid-js";

type CardProps = ParentProps<{
  title?: string;
  subtitle?: string;
  icon?: string;
  color: string;
  class?: string;
  backdrop?: JSX.Element;
}>;

export default function Card(props: CardProps) {
  const content = children(() => props.children);
  return (
    <div
      class={`card bg-base-100 w-full min-w-0 shadow-sm border-2 ${props.backdrop ? "relative isolate overflow-hidden" : ""} ${props.class ?? ""}`}
      style={{
        "border-color": `var(--color-${props.color})`,
      }}
    >
      <Show when={props.backdrop}>
        <div class="pointer-events-none absolute inset-0 z-0" aria-hidden="true">{props.backdrop}</div>
      </Show>
      <div class={`card-body ${props.backdrop ? "relative z-10" : ""}`}>
        {/* (Optional) Card Header */}
        <Show when={props.title || props.subtitle || props.icon}>
          <div class="flex items-start gap-4">
            <Show when={props.icon}>
              <span
                class={`${props.icon} size-12 shrink-0`}
                style={{ color: `var(--color-${props.color})` }}
                aria-hidden="true"
              />
            </Show>
            <div class="min-w-0">
              <Show when={props.title}>
                <h2 class="card-title font-sans font-extrabold text-2xl break-words">
                  {props.title}
                </h2>
              </Show>
              <Show when={props.subtitle}>
                <p class="text-ap-muted">{props.subtitle}</p>
              </Show>
            </div>
          </div>
        </Show>

        {/* Card Content */}
        <div>{content()}</div>

        {/* <div class="card-actions justify-end">
          <button class="btn btn-primary">Buy Now</button>
        </div> */}
      </div>
    </div>
  );
}
