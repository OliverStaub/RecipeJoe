import { createOpenApiHttp } from 'openapi-msw';
import { setupServer } from 'msw/node';
import type { paths } from '@/api/schema';

export const http = createOpenApiHttp<paths>({ baseUrl: 'http://localhost' });
export const server = setupServer();
