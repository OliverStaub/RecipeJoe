import { screen } from '@testing-library/react';
import { http, server } from '@/test/server';
import { renderApp } from '@/test/render';

it('renders the Library heading on /', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
  );
  renderApp('/');

  expect(screen.getByRole('heading', { name: 'Rezepte' })).toBeInTheDocument();
  await screen.findByText('Importiere dein erstes Rezept');
});
