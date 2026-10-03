import type { ComponentType } from 'react';
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { composeStories } from '@storybook/react-vite';
import { cappedBlocks } from './test/measure';
import './i18n';

/**
 * 🔴 **Every block of a record page wraps at one edge, the pane's** (D141, LAYOUT11), in every state its stories carry.
 *
 * On the install at 1600 px a quest's title ran the pane while its body stopped at 456 px (`max-w-prose`), a plugin's
 * detail wrapped its description at 65ch and its source line at the pane's edge, and five more blocks of a quest's and
 * an ask's pages stopped at 48rem. The source scan in `tokens.test.ts` holds the names (`max-w-prose`, a width in `ch`
 * or `em`, a named width); this renders each page and reads what it draws, so a cap an atom adds, or one in `rem`, is
 * seen too.
 *
 * What keeps a size of its own is no page's block (D141 §4), and is set aside by its shape:
 * - **a story's frame**: a named width, which no product source may name (the scan), so on a rendered story it is the
 *   decorator standing in for the column;
 * - **a form's own size**: a cap on an element that holds a control, sized to what it holds (platform language §4);
 * - **a notice**: the main area's *nothing chosen* and *gone*, a centred empty state with its own narrow body.
 */
type StoriesModule = Parameters<typeof composeStories>[0];
const modules = import.meta.glob([
  './quests/QuestPage.stories.tsx',
  './asks/AskPage.stories.tsx',
  './asks/asks.stories.tsx',
  './plugins/PluginPage.stories.tsx',
  './plugins/OfferPage.stories.tsx',
  './projects/ProjectPage.stories.tsx',
  './projects/StandingAnswer.stories.tsx',
  './knowledge/EntryPage.stories.tsx',
  './knowledge/FindingPage.stories.tsx',
  './work/SessionHead.stories.tsx',
  './work/SessionPageHead.stories.tsx',
  './work/WorkAsks.stories.tsx',
  './work/ConversationView.stories.tsx',
  './settings/*.stories.tsx',
], { eager: true }) as Record<string, StoriesModule>;

const STORY_FRAME = /(?:^|:)max-w-(?:xs|sm|md|lg|xl|[2-7]xl)$/;
const CONTROL = 'input, textarea, button, [role="combobox"], [role="switch"]';

/** The caps a rendered story's page wears that no rule sets aside. */
function pageCaps(root: Element): string[] {
  root.querySelectorAll('[data-main-state="none"], [data-main-state="gone"]').forEach((notice) => notice.remove());
  return [...root.querySelectorAll('[class*="max-w-"]')].flatMap((element) => {
    if (element.querySelector(CONTROL)) return [];
    return cappedBlocks(wrap(element)).filter((cap) => !STORY_FRAME.test(cap.split(' ')[1]!));
  });
}

/** The element alone, in a holder, so `cappedBlocks` reads its own classes and none of its descendants'. */
function wrap(element: Element): Element {
  const holder = document.createElement('div');
  holder.appendChild(element.cloneNode(false));
  return holder;
}

describe('every record page wraps at one edge', () => {
  it('reads the pages at all — a glob that matched nothing would pass every story below', () => {
    expect(Object.keys(modules).length).toBeGreaterThanOrEqual(14);
  });

  it('catches a capped block, and sets aside a story\'s frame, a form and a notice — the check itself', () => {
    const root = document.createElement('div');
    root.innerHTML = [
      '<div class="max-w-3xl">',
      '<p class="m-0 max-w-prose">the body</p>',
      '<p class="max-w-[40rem]">a note</p>',
      '<div class="grid max-w-[48rem]"><input /></div>',
      '<main data-main-state="none"><p class="max-w-[28rem]">Choose one in the list.</p></main>',
      '</div>',
    ].join('');
    expect(pageCaps(root)).toEqual(['<p> max-w-prose', '<p> max-w-[40rem]']);
  });

  for (const [path, module] of Object.entries(modules)) {
    const composed = composeStories(module) as unknown as Record<string, ComponentType>;
    for (const [name, Story] of Object.entries(composed)) {
      it(`${path.replace(/^\.\/|\.stories\.tsx$/g, '')} · ${name} caps no block`, () => {
        const { container } = render(<Story />);
        expect(pageCaps(container)).toEqual([]);
      });
    }
  }
});
