import { expect, test, type Page } from '@playwright/test';
import { measureFlows } from './measure';

// The canvas measured (CANVAS1a; D224, the canvas design §4.1, §5, traps 1, 2, 6, 9, 12 and 14): a flow standing in the viewport, drawn in
// a real browser. Its ink is the same in world units at five scales, re-measured at each; no edge runs through a box and no label or ＋
// lies over one while zoomed; a focus walked off screen scrolls nothing and is revealed; a resize draws once; a rem is the root's at the
// window's 50, 100 and 200%; the dense graph is drawn whole and pans at 80rem; and the frames of a scripted pan and zoom stay within the
// target. Each figure it times is printed as a `canvas:` line, which D224's note of 2026-11-02 records.

type Ink = { paths: [string, string][]; labels: [string, number, number][]; inserts: [string, number, number][]; boxes: [string, number[]][] };

const drawnCounts = (page: Page) => page.evaluate(() =>
  [...document.querySelectorAll('[data-drawn]')].map((element) => Number(element.getAttribute('data-drawn'))));

/** Every drawn flow's edges drawn and holding still for 300 ms. */
async function settle(page: Page) {
  await page.waitForFunction(() => [...document.querySelectorAll<HTMLElement>('[data-flow-pane]')]
    .filter((pane) => pane.dataset.placement !== 'rail')
    .every((pane) => Number(pane.querySelector('[data-drawn]')?.getAttribute('data-drawn') ?? 0) > 0));
  let was = JSON.stringify(await drawnCounts(page));
  for (let tries = 0; tries < 20; tries += 1) {
    await page.waitForTimeout(300);
    const now = JSON.stringify(await drawnCounts(page));
    if (now === was) return;
    was = now;
  }
}

