import { render, screen } from '@testing-library/react';
import { http, server } from '@/test/server';
import { App } from './App';

it('mounts the router inside the providers', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
  );
  render(<App />);

  expect(screen.getByRole('heading', { name: 'Rezepte' })).toBeInTheDocument();
  await screen.findByText('Importiere dein erstes Rezept');
});
