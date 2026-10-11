import { createEffect, createSignal, Show } from "solid-js";

type GameBoxProps = {
  playerName: string;
  gameName?: string | null;
  coverUrl?: string | null;
  teamNumber?: number;
  onClick?: () => void;
};

export default function GameBox(props: GameBoxProps) {
  const [imageFailed, setImageFailed] = createSignal(false);
  createEffect((previousUrl: string | null | undefined) => {
    const url = props.coverUrl;
    if (url !== previousUrl) setImageFailed(false);
    return url;
  }, undefined);

  const gameName = () => props.gameName?.trim() || "Unknown game";
  const accessibleName = () =>
    [
      props.playerName,
      gameName(),
      props.teamNumber === undefined ? undefined : `Team ${props.teamNumber}`,
    ]
      .filter(Boolean)
      .join(", ");

  return (
    <article class="game-box min-w-0">
      <button
        type="button"
        class="hover-3d game-box-motion w-full min-w-0 cursor-pointer border-0 bg-transparent p-0 text-left focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary"
        aria-label={accessibleName()}
        onClick={() => props.onClick?.()}
      >
        <span class="game-box-surface relative block min-h-0 min-w-0 overflow-hidden rounded-md bg-white">
          <Show
            when={props.coverUrl && !imageFailed()}
            fallback={
              <span class="game-box-fallback absolute inset-0 grid place-items-center border-2 border-dashed border-gray-400 bg-gray-100 p-2 text-center text-sm text-gray-600">
                No cover art
              </span>
            }
          >
            <img
              src={props.coverUrl!}
              alt={`${gameName()} cover art`}
              class="absolute inset-0 size-full object-contain"
              loading="lazy"
              decoding="async"
              draggable={false}
              onError={() => setImageFailed(true)}
            />
          </Show>
        </span>
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
        <span aria-hidden="true" />
      </button>
      <div class="mt-2 min-w-0 text-center">
        <p class="wrap-break-word text-sm font-bold text-base-content">
          {props.playerName}
        </p>
        <p class="wrap-break-word text-xs text-ap-muted">{gameName()}</p>
        <Show when={props.teamNumber !== undefined}>
          <p class="mt-1 text-xs font-semibold text-ap-muted">
            Team {props.teamNumber}
          </p>
        </Show>
      </div>
    </article>
  );
}
