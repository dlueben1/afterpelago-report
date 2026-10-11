import { useQueryClient } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createSignal } from "solid-js";
import {
  getGetUserApprovalsQueryKey,
  updateUserApproval,
  useGetUserApprovals,
} from "../../api/generated/afterpelago";
import type { UserApprovalResponse } from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";

type ApprovalDecision = "Approved" | "Denied";

/** Turns a request failure into a short message suitable for an alert. */
function errorMessage(error: unknown) {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Something went wrong.";
}

/** Builds the Discord CDN URL for a user's custom or default avatar. */
function avatarUrl(user: UserApprovalResponse, size: number) {
  const cdnSize = Math.min(4096, Math.max(16, 2 ** Math.ceil(Math.log2(size))));

  if (user.avatarHash) {
    const extension = user.avatarHash.startsWith("a_") ? "gif" : "png";
    return `https://cdn.discordapp.com/avatars/${user.discordUserId}/${user.avatarHash}.${extension}?size=${cdnSize}`;
  }

  const defaultAvatarIndex = (BigInt(user.discordUserId) >> 22n) % 6n;
  return `https://cdn.discordapp.com/embed/avatars/${defaultAvatarIndex}.png?size=${cdnSize}`;
}

/** Formats an ISO timestamp for the current browser locale. */
function formatTimestamp(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

/** Returns the DaisyUI badge classes for an access status. */
function statusClass(status: string) {
  switch (status) {
    case "Approved":
      return "badge badge-success";
    case "Denied":
      return "badge badge-error";
    default:
      return "badge badge-warning";
  }
}

/** Lists registered users and lets admins change their access approval status. */
export default function UserAdminPage() {
  const queryClient = useQueryClient();
  const approvals = useGetUserApprovals();
  const [pendingAction, setPendingAction] = createSignal<{
    userId: string;
    status: ApprovalDecision;
  }>();
  const [error, setError] = createSignal<string>();
  const isBusy = () => pendingAction() !== undefined;

  /** Saves an approval decision and reloads the user list. */
  async function setApproval(
    user: UserApprovalResponse,
    status: ApprovalDecision,
  ) {
    setError(undefined);
    setPendingAction({ userId: user.discordUserId, status });
    try {
      await updateUserApproval(user.discordUserId, { status });
      await queryClient.invalidateQueries({
        queryKey: getGetUserApprovalsQueryKey(),
      });
    } catch (requestError) {
      setError(errorMessage(requestError));
    } finally {
      setPendingAction(undefined);
    }
  }

  return (
    <div class="mx-auto flex w-full max-w-5xl flex-col gap-4">
      <Card
        title="User Management"
        subtitle="Review access requests and manage Afterpelago users"
        icon="icon-[tabler--user-bolt]"
        color="ap-violet"
      />

      <Show when={error()}>
        <div class="alert alert-error" role="alert">
          <span class="icon-[tabler--alert-circle] size-5" aria-hidden="true" />
          <span>{error()}</span>
        </div>
      </Show>

      <Switch>
        <Match when={approvals.isPending}>
          <div
            class="flex min-h-40 items-center justify-center"
            role="status"
            aria-label="Loading users"
          >
            <span class="loading loading-spinner loading-lg text-primary" />
          </div>
        </Match>
        <Match when={approvals.isError}>
          <div class="alert alert-error" role="alert">
            <span>Could not load users: {errorMessage(approvals.error)}</span>
          </div>
        </Match>
        <Match when={approvals.data?.length === 0}>
          <div class="alert bg-base-100 shadow-sm">
            <span>No users have signed in to this Afterpelago server yet.</span>
          </div>
        </Match>
        <Match when={approvals.data}>
          {(users) => (
            <ul class="list overflow-hidden rounded-box bg-base-100 shadow-md">
              <li class="p-4 pb-2 text-xs tracking-wide opacity-60">
                Registered users ({users().length})
              </li>
              <For each={users()}>
                {(user) => {
                  const action = () =>
                    pendingAction()?.userId === user.discordUserId
                      ? pendingAction()?.status
                      : undefined;
                  return (
                    <li class="list-row items-center">
                      <div class="avatar">
                        <div class="mask mask-circle size-12">
                          <img src={avatarUrl(user, 48)} alt="" />
                        </div>
                      </div>

                      <div class="min-w-0">
                        <div class="truncate font-semibold">
                          {user.displayName}
                        </div>
                        <div class="truncate text-xs opacity-60">
                          {user.username ? `@${user.username}` : "Discord user"}{" "}
                          | {user.role}
                        </div>
                        <div class="break-all text-xs opacity-60">
                          ID: {user.discordUserId}
                        </div>
                        <div class="mt-1 text-xs opacity-60">
                          Requested {formatTimestamp(user.requestedAt)}
                          <span class="mx-1" aria-hidden="true">
                            |
                          </span>
                          Last seen {formatTimestamp(user.lastSeenAt)}
                        </div>
                      </div>

                      <div class="flex flex-wrap items-center gap-2">
                        <span class={statusClass(user.status)}>
                          {user.status}
                        </span>
                      </div>

                      <div class="flex flex-wrap justify-end gap-2">
                        <button
                          type="button"
                          class="btn btn-success btn-sm"
                          disabled={isBusy() || user.status === "Approved"}
                          aria-label={`Approve ${user.displayName}`}
                          onClick={() => setApproval(user, "Approved")}
                        >
                          <Show
                            when={action() === "Approved"}
                            fallback="Approve"
                          >
                            <span class="loading loading-spinner loading-xs" />
                          </Show>
                        </button>
                        <button
                          type="button"
                          class="btn btn-error btn-sm"
                          disabled={isBusy() || user.status === "Denied"}
                          aria-label={`Reject ${user.displayName}`}
                          onClick={() => setApproval(user, "Denied")}
                        >
                          <Show when={action() === "Denied"} fallback="Reject">
                            <span class="loading loading-spinner loading-xs" />
                          </Show>
                        </button>
                      </div>
                    </li>
                  );
                }}
              </For>
            </ul>
          )}
        </Match>
      </Switch>
    </div>
  );
}
