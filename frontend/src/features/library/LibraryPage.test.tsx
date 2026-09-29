import { screen, waitFor } from '@testing-library/react';
import { HttpResponse, delay } from 'msw';
import userEvent from '@testing-library/user-event';
import { http, server } from '@/test/server';
import { renderApp } from '@/test/render';

const summary = {
  id: 7,
  title: 'Kartoffelsuppe',
  sourceUrl: 'http://fixtures.test/e2e/recipe.html',
  hasImage: false,
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

it('invites to import when the Library is empty', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
  );
  renderApp();

  expect(
    await screen.findByText('Importiere dein erstes Rezept'),
  ).toBeInTheDocument();
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
