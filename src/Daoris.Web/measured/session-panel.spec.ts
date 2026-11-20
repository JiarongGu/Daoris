import { expect, test, type Page } from '@playwright/test';

// The session panel, measured: at the main area's wide width the Changes rail stands beside the story, and narrow it is gone and
// the head carries the changes as one fact; at both, the head, the track and the foot (the band, the dock, the composer) hold
// still and the story alone scrolls, never under the foot, the composer whole inside the panel, in both languages. jsdom lays
// nothing out, so `SessionPanel.test.tsx` holds the order of the regions, and this their boxes. The stories are
// `work/session/SessionPanel.stories.tsx`. `npm --prefix src/Daoris.Web run e2e:measured -- --grep "session panel"`.

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=sessionPanel/${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForTimeout(150);
}

/** Each region's box in CSS px of the viewport, the story's scroll, and the panel's layout word. */
async function measure(page: Page) {
  return page.evaluate(() => {
    const panel = document.querySelector<HTMLElement>('section[data-layout]')!;
    const box = (name: string) => {
      const region = panel.querySelector(`[data-region="${name}"]`);
      if (!region) return null;
      const rect = region.getBoundingClientRect();
      return { top: rect.top, bottom: rect.bottom, left: rect.left, right: rect.right, width: rect.width };
    };
    const story = panel.querySelector<HTMLElement>('[data-region="story"]')!;
    const whole = panel.getBoundingClientRect();
    return {
      layout: panel.dataset.layout,
      panel: { top: whole.top, bottom: whole.bottom, right: whole.right },
      head: box('head'), story: box('story'), rail: box('rail'), composer: box('composer'),
      scrolls: getComputedStyle(story).overflowY,
      // Only the story scrolls: the panel itself never does.
      panelScrolls: panel.scrollHeight > panel.clientHeight + 1,
    };
  });
}

test.describe('the session panel', () => {
  for (const story of ['Live', 'LiveInChinese', 'WaitingOnYou', 'Failed']) {
    test(`draws the Changes rail beside the story at the wide width: ${story}`, async ({ page }) => {
      await open(page, story);
      const at = await measure(page);
      console.log(`${story}: ${JSON.stringify(at)}`);
      expect(at.layout).toBe('wide');
      expect(at.rail).not.toBeNull();
      expect(at.rail!.left).toBeGreaterThanOrEqual(at.story!.right - 1);
      expect(at.rail!.right).toBeLessThanOrEqual(at.panel.right + 1);
    });
  }

  for (const story of ['LiveNarrow', 'LiveNarrowInChinese', 'WaitingOnYouNarrow', 'FailedNarrow']) {
    test(`folds the rail into the head's one fact at the narrow width: ${story}`, async ({ page }) => {
      await open(page, story);
      const at = await measure(page);
      console.log(`${story}: ${JSON.stringify(at)}`);
      expect(at.layout).toBe('narrow');
      expect(at.rail).toBeNull();
    });
  }

  for (const story of ['Live', 'LiveNarrow', 'WaitingOnYou', 'WaitingOnYouNarrow']) {
    test(`scrolls the story alone, under the head and above the foot, the composer whole: ${story}`, async ({ page }) => {
      await open(page, story);
      const at = await measure(page);
      expect(at.scrolls).toBe('auto');
      expect(at.panelScrolls).toBe(false);
      expect(at.story!.top).toBeGreaterThanOrEqual(at.head!.bottom - 1);
      expect(at.story!.bottom).toBeLessThanOrEqual(at.composer!.top + 1);
      expect(at.composer!.bottom).toBeLessThanOrEqual(at.panel.bottom + 1);
    });
  }
});
