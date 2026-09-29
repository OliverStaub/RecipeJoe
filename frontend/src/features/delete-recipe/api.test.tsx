import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { http, server } from '@/test/server';
import { useDeleteRecipe } from './api';

it('drops only the deleted Recipe from the cache and invalidates the Library', async () => {
  server.use(
    http.delete('/api/recipes/{id}', ({ response }) => response(204).empty()),
  );
  const queryClient = new QueryClient();
  const key = (id: number) => [
    'get',
    '/api/recipes/{id}',
    { params: { path: { id } } },
  ];
  queryClient.setQueryData(key(7), { id: 7 });
  queryClient.setQueryData(key(8), { id: 8 });
  queryClient.setQueryData(['get', '/api/recipes'], []);
  queryClient.setQueryData(['get', '/api/other'], []);
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  const { result } = renderHook(() => useDeleteRecipe(), { wrapper });
  await act(() => result.current.mutateAsync({ params: { path: { id: 7 } } }));

  await waitFor(() =>
    expect(
      queryClient.getQueryState(['get', '/api/recipes'])?.isInvalidated,
    ).toBe(true),
  );
  expect(queryClient.getQueryData(key(7))).toBeUndefined();
  expect(queryClient.getQueryData(key(8))).toEqual({ id: 8 });
  expect(queryClient.getQueryState(['get', '/api/other'])?.isInvalidated).toBe(
    false,
  );
});
