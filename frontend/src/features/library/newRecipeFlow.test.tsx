import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, server } from '@/test/server';
import { recipe, renderApp } from '@/test/render';

const summary = {
  id: 7,
  title: 'Kartoffelsuppe',
  sourceUrl: 'http://fixtures.test/e2e/recipe.html',
  hasImage: false,
  isNew: true,
};

/** The fake API marks the Recipe seen the first time it's fetched by id, same as the real GET side effect. */
function serveLibrary() {
  let library = [summary];
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json(library)),
    http.get('/api/recipes/{id}', ({ params, response }) => {
      library = library.map((r) =>
        r.id === Number(params.id) ? { ...r, isNew: false } : r,
      );
      return response(200).json(recipe);
    }),
  );
}

it('clears the "Neu" badge after the Recipe is opened in Cook View and the cook goes back', async () => {
  serveLibrary();
  const user = userEvent.setup();
  renderApp();

  const link = await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  expect(within(link).getByText('Neu')).toBeInTheDocument();

  await user.click(link);
  await screen.findByRole('heading', { name: 'Kartoffelsuppe' });
  await user.click(screen.getByRole('link', { name: /Rezepte/ }));

  const rowAfter = await screen.findByRole('link', { name: /Kartoffelsuppe/ });
  await waitFor(() =>
    expect(within(rowAfter).queryByText('Neu')).not.toBeInTheDocument(),
  );
});
