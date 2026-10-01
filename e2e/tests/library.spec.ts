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
