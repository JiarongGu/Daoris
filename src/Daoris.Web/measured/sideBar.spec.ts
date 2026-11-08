import { expect, test } from '@playwright/test';

// A session's Timeline in the side bar, drawn at the widths the dock gives it and measured (UIFIND12): its states chart's edges
// stay inside the pane, and the placement holds still. The dock scrolls, and a classic scrollbar takes its width from the pane
// once the drawing is taller than the dock, so the chart was placed by a width the bar then took back, and flipped between top to
// bottom and the list with its edges past the pane. The story is `measured/sideBarTimeline.tsx`; classic scrollbars are
// the headless browser's own to hide, so they are kept.
// `npm --prefix src/Daoris.Web run e2e:measured -- --grep sidebar/`.

test.use({ launchOptions: { ignoreDefaultArgs: ['--hide-scrollbars'] } });

/** The frame's width, from the narrowest the dock draws the chart in to a few more, in steps of the bar's own. */
const WIDTHS = [372, 376, 380, 384, 388, 392, 396, 400];

test('sidebar/At372: the states chart keeps every edge inside its pane and holds its placement still as the dock is widened', async ({ page }) => {
  await page.goto('/measured/flow.html?story=sidebar/At372');
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  for (const width of WIDTHS) {
    await page.evaluate((to) => { document.querySelector<HTMLElement>('[data-frame]')!.style.width = `${to}px`; }, width);
    const seen = new Set<string>();
    for (let take = 0; take < 6; take += 1) {
      await page.waitForTimeout(350);
      const drawn = await page.evaluate(() => {
        const pane = document.querySelector<HTMLElement>('[data-flow-pane]')!;
        const box = pane.getBoundingClientRect();
        const past = [...document.querySelectorAll('svg path[data-flow-edge]')].map((edge) => edge.getBoundingClientRect().right - box.right);
        return { placement: pane.dataset.placement, past: past.length ? Math.max(...past) : -1e9 };
      });
      expect(drawn.past, `at ${width} px the chart's farthest edge lies past its pane`).toBeLessThanOrEqual(0);
      seen.add(drawn.placement ?? '');
    }
    expect([...seen], `at ${width} px the placement flips`).toHaveLength(1);
  }
});
