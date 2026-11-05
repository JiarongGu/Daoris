import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { RoleCatalogue } from '../../data/wiring';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { CLAUDE_USE, CODEX_USE } from '../agentsFixtures';
import { AllRolesView } from '../roles/AllRoles';
import { DiagramSection } from '../roles/StructureCanvas';
import { buildStructure, NO_FILTERS, type StructureFilters } from '../roles/roleStructure';
import type { RolesView } from '../roles/RoleSections';
import { RolesCard, type RoleScope } from '../roles/Roles';
import { DRIVER, LINES, PLUGINS, REGISTRY, STRUCTURE, TOOLS } from '../roles/structureFixtures';
import { MachineNotesView } from './MachineNotes';
import { PlacePageView } from './PlacePage';
import { foldedLines, headOf, type PlaceSection, sectionsOf } from './placeShell';
import { AGENTS, CATALOGUE, PLACES } from './setupFixtures';

// This machine's page (SETUPUX1m, the setup-UX design §2.4): Roles folded to its line and All roles open, every role once with its scope
// filter and search, a role placed elsewhere read-only with *Open at ‹place›*, the diagram beside the list, and the suggested-choices
// line above the sections. At the main area's three widths (80, 39 and 25 rem), in both languages and both
// themes; a `play` presses what a person would, by keyboard-reachable controls.

const nothing = () => {};
const READ = PLACES({});

const ROLES: RoleCatalogue = {
  ...CATALOGUE,
  roles: [
    ...CATALOGUE.roles,
    {
      id: 'aurora-review', state: 'set', seeded: false, name: { en: 'Aurora review', zh: '极光审阅' }, takes: ['review'], scopeKind: 'workspace',
      scopeId: 'aurora', revision: 1, choices: [{ agent: 'codex-acp', model: 'gpt-5', effort: 'high' }],
    },
  ],
  suggestedChoicesDated: '2026-10-31',
  suggestedChoices: [{ role: 'build', choices: [{ agent: 'claude-code', model: 'sonnet', effort: 'high' }] }],
};

const structure = buildStructure({
  structure: STRUCTURE, registry: REGISTRY, tools: TOOLS, accounts: { agents: [CLAUDE_USE, CODEX_USE] }, plugins: PLUGINS, lines: LINES,
  driver: DRIVER, filters: NO_FILTERS, appointed: null,
});

type Args = { view?: RolesView; search?: string; scope?: RoleScope; notes?: boolean };

function Page({ view: first = 'list', search: typed = '', scope: chosen = 'all', notes = true }: Args) {
  const { t, i18n } = useTranslation();
  const [view, setView] = useState<RolesView>(first);
  const [search, setSearch] = useState(typed);
  const [scope, setScope] = useState<RoleScope>(chosen);
  const [selected, setSelected] = useState<string | null>(null);
  const [filters, setFilters] = useState<StructureFilters>(NO_FILTERS);
  const [open, setOpen] = useState<readonly PlaceSection[]>(['allRoles']);
  const lines = foldedLines(READ.machine, 'machine', t, i18n.language);
  const facts = headOf({ kind: 'machine', row: READ.machine }, t);
  const list = (
    <RolesCard
      bare catalogue={ROLES} agents={AGENTS} offers={{ 'claude-code': { models: ['opus', 'sonnet', 'haiku'], efforts: ['low', 'medium', 'high'] } }}
      onSet={nothing} onSetAll={nothing}
      scope={scope} onScope={setScope} search={search} onSearch={setSearch} onOpenPlace={nothing}
    />
  );
  const diagram = <DiagramSection structure={structure} read="ok" filters={filters} onFilters={setFilters} selected={selected} onSelect={setSelected} detail={null} />;
  const bodies: Record<PlaceSection, React.ReactNode> = {
    workflow: null,
    sessions: null,
    roles: <p className="m-0 pb-3 pl-3 text-small text-ink-soft">One row for each work that differs; the rest fold.</p>,
    allRoles: <AllRolesView view={view} onView={setView}>{view === 'diagram' ? diagram : list}</AllRolesView>,
  };
  return (
    <PlacePageView
      head={{ title: t('agents.setup.machine'), pill: facts.pill, facts: facts.facts }}
      notes={notes ? <MachineNotesView suggested={1} onShow={nothing} /> : undefined}
      sections={sectionsOf('machine').map((section) => ({
        id: section,
        title: t(`agents.setup.${section}.title`),
        summary: lines[section],
        open: open.includes(section),
        onOpenChange: (on: boolean) => setOpen((was) => (on ? [...was, section] : was.filter((each) => each !== section))),
        body: bodies[section],
      }))}
      focusAsked={0}
    />
  );
}

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof Page> = { title: 'Agents/MachineRoles', component: Page, decorators: [storyWidths.wide] };
export default meta;

