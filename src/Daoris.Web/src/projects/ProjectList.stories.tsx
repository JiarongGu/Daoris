import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Registration } from '../api';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from '../work/layout';
import { ListMore, ListPane } from '../work/ListPane';
import { CJK, ELSEWHERE, ENGINE, GAME, MIRRORED, NEWBIE, UNDECLARED } from './fixtures';
import { ProjectList, type RepositoryRowFacts, type WorkspaceGroup } from './ProjectList';

// Repositories' list (FRAME1e, D118 §2; UX6g, D150 §4.1) on its list pane, in every state the design names: a group per
// workspace, its head a door to the workspace's page, its adopted then its registered not adopted; a workspace chosen, a
// repository chosen, driven and held on this machine, one with no checkout here, one that declared nothing, one holding
// Daoris's branches; only adopters, a browser's (no ＋, no ⋯, no standing), empty, loading, an error with no answer ever,
// a 中文 name, the install's 29 repositories with its filter, the strip, and laid over the main area.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };

const DEFAULT: WorkspaceGroup = {
  workspace: 'default',
  adopted: [
    { registration: ENGINE, drivable: true, held: false, here: true, branches: 3 },
    { registration: GAME, drivable: true, held: true, here: true, branches: 0 },
    { registration: UNDECLARED, drivable: false, held: false, here: true, branches: 0 },
  ],
  outside: [
    { registration: NEWBIE, drivable: false, held: false, here: true, entries: 3, branches: 0 },
    { registration: ELSEWHERE, drivable: false, held: false, here: false, entries: null, branches: 0 },
  ],
};
const STUDIO: WorkspaceGroup = {
  workspace: 'studio', adopted: [{ registration: MIRRORED, drivable: false, held: false, here: false, branches: 0 }], outside: [],
};

/** The owner's install as §0 measured it: 29 repositories in two workspaces, most of `work`'s registered and not adopted. */
const INSTALL: WorkspaceGroup[] = (() => {
  const made = (name: string, adopted: boolean, workspace: string, summary?: string): Registration => ({
    repository: name, adopted, registered: true, workspace, summary, owns: [], accepts: [], packs: [], entries: adopted ? 12 : 0,
  });
  const row = (registration: Registration, over: Partial<RepositoryRowFacts> = {}): RepositoryRowFacts => ({
    registration, drivable: registration.adopted, held: false, here: true, entries: registration.entries, branches: 0, ...over,
  });
  return [
    {
      workspace: 'forge',
      adopted: [row(made('anvil', true, 'forge', 'The build farm and its caches.'), { branches: 2 })],
      outside: [row(made('bellows', false, 'forge'))],
    },
    {
      workspace: 'work',
      adopted: ['api', 'billing', 'portal', 'reports', 'search', 'workers', 'gateway'].map((name, at) =>
        row(made(name, true, 'work', `The ${name} service.`), { branches: at === 0 ? 3 : 0, held: at === 3 })),
      outside: Array.from({ length: 19 }, (_, at) => row(made(`tool-${String(at + 1).padStart(2, '0')}`, false, 'work'), { drivable: false })),
    },
  ];
})();

type Args = {
  groups: WorkspaceGroup[];
  chosen?: string | null;
  chosenWorkspace?: string | null;
  /** A shell's list: its ＋ adds a repository and its ⋯ imports a folder. A browser's makes nothing (D48 §7). */
  shell?: boolean;
  layout?: ListLayout;
  loading?: boolean;
  unanswered?: string;
};

