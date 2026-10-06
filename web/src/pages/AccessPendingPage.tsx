import { useSearchParams } from "@solidjs/router";
import { BackToSignIn } from "./LoginPage";

export default function AccessPendingPage() {
  const [params] = useSearchParams();
  const denied = () => params.status === "denied";

  return (
    <div
      class="mx-auto mt-10 max-w-md rounded-box border border-base-300 bg-base-100 p-8 text-center"
      data-testid="access-pending"
    >
      <h1 class="text-xl font-semibold">
        {denied() ? "Access not granted" : "Waiting for approval"}
      </h1>
      <p class="mt-3 text-base-content/70">
        {denied()
          ? "Your Discord account has not been granted access to this Afterpelago server."
          : "You signed in with Discord, but an admin still needs to approve your account. Ask them to approve you, then sign in again."}
      </p>
      <p class="mt-6 text-sm">
        <BackToSignIn />
      </p>
    </div>
  );
}
