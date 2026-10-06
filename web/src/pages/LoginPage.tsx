import { A, Navigate, useSearchParams } from '@solidjs/router'
import { Match, Show, Switch } from 'solid-js'
import { useGetSession } from '../api/generated/afterpelago'

export default function LoginPage(props: { returnUrl?: string }) {
  const session = useGetSession()
  const [params] = useSearchParams()
  const returnUrl = () => encodeURIComponent(props.returnUrl ?? '/demo')

  return (
    <Switch>
      <Match when={session.isPending}>
        <p class="py-16 text-center"><span class="loading loading-spinner" /></p>
      </Match>
      <Match when={session.isError}>
        <p class="py-16 text-center text-error" role="alert">Could not reach the server: {session.error?.message}</p>
      </Match>
      <Match when={session.data?.isAuthenticated}>
        <Navigate href="/demo" />
      </Match>
      <Match when={session.data}>
        {(data) => (
          <div class="mx-auto mt-10 max-w-sm rounded-box border border-base-300 bg-base-100 p-8 text-center">
            <h1 class="text-2xl font-bold text-primary">Afterpelago</h1>
            <p class="mt-2 text-base-content/70">Sign in to see your group's Archipelago stats.</p>

            <Show when={params.signin === 'failed'}>
              <p class="mt-4 text-sm text-error" role="alert" data-testid="signin-failed">
                Sign-in did not complete. Please try again.
              </p>
            </Show>

            <Show
              when={data().discordConfigured}
              fallback={<p class="mt-6 text-sm text-warning-content" data-testid="discord-unconfigured">Discord sign-in is not configured on this server.</p>}
            >
              <a class="btn btn-primary mt-6 w-full" rel="external" href={`/api/auth/login?returnUrl=${returnUrl()}`} data-testid="signin-discord">
                Sign in with Discord
              </a>
            </Show>

            <Show when={data().devLoginAvailable}>
              <a class="btn btn-outline mt-3 w-full" rel="external" href={`/api/auth/dev-login?returnUrl=${returnUrl()}`} data-testid="signin-dev">
                Development sign-in
              </a>
            </Show>
          </div>
        )}
      </Match>
    </Switch>
  )
}

export function BackToSignIn() {
  return <A href="/login" class="link link-primary">Back to sign in</A>
}
