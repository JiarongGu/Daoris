import { expect, test, type Page } from '@playwright/test';

// A place's page, measured (SETUPUX1h; the setup-UX design §2.1 and §6 measure 2): the head and the three section lines within the first
// 400 px of the main area at 1546 px (80 rem) and 680 px (39 rem), in English and 中文, with the first section open on the size of body
// the design draws for Workflow. At the 400 px floor (25 rem) the lines wrap more, so only the sideways fit is held there. jsdom lays
// nothing out, so no unit test can hold this.
//
// `npm --prefix src/Daoris.Web run e2e:measured -- place.spec`.

const FIRST_SCREEN = 400;

/** Each story, with the main-area width it is drawn at. */
const TABLE: Record<string, 'wide' | 'half' | 'narrow'> = {
  'place/Repository': 'wide',
  'place/RepositoryHalf': 'half',
  'place/RepositoryNarrow': 'narrow',
  'place/RepositoryChinese': 'wide',
  'place/RepositoryChineseHalf': 'half',
  'place/RepositoryChineseNarrow': 'narrow',
  'place/NotDrivenHalf': 'half',
  'place/NotDrivenNarrowChinese': 'narrow',
  'place/Held': 'wide',
  'place/Workspace': 'wide',
  'place/WorkspaceHalf': 'half',
};

const WIDTH_PX = { wide: 80 * 16, half: 39 * 16, narrow: 25 * 16 } as const;

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForTimeout(150);
}

for (const [story, width] of Object.entries(TABLE)) {
  test(`${story} at ${width}: the head and the three section lines are within the first ${FIRST_SCREEN} px`, async ({ page }) => {
    await open(page, story);
    const measured = await page.evaluate(() => {
      const frame = document.querySelector<HTMLElement>('[data-main-width]')!;
      const top = frame.getBoundingClientRect().top;
      const head = document.querySelector('[data-place-head]')!.getBoundingClientRect();
      const lines = [...document.querySelectorAll<HTMLElement>('section[id^="place-"] > h2')].map((each) => each.getBoundingClientRect());
      return {
        width: frame.getBoundingClientRect().width,
        headTop: head.top - top,
        headBottom: head.bottom - top,
        lines: lines.map((each) => ({ top: each.top - top, bottom: each.bottom - top })),
        overflow: frame.scrollWidth - frame.clientWidth,
      };
    });
    expect(Math.abs(measured.width - WIDTH_PX[width]), `the main area is ${WIDTH_PX[width]} px, not ${measured.width}`).toBeLessThanOrEqual(1);
    // The Workspace stories and the machine's draw fewer sections only where the design says so; a repository draws all three.
    expect(measured.lines.length, 'a section line each').toBeGreaterThanOrEqual(3);
    const last = measured.lines.at(-1)!;
    // eslint-disable-next-line no-console
    console.log(`${story}: head ${Math.round(measured.headTop)}-${Math.round(measured.headBottom)}, last section line ends at ${Math.round(last.bottom)} px`);
    if (width !== 'narrow') {
      expect(last.bottom, `${story}: the third section line ends at ${last.bottom} px`).toBeLessThanOrEqual(FIRST_SCREEN);
    }
    expect(measured.overflow, 'nothing runs past the main area sideways').toBeLessThanOrEqual(0);
  });
}
