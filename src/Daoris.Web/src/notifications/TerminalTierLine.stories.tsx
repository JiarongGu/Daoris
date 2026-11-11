import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { TerminalActsNow, TerminalPress } from '../person/terminalActs';
import { InTheme } from '../plugins/storyIcons';
import { TerminalTierLine } from './TerminalTierLine';

// The terminal's tier in *Notifications*: act, turned on by Ask Daoris with no card, its *Back to ask*, and what a terminal pressed under
// it, one done, one not done with its code and one interrupted by a stop; light and dark, and in 中文.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const minutesAgo = (count: number) => new Date(Date.now() - count * 60_000).toISOString();
const press = (id: string, route: string, at: number, more: Partial<TerminalPress> = {}): TerminalPress => ({
  id, module: 'DAORIS.DRIVER', route, scope: null, target: null, command: null, at: minutesAgo(at), outcome: 'done', code: null, ended: null, ...more,
});
const ACTS: TerminalActsNow = {
  changes: [{ tier: 'act', at: minutesAgo(30), by: 'ask-daoris', session: 'h1a2b3c4' }],
  presses: [
    press('t3', 'SET_HOLD', 2, { scope: 'repository', target: 'engine', outcome: 'interrupted' }),
    press('t2', 'SESSION_ARCHIVE', 5, { outcome: 'failed', code: 'driver-refused' }),
    press('t1', 'WORK_PAUSE', 9, { scope: 'quest', target: 'q12', command: 'daoris quest pause q12' }),
  ],
};

const meta = {
  title: 'Notifications/TerminalTierLine',
  component: TerminalTierLine,
  args: { tier: 'act', acts: ACTS, onBackToAsk: () => {}, onOpen: () => {}, defaultOpen: true },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof TerminalTierLine>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Act, who turned it on, and three presses that ended three ways. */
export const Acting: Story = {};

/** The same, in dark. */
export const ActingDark: Story = { decorators: inTheme('dark') };

/** The same in 中文, at the 400 px floor. */
export const ActingChinese: Story = { decorators: [storyWidths.narrow, chinese] };

/** Back to ask by the person in the window, the presses made under act still listed. */
export const BackToAsk: Story = {
  args: { tier: 'ask', acts: { ...ACTS, changes: [{ tier: 'ask', at: minutesAgo(1), by: 'window', session: null }, ...ACTS.changes] } },
};
