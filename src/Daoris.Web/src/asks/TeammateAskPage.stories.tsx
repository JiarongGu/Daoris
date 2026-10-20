import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { Channel, ElsewhereAsk } from '../work/channel';
import { TeammateAskPage } from './TeammateAskPage';

// A teammate's ask's page (SESSONE2h, D167) in Quests' main area (`AsksPart.tsx`), on its own (STORYGAP1, the UI/UX design §1.2
// item 6): an ask held on another machine, of which this one has only the channel the sync mirrored, read with no composer.
// Its channel, one being read, an empty one, one with no circle; at the main area's three widths, in 中文 and dark.

// A flex item of the main area's height frame, with no padding of its own, so the page's scroll box stays bounded.
const dark: Decorator = (Story) => <InTheme theme="dark" className="flex min-h-0 min-w-0 flex-1 p-0"><Story /></InTheme>;

const ASK: ElsewhereAsk = { id: '7e6f51', workspace: 'default', origins: ['key-a', 'key-b'], latest: '2026-10-15T09:05:00Z', messages: 2, unread: 1 };

const CHANNEL: Channel = {
  ask: '7e6f51', entry: 'person', unread: 1, mark: '2026-10-15T09:01:00Z',
  items: [
    {
      kind: 'message', at: '2026-10-15T09:00:00Z', id: 'm1', person: false, session: 'key-a/s1', repository: 'db', agent: 'claude-code',
      text: 'Which column should the feed read: `total` or `total_v3`?', origin: 'key-a',
    },
    {
      kind: 'message', at: '2026-10-15T09:05:00Z', id: 'm2', person: false, session: 'key-b/s2', repository: 'api', agent: 'codex',
      text: 'The v3 one, as common-report does.', origin: 'key-b',
    },
  ],
};

const meta = {
  title: 'Asks/TeammateAskPage',
  component: TeammateAskPage,
  args: { ask: ASK, channel: CHANNEL },
  // The main area's height, for its scroll box; its width is the helper's.
  decorators: [(Story) => <div className="flex h-[40rem] border border-line bg-page"><Story /></div>, storyWidths.wide],
} satisfies Meta<typeof TeammateAskPage>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Its channel, read only, under the circle, the machines its messages came from and the latest. */
export const Read: Story = {};

/** The channel still being read: the page says what it knows, and the section waits. */
export const Reading: Story = { args: { channel: null } };

/** A channel with nothing in it yet. */
export const Empty: Story = { args: { ask: { ...ASK, messages: 0, unread: 0 }, channel: { ...CHANNEL, items: [], unread: 0, mark: null } } };

/** No circle named, and one machine. */
export const NoCircle: Story = { args: { ask: { ...ASK, workspace: '', origins: ['key-a'] } } };

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { decorators: [storyWidths.half] };

/** The main area's 400 px floor. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { decorators: [chinese, dark, storyWidths.half] };
