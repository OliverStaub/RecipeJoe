import { QueryClient } from '@tanstack/react-query';
import { HttpError } from '@/api/client';

const isClientError = (error: unknown) =>
  error instanceof HttpError && error.status >= 400 && error.status < 500;

/** The one query-client configuration, used by the app and every test. */
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // A 4xx won't change on retry; anything else (network, 5xx) gets one more try.
        retry: (failureCount, error) =>
          failureCount < 1 && !isClientError(error),
        // Short enough to keep a blip unnoticed and the test suite fast.
        retryDelay: 200,
      },
      mutations: { retry: false },
    },
  });
}
