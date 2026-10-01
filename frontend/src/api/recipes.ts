import { useEffect, useRef } from 'react';
import {
  keepPreviousData,
  useQuery,
  useQueryClient,
} from '@tanstack/react-query';
import { $api, HttpError } from './client';
import type { components } from './schema';

type Recipe = components['schemas']['RecipeDto'];
type Import = components['schemas']['ImportDto'];

export type RecipeState =
  | { status: 'loading' }
  | { status: 'notFound' }
  | { status: 'error' }
  | { status: 'loaded'; recipe: Recipe };

const libraryQuery = (q: string) =>
  $api.queryOptions('get', '/api/recipes', {
    params: { query: q ? { q } : {} },
  });

// No `init`, so the key is a prefix of every search's key.
const allLibraryQueries = $api.queryOptions('get', '/api/recipes').queryKey;

const recipeByIdQuery = (id: number) =>
  $api.queryOptions('get', '/api/recipes/{id}', { params: { path: { id } } });

/** The Library for a search text; keeps the previous results while a new search loads. */
export function useRecipes(q: string) {
  return useQuery({ ...libraryQuery(q), placeholderData: keepPreviousData });
}

/** One Recipe for the Cook View; a non-integer id is not found without a request. Loading it also clears "Neu" in the Library, since the API marks the Recipe seen as a side effect. */
export function useRecipe(id: number): RecipeState {
  const valid = Number.isInteger(id);
  const query = useQuery({ ...recipeByIdQuery(id), enabled: valid });
  const queryClient = useQueryClient();

  useEffect(() => {
    if (query.isSuccess) {
      void queryClient.invalidateQueries({ queryKey: allLibraryQueries });
    }
  }, [id, query.isSuccess, queryClient]);

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

/** Deletes a Recipe; drops its cached entry (other Recipes' stay) and refetches the Library without waiting. */
export function useDeleteRecipe() {
  const queryClient = useQueryClient();
  return $api.useMutation('delete', '/api/recipes/{id}', {
    onSuccess: (_data, { params }) => {
      queryClient.removeQueries({
        queryKey: recipeByIdQuery(params.path.id).queryKey,
        exact: true,
      });
      void queryClient.invalidateQueries({ queryKey: allLibraryQueries });
    },
  });
}

const importsQuery = $api.queryOptions('get', '/api/imports');

function setImports(
  queryClient: ReturnType<typeof useQueryClient>,
  update: (imports: Import[]) => Import[],
) {
  queryClient.setQueryData<Import[]>(importsQuery.queryKey, (old) =>
    update(old ?? []),
  );
}

/** Pending and Failed Imports; polls while any is Pending, stops when idle. A Pending Import vanishing (succeeding) invalidates the Library; a dismissed Failed one doesn't. */
export function useImports() {
  const queryClient = useQueryClient();
  const previousStates = useRef<Map<string, Import['state']> | null>(null);
  const query = useQuery({
    ...importsQuery,
    refetchInterval: (q) =>
      q.state.data?.some((i) => i.state === 'Pending') ? 1500 : false,
  });

  useEffect(() => {
    if (!query.data) return;
    const current = new Map(query.data.map((i) => [i.id, i.state]));
    if (
      previousStates.current &&
      [...previousStates.current].some(
        ([id, state]) => state === 'Pending' && !current.has(id),
      )
    ) {
      void queryClient.invalidateQueries({ queryKey: allLibraryQueries });
    }
    previousStates.current = current;
  }, [query.data, queryClient]);

  return query;
}

/** Starts an Import; it's added to the Imports list right away so its row appears before the next poll. */
export function useStartImport() {
  const queryClient = useQueryClient();
  return $api.useMutation('post', '/api/imports', {
    onSuccess: (started) =>
      setImports(queryClient, (imports) => [...imports, started]),
  });
}

/** Retries a Failed Import; it goes back to Pending in place. */
export function useRetryImport() {
  const queryClient = useQueryClient();
  return $api.useMutation('post', '/api/imports/{id}/retry', {
    onSuccess: (retried) =>
      setImports(queryClient, (imports) =>
        imports.map((i) => (i.id === retried.id ? retried : i)),
      ),
  });
}

/** Dismisses a Failed Import, removing its row. */
export function useDismissImport() {
  const queryClient = useQueryClient();
  return $api.useMutation('delete', '/api/imports/{id}', {
    onSuccess: (_data, { params }) =>
      setImports(queryClient, (imports) =>
        imports.filter((i) => i.id !== params.path.id),
      ),
  });
}
