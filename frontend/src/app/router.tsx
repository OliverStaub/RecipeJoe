import { createBrowserRouter, type RouteObject } from 'react-router-dom';
import { CookViewPage } from '@/features/cook-view/CookViewPage';
import { LibraryPage } from '@/features/library/LibraryPage';

export const routes: RouteObject[] = [
  { path: '/', element: <LibraryPage /> },
  { path: '/recipes/:id', element: <CookViewPage /> },
];

export const router = createBrowserRouter(routes);
