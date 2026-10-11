import { useSearchParams } from "@solidjs/router";
import {
  For,
  Show,
  createEffect,
  createMemo,
  createSignal,
  onCleanup,
  onMount,
} from "solid-js";
import {
  commitGameScrape,
  getGameImageMappings,
  getGameScrape,
  getGames,
  saveGameImageMappings,
  scrapeGamePack,
  uploadCustomGameImage,
  uploadGameCover,
  uploadGamePack,
} from "../../api/generated/afterpelago";
import type {
  GameCandidateResponse,
  GameMappingsResponse,
  GameResponse,
  GameScrapeResponse,
  ItemMappingResponse,
} from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";

function errorMessage(error: unknown) {
  return error instanceof ApiError
    ? (error.detail ?? error.message)
    : error instanceof Error
      ? error.message
      : "The request failed.";
}

function Thumbnail(props: { url?: string | null; large?: boolean }) {
  return (
    <span
      class={`inline-flex shrink-0 items-center justify-center rounded-lg border border-base-300 bg-base-200 ${props.large ? "size-16" : "size-10"}`}
    >
      <Show
        when={props.url}
        keyed
        fallback={
          <span
            class="icon-[tabler--photo-off] size-5 text-base-content/35"
            aria-label="No image"
          />
        }
      >
        {(url) => (
          <img
            src={url}
            alt=""
            loading="lazy"
            class="size-full object-contain p-1"
            onError={(e) => {
              e.currentTarget.style.visibility = "hidden";
            }}
          />
        )}
      </Show>
    </span>
  );
}

