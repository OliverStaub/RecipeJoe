import { randomUUID } from 'node:crypto';
import { expect, test, type APIRequestContext } from '@playwright/test';

async function importRecipe(request: APIRequestContext) {
  const token = randomUUID();
  const response = await request.post('/api/recipes/import', {
    data: { url: `http://fixtures/e2e/recipe.html?t=${token}` },
  });
  expect(response.status()).toBe(201);
  const { id } = (await response.json()) as { id: number };
  return { id, title: `Testrezept ${token}` };
}

test('delete from the Library', async ({ page, request }) => {
  const { id, title } = await importRecipe(request);
  await page.goto('/');
  const row = page.getByRole('listitem').filter({ hasText: title });

  await row.getByRole('button', { name: 'Mehr' }).click();
  await page.getByRole('menuitem', { name: 'Löschen' }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: 'Löschen' })
    .click();

  await expect(page.getByText('Rezept gelöscht')).toBeVisible();
  await expect(row).toBeHidden();
  expect((await request.get(`/api/recipes/${id}`)).status()).toBe(404);
});

test('delete from the Cook View goes back to the Library', async ({
  page,
  request,
}) => {
  const { id, title } = await importRecipe(request);
  await page.goto(`/recipes/${id}`);
  await expect(page.getByRole('heading', { name: title })).toBeVisible();

  await page.getByRole('button', { name: 'Mehr' }).click();
  await page.getByRole('menuitem', { name: 'Löschen' }).click();
  await page
    .getByRole('alertdialog')
    .getByRole('button', { name: 'Löschen' })
    .click();

  await expect(page).toHaveURL('/');
  await expect(page.getByText('Rezept gelöscht')).toBeVisible();
  await expect(page.getByText(title)).toBeHidden();
  expect((await request.get(`/api/recipes/${id}`)).status()).toBe(404);
});
