import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { AppointmentNow } from '../../person/standing';
import { InTheme } from '../../plugins/storyIcons';
import { WiringSteward } from './WiringSteward';

// The steward's row under This Daoris → *Roles* (STEWARDROLE1b, D209; the steward design §5.3): its states, and the ask *Appoint…* and
// *Appoint again…* open, at the main area's widths. The key is never drawn: the row holds none.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const OFFERED = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'];
const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();
const live: AppointmentNow = {
  role: 'steward', repository: '', holder: 'terminal', since: hoursAgo(2), dormant: false, answers: 0, scope: 'machine', holderId: 'h1',
  acts: OFFERED, setup: false, workspace: null, window: null,
};

const meta = {
  title: 'Settings/WiringSteward',
  component: WiringSteward,
  args: {
    steward: { appointment: null, offered: OFFERED, acts: 0, holders: [], onAppoint: (answered) => answered.done(), onTurnOff: () => {} },
  },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof WiringSteward>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Not appointed: *Appoint…*. */
export const Off: Story = {};

/** The ask open: a line per offered act, the six it never does, its bounds and holder, the set-up tick off; at the 400 px floor. */
export const Asking: Story = { args: { open: true }, decorators: [storyWidths.narrow] };

/** The ask open while a role's terminal lives: add the steward to it or give it one of its own, neither preselected. */
export const AskingWithATerminalOpen: Story = {
  args: {
    open: true,
    steward: {
      appointment: null, offered: OFFERED, acts: 0, holders: [{ id: 'h1', since: hoursAgo(1), roles: ['supervisor'] }],
      onAppoint: (answered) => answered.done(), onTurnOff: () => {},
    },
  },
  decorators: [storyWidths.narrow],
};

/** Appointed: since when and its acts this run, with **Turn off**. */
export const Appointed: Story = {
  args: { steward: { appointment: live, offered: OFFERED, acts: 14, holders: [], started: true, onAppoint: () => {}, onTurnOff: () => {} } },
};

/** Found as Daoris started: dormant, *Appoint again…* and **Turn off**. */
export const Dormant: Story = {
  args: {
    steward: { appointment: { ...live, dormant: true, holderId: null }, offered: OFFERED, acts: 0, holders: [], onAppoint: () => {}, onTurnOff: () => {} },
  },
};

/** The ask open, in dark. */
export const AskingDark: Story = { args: { open: true }, decorators: [storyWidths.narrow, ...inTheme('dark')] };

/** Appointed, in dark. */
export const AppointedDark: Story = { ...Appointed, decorators: inTheme('dark') };

/** The ask open in 中文. */
export const AskingChinese: Story = { args: { open: true }, decorators: [storyWidths.narrow, chinese] };
