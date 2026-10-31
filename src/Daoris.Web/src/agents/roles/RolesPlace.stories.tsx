import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { RoleCatalogue, RoleRow } from '../../data/wiring';
import { InTheme } from '../../plugins/storyIcons';
import { AgentsModes, AgentsStrip, RoleSectionStrip } from '../AgentsModes';
import type { RoleSection } from '../modes';
import { RoleSectionList, RoleSectionMain } from './RoleSections';
import { RolesCard } from './Roles';

// The Agents place's Roles mode (COMPANYVIEW1b, the company-view design §1, §4): the list's head and its five sections, Working roles
// the one built and the other four saying they come later; the list closed to its strip; and the chosen section's main area at the
// widths the frame gives it, 80, 39 and 25 rem, in both languages and both themes.

const nothing = () => {};

const seed = (id: string, en: string, zh: string, work: string): RoleRow => ({
  id, state: 'seed', seeded: true, nameEn: en, nameZh: zh, takes: [work], scopeKind: 'machine', revision: 0,
});

const CATALOGUE: RoleCatalogue = {
  roles: [seed('build', 'Build', '开发', 'build'), seed('intake', 'Intake', '受理', 'intake'), seed('ask-daoris', 'Ask Daoris', '问道衍', 'ask-daoris')],
  works: ['build', 'design', 'chat', 'ask-daoris', 'intake', 'help', 'partner', 'review', 'brainstorm'],
  kept: true,
  suggested: [],
};

const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];

function List({ section = 'working' }: { section?: RoleSection }) {
  return (
    <div className="w-[280px] border border-line bg-page">
      <AgentsModes mode="roles" onMode={nothing} />
      <RoleSectionList section={section} onSection={nothing} />
    </div>
  );
}

function Strip() {
  return (
    <div className="w-[56px] border border-line bg-page">
      <AgentsStrip mode="roles" onMode={nothing}>
        <RoleSectionStrip section="working" onSection={nothing} />
      </AgentsStrip>
    </div>
  );
}

function Main({ section = 'working' }: { section?: RoleSection }) {
  return (
    <RoleSectionMain section={section}>
      <RolesCard catalogue={CATALOGUE} agents={AGENTS} onSet={nothing} onSetActor={nothing} />
    </RoleSectionMain>
  );
}

const meta: Meta<typeof Main> = {
  title: 'Agents/RolesPlace',
  component: Main,
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Main>;

/** The list: the head's two modes, Roles in front, and the five sections, Working roles chosen. */
export const SectionList: StoryObj<typeof List> = { render: () => <List /> };

/** The same in 中文. */
export const SectionListChinese: StoryObj<typeof List> = { render: () => <List />, decorators: [chinese] };

/** The same in dark. */
export const SectionListDark: StoryObj<typeof List> = { render: () => <InTheme theme="dark"><List /></InTheme> };

/** A section not built, chosen. */
export const SectionNotBuiltChosen: StoryObj<typeof List> = { render: () => <List section="accounts" /> };

/** The list closed to its strip: the two modes, then the five sections. */
export const ListStrip: StoryObj<typeof Strip> = { render: () => <Strip /> };

/** The strip in dark. */
export const ListStripDark: StoryObj<typeof Strip> = { render: () => <InTheme theme="dark"><Strip /></InTheme> };

/** The strip in 中文. */
export const ListStripChinese: StoryObj<typeof Strip> = { render: () => <Strip />, decorators: [chinese] };

/** Working roles at the wide width, beside the page. */
export const WorkingRolesWide: Story = {};

/** The same at the half width, where the list may close to its strip. */
export const WorkingRolesHalf: Story = { decorators: [storyWidths.half] };

/** The same at the 400 px floor, where the list is a strip or lies over the page. */
export const WorkingRolesNarrow: Story = { decorators: [storyWidths.narrow] };

/** The same in 中文, at each width. */
export const WorkingRolesChinese: Story = { decorators: [chinese] };
export const WorkingRolesChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const WorkingRolesChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

/** The same in dark, at each width. */
export const WorkingRolesDark: Story = { render: () => <InTheme theme="dark"><Main /></InTheme> };
export const WorkingRolesDarkHalf: Story = { render: () => <InTheme theme="dark"><Main /></InTheme>, decorators: [storyWidths.half] };
export const WorkingRolesDarkNarrow: Story = { render: () => <InTheme theme="dark"><Main /></InTheme>, decorators: [storyWidths.narrow] };

/** A section not built: its name and one quiet line that it comes later. */
export const SectionNotBuilt: Story = { render: () => <Main section="work" /> };
export const SectionNotBuiltNarrow: Story = { render: () => <Main section="work" />, decorators: [storyWidths.narrow, chinese] };
