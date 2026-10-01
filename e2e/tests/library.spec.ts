import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';
import { importViaApi } from './imports';

test('search narrows the Library', async ({ page, request }) => {
  const [wanted, other] = [randomUUID(), randomUUID()];
  for (const token of [wanted, other]) {
    await importViaApi(request, `http://fixtures/e2e/recipe.html?t=${token}`);
  }
  await page.goto('/');
  await expect(page.getByText(`Testrezept ${other}`)).toBeVisible();

  await page.getByRole('searchbox').fill(wanted);

  await expect(page.getByText(`Testrezept ${wanted}`)).toBeVisible();
  await expect(page.getByText(`Testrezept ${other}`)).toBeHidden();
  await expect(page).toHaveURL(new RegExp(`\\?q=${wanted}`));
});

test('the "Neu" badge clears after the Recipe is opened and the cook goes back', async ({
  page,
  request,
}) => {
  const token = randomUUID();
  const response = await request.post('/api/recipes/import', {
    data: { url: `http://fixtures/e2e/recipe.html?t=${token}` },
  });
  expect(response.status()).toBe(201);
  const title = `Testrezept ${token}`;
  await page.goto('/');
  const row = page.getByRole('listitem').filter({ hasText: title });
  await expect(row.getByText('Neu')).toBeVisible();

  await row.getByRole('link').click();
  await expect(page.getByRole('heading', { name: title })).toBeVisible();
  await page.getByRole('link', { name: 'Rezepte' }).click();

  await expect(page).toHaveURL('/');
  await expect(
    page.getByRole('listitem').filter({ hasText: title }).getByText('Neu'),
  ).toBeHidden();
});
