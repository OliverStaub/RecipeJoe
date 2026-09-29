import { screen } from '@testing-library/react';
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
