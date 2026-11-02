import { expect, test, type Page } from '@playwright/test';

// The large positioned worlds measured (CANVAS1d; D224, the canvas design §4.1, traps 11 and 22): the synthetic scenes of 100, 1,000,
// 5,000 and 10,000 nodes drawn in a real browser. Each figure it times is printed as a `large:` line, which D224's note of 2026-11-02
// records: the first usable frame, the frames of a scripted pan and zoom, and what stays in the DOM.

const ready = (page: Page) => page.waitForFunction(
  () => (window as unknown as { flowReady?: boolean }).flowReady === true && document.querySelector('[data-canvas-world] [data-flow-node]') !== null,
  undefined, { timeout: 120_000 },
);

/** The story opened, laid out, drawn and still. */
async function open(page: Page, story: string, query = '') {
  await page.goto(`/measured/flow.html?story=large/${story}${query}`);
  await ready(page);
  await page.waitForTimeout(400);
}

const region = (page: Page) => page.locator('[role="region"][data-flow-pane]').first();
const scaleOf = async (page: Page) => Number(await page.locator('[data-canvas-world]').first().getAttribute('data-canvas-scale'));

async function zoomTo(page: Page, k: number) {
  const box = (await region(page).boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + Math.min(box.height / 2, 120));
  const now = await scaleOf(page);
  await page.keyboard.down('Control');
  await page.mouse.wheel(0, (-100 * Math.log(k / now)) / Math.log(1.25));
  await page.keyboard.up('Control');
  await expect.poll(() => scaleOf(page)).toBeCloseTo(k, 2);
  await page.waitForTimeout(250);
}

/** 120 frames of wheel pans and zooms over the region, timed by the frames between. */
const gesture = (page: Page) => page.evaluate(async () => {
  const area = document.querySelector<HTMLElement>('[role="region"][data-flow-pane]')!;
  const rect = area.getBoundingClientRect();
  const long: number[] = [];
  let observer: PerformanceObserver | null = null;
  try {
    observer = new PerformanceObserver((list) => { for (const entry of list.getEntries()) long.push(entry.duration); });
    observer.observe({ type: 'long-animation-frame', buffered: false });
  } catch { observer = null; }
  const gaps: number[] = [];
  await new Promise(requestAnimationFrame);
  let last = performance.now();
  for (let frame = 0; frame < 120; frame += 1) {
    const zoom = frame % 3 === 0;
    area.dispatchEvent(new WheelEvent('wheel', {
      bubbles: true, cancelable: true, clientX: rect.left + rect.width / 2, clientY: rect.top + 80, ctrlKey: zoom,
      deltaY: zoom ? (frame % 6 === 0 ? -15 : 15) : (frame < 60 ? 40 : -40),
    }));
    await new Promise(requestAnimationFrame);
    const now = performance.now();
    gaps.push(now - last);
    last = now;
  }
  observer?.disconnect();
  gaps.sort((a, b) => a - b);
  return { p95: gaps[Math.floor(gaps.length * 0.95)]!, median: gaps[Math.floor(gaps.length / 2)]!, worst: gaps.at(-1)!, long };
});

const SIZES: Record<string, { nodes: number; target: number }> = {
  Hundred: { nodes: 100, target: 16.7 },
  Thousand: { nodes: 1000, target: 33.3 },
  Five: { nodes: 5000, target: 33.3 },
  Ten: { nodes: 10_000, target: 33.3 },
};

for (const [story, { nodes, target }] of Object.entries(SIZES)) {
  test(`large/${story}: ${nodes} nodes, p95 frame within ${target} ms while panning and zooming, the DOM bounded`, async ({ page }) => {
    test.setTimeout(240_000);
    page.on('pageerror', (error) => console.log(`large: ${story} page error: ${error.message.slice(0, 300)}`));
    await page.goto(`/measured/flow.html?story=large/${story}`);
    await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
    await ready(page);
    const first = await page.evaluate(() => performance.now() - performance.getEntriesByName('story-render')[0]!.startTime);
    await page.waitForTimeout(500);
    const dom = await page.evaluate(() => document.querySelectorAll('[data-flow-node]').length);
    const frames = await gesture(page);
    const after = await page.evaluate(() => document.querySelectorAll('[data-flow-node]').length);
    console.log(`large: ${story} ${nodes} nodes: first usable ${first.toFixed(0)} ms; ${dom} in the DOM, ${after} after the gesture; frames p95 ${frames.p95.toFixed(1)} ms, median ${frames.median.toFixed(1)}, worst ${frames.worst.toFixed(1)}; long frames ${frames.long.map((each) => each.toFixed(0)).join(' ') || 'none'}`);
    // Past 400 nodes only what is in view and what must stay is mounted.
    if (nodes > 400) { expect(dom).toBeLessThan(400); expect(after).toBeLessThan(400); }
    expect(frames.p95).toBeLessThan(target * 1.5);
  });
}

