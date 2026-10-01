import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay } from 'msw';
import { HttpResponse } from 'msw';
import type { components } from '@/api/schema';
import { http, server } from '@/test/server';
import { recipe, renderApp } from '@/test/render';

async function openDialog() {
  const user = userEvent.setup();
  renderApp();
  await user.click(screen.getByRole('button', { name: 'Importieren' }));
  return user;
}

const urlInput = () => screen.getByRole('textbox', { name: 'Webadresse' });

it('shows the failure message and clears it when the URL is edited', async () => {
  server.use(
    http.post('/api/recipes/import', ({ response }) =>
      response(422).json(
        { type: 't', title: 'x', status: 422, kind: 'NoRecipe' },
        { headers: { 'content-type': 'application/problem+json' } },
      ),
    ),
  );
  const user = await openDialog();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(screen.getByRole('button', { name: 'Importieren' }));

  expect(await screen.findByText('Import fehlgeschlagen')).toBeInTheDocument();
  expect(
    screen.getByText('Auf dieser Seite wurde kein Rezept gefunden.'),
  ).toBeInTheDocument();

  await user.type(urlInput(), 'b');

  expect(screen.queryByText('Import fehlgeschlagen')).not.toBeInTheDocument();
});

it('disables the form and cannot be closed while importing', async () => {
  server.use(
    http.post('/api/recipes/import', async ({ response }) => {
      await delay(200);
      return response(201).json(recipe);
    }),
  );
  const user = await openDialog();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(screen.getByRole('button', { name: 'Importieren' }));

  expect(await screen.findByText('Wird importiert…')).toBeInTheDocument();
  expect(urlInput()).toBeDisabled();

  await user.keyboard('{Escape}');

  expect(screen.getByRole('dialog')).toBeInTheDocument();
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
});

it('closes, toasts and opens the Cook View on success', async () => {
  server.use(
    http.post('/api/recipes/import', ({ response }) =>
      response(201).json(recipe),
    ),
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  const user = await openDialog();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(screen.getByRole('button', { name: 'Importieren' }));

  expect(
    await screen.findByRole('heading', { name: 'Kartoffelsuppe' }),
  ).toBeInTheDocument();
  expect(await screen.findAllByText('Rezept importiert')).not.toHaveLength(0);
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

const importButton = () => screen.getByRole('button', { name: 'Importieren' });
const dialogSubmit = () =>
  within(screen.getByRole('dialog')).getByRole('button', {
    name: 'Importieren',
  });

it('only enables submit for a non-blank URL', async () => {
  const user = await openDialog();
  expect(dialogSubmit()).toBeDisabled();

  await user.type(urlInput(), '   ');
  expect(dialogSubmit()).toBeDisabled();

  await user.type(urlInput(), 'http://x.test/a');
  expect(dialogSubmit()).toBeEnabled();
});

it('sends the trimmed URL, also when submitted with Enter', async () => {
  const bodies: unknown[] = [];
  server.use(
    http.post('/api/recipes/import', async ({ request, response }) => {
      bodies.push(await request.json());
      return response(201).json(recipe);
    }),
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  const user = await openDialog();

  await user.type(urlInput(), '  http://x.test/a {Enter}');

  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  expect(bodies).toEqual([{ url: 'http://x.test/a' }]);
});

it('starts empty again after a successful import', async () => {
  server.use(
    http.post('/api/recipes/import', ({ response }) =>
      response(201).json(recipe),
    ),
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  const user = await openDialog();
  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });

  await user.click(screen.getByRole('link', { name: /Rezepte/ }));
  await user.click(await screen.findByRole('button', { name: 'Importieren' }));

  expect(urlInput()).toHaveValue('');
});

it('forgets a failure when the dialog is closed and reopened', async () => {
  server.use(
    http.post('/api/recipes/import', ({ response }) =>
      response.untyped(HttpResponse.json({}, { status: 500 })),
    ),
  );
  const user = await openDialog();
  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await screen.findByText('Import fehlgeschlagen');

  await user.keyboard('{Escape}');
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
  await user.click(importButton());

  expect(screen.queryByText('Import fehlgeschlagen')).not.toBeInTheDocument();
});

it('offers a close button, except while importing', async () => {
  server.use(
    http.post('/api/recipes/import', async ({ response }) => {
      await delay(200);
      return response(201).json(recipe);
    }),
  );
  const user = await openDialog();
  expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await screen.findByText('Wird importiert…');

  expect(screen.queryByRole('button', { name: 'Close' })).toBeNull();
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
});

it('refreshes the Library before opening the Cook View', async () => {
  let libraryLoads = 0;
  server.use(
    http.get('/api/recipes', ({ response }) => {
      libraryLoads++;
      return response(200).json([]);
    }),
    http.post('/api/recipes/import', ({ response }) =>
      response(201).json(recipe),
    ),
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  const user = await openDialog();
  await waitFor(() => expect(libraryLoads).toBe(1));
  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());

  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  expect(libraryLoads).toBe(2);
});

it('lists the imported Recipe back in the Library', async () => {
  let library: components['schemas']['RecipeSummaryDto'][] = [];
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json(library)),
    http.post('/api/recipes/import', ({ response }) => {
      library = [
        {
          id: 7,
          title: 'Kartoffelsuppe',
          sourceUrl: 'http://x.test/a',
          hasImage: false,
          isNew: false,
        },
      ];
      return response(201).json(recipe);
    }),
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
  );
  const user = await openDialog();
  await screen.findByText('Importiere dein erstes Rezept');
  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });

  await user.click(screen.getByRole('link', { name: /Rezepte/ }));

  expect(
    await screen.findByRole('link', { name: /Kartoffelsuppe/ }),
  ).toBeInTheDocument();
});
