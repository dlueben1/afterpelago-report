import { A } from "@solidjs/router";
import { useQueryClient } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createMemo, createSignal } from "solid-js";
import {
  getGetSessionDetailsQueryKey, getGetSessionsQueryKey,
  useDeleteSession, useGetSession, useGetSessions, useSetSessionFavorite,
} from "../../api/generated/afterpelago";
import type { SessionSummaryResponse } from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";
import HeroStatCard from "../../components/cards/HeroStatCard";
import { isAdmin } from "../../lib/auth";
import { formatSessionDate, formatSessionTime, selectSessions, type SessionSort, type SessionSortColumn } from "../../lib/session-overview";

const columns: { key: SessionSortColumn; label: string }[] = [
  { key: "isFavorite", label: "Favorite" }, { key: "name", label: "Session Name" },
  { key: "status", label: "Status" }, { key: "slotCount", label: "Players" },
  { key: "startUtc", label: "Started" }, { key: "endUtc", label: "Ended" },
];

function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Something went wrong. Please try again.";
}

function statusClass(status: string): string {
  return status === "Live" ? "badge-success" : status === "Abandoned" ? "badge-warning" : "badge-info";
}

export default function AllSessionsPage() {
  const queryClient = useQueryClient();
  const account = useGetSession();
  const queryKey = () => [...getGetSessionsQueryKey(), account.data?.user?.discordUserId];
  const sessions = useGetSessions({ query: {
    get queryKey() { return queryKey(); },
    get enabled() { return !!account.data?.user?.discordUserId; },
  } });
  const favoriteMutation = useSetSessionFavorite();
  const deleteMutation = useDeleteSession();
  const [search, setSearch] = createSignal("");
  const [favoritesOnly, setFavoritesOnly] = createSignal(false);
  const [sort, setSort] = createSignal<SessionSort>({ column: "startUtc", direction: "desc" });
  const [pendingFavorites, setPendingFavorites] = createSignal(new Set<number>());
  const [favoriteError, setFavoriteError] = createSignal<{ message: string; session: SessionSummaryResponse; desired: boolean }>();
  const [deleteError, setDeleteError] = createSignal<{ message: string; session: SessionSummaryResponse }>();
  const [deleteTarget, setDeleteTarget] = createSignal<SessionSummaryResponse>();
  const [deleting, setDeleting] = createSignal(false);
  let deleteDialog!: HTMLDialogElement;
  const rows = createMemo(() => selectSessions(sessions.data ?? [], search(), favoritesOnly(), sort()));
  const latest = createMemo(() => sessions.data?.reduce<SessionSummaryResponse | undefined>((result, session) =>
    !result || Date.parse(session.startUtc) > Date.parse(result.startUtc) ? session : result, undefined));
  const statsValue = () => sessions.data ? String(sessions.data.length) : sessions.isError ? "Unavailable" : "Loading…";
  const latestValue = () => sessions.data ? latest() ? formatSessionDate(latest()!.startUtc) : "No sessions yet" : sessions.isError ? "Unavailable" : "Loading…";

  function changeSort(column: SessionSortColumn) {
    setSort((previous) => ({ column, direction: previous.column === column && previous.direction === "asc" ? "desc" : "asc" }));
  }

  async function saveFavorite(session: SessionSummaryResponse, desired = !session.isFavorite) {
    const id = Number(session.id);
    if (pendingFavorites().has(id)) return;
    const key = queryKey();
    setPendingFavorites((previous) => new Set(previous).add(id));
    setFavoriteError(undefined);
    try {
      const result = await favoriteMutation.mutateAsync({ id, data: { isFavorite: desired } });
      // Stop stale list responses before updating the shared cache.
      await queryClient.cancelQueries({ queryKey: key, exact: true });
      queryClient.setQueryData<SessionSummaryResponse[]>(key, (previous) => previous?.map((row) =>
        Number(row.id) === id ? { ...row, isFavorite: result.isFavorite } : row));
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: key, exact: true }),
        queryClient.invalidateQueries({ queryKey: getGetSessionDetailsQueryKey(id) }),
      ]);
    } catch (error) {
      setFavoriteError({ message: `Could not update favorites for “${session.name}”: ${errorMessage(error)}`, session, desired });
    } finally {
      setPendingFavorites((previous) => { const next = new Set(previous); next.delete(id); return next; });
    }
  }

  function askToDelete(session: SessionSummaryResponse) {
    setDeleteTarget(session);
    deleteDialog.showModal();
  }

  async function confirmDelete() {
    const target = deleteTarget();
    if (!target || deleting()) return;
    const key = queryKey();
    const id = Number(target.id);
    setDeleting(true);
    setDeleteError(undefined);
    try {
      await deleteMutation.mutateAsync({ id });
      await queryClient.cancelQueries({ queryKey: key, exact: true });
      queryClient.setQueryData<SessionSummaryResponse[]>(key, (previous) => previous?.filter((row) => Number(row.id) !== id));
      queryClient.removeQueries({ queryKey: getGetSessionDetailsQueryKey(id) });
      await queryClient.invalidateQueries({ queryKey: getGetSessionsQueryKey() });
    } catch (error) {
      setDeleteError({ message: `Could not finish deleting “${target.name}”: ${errorMessage(error)}`, session: target });
      // A 503 still means the server hid the session while cleanup is retried.
      if (error instanceof ApiError && error.status === 503) {
        await queryClient.cancelQueries({ queryKey: key, exact: true });
        queryClient.setQueryData<SessionSummaryResponse[]>(key, (previous) => previous?.filter((row) => Number(row.id) !== id));
        await queryClient.invalidateQueries({ queryKey: getGetSessionsQueryKey() });
      }
    } finally {
      setDeleting(false);
      deleteDialog.close();
    }
  }

  return (
    <div class="flex flex-col gap-4">
      <div class="grid min-w-0 gap-4 lg:grid-cols-12">
        <Card title="Archipelago Sessions" subtitle="Every session, across your entire playgroup"
          icon="icon-[tabler--device-gamepad-filled]" color="ap-blue" class="min-h-32 lg:col-span-5" />
        <div class="grid min-w-0 gap-4 sm:grid-cols-2 lg:col-span-7">
          <HeroStatCard label="Total Sessions" value={statsValue()} color="ap-violet" icon="icon-[tabler--cube]" />
          <HeroStatCard label="Latest Session" value={latestValue()} color="ap-blue" icon="icon-[tabler--calendar-event]" />
        </div>
      </div>

      <Card color="ap-grid">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
          <label class="input flex w-full min-w-0 flex-1 items-center gap-2">
            <span class="icon-[tabler--search] size-5 shrink-0 text-ap-muted" aria-hidden="true" />
            <span class="sr-only">Search Sessions</span>
            <input type="search" class="min-w-0 grow" placeholder="Search sessions…" value={search()} onInput={(event) => setSearch(event.currentTarget.value)} />
          </label>
          <div class="flex flex-wrap gap-2">
            <button type="button" class={`btn ${favoritesOnly() ? "btn-secondary" : "btn-outline"}`} aria-pressed={favoritesOnly()} onClick={() => setFavoritesOnly(!favoritesOnly())}>
              <span class="icon-[tabler--star-filled] size-4" aria-hidden="true" />Favorites
            </button>
            <button type="button" class="btn btn-outline" disabled title="Coming soon" aria-label="My Sessions — coming soon">
              <span class="icon-[tabler--users] size-4" aria-hidden="true" />My Sessions
            </button>
          </div>
        </div>
      </Card>

      <Show when={favoriteError()}>{(error) => <div class="alert alert-error" role="alert">
        <span>{error().message}</span><button type="button" class="btn btn-sm" disabled={pendingFavorites().has(Number(error().session.id))} onClick={() => saveFavorite(error().session, error().desired)}>Retry favorite</button>
      </div>}</Show>
      <Show when={deleteError()}>{(error) => <div class="alert alert-error" role="alert">
        <span>{error().message}</span><Show when={isAdmin(account.data?.user?.role)}><button type="button" class="btn btn-sm" onClick={() => askToDelete(error().session)}>Retry deletion</button></Show>
      </div>}</Show>

      <div class="grid min-w-0 items-start gap-4 lg:grid-cols-[minmax(0,3fr)_minmax(0,1fr)]">
        <Card color="ap-violet" class="min-h-96 lg:min-h-136">
          <Show when={sessions.isError && sessions.data}><div class="alert alert-error mb-4" role="alert"><span>Could not refresh sessions: {errorMessage(sessions.error)}</span><button type="button" class="btn btn-sm" onClick={() => sessions.refetch()}>Retry</button></div></Show>
          <Switch>
            <Match when={sessions.isPending}><div class="flex min-h-64 items-center justify-center gap-3" role="status"><span class="loading loading-spinner text-primary" /><span>Loading sessions…</span></div></Match>
            <Match when={sessions.isError && !sessions.data}><div class="alert alert-error" role="alert"><span>Could not load sessions: {errorMessage(sessions.error)}</span><button type="button" class="btn btn-sm" onClick={() => sessions.refetch()}>Retry</button></div></Match>
            <Match when={sessions.data}>
              <div class="overflow-x-auto">
                <table class="table">
                  <caption class="sr-only">Published Archipelago sessions</caption>
                  <thead><tr>
                    <For each={columns}>{(column) => <th scope="col" aria-sort={sort().column === column.key ? sort().direction === "asc" ? "ascending" : "descending" : "none"}>
                      <button type="button" class="inline-flex items-center gap-1 rounded cursor-pointer py-2 focus-visible:outline-2 focus-visible:outline-primary" onClick={() => changeSort(column.key)} aria-label={`Sort by ${column.label}`}>
                        {column.label}<span class={`${sort().column !== column.key ? "icon-[tabler--arrows-sort]" : sort().direction === "asc" ? "icon-[tabler--arrow-up]" : "icon-[tabler--arrow-down]"} size-4 shrink-0`} aria-hidden="true" />
                      </button>
                    </th>}</For>
                    <th scope="col">Actions</th>
                  </tr></thead>
                  <tbody>
                    <For each={rows()}>{(session) => <tr>
                      <td><button type="button" class="btn btn-ghost btn-square btn-sm text-ap-gold" aria-pressed={session.isFavorite} aria-label={`${session.isFavorite ? "Unfavorite" : "Favorite"} ${session.name}`} disabled={pendingFavorites().has(Number(session.id))} onClick={() => saveFavorite(session)}>
                        <span class={`${pendingFavorites().has(Number(session.id)) ? "loading loading-spinner loading-xs" : session.isFavorite ? "icon-[tabler--star-filled] size-6" : "icon-[tabler--star] size-6"}`} aria-hidden="true" />
                      </button></td>
                      <td class="min-w-36 max-w-72 whitespace-normal break-words font-bold">{session.name}</td>
                      <td><span class={`badge badge-soft whitespace-nowrap ${statusClass(session.status)}`}>{session.status}</span></td>
                      <td>{session.slotCount}</td>
                      <td class="whitespace-nowrap"><time datetime={session.startUtc}>{formatSessionDate(session.startUtc)}<span class="block text-xs text-ap-muted">{formatSessionTime(session.startUtc)}</span></time></td>
                      <td class="whitespace-nowrap"><time datetime={session.endUtc}>{formatSessionDate(session.endUtc)}<span class="block text-xs text-ap-muted">{formatSessionTime(session.endUtc)}</span></time></td>
                      <td><div class="flex items-center gap-2">
                        <A href={`/sessions/${session.id}`} class="btn btn-primary btn-sm" aria-label={`View ${session.name}`}>View</A>
                        <Show when={isAdmin(account.data?.user?.role)}><button type="button" class="btn btn-ghost btn-square btn-sm text-error" disabled={deleting()} aria-label={`Delete ${session.name}`} onClick={() => askToDelete(session)}><span class="icon-[tabler--trash] size-5" aria-hidden="true" /></button></Show>
                      </div></td>
                    </tr>}</For>
                    <Show when={rows().length === 0}><tr><td colspan={7} class="py-16 text-center text-ap-muted" role="status">{sessions.data?.length === 0 ? "No sessions yet. Published imports will appear here." : "No sessions match your search and favorites filter."}</td></tr></Show>
                  </tbody>
                </table>
              </div>
            </Match>
          </Switch>
        </Card>
        <div class="grid min-w-0 gap-4 sm:grid-cols-2 lg:grid-cols-1">
          <Card title="Needs Review" icon="icon-[tabler--clipboard-check]" color="ap-orange" class="min-h-40"><p class="text-ap-muted mt-2">Coming soon</p></Card>
          <Card title="Recent Memories" icon="icon-[tabler--photo]" color="ap-orange" class="min-h-40"><p class="text-ap-muted mt-2">Coming soon</p></Card>
        </div>
      </div>

      <dialog ref={deleteDialog} class="modal" aria-labelledby="delete-session-title" aria-describedby="delete-session-description"
        onCancel={(event) => { if (deleting()) event.preventDefault(); }} onClose={() => setDeleteTarget(undefined)}>
        <div class="modal-box">
          <h2 id="delete-session-title" class="font-display text-xl font-bold">Delete session?</h2>
          <p id="delete-session-description" class="mt-3 break-words">Delete “{deleteTarget()?.name}” and its imported logs? This cannot be undone.</p>
          <div class="modal-action">
            <button type="button" class="btn" autofocus disabled={deleting()} onClick={() => deleteDialog.close()}>Cancel</button>
            <button type="button" class="btn btn-error" disabled={deleting()} onClick={confirmDelete}><Show when={deleting()} fallback="Delete session"><span class="loading loading-spinner loading-xs" aria-hidden="true" />Deleting…</Show></button>
          </div>
        </div>
        <form method="dialog" class="modal-backdrop"><button disabled={deleting()} aria-label="Cancel deletion">Close</button></form>
      </dialog>
    </div>
  );
}