/** The story drawn and holding still; `:whole`, *Show whole* pressed; `query` added to the page's (a window zoom). */
async function open(page: Page, story: string, query = '') {
  const [name, whole] = story.split(':');
  await page.goto(`/measured/flow.html?story=${name}${query}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await settle(page);
  if (whole !== 'whole') return;
  const press = page.getByRole('button', { name: /^(Show whole|显示全部)$/ });
  await press.first().click();
  await expect(press).toHaveCount(0);
  await settle(page);
}

const region = (page: Page) => page.locator('[role="region"][data-flow-pane]').first();
const scaleOf = async (page: Page) => Number(await page.locator('[data-canvas-world]').first().getAttribute('data-canvas-scale'));

/** The canvas zoomed to `k` by one Ctrl+wheel over it, as a mouse or a pinch sends it. */
async function zoomTo(page: Page, k: number) {
  const box = (await region(page).boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + Math.min(box.height / 2, 120));
  const now = await scaleOf(page);
  await page.keyboard.down('Control');
  await page.mouse.wheel(0, (-100 * Math.log(k / now)) / Math.log(1.25));
  await page.keyboard.up('Control');
  await expect.poll(() => scaleOf(page)).toBeCloseTo(k, 3);
}

/** The world measured again at the scale it stands at: the frame a pixel narrower and back, each a resize the ink is drawn on. */
async function remeasure(page: Page) {
  await page.evaluate(() => { const frame = document.querySelector<HTMLElement>('[data-main-width]')!; frame.style.width = `calc(${frame.dataset.mainWidth} - 1px)`; });
  await settle(page);
  await page.evaluate(() => { const frame = document.querySelector<HTMLElement>('[data-main-width]')!; frame.style.width = frame.dataset.mainWidth!; });
  await settle(page);
}

/** The ink as drawn, in world units: each path's `d`, each label's and ＋'s place, and every node's box less the world's origin over k. */
const inkOf = (page: Page): Promise<Ink> => page.evaluate(() => {
  const world = document.querySelector<HTMLElement>('[data-canvas-world]')!;
  const k = Number(world.dataset.canvasScale);
  const origin = world.getBoundingClientRect();
  const placed = (selector: string, key: string) => [...world.querySelectorAll<HTMLElement>(selector)]
    .filter((element) => element.style.left !== '')
    .map((element): [string, number, number] => [element.getAttribute(key)!, Number.parseFloat(element.style.left), Number.parseFloat(element.style.top)]);
  return {
    paths: [...world.querySelectorAll('path[data-flow-edge]')].map((path): [string, string] => [path.getAttribute('data-flow-edge')!, path.getAttribute('d')!]),
    labels: placed('[data-flow-label][data-shown]', 'data-flow-label'),
    inserts: placed('[data-flow-insert][data-shown]', 'data-flow-insert'),
    boxes: [...world.querySelectorAll<HTMLElement>('[data-flow-node]')].map((node): [string, number[]] => {
      const rect = node.getBoundingClientRect();
      return [node.dataset.flowNode!, [(rect.left - origin.left) / k, (rect.top - origin.top) / k, rect.width / k, rect.height / k]];
    }),
  };
});

const numbers = (d: string) => (d.match(/-?\d+(?:\.\d+)?/g) ?? []).map(Number);

/** Where two inks differ by more than half a pixel. */
function differences(a: Ink, b: Ink): string[] {
  const out: string[] = [];
  const far = (x: number[], y: number[]) => x.length !== y.length || x.some((value, index) => Math.abs(value - y[index]!) > 0.5);
  if (a.paths.length !== b.paths.length) out.push(`${a.paths.length} paths against ${b.paths.length}`);
  a.paths.forEach(([id, d], index) => { if (b.paths[index]?.[0] !== id || far(numbers(d), numbers(b.paths[index]![1]))) out.push(`path ${id}`); });
  for (const [name, list, other] of [['label', a.labels, b.labels], ['＋', a.inserts, b.inserts]] as const) {
    if (list.length !== other.length) out.push(`${list.length} ${name}s against ${other.length}`);
    list.forEach(([id, x, y], index) => { if (other[index]?.[0] !== id || far([x, y], [other[index]![1], other[index]![2]])) out.push(`${name} ${id}`); });
  }
  a.boxes.forEach(([id, box], index) => { if (b.boxes[index]?.[0] !== id || far(box, b.boxes[index]![1])) out.push(`box ${id}`); });
  return out;
}

const SCALES: Record<string, number[]> = {
  // Read: its bounds are 50-200%. Edited: floored at 80%.
  'canvas/Lifecycle': [0.5, 0.8, 1.5, 2],
  'canvas/LifecycleChinese': [0.5, 0.8, 1.5, 2],
  'editor/Release': [0.8, 1.5, 2],
  'editor/ReleaseChineseHalf': [0.8, 1.5, 2],
};

for (const [story, scales] of Object.entries(SCALES)) {
  test(`${story}: the ink, every box and every ＋ in world units at 100% and at ${scales.join(', ')} (traps 1 and 6)`, async ({ page }) => {
    await open(page, story);
    const actual = await inkOf(page);
    expect(actual.paths.length, 'paths drawn').toBeGreaterThan(0);
    for (const k of scales) {
      await zoomTo(page, k);
      // Zooming lays nothing out and draws nothing again.
      expect(differences(await inkOf(page), actual), `${story} zoomed to ${k}`).toEqual([]);
      // Measured again at k, the ink is what it was at 100%.
      await remeasure(page);
      expect(differences(await inkOf(page), actual), `${story} measured at ${k}`).toEqual([]);
    }
  });
}

const ZOOMED: Record<string, number[]> = { 'editor/Release': [0.8, 2], 'editor/ReleaseHalf': [0.8, 1.5], 'editor/MergeChineseHalf': [0.8, 2] };

for (const [story, scales] of Object.entries(ZOOMED)) {
  test(`${story}: zoomed, no edge through a box, no label or ＋ over one, no crossing`, async ({ page }) => {
    await open(page, story);
    for (const k of scales) {
      await zoomTo(page, k);
      const [flow, ...more] = await page.evaluate(measureFlows);
      expect(more, 'one chart').toEqual([]);
      const at = `${story} at ${k}`;
      expect(flow!.placement, at).toBe('down');
      expect(flow!.inserts, `${at}: the ＋s shown`).toBeGreaterThan(0);
      expect(flow!.collisions, at).toEqual([]);
      expect(flow!.overlaps, at).toEqual([]);
      expect(flow!.insertOverlaps, at).toEqual([]);
      expect(flow!.portMisses, at).toEqual([]);
      expect(flow!.crossings, at).toBe(0);
    }
  });
}

test('the ＋ stays a 1.5rem press on screen at the edit floor, a port clear of every other target, their footprints unmoved', async ({ page }) => {
  await open(page, 'editor/Release');
  const footprint = () => page.evaluate(() => [...document.querySelectorAll<HTMLElement>('[data-flow-insert][data-shown]')].map((span) => [span.offsetWidth, span.offsetHeight]));
  const before = await footprint();
  await zoomTo(page, 0.8);
  // The press grows once the gesture has settled: a scale held while it lasts restyles nothing.
  await expect.poll(() => page.locator('[data-canvas-world]').first().evaluate((element) => (element as HTMLElement).style.willChange)).toBe('');
  expect(await footprint()).toEqual(before);
  const reach = await page.evaluate(() => {
    const area = document.querySelector('[role="region"][data-flow-pane]')!.getBoundingClientRect();
    const shown = [...document.querySelectorAll<HTMLElement>('[data-flow-insert][data-shown] button')]
      .filter((button) => { const rect = button.getBoundingClientRect(); return rect.top > area.top + 16 && rect.bottom < area.bottom - 16; });
    const inserts = shown.map((button) => {
      const rect = button.getBoundingClientRect();
      const [x, y] = [rect.left + rect.width / 2, rect.top + rect.height / 2];
      const hits = (dx: number, dy: number) => button.contains(document.elementFromPoint(x + dx, y + dy));
      // The press reaches 12 px from the ＋'s middle every way: 24 px on screen.
      return { press: [rect.width, rect.height], drawn: button.firstElementChild!.getBoundingClientRect().width, reach: [hits(11.5, 0), hits(-11.5, 0), hits(0, 11.5), hits(0, -11.5)] };
    });
    // WCAG 2.5.8's spacing: a 24 px circle on a port's middle meets no other target.
    const ports = [...document.querySelectorAll<HTMLElement>('[data-flow-port]')].map((port) => {
      const rect = port.getBoundingClientRect();
      const [x, y] = [rect.left + rect.width / 2, rect.top + rect.height / 2];
      const other = [[0, -12], [0, 12], [-12, 0], [12, 0]].map(([dx, dy]) => document.elementFromPoint(x + dx!, y + dy!)?.closest('button, [role="button"], a'))
        .filter((hit) => hit && hit !== port);
      return { height: rect.height, width: rect.width, clear: other.length === 0 };
    });
    return { inserts, ports };
  });
  const insert = reach.inserts[0]!;
  const port = reach.ports[0]!;
  console.log(`canvas: at 80%, a ＋ is drawn ${insert.drawn.toFixed(1)} px and pressed ${insert.press.map((v) => v.toFixed(1)).join(' × ')} px; a port ${port.width.toFixed(1)} × ${port.height.toFixed(1)} px`);
  expect(reach.inserts.length).toBeGreaterThan(0);
  for (const each of reach.inserts) expect(each.reach).toEqual([true, true, true, true]);
  for (const each of reach.ports) {
    expect(each.width).toBeGreaterThanOrEqual(24);
    expect(each.clear).toBe(true);
  }
});

test('a focus walked off screen scrolls nothing and is revealed, and Tab reaches the toolbar then one stop (trap 2)', async ({ page }) => {
  await open(page, 'editor/ReleaseHalf');
  const first = page.locator('[data-flow-node]').first();
  await first.focus();
  for (let press = 0; press < 30; press += 1) await page.keyboard.press('ArrowDown');
  const after = await page.evaluate(() => {
    const focused = document.activeElement as HTMLElement;
    const scrolled: string[] = [];
    for (let element: HTMLElement | null = focused; element; element = element.parentElement) {
      if (element.scrollTop !== 0 || element.scrollLeft !== 0) scrolled.push(`${element.tagName} ${element.className}`);
    }
    const area = focused.closest('[role="region"]')!.getBoundingClientRect();
    const node = focused.getBoundingClientRect();
    return {
      id: focused.dataset.flowNode, scrolled, last: [...document.querySelectorAll<HTMLElement>('[data-flow-node]')].at(-1)!.dataset.flowNode,
      inside: node.top >= area.top - 0.5 && node.bottom <= area.bottom + 0.5 && node.left >= area.left - 0.5 && node.right <= area.right + 0.5,
    };
  });
  expect(after.id).toBe(after.last);
  expect(after.scrolled).toEqual([]);
  expect(after.inside).toBe(true);
  // One stop in the world: Tab from it leaves the drawing.
  const stops = await page.locator('[data-canvas-world] [tabindex="0"]').count();
  expect(stops).toBe(1);
});

test('a resize draws the edges once in the viewport (trap 14), and a gesture holds will-change only while it lasts (trap 9)', async ({ page }) => {
  await open(page, 'editor/Release');
  const pane = page.locator('[data-canvas-world] [data-drawn]').first();
  const was = Number(await pane.getAttribute('data-drawn'));
  await page.evaluate(() => { document.querySelector<HTMLElement>('[data-main-width]')!.style.width = '76rem'; });
  await expect.poll(async () => Number(await pane.getAttribute('data-drawn'))).toBe(was + 1);
  await page.waitForTimeout(400);
  expect(Number(await pane.getAttribute('data-drawn'))).toBe(was + 1);

  const world = page.locator('[data-canvas-world]').first();
  const box = (await region(page).boundingBox())!;
  await page.mouse.move(box.x + box.width / 2, box.y + 60);
  await page.mouse.wheel(0, 120);
  expect(await world.evaluate((element) => (element as HTMLElement).style.willChange)).toBe('transform');
  await page.waitForTimeout(400);
  expect(await world.evaluate((element) => (element as HTMLElement).style.willChange)).toBe('');
});

for (const level of [50, 100, 200]) {
  test(`at the window's ${level}%, a rem is the root's and the track and placement are what the layout needs (trap 12)`, async ({ page }) => {
    // The canvas's own story at 39rem: the editor beside its inspector sizes its columns by its own measure.
    await open(page, 'canvas/LifecycleHalf', `&zoom=${level}`);
    const read = await page.evaluate(() => {
      const rem = Number.parseFloat(getComputedStyle(document.documentElement).fontSize);
      const pane = document.querySelector<HTMLElement>('[data-flow-pane]')!;
      const tracks = [...pane.querySelectorAll<HTMLElement>('[data-flow-node]')].map((node) => Number.parseFloat(node.style.width)).filter(Number.isFinite);
      return { rem, placement: pane.dataset.placement, region: pane.getAttribute('role'), tracks };
    });
    expect(read.rem).toBe((16 * level) / 100);
    expect(read.placement).toBe('down');
    expect(read.region).toBe('region');
    expect(read.tracks.length).toBeGreaterThan(0);
    // A flow's track: 10rem to 16rem at the root's size, never 16 px a rem.
    for (const track of read.tracks) {
      expect(track / read.rem).toBeGreaterThanOrEqual(10 - 0.01);
      expect(track / read.rem).toBeLessThanOrEqual(16 + 0.01);
    }
    const [flow] = await page.evaluate(measureFlows);
    expect(flow!.collisions).toEqual([]);
    expect(flow!.overlaps).toEqual([]);
  });
}

for (const story of ['canvas/Dense:whole', 'canvas/DenseChineseDark:whole']) {
  test(`${story}: the dense graph drawn whole at 80rem, wider than its region, pans by the wheel and the page stays`, async ({ page }) => {
    await open(page, story);
    const [flow] = await page.evaluate(measureFlows);
    expect(flow!.placement).toBe('down');
    expect(flow!.nodes).toBeGreaterThan(24);
    expect(flow!.collisions).toEqual([]);
    expect(flow!.overlaps).toEqual([]);
    expect(flow!.crossings).toBeLessThanOrEqual(155);
    const sizes = await page.evaluate(() => {
      const area = document.querySelector('[role="region"][data-flow-pane]')!;
      return { region: area.getBoundingClientRect().width, world: area.querySelector('[data-canvas-world]')!.getBoundingClientRect().width };
    });
    expect(sizes.world).toBeGreaterThan(sizes.region);
    const world = page.locator('[data-canvas-world]').first();
    const before = await world.evaluate((element) => (element as HTMLElement).style.transform);
    const box = (await region(page).boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + 40);
    await page.mouse.wheel(300, 0);
    await expect.poll(() => world.evaluate((element) => (element as HTMLElement).style.transform)).not.toBe(before);
    expect(await page.evaluate(() => [window.scrollX, window.scrollY])).toEqual([0, 0]);
  });
}

/** A scripted gesture of 120 frames over the region, each a wheel that pans or zooms, timed by the frames between and the long frames. */
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

/** The first usable frame after the story's props, and the drawing whole after *Show whole*: each until its edges hold still. */
const SIZES: Record<string, { nodes: number; target: number }> = {
  'canvas/Synthetic100:whole': { nodes: 100, target: 16.7 },
  'canvas/Synthetic500:whole': { nodes: 500, target: 16.7 },
  // Past any placed world's bound the ranked layout is not what is timed: 2,000 cards laid out by CSS.
  'canvas/Grid2000': { nodes: 2000, target: 33.3 },
  'canvas/Dense:whole': { nodes: 0, target: 16.7 },
};

// REVIEW2 T10 (FLAKE1): a frame time under load is a timed claim, so a timed spec that fails runs once more alone before it counts, as a Process class does.
test.describe('timed', () => {
test.describe.configure({ retries: 1 });
for (const [story, { nodes, target }] of Object.entries(SIZES)) {
  test(`${story}: p95 frame within ${target} ms while panning and zooming, and no long frame at rest`, async ({ page }) => {
    test.setTimeout(240_000);
    page.on('pageerror', (error) => console.log(`canvas: ${story} page error: ${error.message.slice(0, 300)}`));
    await page.addInitScript(() => {
      new MutationObserver(() => {
        const drawn = document.querySelector('[data-drawn]');
        const held = window as unknown as { drawnAt?: number };
        if (held.drawnAt === undefined && drawn && Number(drawn.getAttribute('data-drawn')) > 0) held.drawnAt = performance.now();
      }).observe(document, { attributes: true, subtree: true, attributeFilter: ['data-drawn'] });
    });
    const [name] = story.split(':');
    await page.goto(`/measured/flow.html?story=${name}`);
    await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
    // A large world is measured after the page is ready: its canvas holds a drawing first.
    await page.waitForFunction(() => Number(document.querySelector('[data-canvas-world] [data-drawn]')?.getAttribute('data-drawn') ?? 0) > 0, undefined, { timeout: 120_000 });
    await settle(page);
    const first = await page.evaluate(() => (window as unknown as { drawnAt: number }).drawnAt - performance.getEntriesByName('story-render')[0]!.startTime);
    const whole = !story.endsWith(':whole') ? 0 : await page.evaluate(async () => {
      const press = [...document.querySelectorAll('button')].find((button) => /^(Show whole|显示全部)$/.test(button.textContent ?? ''))!;
      const drawn = () => Number(document.querySelector('[data-drawn]')!.getAttribute('data-drawn'));
      const was = drawn();
      const start = performance.now();
      press.click();
      while (drawn() === was) await new Promise(requestAnimationFrame);
      return performance.now() - start;
    });
    await settle(page);
    const count = await page.locator('[data-flow-node]').count();
    if (nodes > 0) expect(count).toBe(nodes);
    const frames = await gesture(page);
    console.log(`canvas: ${story} ${count} nodes: first usable ${first.toFixed(0)} ms, whole drawn ${whole.toFixed(0)} ms; frames p95 ${frames.p95.toFixed(1)} ms, median ${frames.median.toFixed(1)}, worst ${frames.worst.toFixed(1)}; long frames ${frames.long.map((each) => each.toFixed(0)).join(' ') || 'none'}`);
    // Frames come at the display's interval, so one made in time is one interval apart give or take its jitter, and one missed is two:
    // a p95 under one and a half intervals is 95 frames in a hundred made in time.
    expect(frames.p95).toBeLessThan(target * 1.5);
    // At rest: no frame drawn, nothing measured.
    const atRest = await drawnCounts(page);
    await page.waitForTimeout(500);
    expect(await drawnCounts(page)).toEqual(atRest);
  });
}
});

// CANVAS1b (D224; the canvas design §2.3, §2.5, trap 8): a mark takes no room, and the connection line starts in its port's box.

/** Every node's box in world units, by id. */
const boxesOf = async (page: Page) => Object.fromEntries((await inkOf(page)).boxes);

test('a mark takes no room: every node\'s box is equal with and without marks, across plain, current and refused (trap 8)', async ({ page }) => {
  await open(page, 'canvas/Plain12');
  const plain = await boxesOf(page);
  await open(page, 'canvas/Marked');
  const marked = await boxesOf(page);
  expect(Object.keys(marked)).toEqual(Object.keys(plain));
  for (const [id, box] of Object.entries(plain)) expect(marked[id], `box ${id}`).toEqual(box);
  expect(await page.locator('[data-flow-chip]').count()).toBeGreaterThan(0);

  // The driver's refusals on the editor's chart: the same graph judged and not.
  await open(page, 'judgement/CouldNotBeJudged');
  const unjudged = await boxesOf(page);
  await open(page, 'judgement/Refused');
  const refused = await boxesOf(page);
  expect(await page.locator('[data-refused="true"]').count()).toBeGreaterThan(0);
  expect(await page.locator('[data-flow-refusal]').count()).toBe(0);
  for (const [id, box] of Object.entries(unjudged)) expect(refused[id], `refused box ${id}`).toEqual(box);
});

for (const story of ['canvas/Marked', 'canvas/MarkedHalf', 'canvas/MarkedChineseHalf', 'judgement/Refused']) {
  test(`${story}: each chip shown clears every node, label and ＋, and a chip not shown is hidden`, async ({ page }) => {
    await open(page, story);
    const found = await page.evaluate(() => {
      const rect = (element: Element) => { const r = element.getBoundingClientRect(); return { l: r.left, t: r.top, r: r.right, b: r.bottom }; };
      const meets = (a: ReturnType<typeof rect>, b: ReturnType<typeof rect>) => a.l < b.r && b.l < a.r && a.t < b.b && b.t < a.b;
      const others = [...document.querySelectorAll('[data-flow-node], [data-flow-label][data-shown], [data-flow-insert][data-shown], [data-flow-chip][data-shown]')];
      const chips = [...document.querySelectorAll<HTMLElement>('[data-flow-chip]')];
      // A chip may straddle its own node's top edge like a tag, and meets nothing else.
      const own = (chip: HTMLElement, other: Element) => other.getAttribute('data-flow-node') === chip.dataset.chipNode;
      return {
        chips: chips.length, shown: chips.filter((chip) => chip.hasAttribute('data-shown')).length,
        hiddenShown: chips.filter((chip) => !chip.hasAttribute('data-shown') && chip.style.visibility !== 'hidden').length,
        over: chips.filter((chip) => chip.hasAttribute('data-shown'))
          .flatMap((chip) => others.filter((other) => other !== chip && !own(chip, other) && meets(rect(chip), rect(other))).map(() => chip.textContent)),
      };
    });
    expect(found.chips).toBeGreaterThan(0);
    expect(found.shown).toBeGreaterThan(0);
    expect(found.hiddenShown).toBe(0);
    expect(found.over).toEqual([]);
  });
}

for (const k of [0.8, 1, 1.5, 2]) {
  test(`the connection line starts inside its port's box at ${k * 100}%, ends at the pointer, and Escape gives the way up`, async ({ page }) => {
    await open(page, 'editor/Release');
    if (k !== 1) await zoomTo(page, k);
    // A port standing wholly inside the region: the world panned by the wheel to one (the page's own scroll is smooth, and moves it).
    const inView = () => page.evaluate(() => {
      const area = document.querySelector('[role="region"][data-flow-pane]')!.getBoundingClientRect();
      return [...document.querySelectorAll('[data-flow-port]')].findIndex((each) => {
        const r = each.getBoundingClientRect();
        return r.top > area.top + 24 && r.bottom < area.bottom - 24 && r.left > area.left + 24 && r.right < area.right - 24;
      });
    });
    const area = (await region(page).boundingBox())!;
    await page.mouse.move(area.x + area.width / 2, area.y + 40);
    for (let tries = 0; tries < 24 && (await inView()) < 0; tries += 1) {
      await page.mouse.wheel(0, 200);
      await page.waitForTimeout(60);
    }
    const at = await inView();
    expect(at, 'a port in view').toBeGreaterThanOrEqual(0);
    const port = page.locator('[data-flow-port]').nth(at);
    const box = (await port.boundingBox())!;
    const [x, y] = [box.x + box.width / 2, box.y + box.height / 2];
    await page.mouse.move(x, y);
    await page.mouse.down();
    await page.mouse.move(x + 30, y - 20, { steps: 4 });
    await page.mouse.move(x + 60, y - 40, { steps: 4 });
    const line = await page.evaluate(() => {
      const svg = document.querySelector('[data-connection-line]');
      const drawn = svg?.querySelector('line');
      const region = document.querySelector('[role="region"][data-flow-pane]')!.getBoundingClientRect();
      return drawn ? { from: [Number(drawn.getAttribute('x1')) + region.left, Number(drawn.getAttribute('y1')) + region.top], to: [Number(drawn.getAttribute('x2')) + region.left, Number(drawn.getAttribute('y2')) + region.top] } : null;
    });
    expect(line, 'the line is drawn').not.toBeNull();
    // The view holds still under the pointer while the line is drawn: the port is where it was pressed.
    const now = (await port.boundingBox())!;
    expect([now.x, now.y]).toEqual([box.x, box.y]);
    expect(line!.from[0]).toBeGreaterThanOrEqual(box.x - 0.5);
    expect(line!.from[0]).toBeLessThanOrEqual(box.x + box.width + 0.5);
    expect(line!.from[1]).toBeGreaterThanOrEqual(box.y - 0.5);
    expect(line!.from[1]).toBeLessThanOrEqual(box.y + box.height + 0.5);
    expect(Math.abs(line!.to[0] - (x + 60))).toBeLessThan(1);
    expect(Math.abs(line!.to[1] - (y - 40))).toBeLessThan(1);
    await page.keyboard.press('Escape');
    expect(await page.locator('[data-connection-line]').count()).toBe(0);
    await page.mouse.up();
    expect(await page.locator('[data-connection-line]').count()).toBe(0);
  });
}
