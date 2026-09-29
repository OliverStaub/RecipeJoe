import { useQueryClient } from '@tanstack/react-query';
import { $api } from '@/api/client';

export function useDeleteRecipe() {
  const queryClient = useQueryClient();
  return $api.useMutation('delete', '/api/recipes/{id}', {
    onSuccess: (_data, { params }) => {
      queryClient.removeQueries({
        queryKey: ['get', '/api/recipes/{id}', { params }],
      });
      void queryClient.invalidateQueries({
        queryKey: ['get', '/api/recipes'],
      });
    },
  });
}
