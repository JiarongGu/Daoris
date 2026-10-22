import type { ReactNode } from 'react';
import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Ask, Session } from '../data/api';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { DOCK } from '../work/frame/layout';
import { PUBLISHED } from '../asks/fixtures';
import { DECLINED, DONE, OPEN, TAKEN, WORKING } from '../quests/fixtures';
import { type HelpPublishedEntry, publishedCounts, publishedItems, type PublishedLists } from './published';
import { PublishedList } from './PublishedList';

// ASKQUESTS1 (the Ask Daoris design §9 item 12): what a conversation published, in its strip under the conversation's head, at the
// widths the frame gives the panel: the dock's 300 px floor and the 430 px the owner's window gave it, side by side, and 680 px,
// where the dock is the whole frame. Folded and opened, each state its lists can hold, in English and Chinese, light and dark.

const ASK: Ask = {
  ...PUBLISHED, id: 'a1b2c3d4', workspace: 'work', sentence: 'Cap the chunk streamer’s hydration per frame, and say what it costs',
  short: null, quests: [OPEN.id, TAKEN.id],
};
const WORKED: Session = { ...WORKING, id: 's9w8v7u6', quest: TAKEN.id, state: 'working' };

const ENTRIES: HelpPublishedEntry[] = [
  { at: '2026-10-22T11:00:00+00:00', proposal: 'p3', kind: 'ask', workspace: 'work', ask: null, quest: DECLINED.id },
  { at: '2026-10-22T10:00:00+00:00', proposal: 'p2', kind: 'setup', workspace: 'work', ask: null, quest: DONE.id },
  { at: '2026-10-22T09:00:00+00:00', proposal: 'p1', kind: 'ask', workspace: 'work', ask: ASK.id, quest: OPEN.id },
];

const LISTS: PublishedLists = { quests: [OPEN, TAKEN, DONE, DECLINED], asks: [ASK], sessions: [WORKED] };

/** The strip's props from what the conversation published and what its lists hold. */
const from = (lists: PublishedLists, entries = ENTRIES) => {
  const items = publishedItems(entries, lists);
  return { items, counts: publishedCounts(items) };
};

const meta = {
  title: 'Help/PublishedList',
  component: PublishedList,
  args: { ...from(LISTS), open: false, onToggle: () => {}, onOpen: () => {} },
} satisfies Meta<typeof PublishedList>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The panel in the dock, at its floor and at 430 px, the strip under a head as the panel draws it. */
const inTheDock = (Story: () => ReactNode) => (
  <div className="flex flex-wrap items-start gap-6 bg-page">
    {[DOCK.floor, 430].map((width) => (
      <div key={width} style={{ width }} className="flex h-[28rem] flex-col border-x border-line bg-page">
        <p className="m-0 border-b border-line px-3 py-2 text-body font-semibold text-ink">Cap the chunk budget</p>
        <Story />
      </div>
    ))}
  </div>
);

/** At 680 px the dock is the whole frame (below 768). */
const atTheFrame = (Story: () => ReactNode) => (
  <div style={{ width: 680 }} className="flex h-[28rem] flex-col border-x border-line bg-page"><Story /></div>
);

const dark = (Story: () => ReactNode) => <InTheme theme="dark" className="p-0"><Story /></InTheme>;

/** Folded: one line of what stands, each count with its noun. */
export const Folded: Story = { decorators: [inTheDock] };

/** Opened in place: the ask with the quests it became, the taken one named by its session's state, and each quest on its own. */
export const Opened: Story = { args: { open: true }, decorators: [inTheDock] };

export const OpenedChinese: Story = { args: { open: true }, decorators: [inTheDock, chinese] };

export const OpenedDark: Story = { args: { open: true }, decorators: [inTheDock, dark] };

export const OpenedChineseDark: Story = { args: { open: true }, decorators: [inTheDock, dark, chinese] };

/** The whole frame at 680 px. */
export const Frame680: Story = { args: { open: true }, decorators: [atTheFrame] };

export const Frame680Chinese: Story = { args: { open: true }, decorators: [atTheFrame, chinese] };

/** What waits on the person counted first: a done a departure holds for their yes, and an ask asking a go-ahead. */
export const WaitsOnYou: Story = {
  args: {
    open: true,
    ...from({
      quests: [OPEN, TAKEN, { ...DONE, held: true, hold: 'departed' }, DECLINED],
      asks: [{ ...ASK, goAheads: [{ number: 1, kind: 'push', on: 'engine', act: 'push the branch', state: 'asked', asked: [] }] }],
      sessions: [WORKED],
    }),
  },
  decorators: [inTheDock],
};

/** A take another machine holds, said as such where both machines are known. */
export const TakenElsewhere: Story = {
  args: {
    open: true,
    ...from({ quests: [OPEN, { ...TAKEN, holder: 'machine-b', here: 'machine-a' }, DONE, DECLINED], asks: [ASK], sessions: [] }),
  },
  decorators: [inTheDock],
};

/** The lists not answered yet: reading, never empty. */
export const Reading: Story = {
  args: { open: true, ...from({ quests: undefined, asks: undefined, sessions: undefined }) },
  decorators: [inTheDock],
};

/** Records deleted since: each said no longer on this machine, opening nothing. */
export const Gone: Story = {
  args: { open: true, ...from({ quests: [OPEN, TAKEN], asks: [], sessions: [] }) },
  decorators: [inTheDock],
};

export const GoneChinese: Story = { ...Gone, decorators: [inTheDock, chinese] };

/** A list that could not be read: said in place of the counts. */
export const Failed: Story = { args: { failed: true }, decorators: [inTheDock] };
