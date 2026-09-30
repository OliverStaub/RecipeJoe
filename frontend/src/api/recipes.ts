import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { $api, HttpError } from './client';
import type { components } from './schema';

type Recipe = components['schemas']['RecipeDto'];

export type RecipeState =
  | { status: 'loading' }
  | { status: 'notFound' }
  | { status: 'error' }
  | { status: 'loaded'; recipe: Recipe };

const libraryQuery = (q: string) =>
  $api.queryOptions('get', '/api/recipes', {
    params: { query: q ? { q } : {} },
  });

const recipeByIdQuery = (id: number) =>
  $api.queryOptions('get', '/api/recipes/{id}', { params: { path: { id } } });

/** The Library for a search text; keeps the previous results while a new search loads. */
export function useRecipes(q: string) {
  return useQuery({ ...libraryQuery(q), placeholderData: keepPreviousData });
}

/** One Recipe for the Cook View; a non-integer id is not found without a request. */
export function useRecipe(id: number): RecipeState {
  const valid = Number.isInteger(id);
  const query = useQuery({ ...recipeByIdQuery(id), enabled: valid });

  if (!valid) return { status: 'notFound' };
  if (query.isPending) return { status: 'loading' };
  if (query.isError) {
    // Typed `never` by the schema; the client middleware throws HttpError.
    const error: unknown = query.error;
    return error instanceof HttpError && error.status === 404
      ? { status: 'notFound' }
      : { status: 'error' };
  }
  return { status: 'loaded', recipe: query.data };
}
