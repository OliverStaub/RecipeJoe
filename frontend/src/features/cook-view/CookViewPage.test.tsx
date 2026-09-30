import { HttpResponse } from 'msw';
import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, server } from '@/test/server';
import { recipe, renderApp } from '@/test/render';

it('shows title, ingredients and steps with line breaks kept', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  renderApp('/recipes/7');

  expect(
    await screen.findByRole('heading', { name: 'Kartoffelsuppe' }),
  ).toBeInTheDocument();
  expect(screen.getByRole('heading', { name: 'Zutaten' })).toBeInTheDocument();
  expect(screen.getByText('500 g Kartoffeln')).toBeInTheDocument();
  expect(
    screen.getByRole('heading', { name: 'Zubereitung' }),
  ).toBeInTheDocument();
  expect(screen.getByText(/Kartoffeln schälen/)).toHaveClass(
    'whitespace-pre-line',
  );
  expect(screen.getByRole('link', { name: /Rezepte/ })).toHaveAttribute(
    'href',
    '/',
  );
});

it('shows the image in a 4:3 frame when the Recipe has one', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) =>
      response(200).json({ ...recipe, imageUrl: '/api/recipes/7/image' }),
    ),
  );
  renderApp('/recipes/7');

  const image = await screen.findByRole('img', { name: 'Kartoffelsuppe' });
  expect(image).toHaveAttribute('src', '/api/recipes/7/image');
  expect(image.closest('[data-slot="aspect-ratio"]')).toHaveStyle({
    '--ratio': '1.3333333333333333',
  });
});

it('omits the image when the Recipe has none', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  renderApp('/recipes/7');

  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  expect(screen.queryByRole('img')).not.toBeInTheDocument();
});

it('says so when the id is not a number', () => {
  renderApp('/recipes/abc');

  expect(screen.getByText('Rezept nicht gefunden.')).toBeInTheDocument();
});

it('says so when loading the Recipe fails', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) => response(404).empty()),
  );
  renderApp('/recipes/99');

  expect(
    await screen.findByText('Rezept konnte nicht geladen werden.'),
  ).toBeInTheDocument();
});

it('retries a server error once, then says loading failed', async () => {
  let requests = 0;
  server.use(
    http.get('/api/recipes/{id}', ({ response }) => {
      requests++;
      return response.untyped(new HttpResponse(null, { status: 500 }));
    }),
  );
  renderApp('/recipes/7');

  expect(
    await screen.findByText('Rezept konnte nicht geladen werden.'),
  ).toBeInTheDocument();
  expect(requests).toBe(2);
});

it('shows the Recipe when a server error succeeds on retry', async () => {
  let requests = 0;
  server.use(
    http.get('/api/recipes/{id}', ({ response }) =>
      ++requests === 1
        ? response.untyped(new HttpResponse(null, { status: 500 }))
        : response(200).json(recipe),
    ),
  );
  renderApp('/recipes/7');

  expect(
    await screen.findByRole('heading', { name: 'Kartoffelsuppe' }),
  ).toBeInTheDocument();
  expect(requests).toBe(2);
});

it('shows Servings and formatted times, each only when present', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) =>
      response(200).json({
        ...recipe,
        servings: '4 bis 6 Portionen',
        prepMinutes: 15,
        cookMinutes: null,
        totalMinutes: 75,
      }),
    ),
  );
  renderApp('/recipes/7');
  expect(await screen.findByText('4 bis 6 Portionen')).toBeInTheDocument();
  expect(screen.getByText('Vorbereitung: 15 Min.')).toBeInTheDocument();
  expect(screen.getByText('Gesamt: 1 Std. 15 Min.')).toBeInTheDocument();
  expect(screen.queryByText(/Kochen:/)).not.toBeInTheDocument();
});

it('shows no Servings or time badges when the Recipe has none', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  renderApp('/recipes/7');
  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  expect(screen.queryByText(/Vorbereitung|Kochen:|Gesamt/)).toBeNull();
});

it('links the Source in the footer and the menu', async () => {
  server.use(
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  renderApp('/recipes/7');
  expect(
    await screen.findByRole('link', { name: 'Von fixtures' }),
  ).toHaveAttribute('href', 'http://fixtures/e2e/recipe.html');
  await userEvent.click(screen.getByRole('button', { name: 'Mehr' }));
  expect(
    await screen.findByRole('menuitem', { name: 'Quelle öffnen' }),
  ).toHaveAttribute('href', 'http://fixtures/e2e/recipe.html');
});

describe('Wake Lock badge', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('says the screen stays on when supported', async () => {
    vi.stubGlobal('navigator', {
      wakeLock: {
        request: async () =>
          Object.assign(new EventTarget(), { release: async () => {} }),
      },
    });
    server.use(
      http.get('/api/recipes/{id}', ({ response }) =>
        response(200).json(recipe),
      ),
    );
    renderApp('/recipes/7');
    expect(await screen.findByText('Bildschirm bleibt an')).toBeInTheDocument();
  });

  it('says it is unavailable when unsupported', async () => {
    server.use(
      http.get('/api/recipes/{id}', ({ response }) =>
        response(200).json(recipe),
      ),
    );
    renderApp('/recipes/7');
    expect(
      await screen.findByText('Bildschirmsperre nicht verfügbar'),
    ).toBeInTheDocument();
  });
});

it('requests the Recipe by its id', async () => {
  const ids: unknown[] = [];
  server.use(
    http.get('/api/recipes/{id}', ({ params, response }) => {
      ids.push(params.id);
      return response(200).json(recipe);
    }),
  );
  renderApp('/recipes/7');

  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  expect(ids).toEqual(['7']);
});
