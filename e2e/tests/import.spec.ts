import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

// fixme until ticket 12 replaces UnavailablePageFetcher with the HttpClient fetcher;
// against it the backend answers Unreachable for every URL.
// The backend reaches the fixtures site as http://fixtures (Import__AllowedHosts).
const fixtureUrl = (page: string, token: string) =>
  `http://fixtures/e2e/${page}?t=${token}`;

test.fixme('Import opens the Cook View with the tokenised title', async ({
  page,
}) => {
  const token = randomUUID();
  await page.goto('/');

  await page.getByRole('button', { name: 'Importieren' }).click();
  await page
    .getByRole('textbox', { name: 'Webadresse' })
    .fill(fixtureUrl('recipe.html', token));
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Importieren' })
    .click();

  await expect(
    page.getByRole('heading', { name: `Testrezept ${token}` }),
  ).toBeVisible();
  await expect(page.getByText('500 g Kartoffeln')).toBeVisible();
});

test.fixme('a page without a Recipe shows its message inline', async ({
  page,
}) => {
  await page.goto('/');

  await page.getByRole('button', { name: 'Importieren' }).click();
  await page
    .getByRole('textbox', { name: 'Webadresse' })
    .fill(fixtureUrl('no-recipe.html', randomUUID()));
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Importieren' })
    .click();

  await expect(page.getByText('Import fehlgeschlagen')).toBeVisible();
  await expect(
    page.getByText('Auf dieser Seite wurde kein Rezept gefunden.'),
  ).toBeVisible();
});
