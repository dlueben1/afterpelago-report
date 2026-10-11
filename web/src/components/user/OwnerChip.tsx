import { Match, Switch } from "solid-js";
import { getAvatarUrl } from "./UserAvatar";

export interface SlotOwnerInfo {
  ownership: string;
  ownerDiscordUserId?: string | null;
  ownerDisplayName?: string | null;
  ownerAvatarHash?: string | null;
}

/** Shows who a slot belongs to: the owning account, or a label for shared, non-user, and unclaimed slots. */
export default function OwnerChip(props: { owner: SlotOwnerInfo }) {
  return (
    <Switch>
      <Match
        when={
          props.owner.ownership === "User" && props.owner.ownerDiscordUserId
        }
      >
        <span class="inline-flex min-w-0 items-center gap-1.5 text-xs font-semibold">
          <span class="avatar shrink-0">
            <span class="mask mask-circle size-4">
              <img
                src={getAvatarUrl(
                  props.owner.ownerDiscordUserId!,
                  props.owner.ownerAvatarHash,
                  32,
                )}
                alt=""
              />
            </span>
          </span>
          <span class="truncate">{props.owner.ownerDisplayName}</span>
        </span>
      </Match>
      <Match when={props.owner.ownership === "Shared"}>
        <span class="badge badge-soft badge-info badge-sm">Shared</span>
      </Match>
      <Match when={props.owner.ownership === "NonUser"}>
        <span class="badge badge-soft badge-neutral badge-sm">
          Not on Afterpelago
        </span>
      </Match>
      <Match when={true}>
        <span class="badge badge-soft badge-warning badge-sm">Unclaimed</span>
      </Match>
    </Switch>
  );
}
