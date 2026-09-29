import { useQueryClient } from '@tanstack/react-query';
import { $api } from '@/api/client';
import { importFailures, type ImportFailure } from './failureMessage';

const kinds: readonly string[] = importFailures;

/** Network failures are `Unreachable`; any other unrecognised error is `BadResponse`. */
export function toImportFailure(error: unknown): ImportFailure {
  if (typeof error === 'object' && error !== null && 'kind' in error) {
    const { kind } = error;
    if (typeof kind === 'string' && kinds.includes(kind)) {
      return kind as ImportFailure;
    }
  }
  return error instanceof TypeError ? 'Unreachable' : 'BadResponse';
}

export function useImportRecipe() {
  const queryClient = useQueryClient();
  return $api.useMutation('post', '/api/recipes/import', {
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['get', '/api/recipes'] }),
  });
}
