import { useGetDatabaseInfo } from "../../api/generated/afterpelago";

export default function SystemInfoPage() {
  const databaseInfo = useGetDatabaseInfo();
  return (
    <div>
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
      </ul>
    </div>
  );
}
