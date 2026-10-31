import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { AppointmentNow } from '../../person/standing';
import { InTheme } from '../../plugins/storyIcons';
import type { SupervisorSetup } from '../../projects/setup/SupervisorRow';
import type { StewardSetup } from '../../settings/wiring/WiringSteward';
import { AppointedList } from './AppointedList';
import { RoleSectionMain } from './RoleSections';

// The Roles mode's Appointed roles section (COMPANYVIEW1e, the company-view design §3.2): the steward for This Daoris, the supervisor of
// a workspace and of its repositories, at the main area's three widths, in both languages and both themes. Both roles are dark in this
// build: the last story is what it draws today.

const OFFERED = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'];
const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();
const nothing = () => {};

const stewardAt = (appointment: AppointmentNow | null): StewardSetup => ({
  appointment, offered: OFFERED, acts: appointment ? 4 : 0, holders: [], onAppoint: (answered) => answered.done(), onTurnOff: nothing,
});
const supervisorAt = (over: Partial<SupervisorSetup>): SupervisorSetup => ({
  offered: true, appointment: null, onAppoint: (answered) => answered.done(), onTurnOff: nothing, ...over,
});

const STEWARD: AppointmentNow = {
  role: 'steward', repository: '', holder: 'terminal', since: hoursAgo(2), dormant: false, answers: 0, scope: 'machine', holderId: 'h1',
  acts: OFFERED, setup: false, workspace: '', window: 5,
};
const OF_WORKSPACE: AppointmentNow = {
  role: 'supervisor', repository: '', holder: 'terminal', since: hoursAgo(5), dormant: false, answers: 2, scope: 'workspace:games',
  holderId: 'h1', acts: [], setup: false, workspace: 'games', window: 15,
};
const OF_REPOSITORY: AppointmentNow = { ...OF_WORKSPACE, repository: 'engine', workspace: '', scope: 'repository:engine', answers: 3, window: 5 };

/** Offered, with a workspace appointed, its repository covered by it and another with its own. */
function Offered() {
  return (
    <AppointedList
      steward={stewardAt(STEWARD)}
      workspaces={[{ workspace: 'games', supervisor: supervisorAt({ appointment: OF_WORKSPACE }) }]}
      repositories={[
        { repository: 'engine', supervisor: supervisorAt({ appointment: OF_REPOSITORY, covering: OF_WORKSPACE }) },
        { repository: 'tools', supervisor: supervisorAt({ covering: OF_WORKSPACE }) },
      ]}
    />
  );
}

/** Found as Daoris started, and unset: dormant until *Appoint again*, and off. */
function Dormant() {
  return (
    <AppointedList
      steward={stewardAt({ ...STEWARD, dormant: true, holderId: null })}
      workspaces={[{ workspace: 'games', supervisor: supervisorAt({ appointment: { ...OF_WORKSPACE, dormant: true, holderId: null, answers: 0 } }) }]}
      repositories={[{ repository: 'engine', supervisor: supervisorAt({}) }]}
    />
  );
}

/** What this build draws: neither role offered, so nothing to press. */
function NotOffered() {
  return (
    <AppointedList
      steward={null}
      workspaces={[{ workspace: 'games', supervisor: supervisorAt({ offered: false }) }]}
      repositories={[{ repository: 'engine', supervisor: supervisorAt({ offered: false }) }]}
    />
  );
}

function Main({ which = 'offered' }: { which?: 'offered' | 'dormant' | 'notOffered' }) {
  return (
    <RoleSectionMain section="appointed">
      {which === 'offered' ? <Offered /> : which === 'dormant' ? <Dormant /> : <NotOffered />}
    </RoleSectionMain>
  );
}

const meta: Meta<typeof Main> = {
  title: 'Agents/AppointedRoles',
  component: Main,
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Main>;

const dark = (which?: 'offered' | 'dormant' | 'notOffered') => () => <InTheme theme="dark"><Main which={which} /></InTheme>;

/** Offered and appointed, at each width. */
export const Appointed: Story = {};
export const AppointedHalf: Story = { decorators: [storyWidths.half] };
export const AppointedNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, at each width. */
export const AppointedChinese: Story = { decorators: [chinese] };
export const AppointedChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const AppointedChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** The same in dark, at each width. */
export const AppointedDark: Story = { render: dark() };
export const AppointedDarkHalf: Story = { render: dark(), decorators: [storyWidths.half] };
export const AppointedDarkNarrow: Story = { render: dark(), decorators: [storyWidths.narrow] };

/** Dormant and off. */
export const DormantAndOff: Story = { args: { which: 'dormant' } };
export const DormantAndOffNarrow: Story = { args: { which: 'dormant' }, decorators: [storyWidths.narrow, chinese] };
export const DormantAndOffDark: Story = { render: dark('dormant') };

/** Dark in this build: neither role offered. */
export const NotOfferedInThisBuild: Story = { args: { which: 'notOffered' } };
export const NotOfferedInThisBuildNarrow: Story = { args: { which: 'notOffered' }, decorators: [storyWidths.narrow] };
export const NotOfferedInThisBuildChinese: Story = { args: { which: 'notOffered' }, decorators: [chinese] };
export const NotOfferedInThisBuildDark: Story = { render: dark('notOffered') };
