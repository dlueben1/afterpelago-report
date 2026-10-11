import { useNavigate, useSearchParams } from "@solidjs/router";
import { useQueryClient } from "@tanstack/solid-query";
import { For, Match, Show, Switch, createMemo, createSignal, onMount } from "solid-js";
import { getGetSessionsQueryKey, getSessionImport, importWebLog, publishSession } from "../../api/generated/afterpelago";
import type { SessionImportResponse } from "../../api/generated/model";
import { ApiError } from "../../api/transport";
import Card from "../../components/cards/Card";

const maximumFileSize = 25 * 1024 * 1024;
type WizardStep = "build" | "processing" | "review";

/** Formats an unknown request failure for an actionable alert. */
function errorMessage(error: unknown) {
  if (error instanceof ApiError) return error.detail ?? error.message;
  return error instanceof Error ? error.message : "Something went wrong.";
}

/** Formats a source timestamp without assigning it a timezone. */
function formatOriginalTimestamp(value: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})/.exec(value);

  if (!match) return value;

  const [, year, month, day, hourText, minute, second] = match;
  const hour = Number(hourText);
  const period = hour >= 12 ? "PM" : "AM";
  const displayHour = String(hour % 12 || 12).padStart(2, "0");

  return `${month}-${day}-${year} ${displayHour}:${minute}:${second} (${period})`;
}

/** Creates the whole-hour source-time choices around the raw log timestamp. */
function buildTimeChoices(originalStart: string) {
  const [date, time] = originalStart.split("T");
  const [year, month, day] = date.split("-").map(Number);
  const [hour, minute, secondText] = time.split(":");
  const [second, millisecond = "0"] = secondText.split(".");
  const values = [];
  for (let shift = -24; shift <= 24; shift += 1) {
    const shiftedWallClock = new Date(Date.UTC(year, month - 1, day, Number(hour) + shift, Number(minute), Number(second), Number(millisecond.slice(0, 3))));
    const parts = [shiftedWallClock.getUTCFullYear(), shiftedWallClock.getUTCMonth(), shiftedWallClock.getUTCDate(), shiftedWallClock.getUTCHours(), shiftedWallClock.getUTCMinutes(), shiftedWallClock.getUTCSeconds(), shiftedWallClock.getUTCMilliseconds()] as const;
    const local = new Date(...parts);
    if (local.getFullYear() !== parts[0] || local.getMonth() !== parts[1] || local.getDate() !== parts[2]
      || local.getHours() !== parts[3] || local.getMinutes() !== parts[4]) continue;
    const sourceOffsetMinutes = -local.getTimezoneOffset() - shift * 60;
    if (sourceOffsetMinutes < -840 || sourceOffsetMinutes > 840) continue;
    values.push({ shift, sourceOffsetMinutes,
      preview: local.toLocaleString(undefined, { dateStyle: "full", timeStyle: "medium" }),
      utc: new Date(Date.UTC(year, month - 1, day, Number(hour), Number(minute), Number(second), Number(millisecond.slice(0, 3))) - sourceOffsetMinutes * 60_000),
    });
  }
  return values;
}

