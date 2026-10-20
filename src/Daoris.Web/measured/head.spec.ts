import { expect, test, type Page } from '@playwright/test';
import { measureRecordHead, type RecordHeadMeasure } from './recordHead';

// The record head's budget, measured (MILESTONEUX1; the UI/UX design §3.7 and §9 row 6; UX7's budget, the UX7 design §6.2):
// how far down the main area the conversation starts, with every block of the head present (the page head, the parts, the one
// Accept, the chain's line, the relations, the channel, the milestone's line and the trace's line), drawn in a real browser at the
// three widths the frame gives (`MAIN_WIDTHS`), in English and 中文. jsdom lays nothing out, so no unit test can hold it. The
// stories are `work/AttendedSession.head.stories.tsx`.
//
// UX7's 200 px binds a typical record (HEADFIT1b; D185's MILESTONEUX1, HEADFIT1 and HEADFIT1b notes): its page head, the record's
// head and *Details*, the chain's line and one folded line, the trace's, drawn as stories of their own and held to the budget. The
// head with every block is a ratchet: the folds took it from 806-1030 px to 375-472 and the head's blocks 8 px apart, not 16, to
// 307-404; what remains is the page head, the folded lines' own heights and the 8 px between nine blocks. Its budget stays a test
// expected to fail, which turns red the day a head fits it and says to remove the mark, and the head's height today is a ceiling a
// change that makes it worse fails. Each fold's cost is held exactly: the milestone's, the parts' and the relations' is one line
// where the main area is wide (two where its words wrap), and opening one moves what is under it and nothing above.
//
// `npm --prefix src/Daoris.Web run e2e:measured -- --grep head/`.

/** UX7's budget: the conversation's top, down from the main area's top, in px. */
const BUDGET = 200;
const WIDTH_PX = { wide: 80 * 16, half: 39 * 16, narrow: 25 * 16 } as const;
/** The milestone's label, as its region is named in each language. */
const MILESTONE = { en: 'Where this work stands', zh: '这项工作进展到哪里' } as const;

/**
 * The other two folds (HEADFIT1): the block as its landmark is named in each language, the press that opens it, the press that
 * folds it again, and the least it grows by opening, so a fold that opened nothing fails.
 */
const FOLDS = [
  {
    block: { en: 'Parts', zh: '各段' }, show: { en: 'Show the parts', zh: '展开各段' }, hide: { en: 'Hide the parts', zh: '收起各段' }, least: 100,
  },
  {
    block: { en: 'Who it worked with', zh: '协作关系' }, show: { en: 'Show who it worked with', zh: '展开协作关系' },
    hide: { en: 'Hide who it worked with', zh: '收起协作关系' }, least: 60,
  },
] as const;

/**
 * Each story, by its book and name: the main-area width it is drawn at, its language, and the head's height today with the
 * margin a font's rounding needs. A change that draws the head taller fails; one that draws it shorter lowers its ceiling.
 * Measured 2026-10-20, HEADFIT1b: 307, 324 and 342 px in English, 307, 307 and 324 in 中文, the head's blocks 8 px apart, the page
 * head's acts beside its title and the chain one line (375, 433, 472, 375, 416 and 455 before, HEADFIT1; 806, 916, 995, 806, 881
 * and 1030 before the parts and the relations folded).
 */
const TABLE: Record<string, { width: keyof typeof WIDTH_PX; language: keyof typeof MILESTONE; ceiling: number }> = {
  'head/Wide': { width: 'wide', language: 'en', ceiling: 317 },
  'head/Half': { width: 'half', language: 'en', ceiling: 334 },
  'head/Narrow': { width: 'narrow', language: 'en', ceiling: 352 },
  'head/ChineseWide': { width: 'wide', language: 'zh', ceiling: 317 },
  'head/ChineseHalf': { width: 'half', language: 'zh', ceiling: 317 },
  'head/ChineseNarrow': { width: 'narrow', language: 'zh', ceiling: 334 },
};

/** The page measured again until the head holds still, since a flowchart chooses its placement after it is drawn. */
async function settled(page: Page): Promise<RecordHeadMeasure> {
  let last = await page.evaluate(measureRecordHead);
  for (let tries = 0; tries < 20; tries += 1) {
    await page.waitForTimeout(150);
    const now = await page.evaluate(measureRecordHead);
    if (now.down === last.down) return now;
    last = now;
  }
  return last;
}

