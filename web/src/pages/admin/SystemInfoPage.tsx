import { createSignal } from "solid-js";
import {
  useGetDatabaseInfo,
  useRecalculateStatistics,
} from "../../api/generated/afterpelago";
import { ApiError } from "../../api/transport";

export default function SystemInfoPage() {
  const databaseInfo = useGetDatabaseInfo();
  const recalculate = useRecalculateStatistics();
  const [message, setMessage] = createSignal<string>();
  const [failed, setFailed] = createSignal(false);

  async function recalculateAll() {
    setMessage(undefined);
    setFailed(false);
    try {
      const result = await recalculate.mutateAsync({});
      setMessage(
        `Recalculated ${result.sessions} sessions and ${result.users} users.`,
      );
    } catch (error) {
      setFailed(true);
      setMessage(
        error instanceof ApiError
          ? (error.detail ?? error.message)
          : "Recalculation failed.",
      );
    }
  }

  return (
    <div class="flex flex-col gap-4">
      <ul class="list bg-base-100 rounded-box shadow-md">
        <li class="p-4 pb-2 text-xs opacity-60 tracking-wide">Server Info</li>

        <li class="list-row">
          <div>
            <span class="size-10 rounded-box icon-[tabler--database]" />
          </div>
          <div>
            <div>Database Info</div>
            <div class="text-xs font-semibold opacity-60">
              {databaseInfo.data?.path ?? "Unknown"}
            </div>
            <div class="text-xs uppercase font-semibold opacity-60">
              Size: {databaseInfo.data?.sizeGb ?? "Unknown"} GB
            </div>
          </div>
        </li>

        <li class="list-row">
          <div>
            <span class="size-10 rounded-box icon-[tabler--chart-bar]" />
          </div>
          <div class="min-w-0">
            <div>Statistics</div>
            <div class="text-xs font-semibold opacity-60">
              Rebuilds every session and user statistic from the imported data.
              Safe to run repeatedly.
            </div>
            {message() && (
              <div
                class={`text-xs font-semibold ${failed() ? "text-error" : "text-success"}`}
                role="status"
              >
                {message()}
              </div>
            )}
          </div>
          <button
            type="button"
            class="btn btn-sm btn-primary"
            disabled={recalculate.isPending}
            onClick={recalculateAll}
          >
            {recalculate.isPending ? (
              <span class="loading loading-spinner loading-xs" />
            ) : null}
            Recalculate all
          </button>
        </li>
      </ul>
    </div>
  );
}
