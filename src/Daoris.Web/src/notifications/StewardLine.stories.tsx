import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { DeskRequestNow, RoleHoldNow, StandingConfirmationNow, StewardActNow } from '../person/standing';
import { InTheme } from '../plugins/storyIcons';
import { StewardLine } from './StewardLine';

// The steward's line in *Notifications* (STEWARDROLE1b, D209; the steward design §5.3, §5.4): appointed with its run's acts, a stop
// still waiting on a Try again said as *Stopped by the steward*, and dormant; with the steward's desk under it, and the
// desk alone; light and dark, and in 中文. The key is never drawn.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const minutesAgo = (count: number) => new Date(Date.now() - count * 60_000).toISOString();
const STEWARD: RoleHoldNow = { role: 'steward', kind: 'steward', holder: 'h1', since: minutesAgo(90), answers: 0 };
const act = (id: string, kind: string, at: number, more: Partial<StewardActNow> = {}): StewardActNow => ({
  id, act: kind, repository: null, quest: null, session: null, at: minutesAgo(at), standing: true, waiting: false, ...more,
});
const ACTS: StewardActNow[] = [
  act('a4', 'stop', 2, { quest: '7a82cc', session: 's1a2b3c4', waiting: true }),
  act('a3', 'finish', 6, { quest: '7a82cd', session: 's5e6f7a8' }),
  act('a2', 'trees', 9, { repository: 'engine' }),
  act('a1', 'drive', 10, { repository: 'engine', standing: false }),
];
const DESK: DeskRequestNow[] = [
  {
    id: 'r3', act: 'retry', target: '#7a82cc', why: 'The outage has passed and the row is next on the backlog.', filed: minutesAgo(1), state: 'open',
    closed: null, answeredBy: null, reason: null,
  },
  {
    id: 'r2', act: 'hold', target: 'atlas', why: 'Atlas is mid-release; hold it until the tag lands.', filed: minutesAgo(5), state: 'declined',
    closed: minutesAgo(4), answeredBy: null, reason: 'The owner holds atlas during a release.',
  },
  {
    id: 'r1', act: 'trees', target: 'engine', why: 'Engine sessions should work in trees of their own.', filed: minutesAgo(12), state: 'answered',
    closed: minutesAgo(9), answeredBy: 'a2', reason: null,
  },
];
const STANDING: StandingConfirmationNow = {
  holds: [STEWARD], holders: [], stewardActs: ACTS, stewardDesk: DESK,
};

const meta = {
  title: 'Notifications/StewardLine',
  component: StewardLine,
  args: { standing: STANDING, onReverse: () => {} },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof StewardLine>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Appointed, four acts this run, the stop still waiting on a Try again. */
export const Appointed: Story = {};

/** The same, in dark. */
export const AppointedDark: Story = { decorators: inTheme('dark') };

/** The same in 中文, at the 400 px floor. */
export const AppointedChinese: Story = { decorators: [storyWidths.narrow, chinese] };

/** Its terminal ended since, its run's acts still listed; in dark. */
export const TurnedOffDark: Story = { args: { standing: { ...STANDING, holds: [] } }, decorators: inTheme('dark') };

/** No terminal holds it and no act this run, the desk alone holding what callers asked; in dark. */
export const DeskAloneDark: Story = {
  args: { standing: { ...STANDING, holds: [], stewardActs: [] } },
  decorators: inTheme('dark'),
};
