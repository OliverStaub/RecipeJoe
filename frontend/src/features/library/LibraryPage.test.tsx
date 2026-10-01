import { screen, waitFor, within } from '@testing-library/react';
import { HttpResponse, delay } from 'msw';
import userEvent from '@testing-library/user-event';
import type { components } from '@/api/schema';
import { http, server } from '@/test/server';
import { renderApp } from '@/test/render';

type Import = components['schemas']['ImportDto'];
type RecipeSummary = components['schemas']['RecipeSummaryDto'];

const pendingImport = {
  id: '11111111-1111-1111-1111-111111111111',
  url: 'http://www.x.test/recipes/a',
  kind: 'Web',
  state: 'Pending',
  stage: 'Extracting',
  failure: null,
} as const;

const failedImport = {
  id: '22222222-2222-2222-2222-222222222222',
  url: 'http://x.test/b',
  kind: 'Web',
  state: 'Failed',
  stage: null,
  failure: 'Unreachable',
} as const;

const dismissOnlyFailedImport = {
  ...failedImport,
  id: '33333333-3333-3333-3333-333333333333',
  url: 'http://x.test/c',
  failure: 'NotFound',
} as const;

const summary = {
  id: 7,
  title: 'Kartoffelsuppe',
  sourceUrl: 'http://fixtures.test/e2e/recipe.html',
  hasImage: false,
  isNew: false,
};

const searchBox = () =>
  screen.getByRole('searchbox', { name: 'Rezepte durchsuchen…' });

it('lists Recipes as links with title and Source host', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([summary])),
  );
  renderApp();

  const link = await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  expect(link).toHaveAttribute('href', '/recipes/7');
  expect(link).toHaveTextContent('fixtures.test');
});

it('shows the thumbnail when the Recipe has an image and a placeholder otherwise', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) =>
      response(200).json([
        { ...summary, hasImage: true },
        { ...summary, id: 8, title: 'Ohne Bild' },
      ]),
    ),
  );
  renderApp();

  const image = await screen.findByRole('img', { name: 'Kartoffelsuppe' });
  expect(image).toHaveAttribute('src', '/api/recipes/7/image');
  expect(screen.getAllByRole('img')).toHaveLength(1);
});

it('shows a "Neu" badge for a Recipe that has not been opened yet', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) =>
      response(200).json([
        { ...summary, isNew: true },
        { ...summary, id: 8, title: 'Schon gesehen', isNew: false },
      ]),
    ),
  );
  renderApp();

  const link = await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  expect(within(link).getByText('Neu')).toBeInTheDocument();
  const seenLink = screen.getByRole('link', { name: /Schon gesehen/ });
  expect(within(seenLink).queryByText('Neu')).not.toBeInTheDocument();
});

it('invites to import when the Library is empty', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
  );
  renderApp();

  expect(
    await screen.findByText('Importiere dein erstes Rezept'),
  ).toBeInTheDocument();
});

it('does not flash the invite message while Imports are still loading', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
    http.get('/api/imports', async ({ response }) => {
      await delay(300);
      return response(200).json([pendingImport]);
    }),
  );
  renderApp();

  // The Library resolves quickly (empty); Imports is still in flight.
  await delay(50);
  expect(
    screen.queryByText('Importiere dein erstes Rezept'),
  ).not.toBeInTheDocument();

  await screen.findByText('x.test/recipes/a');
});

it('says nothing matched when a search has no hits', async () => {
  server.use(
    http.get('/api/recipes', ({ request, response }) =>
      response(200).json(
        new URL(request.url).searchParams.has('q') ? [] : [summary],
      ),
    ),
  );
  const user = userEvent.setup();
  renderApp();
  await screen.findByRole('link', { name: /Kartoffelsuppe/ });

  await user.type(searchBox(), 'xyz');

  expect(await screen.findByText('Keine Rezepte zu „xyz"')).toBeInTheDocument();
  expect(
    screen.queryByText('Importiere dein erstes Rezept'),
  ).not.toBeInTheDocument();
});

