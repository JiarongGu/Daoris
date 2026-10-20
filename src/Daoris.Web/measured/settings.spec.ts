import { expect, test, type Page } from '@playwright/test';
import { measureSettingRows } from './settingRows';

// The label track, measured (UIFIT1; the UI/UX design R1, §3.1's state table and §5's first trap): Settings' rows drawn in a
// real browser at the three widths the frame gives (`storyWidths`), in English and 中文. At the wide and the half width
// every value's box starts at its track's edge, one column a card; at the 400 px floor the track goes and every value sits
// under its words. The track itself stays between its floor and its cap, and at most half the card. jsdom lays nothing
// out, so no unit test can hold any of it; `ui.test.tsx` holds the structure.
//
// The run prints each story's label widths and the track each card chose: the measurements D185's UIFIT1 note set the
// bounds from. `npm --prefix src/Daoris.Web run e2e:measured -- --grep settings`.

/** The track's bounds, as `SETTING_TRACK` in `design/ui.tsx` spells them, in px at the page's 16 px root. */
const FLOOR = 12 * 16;
const CAP = 22 * 16;
const STACK = 32 * 16;
/** The gap between the track and the value column (`gap-x-6`). */
const GAP = 24;

/** Each story, by its book and name, with the main-area width it is drawn at. */
const TABLE: Record<string, 'wide' | 'half' | 'narrow'> = {
  // The atoms' story: a switch, a number with its notice, a path with its act, a long label, a pill, a row with no control.
  'ui/SettingRows': 'wide',
  'ui/SettingRowsHalf': 'half',
  'ui/SettingRowsNarrow': 'narrow',
  'ui/SettingRowsChinese': 'wide',
  'ui/SettingRowsChineseHalf': 'half',
  'ui/SettingRowsChineseNarrow': 'narrow',
  // Settings → This Daoris, whole.
  'wiring/ThisDaoris': 'wide',
  'wiring/ThisDaorisHalf': 'half',
  'wiring/ThisDaorisNarrow': 'narrow',
  'wiring/ThisDaorisChinese': 'wide',
  'wiring/ThisDaorisChineseHalf': 'half',
  'wiring/ThisDaorisChineseNarrow': 'narrow',
  // Settings → Driver, whole.
  'driver/Driver': 'wide',
  'driver/DriverHalf': 'half',
  'driver/DriverNarrow': 'narrow',
  'driver/DriverChinese': 'wide',
  'driver/DriverChineseHalf': 'half',
  'driver/DriverChineseNarrow': 'narrow',
  // Settings → AI features: its rows stand alone until ACCESSSWAP1 lands and its card takes a list, each on a track of its own.
  'ai/NothingNamed': 'wide',
  'ai/NothingNamedHalf': 'half',
  'ai/NothingNamedNarrow': 'narrow',
  'ai/NothingNamedChinese': 'wide',
  'ai/NothingNamedChineseNarrow': 'narrow',
  // A workspace's Setup, its sections open.
  'workspace/SetupOwnHalf': 'half',
  'workspace/SetupOwnNarrow': 'narrow',
  'workspace/SetupProcessChineseHalf': 'half',
};

const WIDTH_PX = { wide: 80 * 16, half: 39 * 16, narrow: 25 * 16 } as const;

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForTimeout(150);
}

const near = (a: number, b: number, within = 1) => Math.abs(a - b) <= within;

for (const [story, width] of Object.entries(TABLE)) {
  test(`${story} at ${width}: every value at its track's edge, or under its words at the floor`, async ({ page }) => {
    await open(page, story);
    const measured = await page.evaluate(measureSettingRows);
    expect(measured.frame, 'the story is drawn by storyWidths').toBe(`${WIDTH_PX[width] / 16}rem`);
    expect(near(measured.width, WIDTH_PX[width]), `the main area is ${WIDTH_PX[width]} px, not ${measured.width}`).toBe(true);
    const valued = measured.lists.map((list) => ({ ...list, rows: list.rows.filter((row) => row.value) })).filter((list) => list.rows.length > 0);
    expect(valued.length, 'rows with values were found').toBeGreaterThan(0);

    const said: string[] = [];
    for (const list of valued) {
      const at = `${story}, ${list.listed ? 'the list' : 'the lone row'} at y=${Math.round(list.box.y)} (${Math.round(list.box.w)} px wide)`;
      const stacked = list.box.w < STACK;
      const track = list.rows[0]!.cell.w;
      said.push(`${list.listed ? 'list' : 'row'} ${Math.round(list.box.w)}px: track ${Math.round(track)}px; labels ${list.rows.map((row) => `${Math.round(row.content)}`).join(' ')}`);
      for (const row of list.rows) {
        const where = `${at}: "${row.label}"`;
        if (stacked) {
          // The track goes: the value starts where the label does, under it.
          expect(near(row.value!.x, row.cell.x), `${where}: the value starts under its words (x ${row.value!.x} vs ${row.cell.x})`).toBe(true);
          expect(row.value!.y >= row.cell.y + row.cell.h - 1, `${where}: the value is under its words (y ${row.value!.y} vs the label's bottom ${row.cell.y + row.cell.h})`).toBe(true);
          continue;
        }
        // One track a card: every label cell is the track's width, and every value starts at its edge, a gap after.
        expect(near(row.cell.w, track), `${where}: the label cell is the track's width (${row.cell.w} vs ${track})`).toBe(true);
        expect(near(row.value!.x, row.cell.x + row.cell.w + GAP), `${where}: the value starts at the track's edge (x ${row.value!.x} vs ${row.cell.x + row.cell.w + GAP})`).toBe(true);
        expect(near(row.value!.y, row.cell.y, 2), `${where}: the value sits beside its words (y ${row.value!.y} vs ${row.cell.y})`).toBe(true);
      }
      if (!stacked) {
        // The track is the widest label between the floor and the cap, and never past half the card.
        const widest = Math.max(...list.rows.map((row) => row.content));
        const limit = Math.min(CAP, list.box.w / 2);
        const wanted = Math.min(Math.max(FLOOR, widest), limit);
        expect(near(track, wanted, 2), `${at}: the track is ${track}, not the widest label ${Math.round(widest)} between ${FLOOR} and ${Math.round(limit)}`).toBe(true);
      }
    }
    console.log(`[measured] ${story} (${measured.width}px): ${said.join(' | ')}`);
  });
}
