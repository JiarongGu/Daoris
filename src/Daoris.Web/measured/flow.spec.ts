import { expect, test, type Page } from '@playwright/test';
import { measureFlows } from './measure';

// The measured-box check (FLOWCHART1a1; the flowchart design §7, traps 3 and 14): each of the hardest fixtures' stories,
// and the quest's own, drawn by `Flow` in a real browser at the frame's widths; every node's and every label's box
// measured and every edge sampled along its length by the browser's own geometry. No edge enters a box but its own ends',
// no label lies over a node or another label, and each drawn frame's crossings stay at or under its ceiling.
//
// The table is the engine's (§4.3): the same fixtures were drawn by @dagrejs/dagre over the same measured boxes on
// 2026-10-20, and D186's note of that day says what each engine gave and which was locked. Each ceiling is what the hand
// layout drew that day: a change that draws worse fails here, and one that draws better lowers it.

/** A story, by its stories file and name, and each frame it draws (not the rail) with that frame's crossing ceiling. */
const TABLE: Record<string, Record<number, number>> = {
  // The node lifecycle's multigraph, top to bottom with its eight returns on their lanes; the rail narrower.
  'flow/Lifecycle': { 1200: 29 },
  'flow/LifecycleChinese': { 1200: 29 },
  // The dense graph at the bounds, drawn whole at the width it needs; the rail at every frame width until folds come.
  'flow/Dense': { 1440: 155 },
  'flow/DenseChinese': { 1440: 155 },
  // Unequal boxes in both languages, left to right and top to bottom.
  'flow/Unequal': { 570: 0, 1200: 0 },
  'flow/UnequalChinese': { 570: 0, 1200: 0 },
  // An instance before and after an append.
  'flow/BeforeAnAppend': { 400: 0, 544: 0, 1200: 0 },
  'flow/AfterAnAppend': { 400: 0, 544: 0, 1200: 0 },
  // Several nodes current, and none.
  'flow/SeveralCurrent': { 544: 0, 1200: 0 },
  'flow/NoneCurrent': { 544: 0, 1200: 0 },
  // A quest's four statuses, the rail at the side bar's floor.
  'quest/Open': { 400: 0, 544: 0, 1200: 0 },
  'quest/Taken': { 400: 0, 544: 0, 1200: 0 },
  'quest/TakenChinese': { 400: 0, 544: 0, 1200: 0 },
  'quest/Done': { 400: 0, 544: 0, 1200: 0 },
  // The way a quest went (FLOWCHART1a2): its path drawn heavier and lines inside its statuses, which make its boxes taller and
  // unequal; and a conflict's refused edge toward the status it attempted, a path of its own.
  'quest/Went': { 400: 0, 544: 0, 1200: 0 },
  'quest/WentChinese': { 400: 0, 544: 0, 1200: 0 },
  // Its refused edge ranks Done after Declined, so each rank holds one node and the side bar's floor draws it top to bottom.
  'quest/Refused': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  // A session's states (FLOWCHART1b): five ranks of one node each, the outcomes last, its three skipping edges threading the
  // ranks between and the working ⇄ awaiting-person return and going on each on a lane of their own.
  // Top to bottom it crosses nothing; left to right, its three skipping edges cross each other and the returns' runs.
  'session/Working': { 400: 0, 544: 0, 1200: 7 },
  'session/WorkingChinese': { 400: 0, 544: 0, 1200: 7 },
  'session/WaitingOnYou': { 400: 0, 544: 0, 1200: 7 },
  'session/Completed': { 400: 0, 544: 0, 1200: 7 },
  'session/StoodDown': { 400: 0, 544: 0, 1200: 7 },
  // A session's parts (FLOWCHART1b): the strip is the rail at the side bar's floor. A retry and an asked part, and with the landing
  // plan's dashed node, draw top to bottom from 400 px; a fork's six parts need a rank of three, so its 400 px frame is the rail.
  'parts/Retry': { 400: 0, 544: 0, 1200: 0 },
  'parts/RetryChinese': { 400: 0, 544: 0, 1200: 0 },
  'parts/Fork': { 544: 0, 1200: 0 },
  'parts/ForkChinese': { 544: 0, 1200: 0 },
  'parts/WithTheLandingPlan': { 400: 0, 544: 0, 1200: 0 },
  'parts/WithTheLandingPlanChinese': { 400: 0, 544: 0, 1200: 0 },
  // How a landed branch landed (FLOWCHART1c): its tries and its advances stacked, the push, the pull request and the first advance
  // hung side by side from the landing, a rank of three, so it is the rail below 544 px; top to bottom from there, being too long
  // for left to right. A landing from before the advances is two nodes, left to right from 400 px; a merge's is one node a rank.
  'landing/Shared': { 544: 0, 1200: 0 },
  'landing/SharedChinese': { 544: 0, 1200: 0 },
  'landing/Waiting': { 544: 0, 1200: 0 },
  'landing/WaitingChinese': { 544: 0, 1200: 0 },
  'landing/Older': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'landing/MergedIntoTheLine': { 300: 0, 400: 0, 544: 0, 1200: 0 },
};