it('sends the search as ?q= and seeds from the URL', async () => {
  const seen: (string | null)[] = [];
  server.use(
    http.get('/api/recipes', ({ request, response }) => {
      seen.push(new URL(request.url).searchParams.get('q'));
      return response(200).json([summary]);
    }),
  );
  renderApp('/?q=suppe');

  await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  expect(searchBox()).toHaveValue('suppe');
  await waitFor(() => expect(seen).toEqual(['suppe']));
});

it('does not claim "no hits" for a query whose results are still loading', async () => {
  server.use(
    http.get('/api/recipes', async ({ request, response }) => {
      const q = new URL(request.url).searchParams.get('q');
      if (q === 'suppe') await delay(200);
      return response(200).json(q ? [] : [summary]);
    }),
  );
  const user = userEvent.setup();
  renderApp();
  await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  await user.type(searchBox(), 'xyz');
  await screen.findByText('Keine Rezepte zu „xyz"');

  await user.clear(searchBox());
  await user.type(searchBox(), 'suppe');

  await waitFor(() =>
    expect(screen.queryByText(/Keine Rezepte zu/)).not.toBeInTheDocument(),
  );
});

it('says so when loading the Library fails', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) =>
      response.untyped(HttpResponse.json({}, { status: 500 })),
    ),
  );
  renderApp();

  expect(
    await screen.findByText('Rezepte konnten nicht geladen werden.'),
  ).toBeInTheDocument();
  expect(
    screen.queryByText('Importiere dein erstes Rezept'),
  ).not.toBeInTheDocument();
});

it('does not retry a client error', async () => {
  let requests = 0;
  server.use(
    http.get('/api/recipes', ({ response }) => {
      requests++;
      return response.untyped(HttpResponse.json({}, { status: 400 }));
    }),
  );
  renderApp();

  expect(
    await screen.findByText('Rezepte konnten nicht geladen werden.'),
  ).toBeInTheDocument();
  expect(requests).toBe(1);
});

function importRow(urlLabel: string) {
  const row = screen
    .getAllByRole('listitem')
    .find((li) => li.textContent?.includes(urlLabel));
  if (!row) throw new Error(`No row found for "${urlLabel}"`);
  return row;
}

it('shows a Pending Import as a row with its URL label and stage', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
    http.get('/api/imports', ({ response }) =>
      response(200).json([pendingImport]),
    ),
  );
  renderApp();

  expect(await screen.findByText('x.test/recipes/a')).toBeInTheDocument();
  expect(screen.getByText('Rezept wird gelesen…')).toBeInTheDocument();
  expect(
    screen.queryByText('Importiere dein erstes Rezept'),
  ).not.toBeInTheDocument();
});

it('shows a Failed Import with its message, offering retry only when it can help', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
    http.get('/api/imports', ({ response }) =>
      response(200).json([failedImport, dismissOnlyFailedImport]),
    ),
  );
  renderApp();
  await screen.findByText('x.test/b');

  const retryable = importRow('x.test/b');
  expect(
    within(retryable).getByText(
      'Die Seite ist nicht erreichbar. Prüfe die Adresse und versuch es nochmal.',
    ),
  ).toBeInTheDocument();
  expect(
    within(retryable).getByRole('button', { name: 'Erneut versuchen' }),
  ).toBeInTheDocument();
  within(retryable).getByRole('button', { name: 'Verwerfen' });

  const dismissOnly = importRow('x.test/c');
  expect(
    within(dismissOnly).getByText('Diese Seite existiert nicht.'),
  ).toBeInTheDocument();
  expect(
    within(dismissOnly).queryByRole('button', { name: 'Erneut versuchen' }),
  ).not.toBeInTheDocument();
  within(dismissOnly).getByRole('button', { name: 'Verwerfen' });
});

