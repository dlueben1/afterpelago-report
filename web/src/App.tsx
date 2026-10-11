import { QueryClientProvider } from "@tanstack/solid-query";
import { A, Navigate, Route, Router } from "@solidjs/router";
import { Match, Show, Switch } from "solid-js";
import type { JSX, ParentProps } from "solid-js";
import { useGetSession } from "./api/generated/afterpelago";
import AccessPendingPage from "./pages/AccessPendingPage";
import LoginPage from "./pages/LoginPage";
import { queryClient } from "./lib/query-client";
import AppBar from "./components/nav/AppBar";
import SidebarContent from "./components/nav/SidebarContent";
import { isAdmin } from "./lib/auth";
import AllSessionsPage from "./pages/session/AllSessionsPage";
import SessionDetailsPage from "./pages/session/SessionDetailsPage";
import TimelinePage from "./pages/session/TimelinePage";
import SessionStatsPage from "./pages/session/SessionStatsPage";
import SlotStatsPage from "./pages/session/SlotStatsPage";
import SystemInfoPage from "./pages/admin/SystemInfoPage";
import ImportSessionPage from "./pages/admin/ImportSessionPage";
import UserAdminPage from "./pages/admin/UserAdminPage";
import GameScraperPage from "./pages/admin/GameScraperPage";

function Layout(props: ParentProps) {
  const session = useGetSession();
  return (
    <div class="min-h-screen bg-base-200 flex flex-col">
      {/* Top-Level AppBar */}
      <AppBar />

      <div class="flex flex-1 flex-col md:flex-row">
        {/* Sidebar Navigation */}
        <Show when={session.data?.isAuthenticated}>
          <aside class="w-full shrink-0 border-r border-base-300 bg-base-100 p-4 md:w-64">
            <SidebarContent role={session.data?.user?.role} />
          </aside>
        </Show>

        {/* Main Content */}
        <main class="mx-auto w-full min-w-0 max-w-7xl flex-1 p-4 md:p-8">
          {props.children}
        </main>
      </div>
    </div>
  );
}

/** Renders children only for an approved, signed-in user; the API enforces the same rule independently. */
function Protected(props: { children: JSX.Element; adminOnly?: boolean }) {
  const session = useGetSession();
  return (
    <Switch>
      <Match when={session.isPending}>
        <p class="py-16 text-center">
          <span class="loading loading-spinner" />
        </p>
      </Match>
      <Match when={session.isError}>
        <p class="py-16 text-center text-error" role="alert">
          Could not reach the server: {session.error?.message}
        </p>
      </Match>
      <Match
        when={
          session.data?.isAuthenticated &&
          (!props.adminOnly || isAdmin(session.data.user?.role))
        }
      >
        {props.children}
      </Match>
      <Match when={session.data?.isAuthenticated}>
        <p class="py-16 text-center text-error" role="alert">
          You do not have access to this page.
        </p>
      </Match>
      <Match when={true}>
        <Navigate href="/login" />
      </Match>
    </Switch>
  );
}

function NotFound() {
  return (
    <div class="py-16 text-center">
      <h1 class="text-2xl font-semibold">Page not found</h1>
      <A href="/sessions" class="link link-primary mt-4 inline-block">
        Back to the home page
      </A>
    </div>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <Router root={Layout}>
        {/* The real dashboard returns at "/" in Milestone 9; until then it redirects. */}
        <Route path="/" component={() => <Navigate href="/sessions" />} />
        <Route path="/demo" component={() => <Navigate href="/sessions" />} />
        <Route path="/login" component={LoginPage} />
        <Route path="/access-pending" component={AccessPendingPage} />
        <Route
          path="/sessions"
          component={() => (
            <Protected>
              <AllSessionsPage />
            </Protected>
          )}
        />
        <Route
          path="/sessions/:id"
          component={() => (
            <Protected>
              <SessionDetailsPage />
            </Protected>
          )}
        />
        <Route
          path="/sessions/:id/timeline"
          component={() => (
            <Protected>
              <TimelinePage />
            </Protected>
          )}
        />
        <Route
          path="/sessions/:id/stats"
          component={() => (
            <Protected>
              <SessionStatsPage />
            </Protected>
          )}
        />
        <Route
          path="/sessions/:id/slot/:slotId"
          component={() => (
            <Protected>
              <SlotStatsPage />
            </Protected>
          )}
        />
        <Route
          path="/system"
          component={() => (
            <Protected adminOnly>
              <SystemInfoPage />
            </Protected>
          )}
        />
        <Route
          path="/users"
          component={() => (
            <Protected adminOnly>
              <UserAdminPage />
            </Protected>
          )}
        />
        <Route
          path="/create"
          component={() => (
            <Protected adminOnly>
              <ImportSessionPage />
            </Protected>
          )}
        />
        <Route path="*" component={NotFound} />
        <Route
          path="/game-scraper"
          component={() => (
            <Protected adminOnly>
              <GameScraperPage />
            </Protected>
          )}
        />
      </Router>
    </QueryClientProvider>
  );
}