/** The story drawn, its faces loaded, and every drawn flow's edges drawn and holding still. */
async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForFunction(() => [...document.querySelectorAll<HTMLElement>('[data-flow-pane]')]
    .filter((pane) => pane.dataset.placement !== 'rail')
    .every((pane) => Number(pane.querySelector('[data-drawn]')?.getAttribute('data-drawn') ?? 0) > 0));
  await page.waitForTimeout(250);
}

const drawnCounts = (page: Page) => page.evaluate(() =>
  [...document.querySelectorAll('[data-drawn]')].map((element) => Number(element.getAttribute('data-drawn'))));

for (const [story, ceilings] of Object.entries(TABLE)) {
  test(`${story}: no edge through a box but its ends', no label over another box, crossings at or under the ceiling`, async ({ page }) => {
    await open(page, story);
    const measured = await page.evaluate(measureFlows);
    const drawn = measured.filter((flow) => flow.placement !== 'rail');
    // The frames that draw are the table's: a placement that changed is a change to look at, not to pass.
    const byWidth = (a: number, b: number) => a - b;
    expect(drawn.map((flow) => flow.frame).sort(byWidth), 'the frames drawn rather than the rail').toEqual(Object.keys(ceilings).map(Number).sort(byWidth));
    for (const flow of drawn) {
      const at = `${story} at ${flow.frame} px (${flow.placement})`;
      expect(flow.paths, at).toBeGreaterThan(0);
      expect(flow.collisions, at).toEqual([]);
      expect(flow.overlaps, at).toEqual([]);
      expect(flow.crossings, at).toBeLessThanOrEqual(ceilings[flow.frame]!);
    }
    // Drawn, the edges hold still: nothing measured loops back into what is measured.
    const counts = await drawnCounts(page);
    await page.waitForTimeout(400);
    expect(await drawnCounts(page)).toEqual(counts);
  });
}

test('drawing the edges takes no room, so it moves no node, and a resize draws them once (trap 14)', async ({ page }) => {
  await open(page, 'flow/SeveralCurrent');
  const frame = page.locator('[data-frame="1200"]');
  const boxes = () => frame.locator('[data-flow-node]').evaluateAll((nodes) => nodes.map((node) => {
    const rect = node.getBoundingClientRect();
    return [rect.left, rect.top, rect.width, rect.height];
  }));
  const before = await boxes();
  await frame.locator('svg[data-flow-ink], [data-flow-label]').evaluateAll((drawn) => drawn.forEach((element) => element.remove()));
  expect(await boxes()).toEqual(before);

  await open(page, 'flow/SeveralCurrent');
  const pane = page.locator('[data-frame="1200"] [data-drawn]');
  const was = Number(await pane.getAttribute('data-drawn'));
  await page.locator('[data-frame="1200"] > div').evaluate((element) => { (element as HTMLElement).style.width = '1100px'; });
  await expect.poll(async () => Number(await pane.getAttribute('data-drawn'))).toBe(was + 1);
  await page.waitForTimeout(400);
  expect(Number(await pane.getAttribute('data-drawn'))).toBe(was + 1);
});
