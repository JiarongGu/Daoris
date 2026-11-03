import { expect, test, type Page } from '@playwright/test';

// The reference map on the canvas measured (CANVAS1e; D224, the canvas design §3.3, §9 row e): the layered map's largest fixture at 80 and
// 39rem with no card over another, a line pressed through its stroke, the ring fitted with no sizing, and the walk from a card along a
// line and back, in a real browser.

const ready = (page: Page) => page.waitForFunction(
  () => (window as unknown as { flowReady?: boolean }).flowReady === true && document.querySelector('[data-flow-node]') !== null,
  undefined, { timeout: 60_000 },
);

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=map/${story}`);
  await ready(page);
  await page.waitForTimeout(400);
}

const cards = (page: Page) => page.evaluate(() =>
  [...document.querySelectorAll('[data-flow-node]')].map((node) => {
    const { left, top, width, height } = node.getBoundingClientRect();
    return { id: node.getAttribute('data-flow-node')!, left, top, right: left + width, bottom: top + height };
  }));

for (const story of ['Layered', 'LayeredHalf', 'Workspaces', 'WorkspacesHalf']) {
  test(`map/${story}: no card over another`, async ({ page }) => {
    await open(page, story);
    const drawn = await cards(page);
    expect(drawn.length).toBeGreaterThan(10);
    for (let a = 0; a < drawn.length; a += 1) {
      for (let b = a + 1; b < drawn.length; b += 1) {
        const [p, q] = [drawn[a]!, drawn[b]!];
        const apart = p.right <= q.left + 0.5 || q.right <= p.left + 0.5 || p.bottom <= q.top + 0.5 || q.bottom <= p.top + 0.5;
        expect(apart, `${p.id} and ${q.id}`).toBe(true);
      }
    }
  });
}

test('map/Layered: a line is pressed through its stroke, and chosen alone', async ({ page }) => {
  await open(page, 'Layered');
  const line = page.locator('[data-flow-line="q:repo-03>repo-09"]');
  const stroke = line.locator('path').first();
  const box = (await stroke.boundingBox())!;
  // The wide invisible stroke is the target: press where the curve leaves its card, not on a card.
  // The layers open wider than the region, so the middle of a long curve can lie past its edge: press the point nearest the middle that is drawn
  // and reaches this line.
  const at = await stroke.evaluate((path: SVGPathElement) => {
    const total = path.getTotalLength();
    const m = path.getScreenCTM()!;
    for (let step = 0; step <= 100; step += 1) {
      const along = 0.5 + (step % 2 === 0 ? 1 : -1) * Math.ceil(step / 2) * 0.01;
      const p = path.getPointAtLength(total * along);
      const point = { x: p.x * m.a + m.e, y: p.y * m.d + m.f };
      if (path.parentElement!.contains(document.elementFromPoint(point.x, point.y))) return point;
    }
    throw new Error('no point of the line is reachable');
  });
  expect(box.width).toBeGreaterThan(0);
  await page.mouse.click(at.x, at.y);
  await expect(line).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('[data-flow-line="q:repo-09>repo-03"]')).toHaveAttribute('aria-pressed', 'false');
});

test('map/Ring: fitted, with no sizing, and the walk goes from a card along a line and back', async ({ page }) => {
  await open(page, 'Ring');
  await expect(page.getByRole('button', { name: /^Sizing/ })).toHaveCount(0);
  await page.locator('[data-flow-node="game"]').focus();
  await page.keyboard.press('ArrowRight');
  await expect(page.locator('[data-flow-line]:focus')).toHaveCount(1);
  await page.keyboard.press('ArrowRight');
  await expect(page.locator('[data-flow-node]:focus')).toHaveCount(1);
  await page.keyboard.press('ArrowLeft');
  await expect(page.locator('[data-flow-line]:focus')).toHaveCount(1);
});
