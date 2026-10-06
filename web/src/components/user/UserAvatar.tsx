import { Show } from "solid-js";
import { useGetSession } from "../../api/generated/afterpelago";

interface UserAvatarProps {
  size: number;
}

function getAvatarUrl(
  userId: string,
  avatarHash: string | null | undefined,
  size: number,
) {
  const cdnSize = Math.min(4096, Math.max(16, 2 ** Math.ceil(Math.log2(size))));

  if (avatarHash) {
    const extension = avatarHash.startsWith("a_") ? "gif" : "png";
    return `https://cdn.discordapp.com/avatars/${userId}/${avatarHash}.${extension}?size=${cdnSize}`;
  }

  const defaultAvatarIndex = (BigInt(userId) >> 22n) % 6n;
  return `https://cdn.discordapp.com/embed/avatars/${defaultAvatarIndex}.png?size=${cdnSize}`;
}

export default function UserAvatar(props: UserAvatarProps) {
  const session = useGetSession();

  return (
    <Show when={session.data?.isAuthenticated ? session.data.user : undefined}>
      {(user) => (
        <div class="avatar">
          <div
            class="mask mask-hexagon-2"
            style={{ width: `${props.size}px`, height: `${props.size}px` }}
          >
            <img
              src={getAvatarUrl(
                user().discordUserId,
                user().avatarHash,
                props.size,
              )}
              alt={`${user().displayName} avatar`}
            />
          </div>
        </div>
      )}
    </Show>
  );
}