/** The story drawn and its faces loaded, then measured until it holds still. */
async function open(page: Page, story: string): Promise<RecordHeadMeasure> {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.locator('[data-record-frame] [data-conversation]').waitFor();
  return settled(page);
}

const said = (measured: RecordHeadMeasure) => measured.blocks.map((block) => `${Math.round(block.height)} px ${block.name}`).join('; ');

/**
 * The typical record's stories (HEADFIT1b): the width and the language, and the name *Stop…* and the chain's press carry there.
 * Measured 2026-10-20: 167 px at every width in either language, the page head 70 px with its acts beside the title and the
 * chain's line 30 (208 at half and 229 at the English floor when the acts took a row under the facts and the press wrapped).
 */
const TYPICAL: Record<string, { width: keyof typeof WIDTH_PX; language: keyof typeof MILESTONE }> = {
  'head/TypicalWide': { width: 'wide', language: 'en' },
  'head/TypicalHalf': { width: 'half', language: 'en' },
  'head/TypicalNarrow': { width: 'narrow', language: 'en' },
  'head/TypicalChineseWide': { width: 'wide', language: 'zh' },
  'head/TypicalChineseHalf': { width: 'half', language: 'zh' },
  'head/TypicalChineseNarrow': { width: 'narrow', language: 'zh' },
};
const STOP = { en: 'Stop…', zh: '停止…' } as const;
const CHAIN = { en: 'How this work ran', zh: '工作流转' } as const;
const SHOW_CHAIN = { en: 'Show how it ran', zh: '展开流转详情' } as const;

for (const [story, { width, language }] of Object.entries(TYPICAL)) {
  test(`${story} at ${width}: a typical record keeps UX7's ${BUDGET} px budget, its acts beside its title and its chain one line`, async ({ page }) => {
    const measured = await open(page, story);
    expect(measured.width, `the main area is ${WIDTH_PX[width]} px, not ${measured.width}`).toBe(WIDTH_PX[width]);
    // The page head, the head's own line, the chain's line and the trace's: one that lost a block would keep the budget by losing
    // what it measures.
    expect(measured.blocks.length, `the blocks: ${said(measured)}`).toBe(4);
    console.log(`[measured] ${story} (${measured.width}px): the conversation ${measured.down} px down, budget ${BUDGET}; ${said(measured)}`);
    expect(measured.down, `the conversation starts ${measured.down} px down at ${width}, over ${BUDGET}: ${said(measured)}`).toBeLessThanOrEqual(BUDGET);

    // *Stop…* on the title's row at every width, one press by its name, never in the ⋯: its words above 640 px, its square at or
    // below, the words its tip.
    const title = await page.locator('[data-record-frame] h1').boundingBox();
    const stop = page.getByRole('button', { name: STOP[language], exact: true });
    await expect(stop).toBeVisible();
    const box = (await stop.boundingBox())!;
    expect(box.y, "Stop… sits on the title's row").toBeLessThan(title!.y + title!.height);
    if (width === 'wide') await expect(stop).toHaveText(STOP[language]);
    else {
      await expect(stop).toHaveText('');
      await stop.hover();
      await expect(page.getByRole('tooltip')).toHaveText(STOP[language]);
    }
    // The title one line, cut with an ellipsis and whole in its tip; no *Show all* under it.
    await expect(page.locator('[data-record-frame] h1')).toHaveAttribute('title', /Read the export table from the app/);

    // The chain's line is one line, its press at its end.
    const chain = page.getByRole('navigation', { name: CHAIN[language] });
    const line = (await chain.boundingBox())!;
    const show = (await chain.getByRole('button', { name: SHOW_CHAIN[language] }).boundingBox())!;
    expect(show.y, "the press is on the chain's one line").toBeLessThan(line.y + 8);
    expect(show.x + show.width, 'at its end').toBeCloseTo(line.x + line.width, 0);
  });
}

