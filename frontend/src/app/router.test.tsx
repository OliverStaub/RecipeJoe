import { render, screen } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import { routes } from './router';

it('renders the Library heading on /', () => {
  const router = createMemoryRouter(routes, { initialEntries: ['/'] });
  render(<RouterProvider router={router} />);

  expect(screen.getByRole('heading', { name: 'Rezepte' })).toBeInTheDocument();
});
