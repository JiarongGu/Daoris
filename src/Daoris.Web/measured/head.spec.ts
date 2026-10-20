import { expect, test, type Page } from '@playwright/test';
import { measureRecordHead, type RecordHeadMeasure } from './recordHead';

// The record head's budget, measured (MILESTONEUX1; the UI/UX design §3.7 and §9 row 6; UX7's budget, the UX7 design §6.2):
// how far down the main area the conversation starts, with every block of the head present (the page head, the parts strip,
// the one Accept, the chain's line, the relations, the channel, the milestone's line and the trace's line), drawn in a real
// browser at the three widths the frame gives (`MAIN_WIDTHS`), in English and 中文. jsdom lays nothing out, so no unit test can
// hold it. The stories are `work/AttendedSession.head.stories.tsx`.
//
// UX7's 200 px is not kept by the head as it stands, and the run says by how much and which block costs it: the parts'
// flowchart (FLOWCHART1b) alone is more than the whole budget. This row moves no block (D185's MILESTONEUX1 note), so the budget
// is a test expected to fail, which turns red the day a head fits it and says to remove the mark, and the head's height today
// is a ceiling a change that makes it worse fails. The milestone's own cost is held exactly: its fold is one line where the
// main area is wide, two at the floor, and opening it moves what is under it and nothing above.
//
// `npm --prefix src/Daoris.Web run e2e:measured -- --grep head/`.

/** UX7's budget: the conversation's top, down from the main area's top, in px. */
const BUDGET = 200;
const WIDTH_PX = { wide: 80 * 16, half: 39 * 16, narrow: 25 * 16 } as const;
/** The milestone's label, as its region is named in each language. */
const MILESTONE = { en: 'Where this work stands', zh: '这项工作进展到哪里' } as const;

/**
 * Each story, by its book and name: the main-area width it is drawn at, its language, and the head's height today with the
 * margin a font's rounding needs. A change that draws the head taller fails; one that draws it shorter lowers its ceiling.
 * Measured 2026-10-20: 806, 916 and 995 px in English, 806, 881 and 1030 in 中文.
 */
const TABLE: Record<string, { width: keyof typeof WIDTH_PX; language: keyof typeof MILESTONE; ceiling: number }> = {
  'head/Wide': { width: 'wide', language: 'en', ceiling: 820 },
  'head/Half': { width: 'half', language: 'en', ceiling: 930 },
  'head/Narrow': { width: 'narrow', language: 'en', ceiling: 1010 },
  'head/ChineseWide': { width: 'wide', language: 'zh', ceiling: 820 },
  'head/ChineseHalf': { width: 'half', language: 'zh', ceiling: 895 },
  'head/ChineseNarrow': { width: 'narrow', language: 'zh', ceiling: 1045 },
};

/** The story drawn and its faces loaded, then measured again until the flowchart has chosen its placement and holds still. */
async function open(page: Page, story: string): Promise<RecordHeadMeasure> {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.locator('[data-record-frame] [data-conversation]').waitFor();
  let last = await page.evaluate(measureRecordHead);
  for (let tries = 0; tries < 20; tries += 1) {
    await page.waitForTimeout(150);
    const now = await page.evaluate(measureRecordHead);
    if (now.down === last.down) return now;
    last = now;
  }
  return last;
}

const said = (measured: RecordHeadMeasure) => measured.blocks.map((block) => `${Math.round(block.height)} px ${block.name}`).join('; ');

