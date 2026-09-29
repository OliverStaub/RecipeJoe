import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay } from 'msw';
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
