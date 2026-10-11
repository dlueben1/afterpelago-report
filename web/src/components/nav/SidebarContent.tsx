import { A, useLocation } from "@solidjs/router";
import { For, Show } from "solid-js";
import { isAdmin } from "../../lib/auth";

type SidebarItem = {
  label: string;
  href: string;
  icon: string;
};

const items: SidebarItem[] = [
  { label: "Dashboard", href: "/demo", icon: "icon-[tabler--home-2]" },
  {
    label: "Sessions",
    href: "/sessions",
    icon: "icon-[tabler--device-gamepad]",
  },
  { label: "My Stats", href: "/me", icon: "icon-[tabler--user]" },
  { label: "My Playgroup", href: "/us", icon: "icon-[tabler--users]" },
];

const adminItems: SidebarItem[] = [
  {
    label: "User Management",
    href: "/users",
    icon: "icon-[tabler--user-bolt]",
  },
  {
    label: "Import Session",
    href: "/create",
    icon: "icon-[tabler--device-gamepad]",
  },
  {
    label: "Server Info",
    href: "/system",
    icon: "icon-[tabler--server-cog]",
  },
];

export default function SidebarContent(props: { role?: string | null }) {
  const location = useLocation();
  const admin = () => isAdmin(props.role);

  return (
    <nav aria-label="Main navigation">
      <ul class="menu w-full gap-1 px-2 py-3 text-sm">
        <For each={items}>
          {(item) => {
            const active = () => location.pathname === item.href
              || (item.href === "/sessions" && location.pathname.startsWith("/sessions/"));

            return (
              <li>
                <A
                  href={item.href}
                  aria-current={active() ? "page" : undefined}
                  class={`relative flex min-h-10 items-center gap-3 rounded-lg px-3 font-sans ${
                    active()
                      ? "bg-primary/10 font-extrabold text-primary before:absolute before:inset-y-1 before:left-0 before:w-0.75 before:rounded-r-full before:bg-primary"
                      : "text-base-content/70 hover:bg-base-200 hover:text-base-content"
                  }`}
                >
                  <span
                    class={`${item.icon} size-5 shrink-0`}
                    aria-hidden="true"
                  />
                  {item.label}
                </A>
              </li>
            );
          }}
        </For>
        <Show when={admin()}>
          <li class="menu-title select-none">Admin</li>
          <For each={adminItems}>
            {(item) => {
              const active = () => location.pathname === item.href;

              return (
                <li>
                  <A
                    href={item.href}
                    aria-current={active() ? "page" : undefined}
                    class={`relative flex min-h-10 items-center gap-3 rounded-lg px-3 font-sans ${
                      active()
                        ? "bg-primary/10 font-extrabold text-primary before:absolute before:inset-y-1 before:left-0 before:w-0.75 before:rounded-r-full before:bg-primary"
                        : "text-base-content/70 hover:bg-base-200 hover:text-base-content"
                    }`}
                  >
                    <span
                      class={`${item.icon} size-5 shrink-0`}
                      aria-hidden="true"
                    />
                    {item.label}
                  </A>
                </li>
              );
            }}
          </For>
        </Show>
      </ul>
    </nav>
  );
}
