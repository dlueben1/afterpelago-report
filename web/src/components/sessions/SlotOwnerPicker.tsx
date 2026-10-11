import { For, Show, createMemo, createSignal } from "solid-js";
import {
  useGetUserApprovals,
  useSetSlotOwnership,
} from "../../api/generated/afterpelago";
import { ApiError } from "../../api/transport";

export interface OwnerPickerSlot {
  id: number | string;
  playerName: string;
  gameName?: string | null;
  ownership: string;
  ownerDiscordUserId?: string | null;
}

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Something went wrong.";
}

function currentValue(slot: OwnerPickerSlot): string {
  return slot.ownership === "User" && slot.ownerDiscordUserId
    ? `user:${slot.ownerDiscordUserId}`
    : slot.ownership;
}

/** Admin control that maps each slot of a session (draft or published) to an account, shared, or non-user. */
export default function SlotOwnerPicker(props: {
  sessionId: number;
  slots: readonly OwnerPickerSlot[];
  onChanged: () => void | Promise<void>;
}) {
  const users = useGetUserApprovals();
  const save = useSetSlotOwnership();
  const [pending, setPending] = createSignal(new Set<number>());
  const [error, setError] = createSignal<string>();
  const approved = createMemo(() =>
    (users.data ?? []).filter((user) => user.status === "Approved"),
  );

  async function change(slot: OwnerPickerSlot, value: string) {
    const slotId = Number(slot.id);
    setPending((previous) => new Set(previous).add(slotId));
    setError(undefined);
    try {
      await save.mutateAsync({
        id: props.sessionId,
        slotId,
        data: value.startsWith("user:")
          ? { ownership: "User", ownerDiscordUserId: value.slice(5) }
          : { ownership: value },
      });
      await props.onChanged();
    } catch (requestError) {
      setError(errorMessage(requestError));
    } finally {
      setPending((previous) => {
        const next = new Set(previous);
        next.delete(slotId);
        return next;
      });
    }
  }

  return (
    <div class="flex flex-col gap-2">
      <Show when={error()}>
        <div class="alert alert-error" role="alert">
          {error()}
        </div>
      </Show>
      <Show when={users.isError}>
        <p class="text-sm text-error" role="alert">
          Could not load users: {errorMessage(users.error)}
        </p>
      </Show>
      <div class="overflow-x-auto">
        <table class="table">
          <thead>
            <tr>
              <th>Slot</th>
              <th>Game</th>
              <th>Belongs to</th>
            </tr>
          </thead>
          <tbody>
            <For each={props.slots}>
              {(slot) => (
                <tr>
                  <td class="font-semibold">{slot.playerName}</td>
                  <td>
                    {slot.gameName ?? (
                      <span class="text-warning">Unknown game</span>
                    )}
                  </td>
                  <td>
                    <select
                      class="select select-sm w-full min-w-48"
                      aria-label={`Owner of ${slot.playerName}`}
                      disabled={pending().has(Number(slot.id))}
                      value={currentValue(slot)}
                      onChange={(event) =>
                        change(slot, event.currentTarget.value)
                      }
                    >
                      <option value="Unclaimed">Unclaimed</option>
                      <option value="Shared">Shared (everyone plays)</option>
                      <option value="NonUser">Not on Afterpelago</option>
                      <optgroup label="Users">
                        <For each={approved()}>
                          {(user) => (
                            <option value={`user:${user.discordUserId}`}>
                              {user.displayName}
                            </option>
                          )}
                        </For>
                      </optgroup>
                    </select>
                  </td>
                </tr>
              )}
            </For>
          </tbody>
        </table>
      </div>
    </div>
  );
}
