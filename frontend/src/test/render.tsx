import { render } from '@testing-library/react';
import { createMemoryRouter } from 'react-router-dom';
import { AppProviders } from '@/app/AppProviders';
import { routes } from '@/app/router';

export function renderApp(path = '/') {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(<AppProviders router={router} />);
  return { router };
}

export const recipe = {
  id: 7,
  title: 'Kartoffelsuppe',
  servings: null,
  prepMinutes: null,
  cookMinutes: null,
  totalMinutes: null,
  ingredientLines: ['500 g Kartoffeln', '1 Zwiebel'],
  steps: ['Kartoffeln schälen.\nWürfeln.', 'Kochen.'],
  sourceUrl: 'http://fixtures/e2e/recipe.html',
  imageUrl: null,
  createdAt: '2026-01-01T00:00:00Z',
};
