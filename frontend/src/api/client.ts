import createFetchClient, { type Middleware } from 'openapi-fetch';
import createClient from 'openapi-react-query';
import type { paths } from './schema';

/** A non-2xx response; `body` is the parsed JSON, the text, or `undefined` when empty. */
export class HttpError extends Error {
  readonly status: number;
  readonly body: unknown;

  constructor(status: number, body: unknown) {
    super(`HTTP ${status}`);
    this.status = status;
    this.body = body;
  }
}

// openapi-react-query throws only the body (and nothing for an empty one), losing the status.
const throwOnHttpError: Middleware = {
  async onResponse({ response }) {
    if (response.ok) return;
    const text = await response.text();
    let body: unknown = text || undefined;
    try {
      body = JSON.parse(text);
    } catch {
      // Not JSON; keep the text.
    }
    throw new HttpError(response.status, body);
  },
};

const fetchClient = createFetchClient<paths>({
  baseUrl: globalThis.location.origin,
  // Late-bound so MSW's patched fetch is used in tests.
  fetch: (request) => globalThis.fetch(request),
});
fetchClient.use(throwOnHttpError);

export const $api = createClient(fetchClient);
