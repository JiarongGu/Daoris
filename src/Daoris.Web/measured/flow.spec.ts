import { expect, test, type Page } from '@playwright/test';
import { measureFlows } from './measure';

// The measured-box check (the flowchart design §7, traps 3 and 14): each of the hardest fixtures' stories,
// and the quest's own, drawn by `Flow` in a real browser at the frame's widths; every node's and every label's box
// measured and every edge sampled along its length by the browser's own geometry. No edge enters a box but its own ends',
// no label lies over a node or another label, and each drawn frame's crossings stay at or under its ceiling.
// The table is the engine's (§4.3).
// Each ceiling is what the hand
// layout draws: a change that draws worse fails here, and one that draws better lowers it.

/**
 * A story, by its stories file and name, and each frame it draws (not the rail) with that frame's crossing ceiling. A name ending
 * `:whole` is measured once *Show whole* is pressed in every frame.
 */
const TABLE: Record<string, Record<number, number>> = {
  // The node lifecycle's multigraph, top to bottom with its eight returns on their lanes; the rail narrower.
  'flow/Lifecycle': { 1200: 29 },
  'flow/LifecycleChinese': { 1200: 29 },
  // The dense graph at the bounds: folded after its entries, nothing having started, and drawn whole, once *Show
  // whole* is pressed: in its 1440 px frame it fits, and in the 1200 px one, past the canvas's floor, the same drawing pans.
  'flow/Dense': { 1200: 5, 1440: 5 },
  'flow/DenseChinese': { 1200: 5, 1440: 5 },
  'flow/Dense:whole': { 1200: 155, 1440: 155 },
  'flow/DenseChinese:whole': { 1200: 155, 1440: 155 },
  // A run too large: 96 nodes folded around one current node, three, and none, three lines of work a rank wide.
  'flow/TooLarge': { 544: 0, 1200: 0 },
  'flow/TooLargeChinese': { 544: 0, 1200: 0 },
  'flow/TooLargeThreeCurrent': { 544: 0, 1200: 0 },
  'flow/TooLargeNoneCurrent': { 544: 0, 1200: 0 },
  // A workflow and its run, through the one drawing: a step a rank under its sections from the side bar's
  // 300 px, since its 14rem track fits there; a failure path follows its step alone, so it draws at every width too.
  'workflow/Current': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/CurrentChinese': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/NothingSet': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/AFailurePath': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/AFailurePathChinese': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/RunInReview': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/RunInReviewChinese': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/RunPassedOver': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'workflow/RunLanded': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  // One drawing of a workflow graph: always in sections, so top to bottom at 80 and 39rem (1280 and 624 px) and the rail at
  // 25rem, whose two-way branch needs 29.5rem; the way back on its lane, the holds one group, and no crossing at all.
  'graph/Release': { 1280: 0, 624: 0 },
  'graph/ReleaseDark': { 1280: 0, 624: 0 },
  'graph/ReleaseChinese': { 1280: 0, 624: 0 },
  'graph/ReleaseChineseDark': { 1280: 0, 624: 0 },
  'graph/ReleaseHoldsOpen': { 1280: 0, 624: 0 },
  'graph/Merge': { 1280: 0, 624: 0, 400: 0 },
  'graph/MergeChinese': { 1280: 0, 624: 0, 400: 0 },
  // Two outcomes that reach one step, each from its own port, the third going nowhere; and the data wires drawn dotted at the
  // right while a step is selected (they cross the ways on in the gaps they pass), with the lanes kept free when none is.
  'graph/TwoOutcomes': { 1280: 0, 624: 0 },
  'graph/TwoOutcomesDark': { 1280: 0, 624: 0 },
  'graph/TwoOutcomesChinese': { 1280: 0, 624: 0 },
  'graph/ReleaseData': { 1280: 1, 624: 1 },
  'graph/ReleaseDataChinese': { 1280: 1, 624: 1 },
  'graph/ReleaseDataUnselected': { 1280: 0, 624: 0 },
  // A run's view: a run draws the route its decision took, so the release outcome is one chain that draws from the side bar's
  // floor, like a review's; the none outcome keeps the route not taken beside *No further step*, a two-way branch that needs 31rem, so the
  // 300 px and 25rem frames are the rail and 45% of the window (696 px) draws it. A selected step's record makes its node taller, never wider.
  'runview/ReleaseRun': { 300: 0, 400: 0, 696: 0 },
  'runview/ReleaseRunDark': { 300: 0, 400: 0, 696: 0 },
  'runview/ReleaseRunChinese': { 300: 0, 400: 0, 696: 0 },
  'runview/ReleaseRunChineseDark': { 300: 0, 400: 0, 696: 0 },
  'runview/NoneRun': { 696: 0 },
  'runview/NoneRunDark': { 696: 0 },
  'runview/NoneRunChinese': { 696: 0 },
  'runview/NoneRunChineseDark': { 696: 0 },
  'runview/ReviewRun': { 300: 0, 400: 0, 696: 0 },
  'runview/ReviewRunDark': { 300: 0, 400: 0, 696: 0 },
  'runview/ReviewRunChinese': { 300: 0, 400: 0, 696: 0 },
  'runview/ReviewRunChineseDark': { 300: 0, 400: 0, 696: 0 },
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
  // A session's states: five ranks of one node each, the outcomes last, its three skipping edges threading the
  // ranks between and the working ⇄ awaiting-person return and going on each on a lane of their own.
  // Top to bottom it crosses nothing; left to right, its three skipping edges cross each other and the returns' runs.
  'session/Working': { 400: 0, 544: 0, 1200: 7 },
  'session/WorkingChinese': { 400: 0, 544: 0, 1200: 7 },
  'session/WaitingOnYou': { 400: 0, 544: 0, 1200: 7 },
  'session/Completed': { 400: 0, 544: 0, 1200: 7 },
  'session/StoodDown': { 400: 0, 544: 0, 1200: 7 },
  // A session's parts: the strip is the rail at the side bar's floor. A retry and an asked part, and with the landing
  // plan's dashed node, draw top to bottom from 400 px; a fork's six parts need a rank of three, so its 400 px frame is the rail.
  'parts/Retry': { 400: 0, 544: 0, 1200: 0 },
  'parts/RetryChinese': { 400: 0, 544: 0, 1200: 0 },
  'parts/Fork': { 544: 0, 1200: 0 },
  'parts/ForkChinese': { 544: 0, 1200: 0 },
  'parts/WithTheLandingPlan': { 400: 0, 544: 0, 1200: 0 },
  'parts/WithTheLandingPlanChinese': { 400: 0, 544: 0, 1200: 0 },
  // How a landed branch landed: its tries and its advances stacked, the push, the pull request and the first advance
  // hung side by side from the landing, a rank of three, so it is the rail below 544 px; top to bottom from there, being too long
  // for left to right. A landing from before the advances is two nodes, left to right from 400 px; a merge's is one node a rank.
  'landing/Shared': { 544: 0, 1200: 0 },
  'landing/SharedChinese': { 544: 0, 1200: 0 },
  'landing/Waiting': { 544: 0, 1200: 0 },
  'landing/WaitingChinese': { 544: 0, 1200: 0 },
  'landing/Older': { 300: 0, 400: 0, 544: 0, 1200: 0 },
  'landing/MergedIntoTheLine': { 300: 0, 400: 0, 544: 0, 1200: 0 },
};