it('words a Failed Video Import for the video, offering retry only when it can help', async () => {
  const videoImport = {
    ...failedImport,
    kind: 'Video',
    url: 'https://www.youtube.com/shorts/abc',
    failure: 'LlmBadOutput',
  } as const;
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
    http.get('/api/imports', ({ response }) =>
      response(200).json([
        videoImport,
        {
          ...videoImport,
          id: '44444444-4444-4444-4444-444444444444',
          url: 'https://youtu.be/def',
          failure: 'NoCaptions',
        },
      ]),
    ),
  );
  renderApp();
  await screen.findByText('youtube.com/shorts/abc');

  const retryable = importRow('youtube.com/shorts/abc');
  within(retryable).getByText(
    'Die KI hat keine brauchbare Antwort geliefert. Versuch es nochmal.',
  );
  within(retryable).getByRole('button', { name: 'Erneut versuchen' });

  const dismissOnly = importRow('youtu.be/def');
  within(dismissOnly).getByText(
    'Dieses Video hat keine Untertitel, daraus kann kein Rezept gelesen werden.',
  );
  expect(
    within(dismissOnly).queryByRole('button', { name: 'Erneut versuchen' }),
  ).not.toBeInTheDocument();
});

it('retries a Failed Import, turning it back into a Pending row in place', async () => {
  let current: Import = failedImport;
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
    http.get('/api/imports', ({ response }) => response(200).json([current])),
    http.post('/api/imports/{id}/retry', ({ response }) => {
      current = {
        ...failedImport,
        state: 'Pending',
        stage: 'Fetching',
        failure: null,
      };
      return response(202).json(current);
    }),
  );
  const user = userEvent.setup();
  renderApp();
  await user.click(
    await screen.findByRole('button', { name: 'Erneut versuchen' }),
  );

  expect(await screen.findByText('Seite wird geladen…')).toBeInTheDocument();
  expect(
    screen.queryByRole('button', { name: 'Erneut versuchen' }),
  ).not.toBeInTheDocument();
});

it('dismisses a Failed Import, removing its row, without refetching the Library', async () => {
  let libraryLoads = 0;
  server.use(
    http.get('/api/recipes', ({ response }) => {
      libraryLoads++;
      return response(200).json([]);
    }),
    http.get('/api/imports', ({ response }) =>
      response(200).json([failedImport]),
    ),
    http.delete('/api/imports/{id}', ({ response }) => response(204).empty()),
  );
  const user = userEvent.setup();
  renderApp();
  await waitFor(() => expect(libraryLoads).toBe(1));
  await user.click(await screen.findByRole('button', { name: 'Verwerfen' }));

  await waitFor(() =>
    expect(screen.queryByText('x.test/b')).not.toBeInTheDocument(),
  );
  expect(libraryLoads).toBe(1);
});

it('keeps Import rows visible while searching', async () => {
  server.use(
    http.get('/api/recipes', ({ request, response }) =>
      response(200).json(
        new URL(request.url).searchParams.has('q') ? [] : [summary],
      ),
    ),
    http.get('/api/imports', ({ response }) =>
      response(200).json([failedImport]),
    ),
  );
  const user = userEvent.setup();
  renderApp();
  await screen.findByText('x.test/b');

  await user.type(searchBox(), 'xyz');

  await screen.findByText('Keine Rezepte zu „xyz"');
  expect(screen.getByText('x.test/b')).toBeInTheDocument();
});

it('shows a Recipe once its Import vanishes from the list', async () => {
  let imports: Import[] = [pendingImport];
  let recipes: RecipeSummary[] = [];
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json(recipes)),
    http.get('/api/imports', ({ response }) => response(200).json(imports)),
  );
  renderApp();
  await screen.findByText('x.test/recipes/a');

  imports = [];
  recipes = [summary];

  await waitFor(
    () =>
      expect(screen.queryByText('x.test/recipes/a')).not.toBeInTheDocument(),
    {
      timeout: 3000,
    },
  );
  expect(
    await screen.findByRole('link', { name: /Kartoffelsuppe/ }),
  ).toBeInTheDocument();
});
