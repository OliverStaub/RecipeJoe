import { render, screen } from '@testing-library/react';
import { App } from './App';

it('mounts the router inside the providers', () => {
  render(<App />);

  expect(screen.getByRole('heading', { name: 'Rezepte' })).toBeInTheDocument();
});