/** The list as the view hands it to its pane: its ＋ of one kind, its ⋯ with the import, its body. */
function RepositoriesListPane({
  groups, chosen = null, chosenWorkspace = null, shell = true, layout = open(LIST_BOUNDS.projects.initial), loading = false, unanswered,
}: Args) {
  const empty = !loading && !unanswered && groups.length === 0;
  return (
    <ListPane
      name="Repositories"
      labels={{ open: 'Show the repository list', close: 'Hide the repository list', resize: 'repository list width' }}
      layout={layout}
      bounds={LIST_BOUNDS.projects}
      make={shell ? { label: 'Add repository', onMake: () => {} } : undefined}
      more={shell ? <ListMore label="More actions" items={[{ id: 'import', label: 'Import a folder…' }]} onChoose={() => {}} /> : undefined}
      loading={loading}
      empty={empty ? {
        headline: 'No repository is registered yet',
        body: 'Add one here, or register a whole folder of checkouts from a terminal with `daoris import <folder>`. Adding writes nothing into a repository.',
      } : undefined}
      onOpen={() => {}}
      onClose={() => {}}
      onDismiss={() => {}}
      onResize={() => {}}
    >
      <ProjectList
        groups={groups}
        chosen={chosen}
        chosenWorkspace={chosenWorkspace}
        unanswered={unanswered}
        onChoose={() => {}}
        onChooseWorkspace={() => {}}
      />
    </ListPane>
  );
}

const meta: Meta<typeof RepositoriesListPane> = {
  title: 'Repositories/ProjectList',
  component: RepositoriesListPane,
  args: { groups: [DEFAULT, STUDIO], chosen: ENGINE.repository },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[40rem] w-[60rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the chosen workspace's or repository's page.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof RepositoriesListPane>;

/**
 * A group per workspace, each headed by its row: in `default` the engine driven here, chosen, holding three of Daoris's
 * branches, the game held, one that declared nothing, then one not adopted with a root here and one without, each saying
 * what the index reads of it; in `studio` a teammate's with no checkout here.
 */
export const ByWorkspace: Story = {};

/** A workspace chosen: its head wears the list's choice, and its page is the main area's. */
export const WorkspaceChosen: Story = { args: { chosen: null, chosenWorkspace: 'default' } };

/** Nothing chosen: the list as a relaunch with nothing remembered opens it. */
export const NothingChosen: Story = { args: { chosen: null } };

/** One not adopted chosen: its row wears the list's choice too. */
export const NotAdoptedChosen: Story = { args: { chosen: NEWBIE.repository } };

/** Only adopters, in one workspace: *Registered, not adopted* is absent, never an empty heading. */
export const OnlyAdopted: Story = { args: { groups: [{ ...DEFAULT, outside: [] }] } };

/**
 * The install's 29 repositories in two workspaces (§0): past twelve the list offers a filter, and each group's *Registered,
 * not adopted* starts folded under its adopters.
 */
export const TheInstall: Story = { args: { groups: INSTALL, chosen: null, chosenWorkspace: 'work' } };

/** A browser's: no ＋ and no ⋯, since adding and importing touch machine paths, and no standing, which no driver says. */
export const InABrowser: Story = {
  args: {
    shell: false,
    groups: [DEFAULT, STUDIO].map((group) => ({
      ...group,
      adopted: group.adopted.map(({ registration }) => ({ registration })),
      outside: group.outside.map(({ registration, entries }) => ({ registration, entries })),
    })),
  },
};

/** Nothing registered: the empty state, with the ＋'s act. */
export const Empty: Story = { args: { groups: [] } };

/** A first load: skeleton rows, never the empty state. */
export const Loading: Story = { args: { loading: true } };

/** An error with no answer ever: the list says the sentence in place, and is never blank. */
export const ErrorNoAnswer: Story = {
  args: { groups: [], unanswered: 'Daoris could not reach this machine\'s host. Is the service running?' },
};

/** A name in 中文: content, never translated, and its summary cut to one line with the whole in its tip. */
export const ChineseName: Story = {
  args: {
    groups: [{ ...DEFAULT, adopted: [{ registration: CJK, drivable: true, held: false, here: true }, ...DEFAULT.adopted] }, STUDIO],
    chosen: CJK.repository,
  },
};

/** Closed by the person: its strip holds its controls alone, the open and the ＋ (D118 §2). */
export const Strip: Story = { args: { layout: CLOSED } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.projects.initial, beside: LIST_STRIP, auto: true } } };
