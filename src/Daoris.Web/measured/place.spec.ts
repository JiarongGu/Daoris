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

// SETUPUX1o: a place's Workflow on its own. At 80 rem the chart stands in a column of its own beside the routes; at 39 and 25 rem it is one
// press away, and shown it fits the main area sideways. Both languages and both themes.
const WORKFLOW: Record<string, 'wide' | 'half' | 'narrow'> = {
  'placeWorkflow/SavedDefault': 'wide',
  'placeWorkflow/SavedDefaultHalf': 'half',
  'placeWorkflow/SavedDefaultNarrow': 'narrow',
  'placeWorkflow/SavedDefaultChinese': 'wide',
  'placeWorkflow/SavedDefaultChineseHalf': 'half',
  'placeWorkflow/SavedDefaultChineseNarrow': 'narrow',
  'placeWorkflow/SavedDefaultDark': 'wide',
  'placeWorkflow/SavedDefaultDarkHalf': 'half',
  'placeWorkflow/SavedDefaultDarkNarrow': 'narrow',
  'placeWorkflow/StepSelected': 'wide',
  'placeWorkflow/StepSelectedHalf': 'half',
  'placeWorkflow/GatesWaiting': 'wide',
  'placeWorkflow/GatesWaitingHalf': 'half',
  'placeWorkflow/LandedVerified': 'wide',
  'placeWorkflow/WorkspaceWorkflow': 'wide',
  'placeWorkflow/WorkspaceWorkflowHalf': 'half',
};

for (const [story, width] of Object.entries(WORKFLOW)) {
  test(`${story} at ${width}: the chart is ${width === 'wide' ? 'in a column of its own' : 'one press away'}, and nothing runs past the main area`, async ({ page }) => {
    await open(page, story);
    const frame = page.locator('[data-main-width]');
    const section = page.locator('[data-place-workflow]');
    await expect(section).toHaveAttribute('data-columns', width === 'wide' ? 'two' : 'one');
    const show = page.getByRole('button', { name: /^(Show the chart|显示流程图)$/ });
    if (width === 'wide') {
      await expect(show).toHaveCount(0);
      const routes = (await section.locator('ul').first().boundingBox())!;
      const chart = (await section.locator('section[aria-label]').first().boundingBox())!;
      expect(chart.x, 'the chart stands to the right of the routes, not under them').toBeGreaterThanOrEqual(routes.x + routes.width - 1);
    } else {
      await expect(show).toHaveCount(1);
      await expect(page.locator('[data-flow-pane]')).toHaveCount(0);
      await show.click();
      await expect(page.locator('[data-flow-pane]')).toHaveCount(1);
    }
    const overflow = await frame.evaluate((element) => element.scrollWidth - element.clientWidth);
    expect(overflow, 'nothing runs past the main area sideways').toBeLessThanOrEqual(0);
    // A flow in sections is never laid across: top to bottom, or the rail.
    expect(await page.locator('[data-flow-pane]').getAttribute('data-placement')).not.toBe('across');
  });
}

test('place/Repository at wide: the chart stands in the page\'s own column beside the sections, and at half it is one press away', async ({ page }) => {
  await open(page, 'place/Repository');
  const aside = page.locator('[data-place-aside]');
  await expect(aside.locator('[data-flow-pane]')).toHaveCount(1);
  const sections = (await page.locator('#place-fold-workflow').boundingBox())!;
  const chart = (await aside.boundingBox())!;
  expect(chart.x, 'the chart is to the right of the sections').toBeGreaterThanOrEqual(sections.x + sections.width - 1);

  await open(page, 'place/RepositoryHalf');
  await expect(page.locator('[data-place-aside]')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Show the chart' })).toBeVisible();
});

test('placeWorkflow/SavedDefaultHalf: a route is chosen from the keyboard, and landed Current draws no chart', async ({ page }) => {
  await open(page, 'placeWorkflow/SavedDefaultHalf');
  await page.getByRole('button', { name: /^Default work:/ }).focus();
  await page.keyboard.press('Tab');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: /^Landed work:/ })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: /^Landed work:/ })).toHaveAttribute('aria-current', 'true');
  await page.getByRole('button', { name: 'Show the chart' }).click();
  await expect(page.getByText('Landed work follows no verification here, so there is no chart to draw.')).toBeVisible();
});

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
