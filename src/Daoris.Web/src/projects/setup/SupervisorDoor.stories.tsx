import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ReactElement } from 'react';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { AppointmentNow } from '../../person/standing';
import { InTheme } from '../../plugins/storyIcons';
import { SupervisorDoor } from './SupervisorDoor';

// The supervisor's line in a repository's or a workspace's Setup (COMPANYVIEW1e): where it stands in one sentence, and the door to
// Agents → Roles → Appointed roles, at the main area's widths, in both languages and both themes.

const inTheme = (theme: 'light' | 'dark') => [(Story: () => ReactElement) => <InTheme theme={theme}><Story /></InTheme>];
const live: AppointmentNow = {
  role: 'supervisor', repository: 'engine', holder: 'terminal', since: new Date(Date.now() - 7_200_000).toISOString(), dormant: false, answers: 3,
  scope: 'repository:engine', holderId: 'h1', acts: [], setup: false, workspace: '', window: 5,
};

const meta = {
  title: 'Projects/SupervisorDoor',
  component: SupervisorDoor,
  args: { repository: 'engine', supervisor: { offered: true, appointment: null }, onOpen: () => {} },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof SupervisorDoor>;
export default meta;

type Story = StoryObj<typeof meta>;

export const Off: Story = {};
export const OffHalf: Story = { decorators: [storyWidths.half] };
export const OffNarrow: Story = { decorators: [storyWidths.narrow] };
export const Appointed: Story = { args: { supervisor: { offered: true, appointment: live } } };
export const NotOffered: Story = { args: { supervisor: { offered: false, appointment: null } } };
export const OfAWorkspace: Story = { args: { repository: undefined, workspace: 'games' } };
export const Covered: Story = {
  args: { supervisor: { offered: true, appointment: null, covering: { ...live, repository: '', scope: 'workspace:games', workspace: 'games', window: 15 } } },
};
export const OffNarrowChinese: Story = { decorators: [storyWidths.narrow, chinese] };
export const AppointedDark: Story = { args: { supervisor: { offered: true, appointment: live } }, decorators: inTheme('dark') };
export const OffNarrowDark: Story = { decorators: [storyWidths.narrow, ...inTheme('dark')] };
