import Card from "../../components/cards/Card";

export default function AllSessionsPage() {
  return (
    <div class="flex flex-col gap-4">
      {/* Top Section */}
      <div class="grid min-w-0 gap-4 lg:grid-cols-12">
        <Card
          title="Archipelago Sessions"
          subtitle="Every session, across your entire playgroup"
          icon="icon-[tabler--device-gamepad-filled]"
          color="ap-blue"
          class="min-h-32 lg:col-span-5"
        />
        <div
          class="hidden min-h-32 lg:col-span-7 lg:block"
          aria-hidden="true"
        />
      </div>

      {/* Filters */}
      <Card color="ap-grid" class="min-h-16" />

      {/* Main Content */}
      <div class="grid min-w-0 items-stretch gap-4 lg:grid-cols-[minmax(0,3fr)_minmax(0,1fr)]">
        <Card color="ap-violet" class="min-h-96 lg:min-h-136" />

        <div class="grid min-w-0 gap-4 sm:grid-cols-3 lg:grid-cols-1">
          <Card color="ap-orange" class="min-h-40" />
          <Card color="ap-orange" class="min-h-40" />
          <Card color="ap-orange" class="min-h-40" />
        </div>
      </div>
    </div>
  );
}
