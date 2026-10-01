import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { delay } from 'msw';
import { http, server } from '@/test/server';
import { renderApp } from '@/test/render';

async function openDialog() {
  const user = userEvent.setup();
  renderApp();
  await user.click(screen.getByRole('button', { name: 'Importieren' }));
  return user;
}

const urlInput = () => screen.getByRole('textbox', { name: 'Webadresse' });
const importButton = () => screen.getByRole('button', { name: 'Importieren' });
const dialogSubmit = () =>
  within(screen.getByRole('dialog')).getByRole('button', {
    name: 'Importieren',
  });

const pendingImport = {
  id: '11111111-1111-1111-1111-111111111111',
  url: 'http://x.test/a',
  kind: 'Web',
  state: 'Pending',
  stage: 'Fetching',
  failure: null,
} as const;

it('shows the failure message and clears it when the URL is edited', async () => {
  server.use(
    http.post('/api/imports', ({ response }) =>
      response(400).json(
        { type: 't', title: 'x', status: 400, kind: 'InvalidUrl' },
        { headers: { 'content-type': 'application/problem+json' } },
      ),
    ),
  );
  const user = await openDialog();

  await user.type(urlInput(), 'not a url');
  await user.click(screen.getByRole('button', { name: 'Importieren' }));

  expect(await screen.findByText('Import fehlgeschlagen')).toBeInTheDocument();
  expect(
    screen.getByText('Das ist keine gültige Webadresse.'),
  ).toBeInTheDocument();

  await user.type(urlInput(), 'b');

  expect(screen.queryByText('Import fehlgeschlagen')).not.toBeInTheDocument();
});

it('disables the form and cannot be closed while starting', async () => {
  server.use(
    http.post('/api/imports', async ({ response }) => {
      await delay(200);
      return response(202).json(pendingImport);
    }),
  );
  const user = await openDialog();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(screen.getByRole('button', { name: 'Importieren' }));

  expect(await screen.findByText('Wird importiert…')).toBeInTheDocument();
  expect(urlInput()).toBeDisabled();

  await user.keyboard('{Escape}');

  expect(screen.getByRole('dialog')).toBeInTheDocument();
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
});

it('closes and clears the URL on 202, without opening the Cook View', async () => {
  server.use(
    http.get('/api/recipes', ({ response }) => response(200).json([])),
    http.post('/api/imports', ({ response }) =>
      response(202).json(pendingImport),
    ),
  );
  const user = await openDialog();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());

  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
  expect(screen.queryByRole('link')).not.toBeInTheDocument();
});

it('only enables submit for a non-blank URL', async () => {
  const user = await openDialog();
  expect(dialogSubmit()).toBeDisabled();

  await user.type(urlInput(), '   ');
  expect(dialogSubmit()).toBeDisabled();

  await user.type(urlInput(), 'http://x.test/a');
  expect(dialogSubmit()).toBeEnabled();
});

it('sends the trimmed URL, also when submitted with Enter', async () => {
  const bodies: unknown[] = [];
  server.use(
    http.post('/api/imports', async ({ request, response }) => {
      bodies.push(await request.json());
      return response(202).json(pendingImport);
    }),
  );
  const user = await openDialog();

  await user.type(urlInput(), '  http://x.test/a {Enter}');

  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
  expect(bodies).toEqual([{ url: 'http://x.test/a' }]);
});

it('starts empty again after a successful start', async () => {
  server.use(
    http.post('/api/imports', ({ response }) =>
      response(202).json(pendingImport),
    ),
  );
  const user = await openDialog();
  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );

  await user.click(await screen.findByRole('button', { name: 'Importieren' }));

  expect(urlInput()).toHaveValue('');
});

it('forgets a failure when the dialog is closed and reopened', async () => {
  server.use(
    http.post('/api/imports', ({ response }) =>
      response(400).json(
        { type: 't', title: 'x', status: 400, kind: 'InvalidUrl' },
        { headers: { 'content-type': 'application/problem+json' } },
      ),
    ),
  );
  const user = await openDialog();
  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await screen.findByText('Import fehlgeschlagen');

  await user.keyboard('{Escape}');
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
  await user.click(importButton());

  expect(screen.queryByText('Import fehlgeschlagen')).not.toBeInTheDocument();
});

it('offers a close button, except while starting', async () => {
  server.use(
    http.post('/api/imports', async ({ response }) => {
      await delay(200);
      return response(202).json(pendingImport);
    }),
  );
  const user = await openDialog();
  expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument();

  await user.type(urlInput(), 'http://x.test/a');
  await user.click(dialogSubmit());
  await screen.findByText('Wird importiert…');

  expect(screen.queryByRole('button', { name: 'Close' })).toBeNull();
  await waitFor(() =>
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
  );
});

it('invites to paste a Web address or a YouTube link', async () => {
  await openDialog();

  expect(urlInput()).toHaveAttribute(
    'placeholder',
    'Webadresse oder YouTube-Link',
  );
});