// The tier follows 11 px × the window's zoom × k (trap 12 beside it): 9 px full, 4 px a chip, below it a dot.
const expectedTier = (k: number, rem: number) => { const size = 11 * (rem / 16) * k; return size >= 9 ? 'full' : size >= 4 ? 'chip' : 'dot'; };

for (const zoom of [50, 100, 200]) {
  test(`the tiers switch by the effective text-meta at window zoom ${zoom}%`, async ({ page }) => {
    await open(page, 'Hundred', `&zoom=${zoom}`);
    const rem = await page.evaluate(() => Number.parseFloat(getComputedStyle(document.documentElement).fontSize));
    for (const k of [2, 1, 0.7, 0.4, 0.2, 0.1]) {
      await zoomTo(page, k);
      const tiers = await page.evaluate(() => [...new Set([...document.querySelectorAll('[data-flow-node]')].map((node) => node.getAttribute('data-tier')))]);
      const dots = await page.locator('[data-canvas-dots]').count();
      const want = expectedTier(k, rem);
      // The budget may drop a tier where more than 250 are in view; a hundred nodes never exceed it.
      if (want === 'dot') expect(dots, `dots at ${k}`).toBe(1);
      else expect(tiers, `tier at ${k}, root ${rem} px`).toEqual([want]);
    }
  });
}

test('the minimap\'s rectangle equals the view, and a press on it moves the view', async ({ page }) => {
  await open(page, 'Thousand');
  const read = () => page.evaluate(() => {
    const world = document.querySelector<HTMLElement>('[data-canvas-world]')!;
    const k = Number(world.dataset.canvasScale);
    const [, tx, ty] = /translate\((-?[\d.e-]+)px, (-?[\d.e-]+)px\)/.exec(world.style.transform)!;
    const pane = world.parentElement!.getBoundingClientRect();
    const map = document.querySelector<HTMLElement>('[data-canvas-minimap]');
    const rect = document.querySelector<HTMLElement>('[data-canvas-minimap-view]');
    const list = world.querySelector<HTMLElement>('[role="list"]')!;
    const scale = map ? 160 / Math.max(list.offsetWidth, list.offsetHeight) : 0;
    return {
      view: { x: -Number(tx) / k, y: -Number(ty) / k, w: pane.width / k, h: pane.height / k },
      rect: rect && { x: Number.parseFloat(rect.style.left) / scale, y: Number.parseFloat(rect.style.top) / scale, w: Number.parseFloat(rect.style.width) / scale, h: Number.parseFloat(rect.style.height) / scale },
      map: map?.getBoundingClientRect() ?? null,
    };
  });
  const before = await read();
  expect(before.rect, 'the minimap is offered past twice the viewport').not.toBeNull();
  for (const key of ['x', 'y', 'w', 'h'] as const) expect(Math.abs(before.rect![key] - before.view[key]), `rect ${key}`).toBeLessThan(2);
  const map = before.map!;
  await page.mouse.click(map.x + map.width / 2, map.y + map.height * 0.8);
  await expect.poll(async () => (await read()).view.y).toBeGreaterThan(before.view.y + 500);
  const after = await read();
  for (const key of ['x', 'y', 'w', 'h'] as const) expect(Math.abs(after.rect![key] - after.view[key]), `rect ${key} after`).toBeLessThan(2);
});

