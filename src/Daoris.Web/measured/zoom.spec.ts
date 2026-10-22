import { expect, test, type Page } from '@playwright/test';

// The frame at its size while the content zooms, measured (ZOOM1, the UI/UX design §3.11): the window's stories at 50%, 100%,
// 110% and 200% drawn in a real browser, and the title strip, its caption room, the activity bar and the status bar measured at
// each, with the content's heading and a glyph on each side. jsdom lays nothing out, so no unit test can hold it; `zoom.test.ts`
// holds the arithmetic and `chrome.test.tsx` the classes. `npm --prefix src/Daoris.Web run e2e:measured -- --grep zoom`.

const LEVELS = [50, 100, 110, 200] as const;

async function open(page: Page, level: number) {
  await page.goto(`/measured/flow.html?story=zoom/At${level}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForTimeout(150);
}

/** The frame's boxes and the content's heading, in CSS px of the viewport. */
async function measure(page: Page) {
  return page.evaluate(() => {
    const box = (element: Element | null) => {
      const rect = element!.getBoundingClientRect();
      return { width: Math.round(rect.width * 10) / 10, height: Math.round(rect.height * 10) / 10 };
    };
    const window_ = document.querySelector('[data-window]')!;
    return {
      strip: box(window_.querySelector('header')).height,
      captions: [...window_.querySelectorAll('[data-caption]')].map((slot) => box(slot)),
      bar: box(window_.querySelector('nav')).width,
      place: box(window_.querySelector('nav button svg')),
      status: box(window_.querySelector('footer')).height,
      heading: Number.parseFloat(getComputedStyle(window_.querySelector('h1')!).fontSize),
      headingBox: box(window_.querySelector('h1')).height,
      readout: document.querySelector('[role="status"].frame-held')?.textContent ?? null,
    };
  });
}

test.describe("the window's zoom", () => {
  test('keeps the frame at its size at every level, and scales the content with the level', async ({ page }) => {
    await open(page, 100);
    const at100 = await measure(page);
    // The frame as the desktop frame design draws it: a 36 px strip, 44 px caption slots, a 48 px bar and a 24 px status bar.
    expect(at100.strip).toBe(36);
    expect(at100.captions.map((slot) => slot.width)).toEqual([44, 44, 44]);
    expect(at100.bar).toBe(48);
    expect(at100.status).toBe(24);
    expect(at100.readout).toBeNull();
    console.log(`zoom 100%: ${JSON.stringify(at100)}`);

    for (const level of LEVELS.filter((each) => each !== 100)) {
      await open(page, level);
      const at = await measure(page);
      console.log(`zoom ${level}%: ${JSON.stringify(at)}`);
      expect(at.strip, `${level}%`).toBeCloseTo(at100.strip, 0);
      expect(at.captions, `${level}%`).toEqual(at100.captions);
      expect(at.bar, `${level}%`).toBeCloseTo(at100.bar, 0);
      expect(at.place.width, `${level}%`).toBeCloseTo(at100.place.width, 0);
      expect(at.status, `${level}%`).toBeCloseTo(at100.status, 0);
      // The content scales: the view's heading is the level's share of its 18 px, and its box with it.
      expect(at.heading, `${level}%`).toBeCloseTo((18 * level) / 100, 1);
      expect(at.headingBox / at100.headingBox, `${level}%`).toBeCloseTo(level / 100, 1);
      expect(at.readout, `${level}%`).toContain(`${level}%`);
    }
  });
});
