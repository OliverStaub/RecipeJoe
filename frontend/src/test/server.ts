import { createOpenApiHttp } from 'openapi-msw';
import { setupServer } from 'msw/node';
import type { paths } from '@/api/schema';

export const http = createOpenApiHttp<paths>({ baseUrl: 'http://localhost' });
// Every page mounts the Library, which always polls Imports; most tests don't care about them.
export const server = setupServer(
  http.get('/api/imports', ({ response }) => response(200).json([])),
);
