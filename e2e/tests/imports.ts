import { expect, type APIRequestContext } from '@playwright/test';

/**
 * Starts a Web Import through the Imports API and waits for it to succeed, returning the Recipe's
 * id. Matched by Source URL, which must be unique per call (e.g. a `randomUUID()` token) since the
 * 3 browser projects run this file concurrently against one shared backend. Doesn't handle a
 * redirecting `url`: the saved Source is the page after the redirect, not `url` itself.
 */
export async function importViaApi(request: APIRequestContext, url: string) {
  const started = await request.post('/api/imports', { data: { url } });
  expect(started.status()).toBe(202);
  const { id } = (await started.json()) as { id: string };

  const deadline = Date.now() + 10_000;
  while (Date.now() < deadline) {
    const list = (await (await request.get('/api/imports')).json()) as {
      id: string;
    }[];
    if (!list.some((i) => i.id === id)) {
      const recipes = (await (await request.get('/api/recipes')).json()) as {
        id: number;
        sourceUrl: string;
      }[];
      const match = recipes.find((r) => r.sourceUrl === url);
      if (!match) throw new Error(`No Recipe found with Source ${url}`);
      return match.id;
    }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(`Import of ${url} did not finish in time`);
}
