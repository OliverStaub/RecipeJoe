import { screen } from '@testing-library/react';
import { renderApp } from '@/test/render';

it('renders the Library heading on /', () => {
  renderApp('/');

  expect(screen.getByRole('heading', { name: 'Rezepte' })).toBeInTheDocument();
});
