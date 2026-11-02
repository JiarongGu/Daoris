import { expect, test, type Page } from '@playwright/test';

// The code map on the canvas measured (CANVAS1f; D224, the canvas design §3.4, §9 row f): no card over another, opened whole or closed, a
// count on a line between closed folders, find opening the path to a module inside a closed folder, and an id cut on its card, in a
// real browser.

const ready = (page: Page) => page.waitForFunction(
  () => (window as unknown as { flowReady?: boolean }).flowReady === true && document.querySelector('[data-flow-node]') !== null,
  undefined, { timeout: 60_000 },
);

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=code/${story}`);
  await ready(page);
  await page.waitForTimeout(400);
}

const cards = (page: Page) => page.evaluate(() =>
  [...document.querySelectorAll('[data-flow-node]')].map((node) => {
    const { left, top, width, height } = node.getBoundingClientRect();
    return { id: node.getAttribute('data-flow-node')!, left, top, right: left + width, bottom: top + height };
  }));

for (const story of ['Layered', 'LayeredHalf', 'WithACycle', 'Hundreds', 'HundredsHalf']) {
  test(`code/${story}: no card over another`, async ({ page }) => {
    await open(page, story);
    const drawn = await cards(page);
    expect(drawn.length).toBeGreaterThan(4);
    for (let a = 0; a < drawn.length; a += 1) {
      for (let b = a + 1; b < drawn.length; b += 1) {
        const [p, q] = [drawn[a]!, drawn[b]!];
        const apart = p.right <= q.left + 0.5 || q.right <= p.left + 0.5 || p.bottom <= q.top + 0.5 || q.bottom <= p.top + 0.5;
        expect(apart, `${p.id} and ${q.id}`).toBe(true);
      }
    }
  });
}

test('code/Hundreds: opens on closed folders, a count on the lines between them, and find opens the path to a module', async ({ page }) => {
  await open(page, 'Hundreds');
  expect((await cards(page)).every((card) => card.id.startsWith('folder:'))).toBe(true);
  await expect(page.locator('[data-flow-line] text').first()).toHaveText(/×\d+/);
  await page.getByRole('searchbox', { name: 'find a module' }).fill('module-0417');
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-flow-node="module-0417"]')).toHaveAttribute('aria-pressed', 'true');
  const box = (await page.locator('[data-flow-node="module-0417"]').boundingBox())!;
  const view = page.viewportSize()!;
  expect(box.x).toBeGreaterThanOrEqual(0);
  expect(box.x + box.width).toBeLessThanOrEqual(view.width);
});

test('code/LongIds: an id is cut on its card and whole in the detail', async ({ page }) => {
  await open(page, 'LongIds');
  const card = page.locator('[data-flow-node="Daoris.Desktop.Driver.Host.Integration"]');
  await expect(card).toContainText('…');
  await expect(page.getByText('Daoris.Desktop.Driver.Host.Integration', { exact: true })).toBeVisible();
});
