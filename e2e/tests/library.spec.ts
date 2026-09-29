import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

test('search narrows the Library', async ({ page, request }) => {
  const [wanted, other] = [randomUUID(), randomUUID()];
  for (const token of [wanted, other]) {
    const response = await request.post('/api/recipes/import', {
      data: { url: `http://fixtures/e2e/recipe.html?t=${token}` },
    });
    expect(response.status()).toBe(201);
  }
  await page.goto('/');
  await expect(page.getByText(`Testrezept ${other}`)).toBeVisible();

  await page.getByRole('searchbox').fill(wanted);

  await expect(page.getByText(`Testrezept ${wanted}`)).toBeVisible();
  await expect(page.getByText(`Testrezept ${other}`)).toBeHidden();
  await expect(page).toHaveURL(new RegExp(`\\?q=${wanted}`));
});