export default function ImportSessionPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const importId = () => Array.isArray(searchParams.importId) ? searchParams.importId[0] : searchParams.importId;
  const [step, setStep] = createSignal<WizardStep>(importId() ? "processing" : "build");
  const [name, setName] = createSignal("");
  const [file, setFile] = createSignal<File>();
  const [result, setResult] = createSignal<SessionImportResponse>();
  const [selectedShift, setSelectedShift] = createSignal<number>();
  const [error, setError] = createSignal<string>();
  const [recoveryMissing, setRecoveryMissing] = createSignal(false);
  const choices = createMemo(() => (result() ? buildTimeChoices(result()!.originalStart) : []));
  const selectedChoice = createMemo(() => choices().find((choice) => choice.shift === selectedShift()));
  const isValid = createMemo(() => {
    const selected = file();
    return name().trim().length > 0 && name().trim().length <= 200 && !!selected?.name.toLowerCase().endsWith(".txt") && selected.size > 0 && selected.size <= maximumFileSize;
  });

  /** Recovers a completed draft after a refresh or copied URL. */
  onMount(async () => {
    const requestId = importId();
    if (!requestId) return;
    try {
      setResult(await getSessionImport(requestId));
      setStep("review");
    } catch {
      setRecoveryMissing(true);
      setError("This import did not finish. Select the original file to retry with the same import ID.");
      setStep("build");
    }
  });

  /** Uploads and processes the selected WebLog. */
  async function submitImport(event: SubmitEvent) {
    event.preventDefault();
    const selected = file();
    if (!selected || !isValid()) return;
    setError(undefined);
    setStep("processing");
    const requestId = importId() ?? crypto.randomUUID();
    setSearchParams({ importId: requestId }, { replace: true });
    try {
      setResult(await importWebLog(requestId, { name: name().trim(), file: selected }));
      setRecoveryMissing(false);
      setStep("review");
    } catch (requestError) {
      setError(errorMessage(requestError));
      setStep("build");
    }
  }

  /** Publishes the reviewed draft with the selected source offset. */
  async function finish() {
    const imported = result();
    const choice = selectedChoice();
    if (!imported || !choice) return;
    setError(undefined);
    try {
      await publishSession(Number(imported.sessionId), { sourceUtcOffsetMinutes: choice.sourceOffsetMinutes });
      await queryClient.invalidateQueries({ queryKey: getGetSessionsQueryKey() });
      navigate("/sessions");
    } catch (requestError) { setError(errorMessage(requestError)); }
  }

  return (
    <div class="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <Card title="Import an Archipelago session" subtitle="Build a session report from a WebWorld log" icon="icon-[tabler--file-upload]" color="ap-violet">
        <ul class="steps mt-4 w-full select-none"><For each={[{ id: "build", label: "Build Session" }, { id: "processing", label: "Processing" }, { id: "review", label: "Review & Finish" }] as const}>{(item) => {
          const active = () => ["build", "processing", "review"].indexOf(item.id) <= ["build", "processing", "review"].indexOf(step());
          return <li class={`step ${active() ? "step-primary" : ""}`} aria-current={step() === item.id ? "step" : undefined}>{item.label}</li>;
        }}</For></ul>
      </Card>
      <Show when={error()}><div class="alert alert-error" role="alert"><span class="icon-[tabler--alert-circle] size-5" /><span>{error()}</span></div></Show>
      <Switch>
        <Match when={step() === "build"}>
          <Card title="Session details" subtitle="Choose a name and the downloaded WebWorld log" color="ap-blue">
            <form class="mt-4 flex flex-col gap-5" onSubmit={submitImport}>
              <fieldset class="fieldset"><label class="fieldset-legend" for="session-name">Session name</label><input id="session-name" class="input w-full" maxlength="200" required value={name()} onInput={(event) => setName(event.currentTarget.value)} placeholder="Winter multiworld" /></fieldset>
              <fieldset class="fieldset"><label class="fieldset-legend" for="log-file">WebLog file (.txt)</label><input id="log-file" type="file" class="file-input w-full" accept=".txt,text/plain" required onChange={(event) => setFile(event.currentTarget.files?.[0])} /><Show when={file()}>{(selected) => <p class="label">{selected().name} · {(selected().size / 1024).toFixed(1)} KiB</p>}</Show></fieldset>
              <Show when={recoveryMissing()}><p class="text-sm text-warning">Retrying will reuse the import ID from this page.</p></Show>
              <button class="btn btn-secondary self-end" type="submit" disabled={!isValid()}>Import session</button>
            </form>
          </Card>
        </Match>
        <Match when={step() === "processing"}><Card title="Processing WebLog" subtitle="The page will advance when the server finishes" color="ap-gold"><div class="flex min-h-48 flex-col items-center justify-center gap-4"><span class="loading loading-spinner loading-lg text-primary" /><p>Please keep this page open.</p></div></Card></Match>
        <Match when={step() === "review" && result()}>{(imported) => <div class="flex flex-col gap-6">
          <Card title={imported().name} subtitle={`${imported().counts.slots} slots · ${imported().counts.checks} checks · ${imported().counts.hints} hints`} color="ap-green">
            <div class="mt-4 overflow-x-auto"><table class="table"><thead><tr><th>Player</th><th>Team</th><th>Game</th><th>Goal</th></tr></thead><tbody><For each={imported().slots}>{(slot) => <tr><td class="font-semibold">{slot.playerName}</td><td>#{slot.teamNumber}</td><td>{slot.gameName ?? <span class="text-warning">Unknown game</span>}</td><td>{slot.goalCompleted ? "Completed" : "Not completed"}</td></tr>}</For></tbody></table></div>
          </Card>
          <Card title="Set the session time" subtitle={`The log starts at ${formatOriginalTimestamp(imported().originalStart)}. Choose when that was in ${Intl.DateTimeFormat().resolvedOptions().timeZone}.`} color="ap-blue">
            <fieldset class="fieldset mt-4"><label class="fieldset-legend" for="time-shift">Remembered start time</label><select id="time-shift" class="select w-full" value={selectedShift() ?? ""} onChange={(event) => setSelectedShift(Number(event.currentTarget.value))}><option value="" disabled>Select a time</option><For each={choices()}>{(choice) => <option value={choice.shift}>{choice.preview} ({choice.shift >= 0 ? "+" : ""}{choice.shift} hours)</option>}</For></select></fieldset>
            <Show when={selectedChoice()}>{(choice) => <div class="alert alert-info mt-4"><span>Source UTC offset: {choice().sourceOffsetMinutes >= 0 ? "+" : ""}{choice().sourceOffsetMinutes} minutes. UTC start: {choice().utc.toISOString()}</span></div>}</Show>
          </Card>
          <Show when={Number(imported().warningCount) > 0}><Card title={`${imported().warningCount} import warnings`} subtitle="The session can still be published" color="ap-gold"><ul class="mt-4 space-y-2 text-sm"><For each={imported().warnings}>{(warning) => <li><span class="font-bold">Line {warning.lineNumber}:</span> {warning.reason}</li>}</For></ul></Card></Show>
          <button class="btn btn-primary btn-lg self-end" disabled={!selectedChoice()} onClick={finish}>Finish</button>
        </div>}</Match>
      </Switch>
    </div>
  );
}
