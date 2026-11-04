import type { Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { AccountsSection, RepositorySection, type RoleDoors, type StructureRead, WorkSection } from './StructureSections';
import { buildStructure, NO_FILTERS, type StructureFilters } from './roleStructure';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKFLOWS } from './structureFixtures';

// Work, Accounts and By repository (COMPANYVIEW1d, the company-view design §3.3 to §3.5) at the widths the frame gives the main area,
// 80, 39 and 25 rem, in both languages and both themes; a failed read and a shell that does not offer it are said, never drawn empty.

const nothing = () => {};
const DOORS: RoleDoors = { agent: nothing, setting: nothing, repository: nothing, plugin: nothing, addRole: nothing };

type Which = 'work' | 'accounts' | 'repository';

function Section({ which, opened = [], filters = NO_FILTERS, read = 'ok' }: {
  which: Which; opened?: string[]; filters?: StructureFilters; read?: StructureRead;
}) {
  const [now, setNow] = useState(filters);
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set(opened));
  const structure = read === 'ok'
    ? buildStructure({
      structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
      driver: DRIVER, workflows: WORKFLOWS, filters: now,
    })
    : null;
  const props = {
    structure, read, filters: now, onFilters: setNow, open, doors: DOORS,
    onToggle: (id: string) => setOpen((was) => {
      const next = new Set(was);
      if (!next.delete(id)) next.add(id);
      return next;
    }),
  };
  return which === 'work' ? <WorkSection {...props} /> : which === 'accounts' ? <AccountsSection {...props} /> : <RepositorySection {...props} />;
}

const meta: Meta<typeof Section> = {
  title: 'Agents/RolesStructure',
  component: Section,
  decorators: [storyWidths.wide],
  args: { which: 'work' },
};
export default meta;

type Story = StoryObj<typeof Section>;

/** Work: the nine works folded, each with its role, how many places have their own, and the effective rule. */
export const WorkFolded: Story = { args: { which: 'work' } };
export const WorkFoldedHalf: Story = { args: { which: 'work' }, decorators: [storyWidths.half] };
export const WorkFoldedNarrow: Story = { args: { which: 'work' }, decorators: [storyWidths.narrow] };
export const WorkFoldedChinese: Story = { args: { which: 'work' }, decorators: [chinese] };
export const WorkFoldedChineseNarrow: Story = { args: { which: 'work' }, decorators: [storyWidths.narrow, chinese] };
export const WorkFoldedDark: Story = { render: (args) => <InTheme theme="dark"><Section {...args} /></InTheme>, args: { which: 'work' } };

/** Work, opened: the choices at their positions with each state by the last readings. */
export const WorkOpened: Story = { args: { which: 'work', opened: ['work:build', 'work:review', 'work:brainstorm'] } };
/** Work, opened for what it may call (COMPANYVIEW1i): partnering's named tools, and building's every tool but those refused. */
export const WorkOpenedCalls: Story = { args: { which: 'work', opened: ['work:partner', 'work:build'] } };
export const WorkOpenedCallsChinese: Story = { args: { which: 'work', opened: ['work:partner', 'work:build'] }, decorators: [chinese] };
export const WorkOpenedHalf: Story = { args: { which: 'work', opened: ['work:build', 'work:review'] }, decorators: [storyWidths.half] };
export const WorkOpenedNarrow: Story = { args: { which: 'work', opened: ['work:build', 'work:review'] }, decorators: [storyWidths.narrow] };
export const WorkOpenedChinese: Story = { args: { which: 'work', opened: ['work:build', 'work:review'] }, decorators: [chinese] };
export const WorkOpenedDark: Story = { render: (args) => <InTheme theme="dark"><Section {...args} /></InTheme>, args: { which: 'work', opened: ['work:build', 'work:review'] } };

/** Work at a workspace's scope, and *Needs a look* alone. */
export const WorkAtWorkspace: Story = { args: { which: 'work', filters: { ...NO_FILTERS, workspace: 'aurora' } } };
export const WorkNeedsALook: Story = { args: { which: 'work', filters: { ...NO_FILTERS, needsLook: true }, opened: ['work:review'] } };

/** The read said, never an empty list. */
export const NotRead: Story = { args: { which: 'work', read: 'loading' } };
export const ReadFailed: Story = { args: { which: 'work', read: 'failed' }, decorators: [storyWidths.narrow, chinese] };

/** Accounts: each agent a choice names, who leans on it at which position, and what cannot run. */
export const AccountsFolded: Story = { args: { which: 'accounts' } };
export const AccountsFoldedHalf: Story = { args: { which: 'accounts' }, decorators: [storyWidths.half] };
export const AccountsFoldedNarrow: Story = { args: { which: 'accounts' }, decorators: [storyWidths.narrow] };
export const AccountsOpened: Story = { args: { which: 'accounts', opened: ['agent:codex', 'agent:dsh', 'agent:ghost-agent'] } };
export const AccountsOpenedHalf: Story = { args: { which: 'accounts', opened: ['agent:codex', 'agent:dsh'] }, decorators: [storyWidths.half] };
export const AccountsOpenedNarrow: Story = { args: { which: 'accounts', opened: ['agent:codex', 'agent:dsh'] }, decorators: [storyWidths.narrow] };
export const AccountsOpenedChinese: Story = { args: { which: 'accounts', opened: ['agent:codex', 'agent:dsh', 'agent:ghost-agent'] }, decorators: [chinese] };
export const AccountsOpenedDark: Story = { render: (args) => <InTheme theme="dark"><Section {...args} /></InTheme>, args: { which: 'accounts', opened: ['agent:codex', 'agent:dsh'] } };

/** By repository: workspaces as folded groups, repositories inside, one opened to its work, line, landing, workflow and plugins. */
export const RepositoriesFolded: Story = { args: { which: 'repository' } };
export const RepositoriesOpened: Story = { args: { which: 'repository', opened: ['place:aurora', 'place:aurora/engine', 'place:default', 'place:default/tools'] } };
export const RepositoriesOpenedHalf: Story = { args: { which: 'repository', opened: ['place:aurora', 'place:aurora/engine'] }, decorators: [storyWidths.half] };
export const RepositoriesOpenedNarrow: Story = { args: { which: 'repository', opened: ['place:aurora', 'place:aurora/engine'] }, decorators: [storyWidths.narrow] };
export const RepositoriesOpenedChinese: Story = { args: { which: 'repository', opened: ['place:aurora', 'place:aurora/engine'] }, decorators: [chinese] };
export const RepositoriesOpenedChineseNarrow: Story = { args: { which: 'repository', opened: ['place:aurora', 'place:aurora/engine'] }, decorators: [storyWidths.narrow, chinese] };
export const RepositoriesOpenedDark: Story = { render: (args) => <InTheme theme="dark"><Section {...args} /></InTheme>, args: { which: 'repository', opened: ['place:aurora', 'place:aurora/engine', 'place:default', 'place:default/tools'] } };
