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

/** A session's page's gutters (`GUTTERS.session`), the same at every width: its head, conversation and foot are drawn in them. */
const SESSION_GUTTERS = 'flex flex-col px-4 py-3';

type Width = keyof typeof MAIN_WIDTHS;
type Gutters = 'page' | 'session';

interface Frame { width: Width; gutters: Gutters }

/** What an inner width tells the box outside it: a file's `storyWidths.wide` yields to a story's `storyWidths.narrow`. */
const Innermost = createContext<((frame: Frame) => void) | null>(null);

/**
 * The main area at one width: its container (`@container/main`, so a split inside follows it as on the window), its
 * gutters, and nothing else. No gap, no cap, no padding of the story's own: a domain's cards are drawn in the domain's
 * own container (`CardStack`), by the story. A file's decorator and a story's nest, the story's inside; one box is drawn,
 * at the innermost width, so a story is never a narrow box in a wide one.
 */
function MainArea({ width, gutters, children }: Frame & { children: ReactNode }) {
  const outer = useContext(Innermost);
  const [inner, setInner] = useState<Frame | null>(null);
  useLayoutEffect(() => { outer?.({ width, gutters }); }, [outer, width, gutters]);
  if (outer) return <>{children}</>;
  const { width: at, gutters: kind } = inner ?? { width, gutters };
  return (
    <Innermost.Provider value={setInner}>
      <div
        data-main-width={MAIN_WIDTHS[at]}
        style={{ width: MAIN_WIDTHS[at] }}
        className={cn('@container/main max-w-full bg-page text-ink',
          kind === 'session' ? SESSION_GUTTERS : at === 'narrow' ? PAGE_GUTTERS.narrow : PAGE_GUTTERS.wide)}
      >
        {children}
      </div>
    </Innermost.Provider>
  );
}

const mainAt = (width: Width, gutters: Gutters = 'page'): Decorator => (Story) =>
  <MainArea width={width} gutters={gutters}><Story /></MainArea>;

/** The three widths, each a decorator: `decorators: [storyWidths.narrow, chinese, dark]`. */
export const storyWidths = { wide: mainAt('wide'), half: mainAt('half'), narrow: mainAt('narrow') } as const;

/** The same widths in a session's page, whose gutters are its own: the head, the conversation, the foot and their parts. */
export const sessionWidths = {
  wide: mainAt('wide', 'session'), half: mainAt('half', 'session'), narrow: mainAt('narrow', 'session'),
} as const;

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
 * The stories that still draw a frame of their own, each a part of another region, with where it is mounted: the helper gives
 * the main area, and these are not in one. STORYGAP1 moved every main-area story to the helper (UIFIT1 had moved Settings',
 * Setup's and the workspace page's); a main-area story added here instead of taking the helper fails review, and one not
 * named here fails `storyWidths.test.tsx`.
 */
export const NOT_IN_A_MAIN_AREA: Readonly<Record<string, string>> = {
  './settings/DomainList.stories.tsx': "Settings' list column (`settings/SettingsView.tsx:94`)",
  './work/console/StreamTile.stories.tsx': "a tile of the monitor's grid (`work/console/MonitorWindow.tsx:221`)",
  './work/conversation/ContextRing.stories.tsx': "an atom in the composer's control row (`work/conversation/Composer.tsx:707`)",
  './work/conversation/SessionOptions.stories.tsx': "an atom in the composer's control row (`work/conversation/Composer.tsx:706`)",
  './work/frame/chrome.stories.tsx': "the window's strip and activity bar (`app/App.tsx:985`, `app/App.tsx:1102`)",
  './work/rail/SessionRailSearch.stories.tsx': "the session rail's list column (`work/rail/SessionRail.tsx:395`)",
  './work/review/DiffFileRow.stories.tsx': "a row of the review's list, in the side bar or panel (`work/review/ReviewFrame.tsx:175`)",
  './work/review/LandedNote.stories.tsx': "the review's note, in the side bar or panel (`work/review/DiffPane.tsx:512`)",
  './work/review/PatchView.stories.tsx': "a file's patch in the review and the side bar's preview (`work/review/FilePreview.tsx:95`)",
  './work/terminals/TerminalTabs.stories.tsx': "the terminal view's tabs, a panel or side-bar view (`work/terminals/TerminalView.tsx:163`)",
  // R6 draws a side-bar view at the side bar's floor (300 px), which its `AtTheFloor` stories do, and at 45% of the window.
  './workflow/WorkflowRunView.stories.tsx': "a side-bar or panel session view (`work/WorkFrame.tsx:1172`)",
};
