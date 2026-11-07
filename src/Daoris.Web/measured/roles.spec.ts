import { expect, test, type Page } from '@playwright/test';
import { measureFlows } from './measure';

// The roles' diagram, measured: each story of `RoleDiagram.stories.tsx` drawn
// in a real browser at the main area's widths. At 80rem the diagram is across, its three 12rem columns and their gaps (42rem) beside
// the chosen node's detail, and every edge, sampled along its length by the browser's own geometry, is clear of every node but its own
// two ends, and no label lies over a node or another label. At 39 and 25rem it is its list, the same nodes with their edges in words,
// the head saying so, and the detail below it. jsdom lays nothing out, so no unit test can hold this.
// `npm --prefix src/Daoris.Web run e2e:measured -- roles.spec`.

/** Each story and the placement its width gives it. */
const TABLE: Record<string, 'across' | 'rail'> = {
  'roles/Wide': 'across',
  'roles/Chinese': 'across',
  'roles/Dark': 'across',
  'roles/PlaceChosen': 'across',
  'roles/PlaceChosenChinese': 'across',
  'roles/PlaceChosenDark': 'across',
  'roles/AgentChosen': 'across',
  'roles/RoleChosen': 'across',
  'roles/Appointed': 'across',
  'roles/AppointedDark': 'across',
  'roles/OneAgent': 'across',
  'roles/TooLarge': 'across',
  'roles/TooLargeChinese': 'across',
  // At 39rem the region holds the canvas's 36rem floor, the label above the roles; at 25rem, the list.
  'roles/Half': 'across',
  'roles/Narrow': 'rail',
  'roles/ChineseHalf': 'across',
  'roles/ChineseNarrow': 'rail',
  'roles/DarkHalf': 'across',
  'roles/DarkNarrow': 'rail',
  'roles/PlaceChosenHalf': 'across',
  'roles/AgentChosenNarrowChinese': 'rail',
  'roles/TooLargeNarrow': 'rail',
  'roles/TooLargeHalf': 'across',
};

/**
 * The crossings each story's staffed edges make, the table of what the bands may cross; none above it. The fixture's ten
 * choices cross six times as its agents are ordered by the bands their roles stand in; a fixture with one agent or folded bands crosses none.
 */
const CROSSINGS: Record<string, number> = {
  'roles/OneAgent': 0, 'roles/TooLarge': 0, 'roles/TooLargeChinese': 0, 'roles/TooLargeHalf': 0,
};
const FIXTURE_CROSSINGS = 6;

/** The story drawn, its faces loaded, and a drawn diagram's edges drawn and holding still. */
async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForFunction(() => [...document.querySelectorAll<HTMLElement>('[data-flow-pane]')]
    .filter((pane) => pane.dataset.placement !== 'rail')
    .every((pane) => Number(pane.querySelector('[data-drawn]')?.getAttribute('data-drawn') ?? 0) > 0));
  await page.waitForTimeout(250);
}

/** Where the diagram's pane and the detail stand, so beside and below are read from their boxes. */
const boxes = (page: Page) => page.evaluate(() => {
  const box = (selector: string) => {
    const rect = document.querySelector(selector)?.getBoundingClientRect();
    return rect ? { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom } : null;
  };
  return { pane: box('[data-flow-pane]'), detail: box('[data-diagram-detail]'), narrow: document.querySelector('[data-diagram-narrow]') !== null };
});

for (const [story, placement] of Object.entries(TABLE)) {
  test(`${story}: ${placement === 'across' ? 'across, every edge clear of every node but its ends' : 'its list, the head saying so'}`, async ({ page }) => {
    await open(page, story);
    const measured = await page.evaluate(measureFlows);
    expect(measured, story).toHaveLength(1);
    const [diagram] = measured;
    expect(diagram!.placement, story).toBe(placement);
    expect(diagram!.nodes, story).toBeGreaterThan(3);
    const at = await boxes(page);
    if (placement === 'across') {
      expect(diagram!.paths, story).toBeGreaterThan(0);
      expect(diagram!.collisions, story).toEqual([]);
      expect(diagram!.overlaps, story).toEqual([]);
      expect(at.narrow, story).toBe(false);
      // The detail stands beside the structure at 80rem, and below it where the main area holds one column.
      if (/Half$/.test(story)) expect(at.detail!.top, story).toBeGreaterThanOrEqual(at.pane!.bottom);
      else expect(at.detail!.left, story).toBeGreaterThanOrEqual(at.pane!.right);
      console.log(`roles: ${story} ${diagram!.nodes} nodes, ${diagram!.paths} paths, ${diagram!.crossings} crossings`);
      expect(diagram!.crossings, story).toBeLessThanOrEqual(CROSSINGS[story] ?? FIXTURE_CROSSINGS);
    } else {
      expect(diagram!.paths, story).toBe(0);
      expect(at.narrow, story).toBe(true);
      // The detail stands below the list.
      expect(at.detail!.top, story).toBeGreaterThanOrEqual(at.pane!.bottom);
    }
  });
}

for (const story of ['roles/Hundred', 'roles/HundredHalf']) {
  test(`${story}: a hundred places and roles drawn whole, every staffed edge clear of every node, within the crossings of its fixture`, async ({ page }) => {
    test.setTimeout(120_000);
    await open(page, story);
    await page.getByRole('button', { name: 'Show whole' }).click();
    await expect(page.locator('[data-flow-node][data-kind="fold"]')).toHaveCount(0);
    await page.waitForFunction(() => Number(document.querySelector('[data-drawn]')?.getAttribute('data-drawn') ?? 0) > 0);
    await page.waitForTimeout(600);
    const [diagram] = await page.evaluate(measureFlows);
    console.log(`roles: ${story} whole: ${diagram!.nodes} nodes, ${diagram!.paths} paths, ${diagram!.crossings} crossings, ${diagram!.collisions.length} collisions`);
    expect(diagram!.placement).toBe('across');
    expect(diagram!.nodes).toBeGreaterThan(100);
    expect(diagram!.collisions).toEqual([]);
    expect(diagram!.overlaps).toEqual([]);
  });
}

test('the folded workspaces open whole, still across and every edge clear of its nodes', async ({ page }) => {
  await open(page, 'roles/TooLarge');
  const folds = await page.locator('[data-flow-node][data-kind="fold"]').count();
  expect(folds).toBe(2);
  await page.getByRole('button', { name: 'Show whole' }).click();
  await expect(page.locator('[data-flow-node][data-kind="fold"]')).toHaveCount(0);
  await page.waitForTimeout(250);
  const [diagram] = await page.evaluate(measureFlows);
  expect(diagram!.placement).toBe('across');
  expect(diagram!.collisions).toEqual([]);
  expect(diagram!.overlaps).toEqual([]);
});