for (const [story, { width, language, ceiling }] of Object.entries(TABLE)) {
  test(`${story} at ${width}: every block of the head is drawn, the milestone folded to one line (two at the floor), the head no taller than today`, async ({ page }) => {
    const measured = await open(page, story);
    expect(measured.width, `the main area is ${WIDTH_PX[width]} px, not ${measured.width}`).toBe(WIDTH_PX[width]);
    // The page head, the head's own line, the parts, Accept, the chain's line, the relations, the channel, the milestone and the
    // trace: a head that lost one would pass its ceiling by losing what it measures.
    expect(measured.blocks.length, `the blocks: ${said(measured)}`).toBe(9);
    const milestone = measured.blocks.find((block) => block.name.includes(`[${MILESTONE[language]}]`));
    expect(milestone, `the milestone is among the blocks: ${said(measured)}`).toBeDefined();
    console.log(`[measured] ${story} (${measured.width}px): the conversation ${measured.down} px down, budget ${BUDGET}; ${said(measured)}`);

    // Folded: the line, whole, in the one text size's line height, two lines at the floor where it wraps. The facts are not drawn.
    const LINE = 20;
    expect(milestone!.height, `the milestone's fold at ${width}`).toBeLessThanOrEqual(width === 'wide' && language === 'en' ? LINE : 2 * LINE);
    await expect(page.getByRole('button', { name: new RegExp(MILESTONE[language]) })).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByRole('region', { name: MILESTONE[language] }).locator('dl')).toHaveCount(0);
    expect(measured.down, `the head's height at ${width} is ${measured.down}: ${said(measured)}`).toBeLessThanOrEqual(ceiling);
  });

  test(`${story} at ${width}: UX7's ${BUDGET} px budget, kept by no head with every block yet`, async ({ page }) => {
    // The gap, held open: the parts' flowchart and the relations alone spend more than the budget, and the pack moves no
    // block in MILESTONEUX1. A head that fits turns this red ("expected to fail"): remove the mark and the ceiling above.
    test.fail(true, `the head with every block is over ${BUDGET} px at every width; MILESTONEUX1 measured it and moved no block`);
    const measured = await open(page, story);
    expect(measured.down, `the conversation starts ${measured.down} px down at ${width}, over ${BUDGET}: ${said(measured)}`).toBeLessThanOrEqual(BUDGET);
  });

  test(`${story} at ${width}: the milestone opens in place, moving what is under it and nothing above, its labels stacked at the floor`, async ({ page }) => {
    const folded = await open(page, story);
    const fold = page.getByRole('button', { name: new RegExp(MILESTONE[language]) });
    await fold.click();
    await expect(fold).toHaveAttribute('aria-expanded', 'true');
    await page.waitForTimeout(150);
    const opened = await page.evaluate(measureRecordHead);

    const index = folded.blocks.findIndex((block) => block.name.includes(`[${MILESTONE[language]}]`));
    const grew = opened.blocks[index]!.height - folded.blocks[index]!.height;
    console.log(`[measured] ${story}: the milestone folded ${Math.round(folded.blocks[index]!.height)} px, opened ${Math.round(opened.blocks[index]!.height)} px`);
    expect(grew, 'opening it adds its facts').toBeGreaterThan(60);
    // Everything above keeps its place, and the conversation moves down by what it grew. The page head is pinned, so it stays on
    // the screen as the area scrolls and is no block of the page's own flow.
    for (let above = 1; above < index; above += 1) {
      expect(opened.blocks[above]!.top, `${folded.blocks[above]!.name} stays where it was`).toBeCloseTo(folded.blocks[above]!.top, 0);
    }
    expect(opened.down - folded.down, 'the conversation moves down by what the milestone grew').toBeCloseTo(grew, 0);

    // The label column stacks over its words under 24rem of the list's own width: at the floor, not where there is room.
    const rows = await page.getByRole('region', { name: MILESTONE[language] }).locator('dl > div').evaluateAll((all) => all.map((row) => {
      const label = row.querySelector('dt')!.getBoundingClientRect();
      const value = row.querySelector('dd')!.getBoundingClientRect();
      return { labelTop: label.top, labelBottom: label.bottom, valueTop: value.top, labelLeft: label.left, valueLeft: value.left };
    }));
    expect(rows.length, 'the end, the result, the clean-up, the held attempt and the recovery').toBe(5);
    for (const row of rows) {
      if (width === 'narrow') {
        expect(row.valueTop, 'the value sits under its label').toBeGreaterThanOrEqual(row.labelBottom - 1);
        expect(row.valueLeft, 'and starts where it does').toBeCloseTo(row.labelLeft, 0);
      } else {
        expect(row.valueTop, 'the value sits beside its label').toBeLessThan(row.labelBottom);
        expect(row.valueLeft, 'in a column of its own').toBeGreaterThan(row.labelLeft + 40);
      }
    }
  });
}
