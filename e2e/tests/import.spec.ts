import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

// The backend reaches the fixtures site as http://fixtures (Import__AllowedHosts).
const fixtureUrl = (page: string, token: string) =>
  `http://fixtures/e2e/${page}?t=${token}`;

// The three browser projects run this file concurrently against one shared backend, so two
// Imports of the same fixture page render as identical-looking rows (same URL label, no token).
// The row's `title` attribute carries the full URL, letting a test find its own row regardless.
const rowFor = (page: import('@playwright/test').Page, url: string) =>
  page.getByRole('listitem').filter({ has: page.locator(`[title="${url}"]`) });

test('a Web Import shows as a Pending row, then its Recipe appears in the Library', async ({
  page,
}) => {
  const token = randomUUID();
  const url = fixtureUrl('recipe.html', token);
  await page.goto('/');

  await page.getByRole('button', { name: 'Importieren' }).click();
  await page.getByRole('textbox', { name: 'Webadresse' }).fill(url);
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Importieren' })
    .click();

  await expect(page.getByRole('dialog')).toBeHidden();
  const row = rowFor(page, url);
  await expect(row).toBeVisible();
  await expect(row.getByText(/wird|Rezept/)).toBeVisible();

  await expect(row).toBeHidden({ timeout: 10_000 });
  await expect(
    page.getByRole('link', { name: new RegExp(`Testrezept ${token}`) }),
  ).toBeVisible();
});

test('a page without a Recipe shows a Failed row with "Verwerfen"', async ({
  page,
}) => {
  const token = randomUUID();
  const url = fixtureUrl('no-recipe.html', token);
  await page.goto('/');

  await page.getByRole('button', { name: 'Importieren' }).click();
  await page.getByRole('textbox', { name: 'Webadresse' }).fill(url);
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Importieren' })
    .click();

  const row = rowFor(page, url);
  await expect(
    row.getByText('Auf dieser Seite wurde kein Rezept gefunden.'),
  ).toBeVisible({ timeout: 10_000 });
  await expect(
    row.getByRole('button', { name: 'Erneut versuchen' }),
  ).toHaveCount(0);

  await row.getByRole('button', { name: 'Verwerfen' }).click();

  await expect(row).toBeHidden();
});
