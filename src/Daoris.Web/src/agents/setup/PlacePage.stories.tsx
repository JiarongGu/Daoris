import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { PlaceRow } from '../../data/wiring';
import { Button } from '../../design/ui';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { PlacePageView, type PlaceDriving } from './PlacePage';
import { PlaceRolesView } from './PlaceRoles';
import { foldedLines, headOf, type PlaceKind, type PlaceSection, sectionsOf } from './placeShell';
import { AGENTS, CATALOGUE, CHAIN, PLACES } from './setupFixtures';

// A place's page (SETUPUX1h, the setup-UX design §2.1): the head and three sections folded to the line that names their values, at the
// main area's three widths (80, 39 and 25 rem), in both languages and both themes. The first section's open body is a stand-in the size
// the design draws for Workflow (its routes and folds, the chart one press away), since the section's own is row o's: the measured spec
// (`measured/place.spec.ts`) holds the head and the three lines within the first 400 px with it.

const nothing = () => {};

const READ = PLACES({});
const ENGINE = READ.repositories.find((row) => row.repository === 'engine')!;
const SITE = READ.repositories.find((row) => row.repository === 'site')!;
const IMPORTED = PLACES({ detail: { workspace: 'aurora', repository: 'archive-1' } }).detail!;
const WORKSPACE = READ.workspaces[0]!.place;

const DRIVING: PlaceDriving = {
  drivable: true, held: false, ownTree: true, busy: false, terminal: 'daoris driver drive|undrive engine',
  onDrive: nothing, onHold: nothing, onTrees: nothing, onOpenAppointed: nothing, onOpenRepositories: nothing,
};

const editor = { agents: AGENTS, offers: {}, staffing: [], catalogue: CATALOGUE, read: 'ok' as const, busy: false, onSet: nothing, onSetActor: nothing };

/** Workflow's open section as the design draws it: the routes, the chart one press away, and the folds wrapped as one row. */
function WorkflowStandIn() {
  return (
    <div className="grid gap-1 pb-2 pl-3 text-small text-ink-soft">
      <p className="m-0">Default work → Release for engine v2</p>
      <p className="m-0">Documentation → Documents workflow v1</p>
      <p className="m-0">Landed work → No verification here</p>
      <div><Button variant="ghost" className="px-0">Show the chart ▸</Button></div>
      <p className="m-0 flex flex-wrap gap-x-3">
        {['Gates', 'Landing', 'Second opinion', 'Your look', 'Landed route', 'The line', 'More'].map((fold) => <span key={fold}>▸ {fold}</span>)}
      </p>
    </div>
  );
}

function Page({ row = ENGINE, kind = 'repository', opened, driving = DRIVING, back = false }: {
  row?: PlaceRow;
  kind?: PlaceKind;
  opened?: readonly PlaceSection[];
  driving?: PlaceDriving | null;
  back?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const first = sectionsOf(kind)[0]!;
  const [open, setOpen] = useState<readonly PlaceSection[]>(opened ?? [first]);
  const lines = foldedLines(row, kind, t, i18n.language);
  const facts = headOf({ kind, row, counts: { driven: 2, notDriven: 41 }, members: READ.repositories }, t);
  const ask = kind === 'machine' ? {} : kind === 'workspace' ? { workspace: 'aurora' } : { workspace: 'aurora', repository: row.repository ?? '' };
  const bodies: Record<PlaceSection, React.ReactNode> = {
    workflow: <WorkflowStandIn />,
    roles: <PlaceRolesView place={ask} read="ok" answer={CHAIN(ask)} editor={editor} appointed={null} onSource={nothing} />,
    sessions: <p className="m-0 pb-3 pl-3 text-small text-ink-soft">Access, language, helpers and the reach.</p>,
    allRoles: <p className="m-0 pb-3 pl-3 text-small text-ink-soft">Every role once; drawn in full by MachineRoles' stories.</p>,
  };
  const name = kind === 'machine' ? t('agents.setup.machine') : kind === 'workspace' ? row.workspace ?? '' : row.repository ?? '';
  return (
    <PlacePageView
      head={{
        title: name,
        pill: facts.pill,
        facts: facts.facts,
        parent: kind === 'repository' ? { name: row.workspace ?? '', onOpen: nothing } : undefined,
        setUp: kind === 'machine' ? undefined : { onOpen: nothing },
      }}
      sections={sectionsOf(kind).map((section) => ({
        id: section,
        title: t(`agents.setup.${section}.title`),
        summary: lines[section],
        open: open.includes(section),
        onOpenChange: (on: boolean) => setOpen((was) => (on ? [...was, section] : was.filter((each) => each !== section))),
        body: bodies[section],
      }))}
      driving={kind === 'repository' ? driving : null}
      back={back ? { place: 'engine', section: t('agents.setup.roles.title'), onBack: nothing } : null}
      focusAsked={0}
    />
  );
}

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof Page> = { title: 'Agents/PlacePage', component: Page, decorators: [storyWidths.wide] };
export default meta;

type Story = StoryObj<typeof Page>;

/** A driven repository: the head, Workflow open on arrival, Roles and Sessions folded to their lines with each value's source. */
export const Repository: Story = {};
export const RepositoryHalf: Story = { decorators: [storyWidths.half] };
export const RepositoryNarrow: Story = { decorators: [storyWidths.narrow] };

export const RepositoryChinese: Story = { decorators: [chinese] };
export const RepositoryChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const RepositoryChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };

export const RepositoryDark: Story = { decorators: [dark] };
export const RepositoryDarkHalf: Story = { decorators: [storyWidths.half, dark] };
export const RepositoryDarkNarrow: Story = { decorators: [storyWidths.narrow, dark] };

/** A repository not driven, read through its detail: its pill, *Set up…* primary, no Hold, and the same three sections. */
export const NotDriven: Story = { args: { row: IMPORTED, driving: { ...DRIVING, drivable: false, ownTree: false } } };
export const NotDrivenHalf: Story = { ...NotDriven, decorators: [storyWidths.half] };
export const NotDrivenNarrowChinese: Story = { ...NotDriven, decorators: [storyWidths.narrow, chinese] };

/** A held repository: the pill says Held and the act says Resume; a place that needs a look says why in its facts. */
export const Held: Story = { args: { row: { ...SITE, held: true, driven: true }, driving: { ...DRIVING, held: true } } };

/** A workspace: its driven count and how its repositories land; Workflow first. */
export const Workspace: Story = { args: { kind: 'workspace', row: WORKSPACE, driving: null } };
export const WorkspaceHalf: Story = { ...Workspace, decorators: [storyWidths.half] };

/** This machine: Roles first, open on arrival. */
export const Machine: Story = { args: { kind: 'machine', row: READ.machine, driving: null } };

/** Reached by a door from another place: *Back* names the page it came from and its section; Roles opened at the work. */
export const FromADoor: Story = { args: { back: true, opened: ['roles'] } };
export const FromADoorNarrow: Story = { ...FromADoor, decorators: [storyWidths.narrow] };
