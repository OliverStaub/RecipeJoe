import { randomUUID } from 'node:crypto';
import { expect, test } from '@playwright/test';

// The Fake video source answers by video id, so `t` only makes the URL unique per test: the
// 3 browser projects run concurrently against one shared backend, and the Library row is found
// by its full URL in the `title` attribute.
const videoUrl = (token: string) =>
  `https://www.youtube.com/watch?v=kuchen-video&t=${token}`;

test('a Video Import shows a video row, then its Recipe appears with the video as Source', async ({
  page,
  request,
}) => {
  const url = videoUrl(randomUUID());
  await page.goto('/');

  await page.getByRole('button', { name: 'Importieren' }).click();
  await page.getByRole('textbox', { name: 'Webadresse' }).fill(url);
  await page
    .getByRole('dialog')
    .getByRole('button', { name: 'Importieren' })
    .click();

  await expect(page.getByRole('dialog')).toBeHidden();
  const row = page
    .getByRole('listitem')
    .filter({ has: page.locator(`[title="${url}"]`) });
  await expect(row).toBeVisible();
  await expect(row.locator('.lucide-video')).toBeVisible();

  await expect(row).toBeHidden({ timeout: 10_000 });
  // The LLM stub echoes the video title; concurrent projects import the same video, so several
  // Recipes may carry it. Ours is the one whose Source is this URL.
  await expect(
    page.getByRole('link', { name: /Omas Apfelkuchen/ }).first(),
  ).toBeVisible();
  const recipes = (await (await request.get('/api/recipes')).json()) as {
    title: string;
    sourceUrl: string;
  }[];
  expect(recipes.find((r) => r.sourceUrl === url)?.title).toBe(
    'Omas Apfelkuchen',
  );
});
