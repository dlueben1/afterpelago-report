import AppBarLip from "./AppBarLip";
import UserMiniHero from "../user/UserMiniHero";

/**
 * The App Bar
 */
export default function AppBar() {
  return (
    <header
      class="
        relative isolate z-50
        h-17
        overflow-visible
        bg-[linear-gradient(105deg,#2f7bff_0%,#5363f5_52%,#a34cf3_100%)]
        text-white
        font-display
        shadow-[0_3px_14px_rgb(72_86_205/0.18)]
      "
    >
      {/* F a n c y L i p */}
      <AppBarLip />
      {/* TODO! <AppBarScenery /> */}

      <div
        class="
          relative z-10 gap-5
          flex h-full items-center
          px-5
        "
      >
        {/* Title */}
        <div class="flex items-center gap-3">
          <span
            class="
              icon-[tabler--beach]
              text-[2.35rem]
              drop-shadow-sm
            "
          />

          <span
            class="
              text-[1.55rem]
              font-extrabold
              tracking-tight
            "
          >
            Afterpelago
          </span>
        </div>

        {/* S p a c e */}
        <div class="flex-1" />

        {/* Account/User */}
        <UserMiniHero />

        {/* One action for now */}
        <button
          type="button"
          aria-label="Settings"
          class="
            btn btn-square
            border-0
            bg-white/10
            text-white
            shadow-none
            backdrop-blur-sm
            hover:bg-white/20
          "
        >
          <span class="icon-[tabler--settings] text-xl" />
        </button>
      </div>
    </header>
  );
}
