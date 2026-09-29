import { expect, test } from '@playwright/test';

test('Library page shows the Rezepte heading', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Rezepte' })).toBeVisible();
});
