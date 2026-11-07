import { expect, test, type Page } from '@playwright/test';

// The Map's Tab order measured on both levels, in a real browser: Tab walks the toolbar's controls and
// then the drawing is one stop, whichever of its cards and lines is current, never one stop each; the list is one tree row.

const ready = (page: Page) => page.waitForFunction(
  () => (window as unknown as { flowReady?: boolean }).flowReady === true && document.querySelector('[data-flow-node]') !== null,
  undefined, { timeout: 60_000 },
);

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=${story}`);
  await ready(page);
  await page.waitForTimeout(400);
}

/** What Tab reaches, in order, from the top of the page until it leaves the story: each stop as a kind. */
async function walk(page: Page): Promise<string[]> {
  await page.evaluate(() => { (document.activeElement as HTMLElement | null)?.blur(); window.scrollTo(0, 0); });
  const stops: string[] = [];
  for (let step = 0; step < 40; step += 1) {
    await page.keyboard.press('Tab');
    const kind = await page.evaluate(() => {
      const at = document.activeElement as HTMLElement | null;
      if (!at || at === document.body) return 'out';
      if (at.hasAttribute('data-flow-node')) return 'card';
      if (at.hasAttribute('data-flow-line')) return 'line';
      if (at.getAttribute('role') === 'treeitem') return 'tree row';
      if (at.hasAttribute('data-code-fold')) return 'fold';
      return 'control';
    });
    if (kind === 'out') break;
    stops.push(kind);
  }
  return stops;
}

const drawing = (stops: string[]) => stops.filter((kind) => kind === 'card' || kind === 'line');

for (const [level, story] of [['workspaces', 'map/Layered'], ['modules', 'code/Layered']] as const) {
  test(`${level}: the drawing is one stop in the Tab order, after the toolbar's controls`, async ({ page }) => {
    await open(page, story);
    const stops = await walk(page);
    expect(drawing(stops)).toHaveLength(1);
    // Beside it stand the toolbar's controls and, on the code map, one *Fold* per open folder frame: a frame's header comes before its cards.
    expect(stops.every((kind) => kind === 'control' || kind === 'fold' || kind === 'card' || kind === 'line'), stops.join(', ')).toBe(true);
    expect(stops.filter((kind) => kind === 'fold')).toHaveLength(await page.locator('[data-code-fold]').count());
    expect(stops.indexOf('control')).toBeLessThan(stops.findIndex((kind) => kind === 'card' || kind === 'line'));
  });

  test(`${level}: Shift+Tab walks the same stops back`, async ({ page }) => {
    await open(page, story);
    const forward = await walk(page);
    const back: string[] = [];
    // The forward walk ended outside the story, so the first Shift+Tab lands on its last stop.
    for (let step = 0; step < forward.length; step += 1) {
      await page.keyboard.press('Shift+Tab');
      back.push(await page.evaluate(() => {
        const at = document.activeElement as HTMLElement;
        return at.hasAttribute('data-flow-node') ? 'card' : at.hasAttribute('data-flow-line') ? 'line' : at.hasAttribute('data-code-fold') ? 'fold'
          : at.getAttribute('role') === 'treeitem' ? 'tree row' : 'control';
      }));
    }
    expect(back).toEqual([...forward].reverse());
  });

  test(`${level}: the list is one tree row in the Tab order`, async ({ page }) => {
    await open(page, story);
    await page.getByRole('button', { name: /Show as list/ }).click();
    await page.waitForSelector('[role="tree"]');
    const stops = await walk(page);
    expect(stops.filter((kind) => kind === 'tree row')).toHaveLength(1);
    expect(drawing(stops)).toHaveLength(0);
  });
}
