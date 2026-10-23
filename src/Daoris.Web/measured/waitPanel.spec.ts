import { expect, test, type Page } from '@playwright/test';

// The waiting panel, measured (WAITPANEL1): its top edge dragged in a real browser, the height kept for the viewer and the
// conversation's floor held; and the conversation's *To the top* and *Back to bottom* above the panel's edge, the topmost thing
// at their own centres, in both languages and both themes. jsdom lays nothing out and stacks nothing, so `WaitPanel.test.tsx`
// holds the arithmetic and the arrangement, and this the boxes. The stories are `work/waits/WaitPanel.stories.tsx`.
// `npm --prefix src/Daoris.Web run e2e:measured -- --grep "waiting panel"`.

/** What the conversation's main area keeps above the panel (`WAIT_PANEL.conversation`). */
const CONVERSATION = 240;

async function open(page: Page, story: string) {
  await page.goto(`/measured/flow.html?story=waitPanel/${story}`);
  await page.waitForFunction(() => (window as unknown as { flowReady?: boolean }).flowReady === true);
  await page.waitForTimeout(150);
}

/** The panel's edge, the presses, the main area and the parts, in CSS px of the viewport. */
async function measure(page: Page) {
  return page.evaluate(() => {
    const rect = (element: Element | null) => {
      const box = element!.getBoundingClientRect();
      return { top: box.top, bottom: box.bottom, height: box.height, x: box.x + box.width / 2, y: box.y + box.height / 2 };
    };
    const panel = document.querySelector('[data-region="wait-panel"]')!;
    const presses = [...panel.querySelectorAll('[data-wait-controls] button')];
    return {
      panel: rect(panel),
      main: rect(document.querySelector('[data-region="main"]')),
      group: rect(panel.querySelector('[data-wait-controls]')),
      parts: [...panel.querySelectorAll('[data-wait-part]:not([hidden])')].map((part) => rect(part).height),
      // Each press is the topmost thing at its own centre: nothing, the panel least of all, lies over it.
      topmost: presses.map((press) => {
        const at = rect(press);
        return press.contains(document.elementFromPoint(at.x, at.y));
      }),
      handle: panel.querySelector('[role="separator"]')?.getAttribute('aria-valuenow') ?? null,
    };
  });
}

test.describe('the waiting panel', () => {
  for (const story of ['Parked', 'ParkedChinese', 'ParkedDark', 'ParkedDarkChinese', 'StripAndPark', 'NothingWaiting', 'AtTheFloor', 'KeptTall']) {
    test(`lays the conversation's presses above its edge, never under it: ${story}`, async ({ page }) => {
      await open(page, story);
      const at = await measure(page);
      console.log(`${story}: ${JSON.stringify(at)}`);
      expect(at.group.bottom).toBeLessThanOrEqual(at.panel.top - 3);
      expect(at.group.top).toBeGreaterThanOrEqual(at.main.top);
      expect(at.topmost).toEqual([true, true]);
    });
  }

  test('follows a drag of its top edge, keeps the height for the viewer, and keeps the conversation its floor', async ({ page }) => {
    await open(page, 'Parked');
    await page.evaluate(() => window.localStorage.clear());
    await open(page, 'Parked');
    const before = await measure(page);
    expect(before.handle).toBe('240');

    // Down by 80 px from what is drawn: the park's ask takes 80 px less.
    const edge = page.getByRole('separator', { name: 'Height of what waits on you' });
    const box = (await edge.boundingBox())!;
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2 + 80, { steps: 4 });
    await page.mouse.up();
    const dragged = await measure(page);
    expect(dragged.parts[0]!).toBeCloseTo(before.parts[0]! - 80, 0);
    expect(Number(dragged.handle)).toBeCloseTo(before.parts[0]! - 80, 0);

    // Drawn again, it keeps that height.
    await open(page, 'Parked');
    const kept = await measure(page);
    expect(kept.handle).toBe(dragged.handle);
    expect(kept.parts[0]!).toBeCloseTo(dragged.parts[0]!, 0);

    // Dragged far up, the conversation keeps its floor.
    const up = (await edge.boundingBox())!;
    await page.mouse.move(up.x + up.width / 2, up.y + up.height / 2);
    await page.mouse.down();
    await page.mouse.move(up.x + up.width / 2, 0, { steps: 6 });
    await page.mouse.up();
    const tall = await measure(page);
    expect(tall.main.height).toBeGreaterThanOrEqual(CONVERSATION - 4);
    await page.evaluate(() => window.localStorage.clear());
  });

  test('holds a height kept taller than the column under the conversation\'s floor', async ({ page }) => {
    await open(page, 'KeptTall');
    const at = await measure(page);
    expect(at.main.height).toBeGreaterThanOrEqual(CONVERSATION - 4);
  });

  // QUESTIONUI1a (the supervisor design §8.4): six long answers at the panel's floor scroll within the park's part, and the box's field
  // and *Send*, outside every part, stay in sight in the column under it.
  for (const story of ['SixLongAtTheFloor', 'SixLongAtTheFloorChineseDark']) {
    test(`keeps the field and its send in sight under six long answers at the floor: ${story}`, async ({ page }) => {
      await open(page, story);
      const at = await page.evaluate(() => {
        const column = document.querySelector('[data-story-column]')!.getBoundingClientRect();
        const part = document.querySelector('[data-region="wait-panel"] [data-wait-part]') as HTMLElement;
        const field = document.querySelector('textarea')!.getBoundingClientRect();
        const send = document.querySelector('button[type="submit"]')!.getBoundingClientRect();
        return {
          column: { top: column.top, bottom: column.bottom },
          scrolls: part.scrollHeight > part.clientHeight,
          radios: part.querySelectorAll('input[type="radio"]').length,
          field: { top: field.top, bottom: field.bottom },
          send: { top: send.top, bottom: send.bottom },
        };
      });
      console.log(`${story}: ${JSON.stringify(at)}`);
      expect(at.radios).toBe(6);
      expect(at.scrolls).toBe(true);
      for (const box of [at.field, at.send]) {
        expect(box.top).toBeGreaterThanOrEqual(at.column.top);
        expect(box.bottom).toBeLessThanOrEqual(at.column.bottom + 0.5);
      }
    });
  }
});
