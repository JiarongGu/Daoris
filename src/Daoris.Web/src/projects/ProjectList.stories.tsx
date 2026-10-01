import type { Meta, StoryObj } from '@storybook/react-vite';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from '../work/layout';
import { ListMore, ListPane } from '../work/ListPane';
import { CJK, ELSEWHERE, ENGINE, GAME, MIRRORED, NEWBIE, UNDECLARED } from './fixtures';
import { ProjectList, type RepositoryRowFacts } from './ProjectList';

// Repositories' list (FRAME1e, D118 §2) on its list pane, in every state the design names: the adopted then the
// registered not adopted, a repository chosen, driven and held on this machine, one with no checkout here, one that
// declared nothing, only adopters, a browser's (no ＋, no ⋯, no standing), empty, loading, an error with no answer
// ever, a 中文 name, the strip, and laid over the main area.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };

const ADOPTED: RepositoryRowFacts[] = [
  { registration: ENGINE, drivable: true, held: false, here: true },
  { registration: GAME, drivable: true, held: true, here: true },
  { registration: MIRRORED, drivable: false, held: false, here: false },
  { registration: UNDECLARED, drivable: false, held: false, here: true },
];
const OUTSIDE: RepositoryRowFacts[] = [
  { registration: NEWBIE, drivable: false, held: false, here: true, entries: 3 },
  { registration: ELSEWHERE, drivable: false, held: false, here: false, entries: null },
];

type Args = {
  adopted: RepositoryRowFacts[];
  outside: RepositoryRowFacts[];
  chosen?: string | null;
  /** A shell's list: its ＋ adds a repository and its ⋯ imports a folder. A browser's makes nothing (D48 §7). */
  shell?: boolean;
  layout?: ListLayout;
  loading?: boolean;
  unanswered?: string;
};

/** The list as the view hands it to its pane: its ＋ of one kind, its ⋯ with the import, its body. */
function RepositoriesListPane({ adopted, outside, chosen = null, shell = true, layout = open(LIST_BOUNDS.projects.initial), loading = false, unanswered }: Args) {
  const empty = !loading && !unanswered && adopted.length === 0 && outside.length === 0;
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
      <ProjectList adopted={adopted} outside={outside} chosen={chosen} unanswered={unanswered} onChoose={() => {}} />
    </ListPane>
  );
}

const meta: Meta<typeof RepositoriesListPane> = {
  title: 'Repositories/ProjectList',
  component: RepositoriesListPane,
  args: { adopted: ADOPTED, outside: OUTSIDE, chosen: ENGINE.repository },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[40rem] w-[60rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the chosen repository's page.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof RepositoriesListPane>;

/**
 * The adopted, then *Registered, not adopted*, each group with its count: the engine driven here and chosen, the game
 * held, a teammate's with no checkout here, one that declared nothing; then one not adopted with a root here and one
 * without, each saying what the index reads of it.
 */
export const AdoptedThenRegistered: Story = {};

/** Nothing chosen: the list as a relaunch with no repository remembered opens it. */
export const NothingChosen: Story = { args: { chosen: null } };

/** One not adopted chosen: its row wears the list's choice too. */
export const NotAdoptedChosen: Story = { args: { chosen: NEWBIE.repository } };

/** Only adopters: the second group is absent, never an empty heading. */
export const OnlyAdopted: Story = { args: { outside: [] } };

/** A browser's: no ＋ and no ⋯, since adding and importing touch machine paths, and no standing, which no driver says. */
export const InABrowser: Story = {
  args: {
    shell: false,
    adopted: ADOPTED.map(({ registration }) => ({ registration })),
    outside: OUTSIDE.map(({ registration, entries }) => ({ registration, entries })),
  },
};

/** Nothing registered: the empty state, with the ＋'s act. */
export const Empty: Story = { args: { adopted: [], outside: [] } };

/** A first load: skeleton rows, never the empty state. */
export const Loading: Story = { args: { loading: true } };

/** An error with no answer ever: the list says the sentence in place, and is never blank. */
export const ErrorNoAnswer: Story = {
  args: { adopted: [], outside: [], unanswered: 'Daoris could not reach this machine\'s host. Is the service running?' },
};

/** A name in 中文: content, never translated, and its summary cut to one line with the whole in its tip. */
export const ChineseName: Story = {
  args: { adopted: [{ registration: CJK, drivable: true, held: false, here: true }, ...ADOPTED], chosen: CJK.repository },
};

/** Closed by the person: its strip holds its controls alone, the open and the ＋ (D118 §2). */
export const Strip: Story = { args: { layout: CLOSED } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.projects.initial, beside: LIST_STRIP, auto: true } } };
