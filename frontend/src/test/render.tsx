import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import { Toaster } from '@/components/ui/sonner';
import { routes } from '@/app/router';

export function renderApp(path = '/') {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>,
  );
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
