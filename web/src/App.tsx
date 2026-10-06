import { QueryClientProvider } from '@tanstack/solid-query'
import { A, Navigate, Route, Router } from '@solidjs/router'
import { Match, Show, Switch } from 'solid-js'
import type { JSX, ParentProps } from 'solid-js'
import { useGetSession, useLogout } from './api/generated/afterpelago'
import AccessPendingPage from './pages/AccessPendingPage'
import DemoPage from './pages/DemoPage'
import LoginPage from './pages/LoginPage'
import { queryClient } from './lib/query-client'

function UserMenu() {
  const session = useGetSession()
  // A full reload after logout discards every cached private query and the CSRF token.
  const logout = useLogout({ mutation: { onSuccess: () => window.location.assign('/login') } })

  return (
    <Show when={session.data?.isAuthenticated ? session.data.user : undefined}>
      {(user) => (
        <div class="flex items-center gap-3">
          <span class="text-sm text-base-content/70" data-testid="current-user">{user().displayName}</span>
          <button class="btn btn-sm btn-ghost" disabled={logout.isPending} onClick={() => logout.mutate()} data-testid="logout">
            Sign out
          </button>
        </div>
      )}
    </Show>
  )
}

function Layout(props: ParentProps) {
  return (
    <div class="min-h-screen bg-base-200">
      <header class="navbar justify-between border-b border-base-300 bg-base-100 px-4 md:px-8">
        <A href="/demo" class="text-lg font-bold tracking-tight text-primary">Afterpelago</A>
        <UserMenu />
      </header>
      <main class="mx-auto max-w-5xl p-4 md:p-8">{props.children}</main>
    </div>
  )
}

/** Renders children only for an approved, signed-in user; the API enforces the same rule independently. */
function Protected(props: { children: JSX.Element }) {
  const session = useGetSession()
  return (
    <Switch>
      <Match when={session.isPending}>
        <p class="py-16 text-center"><span class="loading loading-spinner" /></p>
      </Match>
      <Match when={session.isError}>
        <p class="py-16 text-center text-error" role="alert">Could not reach the server: {session.error?.message}</p>
      </Match>
      <Match when={session.data?.isAuthenticated}>{props.children}</Match>
      <Match when={true}>
        <Navigate href="/login" />
      </Match>
    </Switch>
  )
}

function NotFound() {
  return (
    <div class="py-16 text-center">
      <h1 class="text-2xl font-semibold">Page not found</h1>
      <A href="/demo" class="link link-primary mt-4 inline-block">Back to the demo</A>
    </div>
  )
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Router root={Layout}>
        <Route path="/" component={() => <Navigate href="/demo" />} />
        <Route path="/login" component={LoginPage} />
        <Route path="/access-pending" component={AccessPendingPage} />
        <Route path="/demo" component={() => <Protected><DemoPage /></Protected>} />
        <Route path="*" component={NotFound} />
      </Router>
    </QueryClientProvider>
  )
}
