import { expect, test, type Page } from '@playwright/test';

// *Set up…*'s review, measured (the setup-UX design §2.2 and §5 row k): the review's order holds at every width, in two columns at
// 80 rem and one below it, and Apply sits last, wholly inside the main area, under every part, at 680 px (39 rem) and the 400 px floor (25 rem)
// as at 1546 px. jsdom lays nothing out, so no unit test can hold this.
// `npm --prefix src/Daoris.Web run e2e:measured -- review.spec`.

/** Each story, with the main-area width it is drawn at. */
const TABLE: Record<string, 'wide' | 'half' | 'narrow'> = {
  'review/Review': 'wide',
  'review/ReviewHalf': 'half',
  'review/ReviewNarrow': 'narrow',
  'review/ReviewChinese': 'wide',
  'review/ReviewChineseHalf': 'half',
  'review/ReviewChineseNarrow': 'narrow',
  'review/ReviewDarkHalf': 'half',
  'review/StoppedHalf': 'half',
  'review/PartialHold': 'wide',
  'review/PartialHoldNarrow': 'narrow',
  'review/AppliedHalf': 'half',
  'review/AppliedNarrow': 'narrow',
};

const WIDTH_PX = { wide: 80 * 16, half: 39 * 16, narrow: 25 * 16 } as const;

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForTimeout(150);
}

for (const [story, width] of Object.entries(TABLE)) {
  test(`${story} at ${width}: Apply sits last, inside the main area, under every part`, async ({ page }) => {
    await open(page, story);
    const measured = await page.evaluate(() => {
      const frame = document.querySelector<HTMLElement>('[data-main-width]')!;
      const box = frame.getBoundingClientRect();
      const rel = (rect: DOMRect) => ({ left: rect.left - box.left, right: rect.right - box.left, top: rect.top - box.top, bottom: rect.bottom - box.top });
      const apply = document.querySelector<HTMLElement>('[data-review-apply]')!.getBoundingClientRect();
      const parts = [...document.querySelectorAll<HTMLElement>('section[aria-labelledby] > h3')].map((each) => rel(each.closest('section')!.getBoundingClientRect()));
      return {
        width: box.width,
        apply: rel(apply),
        parts,
        starts: (() => {
          const changes = [...document.querySelectorAll('h3')].find((each) => /^(What it changes|它会更改)$/.test(each.textContent ?? ''));
          const starts = [...document.querySelectorAll('h3')].find((each) => /^(What it starts|它会启动什么)$/.test(each.textContent ?? ''));
          return changes && starts ? { changes: rel(changes.getBoundingClientRect()), starts: rel(starts.getBoundingClientRect()) } : null;
        })(),
        overflow: frame.scrollWidth - frame.clientWidth,
      };
    });
    expect(Math.abs(measured.width - WIDTH_PX[width]), `the main area is ${WIDTH_PX[width]} px, not ${measured.width}`).toBeLessThanOrEqual(1);
    // eslint-disable-next-line no-console
    console.log(`${story}: Apply at ${Math.round(measured.apply.left)}-${Math.round(measured.apply.right)} across, ${Math.round(measured.apply.top)}-${Math.round(measured.apply.bottom)} down`);
    expect(measured.apply.left, 'Apply starts inside the main area').toBeGreaterThanOrEqual(0);
    expect(measured.apply.right, 'Apply ends inside the main area').toBeLessThanOrEqual(measured.width + 1);
    for (const part of measured.parts) expect(measured.apply.top, 'Apply is under every part').toBeGreaterThanOrEqual(part.bottom - 1);
    expect(measured.overflow, 'nothing runs past the main area sideways').toBeLessThanOrEqual(0);
    if (measured.starts) {
      if (width === 'wide') {
        // Two columns: what it starts stands beside what changes.
        expect(measured.starts.starts.left, 'What it starts is the second column').toBeGreaterThan(measured.starts.changes.right - 1);
      } else {
        // One column, the same order: what it starts follows what changes.
        expect(measured.starts.starts.top, 'What it starts follows what changes').toBeGreaterThanOrEqual(measured.starts.changes.bottom - 1);
      }
    }
  });
}
