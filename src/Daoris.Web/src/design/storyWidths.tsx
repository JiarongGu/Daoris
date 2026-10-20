import type { Decorator } from '@storybook/react-vite';
import { createContext, type ReactNode, useContext, useLayoutEffect, useState } from 'react';
import { cn } from '../lib/cn';

// A story shows the widths the frame gives, without help (the UI/UX design R6; UIFIT1). Stories only: nothing in the
// product imports this. The one helper replaces the local `mainAt` and `at` decorators stories carried, each of which
// chose a width of its own, and some a gap or a cap the page does not have, which is how every R1 defect passed its stories.

/**
 * The main area's widths: wide and half of a 1546 px window (about 1,290 px with the list open, and 630 px at a 680 px
 * window), and narrow, the main area's 400 px floor (`CENTRE_FLOOR`, `work/frame/layout.ts`).
 */
export const MAIN_WIDTHS = { wide: '80rem', half: '39rem', narrow: '25rem' } as const;

/**
 * The page's gutters at each width, as `ViewMain` gives them (`GUTTERS.page`, held equal by `storyWidths.test.tsx`): 24 px,
 * and 12 at the narrow width, where the window is under 768 px.
 */
const PAGE_GUTTERS = { wide: 'px-6 pb-12 pt-5', narrow: 'px-3 pb-8 pt-4' } as const;

type Width = keyof typeof MAIN_WIDTHS;

/** What an inner width tells the box outside it: a file's `storyWidths.wide` yields to a story's `storyWidths.narrow`. */
const Innermost = createContext<((width: Width) => void) | null>(null);

/**
 * The main area at one width: its container (`@container/main`, so a split inside follows it as on the window), its
 * gutters, and nothing else. No gap, no cap, no padding of the story's own: a domain's cards are drawn in the domain's
 * own container (`CardStack`), by the story. A file's decorator and a story's nest, the story's inside; one box is drawn,
 * at the innermost width, so a story is never a narrow box in a wide one.
 */
function MainArea({ width, children }: { width: Width; children: ReactNode }) {
  const outer = useContext(Innermost);
  const [inner, setInner] = useState<Width | null>(null);
  useLayoutEffect(() => { outer?.(width); }, [outer, width]);
  if (outer) return <>{children}</>;
  const at = inner ?? width;
  return (
    <Innermost.Provider value={setInner}>
      <div
        data-main-width={MAIN_WIDTHS[at]}
        style={{ width: MAIN_WIDTHS[at] }}
        className={cn('@container/main max-w-full bg-page text-ink', at === 'narrow' ? PAGE_GUTTERS.narrow : PAGE_GUTTERS.wide)}
      >
        {children}
      </div>
    </Innermost.Provider>
  );
}

const mainAt = (width: Width): Decorator => (Story) => <MainArea width={width}><Story /></MainArea>;

/** The three widths, each a decorator: `decorators: [storyWidths.narrow, chinese, dark]`. */
export const storyWidths = { wide: mainAt('wide'), half: mainAt('half'), narrow: mainAt('narrow') } as const;

/** A line of a stories file that draws a story in a frame of its own: a width, a cap, a gap or a padding beside `<Story />`. */
const STORY_LINE = /^.*<Story\s*\/>.*$/gm;
const OWN_FRAME = /\bw-\[|\bmax-w-(?!full\b|none\b)|style=\{\{ width|\bgap-|\bp[xy]?-(?!0\b)(?:\d|\[)/;

/** Each story drawn in a frame of its own, by file and line: what R6 replaces with the helper. */
export function localFrames(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(STORY_LINE) ?? [])
      .filter((line) => OWN_FRAME.test(line))
      .map((line) => `${path} frames a story by itself: ${line.trim()}`));
}

/**
 * The stories still drawn in a frame of their own on 2026-10-20, for STORYGAP1 to move to the helper; one moved leaves
 * this list, and a new one fails `storyWidths.test.tsx`. UIFIT1 moved Settings', Setup's and the workspace page's.
 */
export const STILL_LOCAL: readonly string[] = [
  './projects/ProjectPage.stories.tsx',
  './projects/git/BranchPage.stories.tsx',
  // The domains' list is a side-bar view: R6 draws one at the side bar's floor, which the helper does not give yet.
  './settings/DomainList.stories.tsx',
  './work/AttendedSession.stories.tsx',
  './work/Note.stories.tsx',
  './work/console/StreamTile.stories.tsx',
  './work/conversation/Composer.stories.tsx',
  './work/conversation/ContextRing.stories.tsx',
  './work/conversation/ConversationView.stories.tsx',
  './work/conversation/SessionBox.stories.tsx',
  './work/conversation/SessionOptions.stories.tsx',
  './work/frame/ViewMain.stories.tsx',
  './work/frame/chrome.stories.tsx',
  './work/head/HowItCameToBe.stories.tsx',
  './work/head/SessionHead.stories.tsx',
  './work/head/SessionPageHead.stories.tsx',
  './work/head/SessionTimeline.stories.tsx',
  './work/rail/SessionRailSearch.stories.tsx',
  './work/review/DiffFileRow.stories.tsx',
  './work/review/LandedNote.stories.tsx',
  './work/review/PatchView.stories.tsx',
  './work/terminals/TerminalTabs.stories.tsx',
  './work/waits/HelperAsks.stories.tsx',
  './work/waits/PermissionAsks.stories.tsx',
  './workflow/WorkflowHold.stories.tsx',
  './workflow/WorkflowRunView.stories.tsx',
];
