import { useQuery, useQueryClient } from "@tanstack/solid-query";
import {
  For,
  Match,
  Show,
  Switch,
  createEffect,
  createSignal,
  untrack,
} from "solid-js";
import {
  getGetSessionDetailsQueryKey,
  getGetSessionDetailsQueryOptions,
  getGetSessionStatisticsQueryKey,
  getGetSessionsNeedingReviewQueryKey,
  useClaimSessionSlots,
  useGetSession,
} from "../../api/generated/afterpelago";
import type { SessionDetailsResponse } from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import OwnerChip from "../user/OwnerChip";

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Something went wrong.";
}

/** Lets the signed-in user claim the slots they played in a session (honor system), or say they were not in it. */
export default function ClaimSlotsDialog(props: {
  sessionId: number | undefined;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const account = useGetSession();
  const claim = useClaimSessionSlots();
  const [selected, setSelected] = createSignal(new Set<number>());
  const [error, setError] = createSignal<string>();
  const [saving, setSaving] = createSignal(false);
  let dialog!: HTMLDialogElement;

  const userId = () => account.data?.user?.discordUserId;
  const details = useQuery<SessionDetailsResponse, ApiError>(() => {
    const id = props.sessionId ?? 0;
    return getGetSessionDetailsQueryOptions(id, {
      query: {
        queryKey: [...getGetSessionDetailsQueryKey(id), userId()],
        enabled: props.sessionId !== undefined && !!userId(),
      },
    });
  });

  const isMine = (slot: SessionDetailsResponse["slots"][number]) =>
    slot.ownership === "User" && slot.ownerDiscordUserId === userId();
  const isAssignedByAdmin = (slot: SessionDetailsResponse["slots"][number]) =>
    isMine(slot) && slot.ownershipSource === "Admin";
  const canChange = (slot: SessionDetailsResponse["slots"][number]) =>
    slot.ownership === "Unclaimed" ||
    (isMine(slot) && !isAssignedByAdmin(slot));
  const hasAdminAssignment = () =>
    (details.data?.slots ?? []).some(isAssignedByAdmin);

  createEffect(() => {
    const open = props.sessionId !== undefined;
    if (open && !dialog.open) {
      setError(undefined);
      dialog.showModal();
    } else if (!open && dialog.open) dialog.close();
  });
  createEffect(() => {
    const data = details.data;
    if (!data) return;
    untrack(() =>
      setSelected(new Set(data.slots.filter(isMine).map((s) => Number(s.id)))),
    );
  });

  function toggle(slotId: number, checked: boolean) {
    setSelected((previous) => {
      const next = new Set(previous);
      if (checked) next.add(slotId);
      else next.delete(slotId);
      return next;
    });
  }

  async function save(notInSession: boolean) {
    const id = props.sessionId;
    if (id === undefined || saving()) return;
    setSaving(true);
    setError(undefined);
    try {
      await claim.mutateAsync({
        id,
        data: { slotIds: notInSession ? [] : [...selected()], notInSession },
      });
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: getGetSessionsNeedingReviewQueryKey(),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetSessionDetailsQueryKey(id),
        }),
        queryClient.invalidateQueries({
          queryKey: getGetSessionStatisticsQueryKey(id),
        }),
      ]);
      props.onClose();
    } catch (requestError) {
      setError(errorMessage(requestError));
    } finally {
      setSaving(false);
    }
  }

  return (
    <dialog
      ref={dialog}
      class="modal"
      aria-labelledby="claim-slots-title"
      onClose={() => props.onClose()}
    >
      <div class="modal-box max-w-xl">
        <h2 id="claim-slots-title" class="font-display text-xl font-bold">
          Which slots were yours?
        </h2>
        <p class="mt-1 text-sm text-ap-muted">
          {details.data?.name}. Select every slot you played. This is an honor
          system, so just pick what was yours.
        </p>
        <Show when={error()}>
          <div class="alert alert-error mt-3" role="alert">
            {error()}
          </div>
        </Show>
        <Switch>
          <Match when={details.isPending}>
            <div
              class="flex min-h-32 items-center justify-center"
              role="status"
            >
              <span class="loading loading-spinner text-primary" />
            </div>
          </Match>
          <Match when={details.isError}>
            <p class="mt-4 text-error" role="alert">
              Could not load this session: {errorMessage(details.error)}
            </p>
          </Match>
          <Match when={details.data}>
            {(data) => (
              <ul class="mt-4 flex max-h-96 flex-col gap-1 overflow-y-auto">
                <For each={data().slots}>
                  {(slot) => (
                    <li>
                      <label
                        class="flex cursor-pointer items-center gap-3 rounded-lg px-2 py-1.5 hover:bg-base-200"
                        classList={{ "opacity-60": !canChange(slot) }}
                      >
                        <input
                          type="checkbox"
                          class="checkbox checkbox-primary checkbox-sm"
                          checked={
                            selected().has(Number(slot.id)) ||
                            isAssignedByAdmin(slot)
                          }
                          disabled={!canChange(slot)}
                          onChange={(event) =>
                            toggle(Number(slot.id), event.currentTarget.checked)
                          }
                        />
                        <span class="min-w-0 grow">
                          <span class="block truncate font-semibold">
                            {slot.playerName}
                          </span>
                          <span class="block truncate text-xs text-ap-muted">
                            {slot.gameName ?? "Unknown game"}
                          </span>
                        </span>
                        <Show
                          when={!canChange(slot) || isAssignedByAdmin(slot)}
                        >
                          <OwnerChip owner={slot} />
                        </Show>
                      </label>
                    </li>
                  )}
                </For>
              </ul>
            )}
          </Match>
        </Switch>
        <div class="modal-action flex-wrap">
          <button
            type="button"
            class="btn btn-ghost mr-auto"
            disabled={saving() || !details.data || hasAdminAssignment()}
            title={
              hasAdminAssignment()
                ? "An admin already assigned slots to you."
                : undefined
            }
            onClick={() => save(true)}
          >
            I'm not in this session
          </button>
          <button
            type="button"
            class="btn"
            disabled={saving()}
            onClick={() => props.onClose()}
          >
            Cancel
          </button>
          <button
            type="button"
            class="btn btn-primary"
            disabled={saving() || !details.data || selected().size === 0}
            onClick={() => save(false)}
          >
            {saving() ? (
              <span class="loading loading-spinner loading-xs" />
            ) : null}
            Save my slots
          </button>
        </div>
      </div>
      <form method="dialog" class="modal-backdrop">
        <button>close</button>
      </form>
    </dialog>
  );
}
