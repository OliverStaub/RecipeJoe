import createFetchClient from 'openapi-fetch';
import createClient from 'openapi-react-query';
import type { paths } from './schema';

const fetchClient = createFetchClient<paths>({
  baseUrl: globalThis.location.origin,
  // Late-bound so MSW's patched fetch is used in tests.
  fetch: (request) => globalThis.fetch(request),
});

export const $api = createClient(fetchClient);