/**
 * The paths that leave a decision's port in each drawn frame of these stories: the release workflow's two outcomes, and the
 * three of the decision whose two outcomes reach one step. Each leaves its own port's box, and a story that drew none fails.
 */
const PORTED: Record<string, number> = {
  'graph/Release': 2, 'graph/ReleaseDark': 2, 'graph/ReleaseChinese': 2, 'graph/ReleaseChineseDark': 2, 'graph/ReleaseHoldsOpen': 2,
  'graph/TwoOutcomes': 3, 'graph/TwoOutcomesDark': 3, 'graph/TwoOutcomesChinese': 3,
  'graph/ReleaseData': 2, 'graph/ReleaseDataChinese': 2, 'graph/ReleaseDataUnselected': 2,
  // A run's decision draws the outcome it returned and the route it left: the release run one route, the none run both.
  'runview/ReleaseRun': 1, 'runview/NoneRun': 2,
};

/** Every drawn flow's edges drawn and holding still. */
async function drawn(page: Page) {
  await page.waitForFunction(() => [...document.querySelectorAll<HTMLElement>('[data-flow-pane]')]
    .filter((pane) => pane.dataset.placement !== 'rail')
    .every((pane) => Number(pane.querySelector('[data-drawn]')?.getAttribute('data-drawn') ?? 0) > 0));
  await page.waitForTimeout(250);
}

