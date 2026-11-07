import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import type { RoleCatalogue } from '../../data/wiring';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { DiagramSection } from './StructureCanvas';
import { RolesCard } from './Roles';
import { type AppointedRead, buildStructure, NO_FILTERS, type StructureFilters } from './roleStructure';
import { DiagramDetail, type RoleDoors } from './StructureSections';
import { DRIVER, largeStructure, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS, WORKS } from './structureFixtures';

// The roles' diagram at the widths the frame gives the main area: across at 80rem with the
// chosen node's detail beside it, and its list at 39 and 25rem, the detail below; in both languages and both themes. Past 24 nodes
// each workspace folds to one node. The measured check (`measured/roles.spec.ts`) reads these stories' edges in a real browser.

const nothing = () => {};
const DOORS: RoleDoors = { agent: nothing, repository: nothing, plugin: nothing };
const AGENTS = [{ value: 'claude-code', label: 'Claude Code' }, { value: 'codex-acp', label: 'Codex' }];
const CATALOGUE: RoleCatalogue = { roles: STRUCTURE.roles, works: WORKS, kept: true };

const APPOINTED: AppointedRead = {
  stewardOffered: true, supervisorOffered: true,
  appointments: [
    { role: 'steward', scope: 'machine', workspace: '', repository: '', dormant: false },
    { role: 'supervisor', scope: 'workspace:aurora', workspace: 'aurora', repository: '', dormant: true },
  ],
};

function Page({ chosen = null, large = false, appointed = null, filters = NO_FILTERS }: {
  chosen?: string | null; large?: boolean | 'hundred'; appointed?: AppointedRead | null; filters?: StructureFilters;
}) {
  const [selected, setSelected] = useState(chosen);
  const [now, setNow] = useState(filters);
  const [open, setOpen] = useState<ReadonlySet<string>>(new Set(chosen ? [chosen] : []));
  const from = large ? largeStructure(large === 'hundred' ? [30, 30] : undefined) : { structure: STRUCTURE, registry: REGISTRY };
  const structure = buildStructure({
    ...from, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: large ? null : LINES,
    driver: large ? null : DRIVER, filters: now, appointed,
  });
  const toggle = (id: string) => setOpen((was) => {
    const next = new Set(was);
    if (!next.delete(id)) next.add(id);
    return next;
  });
  const detail = selected && (
    <DiagramDetail
      structure={structure} id={selected} open={open} onToggle={toggle} doors={DOORS}
      working={<RolesCard catalogue={CATALOGUE} agents={AGENTS} onSet={nothing} only={selected.slice('role:'.length)} />}
    />
  );
  return (
    <DiagramSection
      structure={structure} read="ok" filters={now} onFilters={setNow} selected={selected}
      onSelect={(id) => { setSelected(id); setOpen((was) => new Set([...was, id])); }} detail={detail}
    />
  );
}

const meta: Meta<typeof Page> = {
  title: 'Agents/StructureCanvas',
  component: Page,
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof Page>;

const dark = (args: Parameters<typeof Page>[0]) => <InTheme theme="dark"><Page {...args} /></InTheme>;

/** Across at 80rem: This Daoris, the places that set something of their own, the nine roles and the agents their choices name. */
export const Wide: Story = {};
/** Its list at 39rem, the head saying so, the detail below. */
export const Half: Story = { decorators: [storyWidths.half] };
/** Its list at the 400 px floor. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };

export const Chinese: Story = { decorators: [chinese] };
export const ChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const Dark: Story = { render: dark };
// The theme wraps the width's frame and adds no padding of its own, so the dark story holds the region's 36rem at 39rem as the light one does.
const darkFrame: Decorator = (Story) => <InTheme theme="dark" className="p-0"><Story /></InTheme>;
export const DarkHalf: Story = { decorators: [storyWidths.half, darkFrame] };
export const DarkNarrow: Story = { decorators: [storyWidths.narrow, darkFrame] };

/** A repository chosen: per work the role the driver answered there lit, inherited where it takes this machine's, its row beside. */
export const PlaceChosen: Story = { args: { chosen: 'place:aurora/engine' } };
export const PlaceChosenChinese: Story = { args: { chosen: 'place:aurora/engine' }, decorators: [chinese] };
export const PlaceChosenHalf: Story = { args: { chosen: 'place:aurora/engine' }, decorators: [storyWidths.half] };
export const PlaceChosenDark: Story = { args: { chosen: 'place:aurora/engine' }, render: dark };

/** An agent chosen: every role leaning on it lit. */
export const AgentChosen: Story = { args: { chosen: 'agent:codex' } };
export const AgentChosenNarrowChinese: Story = { args: { chosen: 'agent:codex' }, decorators: [storyWidths.narrow, chinese] };

/** A role chosen: its detail is its catalogue row, with its Edit. */
export const RoleChosen: Story = { args: { chosen: 'role:build' } };

/** The steward appointed and a workspace's supervisor dormant, its edge dashed. */
export const Appointed: Story = { args: { appointed: APPOINTED } };
export const AppointedDark: Story = { args: { appointed: APPOINTED }, render: dark };

/** Filtered to one agent: the roles that lean on it, where they are placed. */
export const OneAgent: Story = { args: { filters: { ...NO_FILTERS, agent: 'codex' } } };

/** Past 24 nodes: each workspace folded to one node, its edges drawn to it. */
export const TooLarge: Story = { args: { large: true } };
export const TooLargeChinese: Story = { args: { large: true }, decorators: [chinese] };
export const TooLargeNarrow: Story = { args: { large: true }, decorators: [storyWidths.narrow] };
/** A hundred places and roles and more, folded to a band for each workspace, and drawn whole once *Show whole* is pressed. */
export const Hundred: Story = { args: { large: 'hundred' } };
export const HundredHalf: Story = { args: { large: 'hundred' }, decorators: [storyWidths.half] };
export const TooLargeHalf: Story = { args: { large: true }, decorators: [storyWidths.half] };
