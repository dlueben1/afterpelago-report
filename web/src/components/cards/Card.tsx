import { Show, children, type ParentProps } from "solid-js";

type CardProps = ParentProps<{
  title?: string;
  subtitle?: string;
  icon?: string;
  color: string;
  class?: string;
}>;

export default function Card(props: CardProps) {
  const content = children(() => props.children);
  return (
    <div
      class={`card bg-base-100 w-full min-w-0 shadow-sm border-2 ${props.class ?? ""}`}
      style={{
        "border-color": `var(--color-${props.color})`,
      }}
    >
      <div class="card-body">
        {/* (Optional) Card Header */}
        <div class="flex items-start gap-4">
          <Show when={props.icon}>
            <span
              class={`${props.icon} size-12 shrink-0`}
              style={{
                color: `var(--color-${props.color})`,
              }}
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

        {/* Card Content */}
        <div>{content()}</div>

        {/* <div class="card-actions justify-end">
          <button class="btn btn-primary">Buy Now</button>
        </div> */}
      </div>
    </div>
  );
}
