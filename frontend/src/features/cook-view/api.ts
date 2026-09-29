import { $api } from '@/api/client';

export function useRecipe(id: number) {
  return $api.useQuery(
    'get',
    '/api/recipes/{id}',
    { params: { path: { id } } },
    { enabled: Number.isInteger(id) },
  );
}
