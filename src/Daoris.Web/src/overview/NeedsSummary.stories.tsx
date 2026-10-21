import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { AttentionRead } from '../bridge/attention';
import type { Notice } from '../bridge/notices';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import { InTheme } from '../plugins/storyIcons';
import type { Confirmation } from '../person/Confirmations';
import type { PushedAttention } from '../work/attention/attention';
import { NeedsSummary } from './NeedsSummary';

// Overview's summary of what needs the person (NOTIFY2b, the notification design §4): a line per group with its count and its oldest
// wait, each a door into *Notifications*; on one label track at 32rem and wider, stacked below it (R1). At the main area's wide,
// half and narrow widths (R6), in English and 中文, light and dark; and with nothing waiting.

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();
const ROWS: PushedAttention[] = [
  { kind: 'parked', id: 's1', since: ago(130), where: 'game' },
  { kind: 'go-ahead', id: 'g1', since: ago(60), where: 'game' },
  { kind: 'parked', id: 's2', since: ago(20), where: 'engine' },
  { kind: 'proposal', id: 'a1', since: ago(24 * 60), where: 'default' },
  { kind: 'review', id: 'r1', since: null, where: '' },
];
const KNOWN: AttentionRead = { state: 'known', rows: ROWS, sessions: 0, working: 2, resyncing: false };
const PRESS: Confirmation = { source: 'pipe', id: 'c1', route: 'STOP_SESSION', fields: [], shown: true, expires: new Date(Date.now() + 100_000).toISOString() };

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Overview/NeedsSummary',
  component: NeedsSummary,
  args: { attention: KNOWN, presses: [PRESS], onNotifications: () => {} },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof NeedsSummary>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Wide: Story = {};
export const WideChinese: Story = { decorators: [chinese] };
export const WideDark: Story = { decorators: [dark] };
export const Half: Story = { decorators: [storyWidths.half] };
export const HalfChinese: Story = { decorators: [storyWidths.half, chinese] };
export const Narrow: Story = { decorators: [storyWidths.narrow] };
export const NarrowChineseDark: Story = { decorators: [storyWidths.narrow, chinese, dark] };
/** Nothing waiting: today's one line, and the sessions working. */
export const Nothing: Story = { args: { attention: { ...KNOWN, rows: [] }, presses: [] } };
/** A plain browser: the app's to judge. */
export const InABrowser: Story = { args: { attention: { state: 'app' }, presses: [], onNotifications: undefined } };

/** *Done and failed*'s line after what waits (NOTIFY2f): its notices, and how many are unread. */
const NOTICES: Notice[] = [
  { id: 'ended-s1', kind: 'ended', at: ago(5), read: false, repository: 'game', state: 'failed' },
  { id: 'ended-s2', kind: 'ended', at: ago(90), read: true, repository: 'engine', state: 'completed' },
];
export const WithDoneAndFailed: Story = { args: { notices: NOTICES } };
export const WithDoneAndFailedChinese: Story = { args: { notices: NOTICES }, decorators: [chinese] };
export const WithDoneAndFailedNarrow: Story = { args: { notices: NOTICES }, decorators: [storyWidths.narrow] };