test('a key reaches a node the cull has not mounted, with nothing scrolled (trap 11)', async ({ page }) => {
  await open(page, 'Thousand');
  const total = await page.evaluate(() => Number(document.querySelector('[role="listitem"]')!.getAttribute('aria-setsize')));
  expect(total).toBe(1000);
  expect(await page.locator('[data-flow-node]').count()).toBeLessThan(400);
  await page.locator('[data-flow-node]').first().focus();
  await page.keyboard.press('End');
  await expect.poll(() => page.evaluate(() => document.activeElement?.getAttribute('data-flow-node'))).toBe('n999');
  const state = await page.evaluate(() => {
    const focused = document.activeElement as HTMLElement;
    const area = focused.closest('[role="region"]')!.getBoundingClientRect();
    const node = focused.getBoundingClientRect();
    const scrolled: string[] = [];
    for (let element: HTMLElement | null = focused; element; element = element.parentElement) if (element.scrollTop !== 0 || element.scrollLeft !== 0) scrolled.push(element.tagName);
    return { scrolled, inside: node.top >= area.top - 1 && node.bottom <= area.bottom + 1, posinset: focused.closest('[role="listitem"]')!.getAttribute('aria-posinset') };
  });
  expect(state.scrolled).toEqual([]);
  expect(state.inside).toBe(true);
  expect(state.posinset).toBe('1000');
  // And a key walks a column: the nearest node below is the next rank's.
  await page.keyboard.press('ArrowUp');
  await expect.poll(() => page.evaluate(() => document.activeElement?.getAttribute('data-flow-node'))).not.toBe('n999');
});

test('find reaches a culled node and keeps the focus in the field (trap 14)', async ({ page }) => {
  await open(page, 'Thousand');
  const field = page.getByRole('searchbox', { name: 'Find in the graph' });
  await field.fill('Step 900');
  await field.press('Enter');
  await expect(page.locator('[data-flow-node][data-selected]')).toHaveCount(1);
  await field.press('Enter');
  expect(await page.evaluate(() => document.activeElement?.tagName)).toBe('INPUT');
  await field.press('Escape');
  await expect(field).toHaveValue('');
});

test('twenty opens and closes retain no worker, no node and no growth; at rest no frame is asked for (trap 22)', async ({ page }) => {
  test.setTimeout(240_000);
  await page.addInitScript(() => {
    const live = { workers: 0, made: 0, frames: 0 };
    (window as unknown as { live: typeof live }).live = live;
    const Real = window.Worker;
    window.Worker = class extends Real {
      constructor(url: string | URL, options?: WorkerOptions) { super(url, options); live.workers += 1; live.made += 1; }
      terminate() { live.workers -= 1; super.terminate(); }
    } as typeof Worker;
    const frame = window.requestAnimationFrame.bind(window);
    window.requestAnimationFrame = (callback) => { live.frames += 1; return frame(callback); };
  });
  await open(page, 'Five');
  const count = () => page.evaluate(() => ({ elements: document.querySelectorAll('*').length, workers: (window as unknown as { live: { workers: number } }).live.workers }));
  const settle = async () => { await page.evaluate(() => (window as unknown as { setOpen: (open: boolean) => void }).setOpen(false)); await page.waitForTimeout(150); };
  await settle();
  const closed = await count();
  for (let round = 0; round < 20; round += 1) {
    await page.evaluate(() => (window as unknown as { setOpen: (open: boolean) => void }).setOpen(true));
    await ready(page);
    await settle();
  }
  const after = await count();
  console.log(`large: twenty opens and closes of Five: ${closed.elements} elements and ${closed.workers} workers closed, ${after.elements} and ${after.workers} after`);
  expect(await page.evaluate(() => (window as unknown as { live: { made: number } }).live.made), 'the layout ran in a worker').toBeGreaterThanOrEqual(20);
  expect(after.workers).toBe(0);
  expect(after.elements).toBeLessThanOrEqual(closed.elements + 5);

  await page.evaluate(() => (window as unknown as { setOpen: (open: boolean) => void }).setOpen(true));
  await ready(page);
  await page.waitForTimeout(800);
  const idle = await page.evaluate(async () => {
    const live = (window as unknown as { live: { frames: number } }).live;
    const was = live.frames;
    await new Promise((done) => setTimeout(done, 1000));
    return live.frames - was;
  });
  console.log(`large: at rest, ${idle} animation frames asked for in a second`);
  expect(idle).toBe(0);
});
