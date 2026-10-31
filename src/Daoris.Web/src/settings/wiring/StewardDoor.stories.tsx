import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { AppointmentNow } from '../../person/standing';
import { InTheme } from '../../plugins/storyIcons';
import { StewardDoor } from './StewardDoor';

// The steward's line under This Daoris → *Roles* (COMPANYVIEW1e): where it stands in one sentence, and the door to Agents → Roles →
// Appointed roles, at the main area's widths, in both languages and both themes.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const live: AppointmentNow = {
  role: 'steward', repository: '', holder: 'terminal', since: new Date(Date.now() - 7_200_000).toISOString(), dormant: false, answers: 0,
  scope: 'machine', holderId: 'h1', acts: [], setup: false, workspace: '', window: 5,
};

const meta = {
  title: 'Settings/StewardDoor',
  component: StewardDoor,
  args: { appointment: null, acts: 0, onOpen: () => {} },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof StewardDoor>;
export default meta;

type Story = StoryObj<typeof meta>;

export const Off: Story = {};
export const OffHalf: Story = { decorators: [storyWidths.half] };
export const OffNarrow: Story = { decorators: [storyWidths.narrow] };
export const Appointed: Story = { args: { appointment: live, acts: 14 } };
export const Dormant: Story = { args: { appointment: { ...live, dormant: true, holderId: null } } };
export const AppointedDark: Story = { args: { appointment: live, acts: 14 }, decorators: inTheme('dark') };
export const OffNarrowChinese: Story = { decorators: [storyWidths.narrow, chinese] };
export const OffNarrowDark: Story = { decorators: [storyWidths.narrow, ...inTheme('dark')] };
