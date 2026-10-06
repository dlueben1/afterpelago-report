import { useGetSession, useLogout } from "../../api/generated/afterpelago";
import { Show } from "solid-js";
import UserAvatar from "../user/UserAvatar";

export default function UserMiniHero() {
  const session = useGetSession();
  // A full reload after logout discards every cached private query and the CSRF token.
  const logout = useLogout({
    mutation: { onSuccess: () => window.location.assign("/login") },
  });

  return (
    <Show when={session.data?.isAuthenticated ? session.data.user : undefined}>
      {(user) => (
        <div class="dropdown dropdown-end font-sans">
          <button
            type="button"
            aria-haspopup="true"
            class="
              btn
              border-0
              bg-white/10
              text-white
              shadow-none
              backdrop-blur-sm
              hover:bg-white/20
            "
          >
            <div class="flex items-center gap-3">
              <UserAvatar size={32} />
              <span class="text-sm" data-testid="current-user">
                {user().displayName}
              </span>
            </div>
          </button>
          <ul
            tabIndex={0}
            class="
              dropdown-content menu
              z-50 mt-2 w-40
              rounded-box bg-base-100 p-2
              text-base-content shadow-lg
            "
          >
            <li>
              <button
                type="button"
                onClick={() => logout.mutate()}
                data-testid="logout"
              >
                Log out
              </button>
            </li>
          </ul>
        </div>
      )}
    </Show>
  );
}
