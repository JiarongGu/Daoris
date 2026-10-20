import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { sessionWidths, storyWidths } from '../design/storyWidths';
import type { Channel, ChannelItem } from '../work/channel';
import { ChannelPanel, ChannelSection } from './ChannelSection';

// An ask's channel (SESSONE2c, D167 points 6 to 8): the owner's ask over an app and its database repository, where the two sessions
// and the person talk. Molecules, so each state is reached by passing a channel: no driver, no service. English and 中文, since
// the section's, the composer's and Daoris's lines' words are chrome, while the messages are shown as written.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

const ITEMS: ChannelItem[] = [
  { kind: 'line', at: ago(50), code: 'published', quest: 'q1', name: 'Read the export figure', from: 'ask #c98a7e', to: 'db' },
  { kind: 'line', at: ago(46), code: 'taken', quest: 'q1', name: 'Read the export figure', from: 'ask #c98a7e', to: 'db' },
  {
    kind: 'message', at: ago(30), id: 'm1', person: false, session: '3f2a9c10', repository: 'db', agent: 'claude-code', entry: 'ask:c98a7e/db',
    to: 'app', question: true, open: true, text: 'Which column should the feed read: `total` or `total_v3`?',
  },
  {
    kind: 'message', at: ago(12), id: 'm2', person: true, to: 'db', re: 'm1', text: 'The v3 one, as common-report does.',
  },
  {
    kind: 'message', at: ago(5), id: 'm3', person: false, session: '81b7d2e4', repository: 'app', agent: 'codex', entry: 'ask:c98a7e/app',
    to: 'db', re: 'm1', text: 'Noted: I will read `total_v3` once your view lands.',
  },
  { kind: 'line', at: ago(2), code: 'done', quest: 'q1', name: 'Read the export figure', from: 'ask #c98a7e', to: 'db' },
];

const CHANNEL: Channel = { ask: 'c98a7e', entry: 'person', items: ITEMS, unread: 0, mark: null };

const meta: Meta<typeof ChannelSection> = {
  title: 'Asks/ChannelSection',
  component: ChannelSection,
  args: { onPost: () => {}, repositories: ['app', 'db'] },
  // An ask's page at the frame's widths (the UI/UX design R6, STORYGAP1): half by default, a story's own width instead.
  decorators: [storyWidths.half],
};
export default meta;

type Story = StoryObj<typeof ChannelSection>;

/** The whole exchange: Daoris's lines, an open question, the person's answer and the other session's note, newest at the foot. */
export const Exchange: Story = { args: { channel: CHANNEL } };
export const ExchangeChinese: Story = { ...Exchange, decorators: [chinese] };
/** The exchange across a maximized window's main area. */
export const ExchangeWide: Story = { ...Exchange, decorators: [storyWidths.wide] };
/** The exchange at the main area's 400 px floor. */
export const ExchangeNarrow: Story = { ...Exchange, decorators: [storyWidths.narrow] };
/** The exchange in dark. */
export const ExchangeDark: Story = { ...Exchange, decorators: [dark] };

/** Opened with two messages past the person's mark: counted in the heading and marked new, as the mark moves. */
export const Unread: Story = {
  args: { channel: { ...CHANNEL, unread: 3, mark: ago(20) }, onRead: () => {}, onOpenQuest: () => {} },
};
export const UnreadChinese: Story = { ...Unread, decorators: [chinese] };

/** Nothing said yet: the composer under a line that says so. */
export const Empty: Story = { args: { channel: { ...CHANNEL, items: [] } } };
export const EmptyChinese: Story = { ...Empty, decorators: [chinese] };

/** The channel on its way. */
export const Reading: Story = { args: { channel: null } };

/**
 * COWORK1d: an urgent claim named so, and what each message reached in the repositories it addresses, with *Send to its session* where
 * it waits or never reached one; an ordinary message says no level.
 */
export const Receipts: Story = {
  args: {
    onForward: (_message, _repository, answered) => answered.done(),
    channel: {
      ...CHANNEL,
      items: [
        {
          kind: 'message', at: ago(9), id: 'u1', person: false, session: '3f2a9c10', repository: 'db', agent: 'claude-code', entry: 'ask:c98a7e/db',
          urgent: true, text: 'Stop: the view now names the column `total_v3`, not `total`.',
          receipts: [{ repository: 'app', state: 'taken' }, { repository: 'docs', state: 'not-reached' }],
        },
        {
          kind: 'message', at: ago(4), id: 'o1', person: false, session: '81b7d2e4', repository: 'app', agent: 'codex', entry: 'ask:c98a7e/app',
          urgent: false, text: 'When you can: the feed reads the view on the hour.',
          receipts: [{ repository: 'db', state: 'held' }, { repository: 'docs', state: 'waiting' }],
        },
      ],
    },
  },
};
export const ReceiptsChinese: Story = { ...Receipts, decorators: [chinese] };

/**
 * A session's panel: the database repository's line of work, folded to its title; opened, it is kept to what concerns it.
 * Drawn in a session's page, with its gutters.
 */
export const Panel: StoryObj<typeof ChannelPanel> = {
  render: () => <ChannelPanel channel={CHANNEL} only={{ entry: 'ask:c98a7e/db', repository: 'db' }} onPost={() => {}} onOpenAsk={() => {}} />,
  decorators: [sessionWidths.half],
};
export const PanelChinese: StoryObj<typeof ChannelPanel> = { ...Panel, decorators: [sessionWidths.half, chinese] };
