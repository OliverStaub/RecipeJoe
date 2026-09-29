import { createBrowserRouter, type RouteObject } from 'react-router-dom';
import { LibraryPage } from '@/features/library/LibraryPage';

export const routes: RouteObject[] = [{ path: '/', element: <LibraryPage /> }];

export const router = createBrowserRouter(routes);
