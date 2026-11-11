import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { HelpPressNow, StandingConfirmationNow } from '../person/standing';
import { InTheme } from '../plugins/storyIcons';
import { HelpPressLine } from './HelpPressLine';

// What Ask Daoris pressed this run in *Notifications*: the presses that ran with no card, one outside its conversation's focus, and
// apart under *Asked you* those it put to the person on a card, each with its card's state; light and dark, and in 中文.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const minutesAgo = (count: number) => new Date(Date.now() - count * 60_000).toISOString();
const press = (id: string, route: string, at: number, more: Partial<HelpPressNow> = {}): HelpPressNow => ({
  id, module: 'DAORIS.DRIVER', route, session: 's1a2b3c4', scope: null, target: null, at: minutesAgo(at), outcome: 'done', ...more,
});
const PRESSES: HelpPressNow[] = [
  press('h4', 'WORK_PAUSE', 1, {
    scope: 'quest', target: 'q12', outcome: 'waiting', asked: true, place: { kind: 'workspace', workspace: 'Work' }, beyond: true,
  }),
  press('h3', 'SET_HOLD', 3, { scope: 'repository', target: 'api', outcome: 'refused', asked: true, place: { kind: 'repository', workspace: 'Work', repository: 'api' }, beyond: true }),
  press('h2', 'SET_HOLD', 6, { scope: 'repository', target: 'engine', place: { kind: 'repository', workspace: 'Games', repository: 'engine' } }),
  press('h1', 'WORK_PAUSE', 9, { scope: 'quest', target: 'q1', place: { kind: 'workspace', workspace: 'Work' }, beyond: true }),
];
const STANDING: StandingConfirmationNow = { holds: [], holders: [], stewardActs: [], stewardDesk: [], helpPresses: PRESSES };

const meta = {
  title: 'Notifications/HelpPressLine',
  component: HelpPressLine,
  args: { standing: STANDING, onOpen: () => {}, onReverse: () => {}, defaultOpen: true },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof HelpPressLine>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Two that ran, one outside its focus, and two that asked the person, one waiting and one refused. */
export const RanAndAsked: Story = {};

/** The same, in dark. */
export const RanAndAskedDark: Story = { decorators: inTheme('dark') };

/** The same in 中文, at the 400 px floor. */
export const RanAndAskedChinese: Story = { decorators: [storyWidths.narrow, chinese] };

/** Folded, only presses that asked the person this run. */
export const OnlyAsked: Story = { args: { standing: { ...STANDING, helpPresses: PRESSES.slice(0, 2) }, defaultOpen: false } };