export default function GameScraperPage() {
  const [params, setParams] = useSearchParams();
  const [games, setGames] = createSignal<GameResponse[]>([]);
  const [gameId, setGameId] = createSignal<number>();
  const [gameSearch, setGameSearch] = createSignal("");
  const [draft, setDraft] = createSignal<GameScrapeResponse>();
  const [review, setReview] = createSignal<GameMappingsResponse>();
  const [edits, setEdits] = createSignal<Record<string, string | null>>(
    Object.create(null),
  );
  const [addedNames, setAddedNames] = createSignal<string[]>([]);
  const [newName, setNewName] = createSignal("");
  const [itemSearch, setItemSearch] = createSignal("");
  const [unmappedOnly, setUnmappedOnly] = createSignal(false);
  const [page, setPage] = createSignal(0);
  const [picker, setPicker] = createSignal<string>();
  const [candidateSearch, setCandidateSearch] = createSignal("");
  const [candidatePage, setCandidatePage] = createSignal(0);
  const [busy, setBusy] = createSignal("Loading games…");
  const [error, setError] = createSignal<string>();
  const [notice, setNotice] = createSignal<string>();
  const [stale, setStale] = createSignal(false);
  let fileInput: HTMLInputElement | undefined;
  let customImageInput: HTMLInputElement | undefined;
  let coverFileInput: HTMLInputElement | undefined;
  let generation = 0;
  const dirty = () => Object.keys(edits()).length > 0;
  const candidates = createMemo(
    () => new Map((review()?.candidates ?? []).map((c) => [c.key, c])),
  );
  const selectedGame = createMemo(() =>
    games().find((g) => Number(g.id) === gameId()),
  );
  const filteredGames = createMemo(() =>
    games().filter(
      (g) =>
        Number(g.id) === gameId() ||
        g.name.toLowerCase().includes(gameSearch().toLowerCase()),
    ),
  );
  const mismatch = () =>
    draft() &&
    gameId() &&
    !draft()!.compatibleGameIds.some((id) => Number(id) === gameId());
  const rows = createMemo<ItemMappingResponse[]>(() => [
    ...(review()?.items ?? []),
    ...addedNames().map((name) => ({
      id: 0,
      name,
      candidateKey: null,
      origin: "Manual",
      evidence: "New item",
      imageUrl: null,
    })),
  ]);
  const isEdited = (name: string) => Object.hasOwn(edits(), name);
  const keyFor = (row: ItemMappingResponse) =>
    isEdited(row.name) ? edits()[row.name] : row.candidateKey;
  const filteredRows = createMemo(() =>
    rows().filter(
      (row) =>
        row.name.toLowerCase().includes(itemSearch().toLowerCase()) &&
        (!unmappedOnly() || !keyFor(row)),
    ),
  );
  const pageCount = () => Math.max(1, Math.ceil(filteredRows().length / 50));
  const displayedRows = () =>
    filteredRows().slice(
      Math.min(page(), pageCount() - 1) * 50,
      (Math.min(page(), pageCount() - 1) + 1) * 50,
    );
  const filteredCandidates = createMemo(() => {
    const search = candidateSearch().trim().toLowerCase();
    return (review()?.candidates ?? []).filter((c) =>
      `${c.name} ${c.itemName} ${c.codes.join(" ")} ${c.apIds.join(" ")} ${c.variant} ${c.packVersion}`
        .toLowerCase()
        .includes(search),
    );
  });
  const candidatePageCount = () =>
    Math.max(1, Math.ceil(filteredCandidates().length / 60));
  createEffect(() => {
    if (picker())
      queueMicrotask(() => {
        document.getElementById("candidate-search")?.focus();
        document
          .getElementById("candidate-search")
          ?.scrollIntoView({ block: "center", behavior: "smooth" });
      });
  });

  function resetEdits() {
    setEdits(Object.create(null));
    setAddedNames([]);
    setPicker(undefined);
    setPage(0);
    setStale(false);
  }
  function showFailure(e: unknown) {
    setError(errorMessage(e));
    if (e instanceof ApiError && e.status === 409) setStale(true);
  }
  function applyDraft(result: GameScrapeResponse) {
    setDraft(result);
    if (result.gameId !== null) setGameId(Number(result.gameId));
    if (result.review) setReview(result.review);
    resetEdits();
    setParams(
      {
        draft: result.id,
        game:
          result.gameId === null ? gameId()?.toString() : String(result.gameId),
      },
      { replace: true },
    );
  }

  onMount(async () => {
    try {
      setGames(await getGames());
      const draftId = Array.isArray(params.draft)
        ? params.draft[0]
        : params.draft;
      const selected = Number(
        Array.isArray(params.game) ? params.game[0] : params.game,
      );
      if (selected && games().some((g) => Number(g.id) === selected))
        setGameId(selected);
      if (draftId) applyDraft(await getGameScrape(draftId));
      if (gameId() && !review())
        setReview(await getGameImageMappings(gameId()!));
    } catch (e) {
      showFailure(e);
    } finally {
      setBusy("");
    }
  });
  const beforeUnload = (event: BeforeUnloadEvent) => {
    if (dirty()) event.preventDefault();
  };
  window.addEventListener("beforeunload", beforeUnload);
  onCleanup(() => {
    generation++;
    window.removeEventListener("beforeunload", beforeUnload);
  });

  async function changeGame(value: string) {
    const token = ++generation;
    setGameId(value ? Number(value) : undefined);
    setReview(undefined);
    resetEdits();
    setError(undefined);
    setNotice(undefined);
    if (draft()?.gameId !== null && draft()?.gameId !== undefined) {
      setDraft(undefined);
      if (fileInput) fileInput.value = "";
    }
    setParams(
      { game: value || undefined, draft: draft()?.id },
      { replace: true },
    );
    if (!value) return;
    setBusy("Loading saved mappings…");
    try {
      const result = await getGameImageMappings(Number(value));
      if (token === generation) setReview(result);
    } catch (e) {
      if (token === generation) showFailure(e);
    } finally {
      if (token === generation) setBusy("");
    }
  }

  async function upload(file?: File) {
    const token = ++generation;
    setDraft(undefined);
    setReview(undefined);
    resetEdits();
    setError(undefined);
    setNotice(undefined);
    setParams({ draft: undefined }, { replace: true });
    if (!file) return;
    if (
      !file.name.toLowerCase().endsWith(".zip") ||
      file.size === 0 ||
      file.size > 50 * 1024 * 1024
    ) {
      setError("Choose a nonempty ZIP no larger than 50 MiB.");
      return;
    }
    setBusy("Uploading and validating ZIP…");
    try {
      if (gameId()) setReview(await getGameImageMappings(gameId()!));
      const result = await uploadGamePack({ file });
      if (token === generation) {
        applyDraft(result);
        setNotice("ZIP validated. Select its game and click Scrape.");
      }
    } catch (e) {
      if (token === generation) showFailure(e);
    } finally {
      if (token === generation) setBusy("");
    }
  }

  async function uploadCustomImage(file?: File) {
    const name = picker();
    const current = review();
    if (!file || !name || !current) return;
    if (file.size === 0 || file.size > 5 * 1024 * 1024) {
      setError("Choose a nonempty image no larger than 5 MiB.");
      return;
    }
    setBusy("Converting custom image to WebP…");
    setError(undefined);
    setNotice(undefined);
    try {
      const result = await uploadCustomGameImage(Number(current.gameId), {
        file,
      });
      setReview((previous) =>
        previous
          ? {
              ...previous,
              revision: result.revision,
              candidates: [
                ...previous.candidates.filter(
                  (c) => c.key !== result.candidate.key,
                ),
                result.candidate,
              ],
            }
          : previous,
      );
      choose(name, result.candidate.key);
      setNotice(
        "Custom image added to the library and selected. Save to apply it to this item.",
      );
    } catch (e) {
      showFailure(e);
    } finally {
      setBusy("");
    }
  }

  async function uploadCover(file?: File) {
    const selected = gameId();
    if (!file || !selected) return;
    if (file.size === 0 || file.size > 10 * 1024 * 1024) {
      setError("Choose a nonempty image no larger than 10 MiB.");
      return;
    }
    setBusy("Converting box art to WebP…");
    setError(undefined);
    setNotice(undefined);
    try {
      const updated = await uploadGameCover(selected, { file });
      setGames((previous) =>
        previous.map((game) =>
          Number(game.id) === Number(updated.id) ? updated : game,
        ),
      );
      setNotice("Game box art saved.");
    } catch (e) {
      showFailure(e);
    } finally {
      setBusy("");
    }
  }

  async function scrape() {
    const current = draft();
    const selected = gameId();
    if (!current || !selected || mismatch()) return;
    setBusy("Scraping items and converting images…");
    setError(undefined);
    setNotice(undefined);
    try {
      applyDraft(await scrapeGamePack(current.id, { gameId: selected }));
      setNotice(
        "Review the proposed images below. Your saved mappings have not changed.",
      );
    } catch (e) {
      showFailure(e);
    } finally {
      setBusy("");
    }
  }

  async function reload() {
    setBusy("Reloading current game data…");
    setError(undefined);
    setNotice(undefined);
    try {
      if (draft()?.state === "Ready")
        applyDraft(await getGameScrape(draft()!.id));
      else if (gameId()) {
        setReview(await getGameImageMappings(gameId()!));
        resetEdits();
      }
    } catch (e) {
      showFailure(e);
    } finally {
      setBusy("");
    }
  }

  async function save() {
    const current = review();
    if (!current || stale()) return;
    setBusy("Saving game images…");
    setError(undefined);
    setNotice(undefined);
    try {
      const request = {
        expectedRevision: current.revision,
        mappings: Object.entries(edits()).map(([name, candidateKey]) => ({
          name,
          candidateKey,
        })),
      };
      const result =
        draft()?.state === "Ready"
          ? await commitGameScrape(draft()!.id, request)
          : await saveGameImageMappings(Number(current.gameId), request);
      setReview(result);
      resetEdits();
      if (draft() && draft()!.state !== "Uploaded")
        setDraft({ ...draft()!, state: "Committed", review: result });
      setNotice("Game images and mappings saved.");
    } catch (e) {
      showFailure(e);
    } finally {
      setBusy("");
    }
  }

  function choose(name: string, candidateKey: string | null) {
    setEdits((previous) =>
      Object.assign(Object.create(null), previous, { [name]: candidateKey }),
    );
    setPicker(undefined);
  }
  function addName(event: SubmitEvent) {
    event.preventDefault();
    const name = newName().trim();
    if (!name || name.length > 500) return;
    if (rows().some((row) => row.name === name)) {
      setError("That item name already exists. Search for it in the table.");
      return;
    }
    setAddedNames((previous) => [...previous, name]);
    choose(name, null);
    setNewName("");
    setItemSearch(name);
    setPage(0);
    setError(undefined);
    setPicker(name);
    setCandidateSearch("");
    setCandidatePage(0);
  }
  const candidateLabel = (c: GameCandidateResponse) =>
    `${c.name}${c.stage === null ? "" : ` · Stage ${Number(c.stage) + 1}`}`;

  return (
    <div class="mx-auto flex max-w-6xl flex-col gap-6">
      <Card
        title="Game Scraper"
        subtitle="Import item images from a PopTracker pack, review the matches, then save"
        icon="icon-[tabler--photo-search]"
        color="ap-violet"
      >
        <div class="mt-4 grid gap-5 md:grid-cols-2">
          <fieldset class="fieldset" disabled={!!busy()}>
            <label for="game-filter" class="fieldset-legend">
              Game
            </label>
            <input
              id="game-filter"
              type="search"
              class="input w-full"
              placeholder="Search imported games"
              value={gameSearch()}
              onInput={(e) => setGameSearch(e.currentTarget.value)}
            />
            <select
              class="select w-full"
              aria-label="Select imported game"
              value={gameId() ?? ""}
              onChange={(e) => void changeGame(e.currentTarget.value)}
            >
              <option value="">Select a game</option>
              <For each={filteredGames()}>
                {(g) => <option value={g.id}>{g.name}</option>}
              </For>
            </select>
            <Show when={!games().length && !busy()}>
              <p class="text-warning">
                Import a session first to populate the game catalog.
              </p>
            </Show>
          </fieldset>
          <fieldset class="fieldset" disabled={!!busy()}>
            <label for="pack-zip" class="fieldset-legend">
              PopTracker ZIP
            </label>
            <input
              ref={fileInput}
              id="pack-zip"
              type="file"
              accept=".zip,application/zip"
              class="file-input w-full"
              onChange={(e) => void upload(e.currentTarget.files?.[0])}
            />
            <p class="text-base-content/60">
              Up to 50 MiB. Uploading validates the archive before scraping.
            </p>
          </fieldset>
        </div>
        <Show when={draft()}>
          {(d) => (
            <div class="mt-4 rounded-box bg-base-200 p-4 text-sm">
              <p class="font-bold">
                {d().manifest.name} · {d().manifest.version}
              </p>
              <p>{d().manifest.gameName}</p>
              <p class="mt-1 text-base-content/60">
                {d().state === "Uploaded"
                  ? "ZIP validated"
                  : d().state === "Ready"
                    ? "Review ready — not saved"
                    : "Saved"}
              </p>
            </div>
          )}
        </Show>
        <Show when={mismatch()}>
          <p class="mt-3 text-error" role="alert">
            This pack is for {draft()!.manifest.gameName}. Select the matching
            game.
          </p>
        </Show>
        <div class="mt-5 flex items-center justify-between gap-3">
          <p class="text-sm text-base-content/60">
            Later uploads preserve your saved choices.
          </p>
          <button
            class="btn btn-primary"
            disabled={
              !!busy() ||
              !gameId() ||
              draft()?.state !== "Uploaded" ||
              !!mismatch()
            }
            onClick={() => void scrape()}
          >
            Scrape
          </button>
        </div>
      </Card>
      <Show when={selectedGame()}>
        {(game) => (
          <Card
            title={`${game().name} box art`}
            subtitle="Upload a cover image for this game"
            color="ap-blue"
          >
            <div class="mt-4 flex flex-wrap items-start gap-5">
              <Show
                when={game().coverUrl}
                keyed
                fallback={
                  <div class="flex h-48 w-36 shrink-0 items-center justify-center rounded-box border border-base-300 bg-base-200 text-center text-sm text-base-content/50">
                    No box art
                  </div>
                }
              >
                {(url) => (
                  <img
                    src={url}
                    alt={`${game().name} box art`}
                    class="h-48 w-36 shrink-0 rounded-box border border-base-300 bg-base-200 object-contain p-1"
                  />
                )}
              </Show>
              <div class="flex min-w-56 flex-1 flex-col gap-3">
                <label for="cover-upload" class="fieldset-legend">
                  Upload box art
                </label>
                <input
                  ref={coverFileInput}
                  id="cover-upload"
                  type="file"
                  accept="image/png,image/jpeg,image/gif,image/webp"
                  class="file-input w-full"
                  disabled={!!busy()}
                  onChange={(e) => {
                    const file = e.currentTarget.files?.[0];
                    e.currentTarget.value = "";
                    void uploadCover(file);
                  }}
                />
                <p class="text-sm text-base-content/60">
                  PNG, JPEG, GIF or WebP, up to 10 MiB. Saved as lossless WebP.
                </p>
              </div>
            </div>
          </Card>
        )}
      </Show>
      <Show when={busy()}>
        <div class="flex items-center gap-3" role="status">
          <span class="loading loading-spinner" />
          {busy()}
        </div>
      </Show>
      <Show when={error()}>
        <div class="alert alert-error" role="alert">
          <span>{error()}</span>
          <Show when={stale()}>
            <button
              class="btn btn-sm"
              disabled={!!busy()}
              onClick={() => void reload()}
            >
              Reload review
            </button>
          </Show>
        </div>
      </Show>
      <Show when={notice()}>
        <div class="alert alert-info" role="status">
          {notice()}
        </div>
      </Show>
      <Show when={dirty()}>
        <p class="text-sm text-warning">
          You have unsaved edits. Changing the game or ZIP discards these edits.
        </p>
      </Show>
      <Show when={draft() && Number(draft()!.warningCount) > 0}>
        <details class="collapse collapse-arrow border border-warning/40 bg-base-100">
          <summary class="collapse-title font-semibold">
            {draft()!.warningCount} scrape warnings — usable images can still be
            saved
          </summary>
          <div class="collapse-content">
            <ul class="list-disc space-y-1 pl-5 text-sm">
              <For each={draft()!.warnings}>
                {(warning) => <li>{warning}</li>}
              </For>
            </ul>
            <Show
              when={Number(draft()!.warningCount) > draft()!.warnings.length}
            >
              <p>Showing the first {draft()!.warnings.length} warnings.</p>
            </Show>
          </div>
        </details>
      </Show>
      <Show when={review()}>
        {(current) => (
          <>
            <Show when={picker()}>
              {(name) => (
                <Card
                  title={`Choose an image for ${name()}`}
                  subtitle="Search tracker names, codes, AP IDs, variants or pack versions"
                  color="ap-blue"
                >
                  <div class="mt-4 flex gap-3">
                    <input
                      id="candidate-search"
                      type="search"
                      aria-label="Search image candidates"
                      class="input w-full"
                      value={candidateSearch()}
                      onInput={(e) => {
                        setCandidateSearch(e.currentTarget.value);
                        setCandidatePage(0);
                      }}
                    />
                    <button
                      class="btn btn-ghost"
                      onClick={() => setPicker(undefined)}
                    >
                      Close
                    </button>
                  </div>
                  <div class="mt-3 flex flex-wrap items-center justify-between gap-3">
                    <div class="flex gap-2">
                      <button
                        class="btn btn-sm"
                        disabled={!!busy()}
                        onClick={() => choose(name(), null)}
                      >
                        No image
                      </button>
                      <button
                        type="button"
                        class="btn btn-sm btn-outline"
                        disabled={!!busy()}
                        onClick={() => customImageInput?.click()}
                      >
                        Upload custom image
                      </button>
                    </div>
                    <span class="text-sm text-base-content/60">
                      {filteredCandidates().length} candidates
                    </span>
                    <input
                      ref={customImageInput}
                      type="file"
                      accept="image/png,image/jpeg,image/gif,image/webp"
                      class="hidden"
                      onChange={(e) => {
                        const file = e.currentTarget.files?.[0];
                        e.currentTarget.value = "";
                        void uploadCustomImage(file);
                      }}
                    />
                  </div>
                  <div class="mt-4 grid max-h-120 grid-cols-2 gap-3 overflow-y-auto md:grid-cols-3 lg:grid-cols-4">
                    <For
                      each={filteredCandidates().slice(
                        candidatePage() * 60,
                        (candidatePage() + 1) * 60,
                      )}
                    >
                      {(c) => (
                        <button
                          type="button"
                          disabled={!!busy()}
                          onClick={() => choose(name(), c.key)}
                          class="flex flex-col items-start gap-2 rounded-box border border-base-300 p-3 text-left hover:border-primary hover:bg-primary/5"
                          title={`${c.sourceFile}\nCodes: ${c.codes.join(", ")}\nAP IDs: ${c.apIds.join(", ")}`}
                        >
                          <Thumbnail url={c.imageUrl} large />
                          <span class="text-sm font-semibold">
                            {candidateLabel(c)}
                          </span>
                          <span class="text-xs text-base-content/60">
                            {c.variant === "Custom"
                              ? "Custom upload"
                              : `${c.variant || "Base"} · ${c.packVersion}`}{" "}
                            · {c.width}×{c.height}
                          </span>
                          <Show when={c.modifiers}>
                            <span class="text-xs text-warning">
                              Base image; modifiers omitted
                            </span>
                          </Show>
                          <Show when={!c.automaticEligible && !c.modifiers}>
                            <span class="text-xs text-base-content/60">
                              Manual selection
                            </span>
                          </Show>
                        </button>
                      )}
                    </For>
                  </div>
                  <Show when={!filteredCandidates().length}>
                    <p class="py-6 text-center text-base-content/60">
                      No matching images. Try a shorter search.
                    </p>
                  </Show>
                  <div class="mt-4 flex justify-end gap-3">
                    <button
                      class="btn btn-sm"
                      disabled={candidatePage() === 0}
                      onClick={() => setCandidatePage((p) => p - 1)}
                    >
                      Previous
                    </button>
                    <span class="self-center text-sm">
                      {candidatePage() + 1} / {candidatePageCount()}
                    </span>
                    <button
                      class="btn btn-sm"
                      disabled={candidatePage() + 1 >= candidatePageCount()}
                      onClick={() => setCandidatePage((p) => p + 1)}
                    >
                      Next
                    </button>
                  </div>
                </Card>
              )}
            </Show>
            <Card
              title="Item image mappings"
              subtitle={`${rows().length} canonical items · ${current().candidates.length} image candidates`}
              color="ap-green"
            >
              <div class="mt-4 flex flex-wrap items-center gap-3">
                <input
                  type="search"
                  class="input min-w-48 flex-1"
                  aria-label="Search item names"
                  placeholder="Search item names"
                  value={itemSearch()}
                  onInput={(e) => {
                    setItemSearch(e.currentTarget.value);
                    setPage(0);
                  }}
                />
                <label class="label cursor-pointer gap-2">
                  <input
                    type="checkbox"
                    class="checkbox checkbox-sm"
                    checked={unmappedOnly()}
                    onChange={(e) => {
                      setUnmappedOnly(e.currentTarget.checked);
                      setPage(0);
                    }}
                  />
                  Without images
                </label>
                <button
                  class="btn btn-ghost btn-sm"
                  disabled={!!busy()}
                  onClick={() => void reload()}
                >
                  Reload
                </button>
              </div>
              <div class="mt-3 overflow-x-auto">
                <table class="table">
                  <thead>
                    <tr>
                      <th>Item</th>
                      <th>Image</th>
                      <th>Match</th>
                      <th>
                        <span class="sr-only">Actions</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <For each={displayedRows()}>
                      {(row) => {
                        const selected = () => {
                          const key = keyFor(row);
                          return key ? candidates().get(key) : undefined;
                        };
                        return (
                          <tr>
                            <td class="font-semibold">{row.name}</td>
                            <td>
                              <div class="flex items-center gap-2">
                                <Thumbnail url={selected()?.imageUrl} />
                                <span class="max-w-48 text-xs">
                                  {selected()
                                    ? candidateLabel(selected()!)
                                    : "No image"}
                                </span>
                              </div>
                            </td>
                            <td class="text-xs text-base-content/65">
                              {isEdited(row.name)
                                ? "Manual · unsaved"
                                : row.evidence}
                            </td>
                            <td>
                              <button
                                class="btn btn-ghost btn-sm"
                                disabled={!!busy()}
                                onClick={() => {
                                  setPicker(row.name);
                                  setCandidateSearch("");
                                  setCandidatePage(0);
                                }}
                              >
                                Choose image
                              </button>
                            </td>
                          </tr>
                        );
                      }}
                    </For>
                  </tbody>
                </table>
              </div>
              <Show when={!displayedRows().length}>
                <p class="py-8 text-center text-base-content/60">
                  {rows().length
                    ? "No items match this filter."
                    : "No logged items yet. Add a canonical item name below, or save the candidate library for future imports."}
                </p>
              </Show>
              <div class="mt-3 flex justify-end gap-3">
                <button
                  class="btn btn-sm"
                  disabled={page() === 0}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </button>
                <span class="self-center text-sm">
                  {Math.min(page() + 1, pageCount())} / {pageCount()}
                </span>
                <button
                  class="btn btn-sm"
                  disabled={page() + 1 >= pageCount()}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </button>
              </div>
              <form
                class="mt-6 flex flex-wrap gap-3 border-t border-base-300 pt-5"
                onSubmit={addName}
              >
                <input
                  class="input min-w-48 flex-1"
                  aria-label="New canonical item name"
                  placeholder="Add an exact Archipelago item name"
                  maxlength="500"
                  value={newName()}
                  onInput={(e) => setNewName(e.currentTarget.value)}
                  disabled={!!busy()}
                />
                <button class="btn" disabled={!!busy() || !newName().trim()}>
                  Add item
                </button>
              </form>
              <div class="mt-6 flex items-center justify-between gap-4">
                <p class="text-sm text-base-content/60">
                  Unresolved items may remain without images. Clear suggestions
                  are saved automatically.
                </p>
                <button
                  class="btn btn-primary"
                  disabled={
                    !!busy() ||
                    stale() ||
                    (draft()?.state !== "Ready" &&
                      !dirty() &&
                      !current().items.some((i) => i.origin === "Suggested"))
                  }
                  onClick={() => void save()}
                >
                  Save
                </button>
              </div>
            </Card>
          </>
        )}
      </Show>
    </div>
  );
}