/** The story drawn, its faces loaded, and every drawn flow's edges drawn and holding still; whole, *Show whole* pressed in each frame. */
async function open(page: Page, story: string) {
  const [name, whole] = story.split(':');
  await page.goto(`/measured/flow.html?story=${name}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await drawn(page);
  if (whole !== 'whole') return;
  // The press in either language: the stories draw both.
  const press = page.getByRole('button', { name: /^(Show whole|显示全部)$/ });
  await expect(press.first()).toBeVisible();
  await press.evaluateAll((presses) => presses.forEach((each) => (each as HTMLElement).click()));
  await expect(press).toHaveCount(0);
  await drawn(page);
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
      expect(flow.portMisses, at).toEqual([]);
      if (PORTED[story] !== undefined) expect(flow.ported, `${at}: the paths that leave a port`).toBe(PORTED[story]);
      expect(flow.crossings, at).toBeLessThanOrEqual(ceilings[flow.frame]!);
    }
    // Drawn, the edges hold still: nothing measured loops back into what is measured.
    const counts = await drawnCounts(page);
    await page.waitForTimeout(400);
    expect(await drawnCounts(page)).toEqual(counts);
  });
}

/**
 * The workflow editor's stories: whether each draws its chart or the rail, at the main area's 80, 39 and 25rem in
 * both languages and themes. Where it draws, every ＋ shown on a way clears every node, every label shown and every other ＋, at least one
 * is shown, and no edge runs through a box or a label lies over one.
 */
const EDITOR: Record<string, 'drawn' | 'rail'> = {
  'editor/Release': 'drawn', 'editor/ReleaseHalf': 'drawn', 'editor/ReleaseNarrow': 'rail',
  'editor/ReleaseChinese': 'drawn', 'editor/ReleaseChineseHalf': 'drawn', 'editor/ReleaseChineseNarrow': 'rail',
  'editor/ReleaseDark': 'drawn', 'editor/ReleaseDarkHalf': 'drawn', 'editor/ReleaseChineseDarkNarrow': 'rail',
  'editor/Merge': 'drawn', 'editor/MergeHalf': 'drawn', 'editor/MergeChineseHalf': 'drawn', 'editor/MergeChineseDark': 'drawn',
  'editor/TwoOutcomes': 'drawn', 'editor/TwoOutcomesHalf': 'drawn',
  'editor/Refused': 'drawn', 'editor/RefusedNarrow': 'rail', 'editor/RefusedChineseHalf': 'drawn', 'editor/RefusedDark': 'drawn',
};

for (const [story, placed] of Object.entries(EDITOR)) {
  test(`${story}: ${placed === 'drawn' ? 'no ＋ over a node, a label or another ＋' : 'the rail'} (FLOWCHART2b)`, async ({ page }) => {
    await open(page, story);
    const [flow, ...more] = await page.evaluate(measureFlows);
    expect(more, 'one chart').toEqual([]);
    expect(flow!.placement === 'rail' ? 'rail' : 'drawn', story).toBe(placed);
    if (placed === 'rail') return;
    expect(flow!.inserts, `${story}: the ＋s shown`).toBeGreaterThan(0);
    expect(flow!.insertOverlaps, story).toEqual([]);
    expect(flow!.collisions, story).toEqual([]);
    expect(flow!.overlaps, story).toEqual([]);
  });
}

test('a data wire drawn on selection moves no node, and is drawn for each wire the selected step is an end of (FLOWCHART2a)', async ({ page }) => {
  const read = async (story: string) => {
    await open(page, story);
    const frame = page.locator('[data-frame="1280"]');
    return {
      boxes: await frame.locator('[data-flow-node]').evaluateAll((nodes) => nodes.map((node) => {
        const rect = node.getBoundingClientRect();
        return [node.getAttribute('data-flow-node'), rect.left, rect.top, rect.width, rect.height];
      })),
      paths: await frame.locator('path[data-flow-edge]').count(),
    };
  };
  const selected = await read('graph/ReleaseData');
  const unselected = await read('graph/ReleaseDataUnselected');
  expect(selected.boxes).toEqual(unselected.boxes);
  expect(selected.paths).toBe(unselected.paths + 2);
});

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
