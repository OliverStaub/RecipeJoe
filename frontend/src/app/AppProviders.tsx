import { QueryClientProvider } from '@tanstack/react-query';
import { useState, type ComponentProps } from 'react';
import { RouterProvider } from 'react-router-dom';
import { Toaster } from '@/components/ui/sonner';
import { createQueryClient } from './queryClient';

type Router = ComponentProps<typeof RouterProvider>['router'];

/** Each mount gets its own query client, so every test starts with an empty cache. */
export function AppProviders({ router }: { router: Router }) {
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
      <Toaster />
    </QueryClientProvider>
  );
}