type Story = StoryObj<typeof Page>;

/** All roles open on This machine: the suggested line above, the roles placed here and the ones placed elsewhere, once each. */
export const List: Story = {};
export const ListHalf: Story = { decorators: [storyWidths.half] };
export const ListNarrow: Story = { decorators: [storyWidths.narrow] };
export const ListChinese: Story = { decorators: [chinese] };
export const ListChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const ListChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const ListDark: Story = { decorators: [dark] };
export const ListDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const ListDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** The list narrowed by a search, and by where the role is placed. */
export const Searched: Story = { args: { search: 'review' } };
export const SearchedNarrowChinese: Story = { args: { search: '极光' }, decorators: [storyWidths.narrow, chinese] };
export const NothingFound: Story = { args: { search: 'zzz' } };
export const OnlyRepositories: Story = { args: { scope: 'repository' } };

/** The diagram beside the list's place: the same roles, drawn across at 80rem and as its list below. */
export const Diagram: Story = { args: { view: 'diagram' } };
export const DiagramHalf: Story = { args: { view: 'diagram' }, decorators: [storyWidths.half] };
export const DiagramNarrow: Story = { args: { view: 'diagram' }, decorators: [storyWidths.narrow] };
export const DiagramChinese: Story = { args: { view: 'diagram' }, decorators: [chinese] };
export const DiagramDark: Story = { args: { view: 'diagram' }, decorators: [dark] };

/** Nothing suggested: no line above the sections. */
export const Quiet: Story = { args: { notes: false } };

const settle = () => new Promise((resolve) => setTimeout(resolve, 80));
const press = (canvasElement: HTMLElement, name: RegExp) => {
  const button = [...canvasElement.querySelectorAll<HTMLButtonElement>('button')].find((each) => name.test(each.getAttribute('aria-label') ?? each.textContent ?? ''));
  button?.click();
  return button;
};

/** *Edit* on a role placed on this machine: the one role editor opens in the row. */
export const EditingARole: Story = {
  play: async ({ canvasElement }) => {
    press(canvasElement, /^Edit Build$|^编辑 开发$|编辑开发/);
    await settle();
  },
};
export const EditingARoleNarrowChinese: Story = { ...EditingARole, decorators: [storyWidths.narrow, chinese] };

/** Keyboard: focus rests on a role placed elsewhere's *Open at*, whose ring shows. */
export const OpenAtFocused: Story = {
  play: async ({ canvasElement }) => {
    [...canvasElement.querySelectorAll<HTMLButtonElement>('button')].find((each) => /Open Engine build at engine|前往engine编辑引擎开发/.test(each.getAttribute('aria-label') ?? ''))?.focus();
  },
};

/** Keyboard: focus rests on the chosen view's radio, whose ring shows. */
export const ViewFocused: Story = {
  play: async ({ canvasElement }) => {
    canvasElement.querySelector<HTMLButtonElement>('[role="radio"][aria-checked="true"]')?.focus();
  },
};
