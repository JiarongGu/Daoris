import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import type { AppointmentNow, StandingConfirmationNow, StewardActNow } from '../person/standing';
import { InTheme } from '../plugins/storyIcons';
import { StewardLine } from './StewardLine';

// The steward's line in *Notifications* (STEWARDROLE1b, D209; the steward design §5.3, §5.4): appointed with its run's acts, a stop
// still waiting on a Try again said as *Stopped by the steward*, and dormant; light and dark, and in 中文. The key is never drawn.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const minutesAgo = (count: number) => new Date(Date.now() - count * 60_000).toISOString();
const OFFERED = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'];
const STEWARD: AppointmentNow = {
  role: 'steward', repository: '', holder: 'terminal', since: minutesAgo(90), dormant: false, answers: 0, scope: 'machine', holderId: 'h1',
  acts: OFFERED, setup: false,
};
const act = (id: string, kind: string, at: number, more: Partial<StewardActNow> = {}): StewardActNow => ({
  id, act: kind, repository: null, quest: null, session: null, at: minutesAgo(at), standing: true, waiting: false, ...more,
});
const ACTS: StewardActNow[] = [
  act('a4', 'stop', 2, { quest: '7a82cc', session: 's1a2b3c4', waiting: true }),
  act('a3', 'finish', 6, { quest: '7a82cd', session: 's5e6f7a8' }),
  act('a2', 'trees', 9, { repository: 'engine' }),
  act('a1', 'drive', 10, { repository: 'engine', standing: false }),
];
const STANDING: StandingConfirmationNow = {
  offered: false, on: false, presses: [], built: ['stop', 'finish', 'hold', 'resume'], since: null, unread: null, unwritten: false, used: 0,
  held: [], appointmentsOffered: true, appointments: [STEWARD], supervisorOffered: false, stewardOffered: true, holders: [],
  stewardActsOffered: OFFERED, stewardActs: ACTS,
};

const meta = {
  title: 'Notifications/StewardLine',
  component: StewardLine,
  args: { standing: STANDING, onTurnOff: () => {}, onReverse: () => {} },
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

/** Found as Daoris started: dormant, its **Turn off**. */
export const Dormant: Story = {
  args: { standing: { ...STANDING, appointments: [{ ...STEWARD, dormant: true, holderId: null }], stewardActs: [] } },
};

/** Turned off since, its run's acts still listed, with no **Turn off**; in dark. */
export const TurnedOffDark: Story = { args: { standing: { ...STANDING, appointments: [] } }, decorators: inTheme('dark') };
