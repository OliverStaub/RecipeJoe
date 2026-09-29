import { keepPreviousData } from '@tanstack/react-query';
import { $api } from '@/api/client';

export function useRecipes(q: string) {
  return $api.useQuery(
    'get',
    '/api/recipes',
    { params: { query: q ? { q } : {} } },
    { placeholderData: keepPreviousData },
  );
}