for (const [story, { width, language, ceiling }] of Object.entries(TABLE)) {
  test(`${story} at ${width}: every block of the head is drawn, the milestone, the parts and the relations folded to one line (two where it wraps), the head no taller than today`, async ({ page }) => {
    const measured = await open(page, story);
    expect(measured.width, `the main area is ${WIDTH_PX[width]} px, not ${measured.width}`).toBe(WIDTH_PX[width]);
    // The page head, the head's own line, the parts, Accept, the chain's line, the relations, the channel, the milestone and the
    // trace: a head that lost one would pass its ceiling by losing what it measures.
    expect(measured.blocks.length, `the blocks: ${said(measured)}`).toBe(9);
    const milestone = measured.blocks.find((block) => block.name.includes(`[${MILESTONE[language]}]`));
    expect(milestone, `the milestone is among the blocks: ${said(measured)}`).toBeDefined();
    console.log(`[measured] ${story} (${measured.width}px): the conversation ${measured.down} px down, budget ${BUDGET}; ${said(measured)}`);

    // Folded: the line, whole, in the one text size's line height (17 px, 20 with the line's leading), two lines at the floor where
    // it wraps. The facts are not drawn.
    const LINE = 20;
    expect(milestone!.height, `the milestone's fold at ${width}`).toBeLessThanOrEqual(width === 'wide' && language === 'en' ? LINE : 2 * LINE);
    await expect(page.getByRole('button', { name: new RegExp(MILESTONE[language]) })).toHaveAttribute('aria-expanded', 'false');
    await expect(page.getByRole('region', { name: MILESTONE[language] }).locator('dl')).toHaveCount(0);

    // The parts and the relations, folded likewise: a line each, and nothing of either drawn (no flowchart, no strip, no list).
    for (const fold of FOLDS) {
      const block = measured.blocks.find((one) => one.name.includes(`[${fold.block[language]}]`));
      expect(block, `${fold.block.en} is among the blocks: ${said(measured)}`).toBeDefined();
      expect(block!.height, `the ${fold.block.en} fold at ${width}`).toBeLessThanOrEqual(width === 'wide' && language === 'en' ? LINE : 2 * LINE);
      await expect(page.getByRole('button', { name: fold.show[language] })).toBeVisible();
    }
    await expect(page.locator('[data-flow-pane]')).toHaveCount(0);
    await expect(page.getByRole('region', { name: FOLDS[1].block[language] }).locator('dl')).toHaveCount(0);
    expect(measured.down, `the head's height at ${width} is ${measured.down}: ${said(measured)}`).toBeLessThanOrEqual(ceiling);
  });

  test(`${story} at ${width}: UX7's ${BUDGET} px budget, kept by no head with every block yet`, async ({ page }) => {
    // The gap, held open: with the parts and the relations folded, what is left is the page head (70 to 111 px), the chain's line
    // and *Accept* (30 px each, the chain's 52 at the floor), six folded lines of 17 px or two, and 8 px between nine blocks; the
    // blocks alone are over the budget. A head that fits turns this red ("expected to fail"): remove the mark and lower the ceiling.
    test.fail(true, `the head with every block is over ${BUDGET} px at every width: its blocks alone are, before any space between them`);
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

  test(`${story} at ${width}: the parts and who it worked with open in place and fold again, moving what is under them and nothing above`, async ({ page }) => {
    const folded = await open(page, story);
    for (const fold of FOLDS) {
      const index = folded.blocks.findIndex((block) => block.name.includes(`[${fold.block[language]}]`));
      expect(index, `${fold.block.en} is among the blocks`).toBeGreaterThan(0);

      await page.getByRole('button', { name: fold.show[language] }).click();
      await expect(page.getByRole('button', { name: fold.hide[language] })).toBeVisible();
      const opened = await settled(page);
      const grew = opened.blocks[index]!.height - folded.blocks[index]!.height;
      console.log(`[measured] ${story}: ${fold.block.en} folded ${Math.round(folded.blocks[index]!.height)} px, opened ${Math.round(opened.blocks[index]!.height)} px`);
      expect(grew, `opening ${fold.block.en} adds what it holds`).toBeGreaterThan(fold.least);
      // Everything above keeps its place (the pinned page head is no block of the page's own flow), and the conversation moves down
      // by what the block grew: one block, opened where it stood.
      expect(opened.blocks.length, 'the same blocks, one of them taller').toBe(folded.blocks.length);
      for (let above = 1; above < index; above += 1) {
        expect(opened.blocks[above]!.top, `${folded.blocks[above]!.name} stays where it was`).toBeCloseTo(folded.blocks[above]!.top, 0);
      }
      expect(opened.down - folded.down, `the conversation moves down by what ${fold.block.en} grew`).toBeCloseTo(grew, 0);

      // The press back folds it to the line it was.
      await page.getByRole('button', { name: fold.hide[language] }).click();
      await expect(page.getByRole('button', { name: fold.show[language] })).toBeVisible();
      const again = await settled(page);
      expect(again.down, `${fold.block.en} folded again is where it was`).toBeCloseTo(folded.down, 0);
    }
  });
}
