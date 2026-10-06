import { MutationCache, QueryCache, QueryClient } from "@tanstack/solid-query";
import { getGetSessionQueryKey } from "../api/generated/afterpelago";
import { ApiError } from "../api/transport";

const isClientError = (error: unknown) =>
  error instanceof ApiError && error.status >= 400 && error.status < 500;

// A private request came back 401 (session ended or access revoked): re-check the session so the UI returns to sign-in.
const onUnauthorized = (error: unknown) => {
  if (error instanceof ApiError && error.status === 401) {
    void queryClient.invalidateQueries({ queryKey: getGetSessionQueryKey() });
  }
};

// Statistics endpoints will be read-heavy: cache for 30s, keep unused data for 5 minutes, and never retry 4xx.
export const queryClient: QueryClient = new QueryClient({
  queryCache: new QueryCache({ onError: onUnauthorized }),
  mutationCache: new MutationCache({ onError: onUnauthorized }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: 5 * 60_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => !isClientError(error) && failureCount < 2,
    },
    mutations: { retry: false },
  },
});
