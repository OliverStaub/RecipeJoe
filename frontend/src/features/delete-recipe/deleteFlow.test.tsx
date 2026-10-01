import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, delay } from 'msw';
import { http, server } from '@/test/server';
import { recipe, renderApp } from '@/test/render';

const summary = {
  id: 7,
  title: 'Kartoffelsuppe',
  sourceUrl: 'http://fixtures.test/e2e/recipe.html',
  hasImage: false,
  isNew: false,
};

function serveLibrary(recipes = [summary]) {
  let library = recipes;
  const deleted: number[] = [];
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json(library)),
    http.get('/api/recipes/{id}', ({ response }) => response(200).json(recipe)),
    http.delete('/api/recipes/{id}', ({ params, response }) => {
      deleted.push(Number(params.id));
      library = library.filter((r) => r.id !== Number(params.id));
      return response(204).empty();
    }),
  );
  return { deleted };
}

const confirmDialog = () => screen.findByRole('alertdialog');

it('deletes from a Library row after confirming, then toasts and drops the row', async () => {
  const { deleted } = serveLibrary();
  const user = userEvent.setup();
  renderApp();
  await screen.findByRole('link', { name: /Kartoffelsuppe/ });

  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));

  const dialog = await confirmDialog();
  expect(dialog).toHaveTextContent('„Kartoffelsuppe" löschen?');
  expect(dialog).toHaveTextContent('Das kann nicht rückgängig gemacht werden.');
  await user.click(within(dialog).getByRole('button', { name: 'Löschen' }));

  expect(await screen.findByText('Rezept gelöscht')).toBeInTheDocument();
  await waitFor(() =>
    expect(
      screen.queryByRole('link', { name: /Kartoffelsuppe/ }),
    ).not.toBeInTheDocument(),
  );
  expect(deleted).toEqual([7]);
});

it('deletes nothing when cancelled', async () => {
  const { deleted } = serveLibrary();
  const user = userEvent.setup();
  renderApp();
  await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));

  await user.click(
    within(await confirmDialog()).getByRole('button', { name: 'Abbrechen' }),
  );

  await waitFor(() =>
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(),
  );
  expect(deleted).toEqual([]);
  expect(
    screen.getByRole('link', { name: /Kartoffelsuppe/ }),
  ).toBeInTheDocument();
});

it('deletes from the Cook View and goes back to a Library without it', async () => {
  const { deleted } = serveLibrary();
  const user = userEvent.setup();
  renderApp();
  await user.click(await screen.findByRole('link', { name: /Kartoffelsuppe/ }));
  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });

  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));
  await user.click(
    within(await confirmDialog()).getByRole('button', { name: 'Löschen' }),
  );

  expect(
    await screen.findByRole('heading', { name: 'Rezepte' }),
  ).toBeInTheDocument();
  // Sonner's toast store outlives each test, so earlier toasts may still render.
  expect(
    (await screen.findAllByText('Rezept gelöscht')).length,
  ).toBeGreaterThan(0);
  expect(deleted).toEqual([7]);
  expect(
    await screen.findByText('Importiere dein erstes Rezept'),
  ).toBeVisible();
  expect(
    screen.queryByRole('link', { name: /Kartoffelsuppe/ }),
  ).not.toBeInTheDocument();
});

it('keeps the dialog open and says so when the delete fails', async () => {
  serveLibrary();
  server.use(
    http.delete('/api/recipes/{id}', ({ response }) =>
      response.untyped(HttpResponse.json({}, { status: 500 })),
    ),
  );
  const user = userEvent.setup();
  renderApp();
  await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));

  await user.click(
    within(await confirmDialog()).getByRole('button', { name: 'Löschen' }),
  );

  expect(
    await screen.findByText('Rezept konnte nicht gelöscht werden.'),
  ).toBeInTheDocument();
  expect(screen.getByRole('alertdialog')).toBeInTheDocument();
});

it('closes the confirmation in the Cook View when cancelled, and can be reopened', async () => {
  const { deleted } = serveLibrary();
  const user = userEvent.setup();
  renderApp('/recipes/7');
  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));

  await user.click(
    within(await confirmDialog()).getByRole('button', { name: 'Abbrechen' }),
  );
  await waitFor(() =>
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(),
  );

  expect(deleted).toEqual([]);
  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));
  expect(await confirmDialog()).toBeInTheDocument();
});

it('forgets a failed delete when the dialog is cancelled and reopened', async () => {
  serveLibrary();
  server.use(
    http.delete('/api/recipes/{id}', ({ response }) =>
      response.untyped(HttpResponse.json({}, { status: 500 })),
    ),
  );
  const user = userEvent.setup();
  renderApp();
  await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));
  await user.click(
    within(await confirmDialog()).getByRole('button', { name: 'Löschen' }),
  );
  await screen.findByText('Rezept konnte nicht gelöscht werden.');
  await user.click(screen.getByRole('button', { name: 'Abbrechen' }));
  await waitFor(() =>
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument(),
  );

  await user.click(screen.getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));

  await confirmDialog();
  expect(
    screen.queryByText('Rezept konnte nicht gelöscht werden.'),
  ).not.toBeInTheDocument();
});

it('keeps other Recipes cached after a delete', async () => {
  serveLibrary([summary, { ...summary, id: 8, title: 'Linsensuppe' }]);
  server.use(
    http.get('/api/recipes/{id}', ({ params, response }) =>
      response(200).json(
        Number(params.id) === 8
          ? { ...recipe, id: 8, title: 'Linsensuppe' }
          : recipe,
      ),
    ),
  );
  const user = userEvent.setup();
  renderApp('/recipes/8');
  await screen.findByRole('heading', { name: 'Linsensuppe' });
  await user.click(screen.getByRole('link', { name: /Rezepte/ }));
  const row = (
    await screen.findByRole('link', { name: /Kartoffelsuppe/ })
  ).closest('li');
  await user.click(within(row!).getByRole('button', { name: 'Mehr' }));
  await user.click(await screen.findByRole('menuitem', { name: 'Löschen' }));
  await user.click(
    within(await confirmDialog()).getByRole('button', { name: 'Löschen' }),
  );
  await waitFor(() =>
    expect(
      screen.queryByRole('link', { name: /Kartoffelsuppe/ }),
    ).not.toBeInTheDocument(),
  );

  // Only a cached entry can show it now.
  server.use(http.get('/api/recipes/{id}', () => delay('infinite')));
  await user.click(screen.getByRole('link', { name: /Linsensuppe/ }));

  expect(
    await screen.findByRole('heading', { name: 'Linsensuppe' }),
  ).toBeVisible();
});
