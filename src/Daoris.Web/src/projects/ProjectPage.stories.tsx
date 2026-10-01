import type { Meta, StoryObj } from '@storybook/react-vite';
import { CJK, COUNTS, ELSEWHERE, ENGINE, GAME, LINE, MIRRORED, NEWBIE, UNDECLARED } from './fixtures';
import { type Driving, ProjectPage, ProjectsMainNotice } from './ProjectPage';

// A repository's page (FRAME1e, D118 §2) in Repositories' main area, where it was a card in a grid: adopted, driven
// here, held, with its line, with unlanded branches, a teammate's with no checkout here, one that declared nothing,
// one not adopted with a root here (and on a direct door), one not adopted with none, a browser's, a 中文 name; and the
// main area with no page: nothing chosen, gone, loading, an error with no answer ever.

const nothing = () => {};
const driving = (choices: Partial<Driving> = {}): Driving => ({
  drivable: false, held: false, ownTree: false, onDrive: nothing, onHold: nothing, onTrees: nothing, ...choices,
});

const meta: Meta<typeof ProjectPage> = {
  title: 'Repositories/ProjectPage',
  component: ProjectPage,
  args: {
    registration: ENGINE,
    counts: COUNTS.engine,
    here: true,
    driving: driving(),
    onManage: nothing,
    onOpenCode: nothing,
  },
  // The main area's height, and a width between the side bar's two states.
  decorators: [(Story) => (
    <div className="flex h-[44rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof ProjectPage>;

/**
 * Adopted: its summary as its one line, *Open code map* and *Manage* in its header; what the index holds and the
 * commit it was fed from, its workspace, its declaration as chips, and this machine's driving row.
 */
export const Adopted: Story = {};

/** Driven on this machine: the head says *drives here*, as the session list's group does. */
export const Driven: Story = { args: { driving: driving({ drivable: true, ownTree: true }) } };

/** Held by the person: *held* outranks *drives here*, which it suspends; the hold is lifted from the row below. */
export const Held: Story = { args: { driving: driving({ drivable: true, held: true }) } };

/** Its line, as the driver resolves it (WSR2): the branch its work grows from and lands on, and what set it. */
export const WithALine: Story = { args: { line: LINE } };

/** Session branches holding work no branch of the person's holds (WSR3): named here, listed in Settings. */
export const UnlandedBranches: Story = { args: { line: LINE, unlanded: 2 } };

/** A teammate's registration with no checkout here: *not on this machine*, and nothing printed of anyone's path. */
export const NoRootHere: Story = { args: { registration: MIRRORED, counts: COUNTS['studio-tools'], here: false } };

/** Adopted, and declared nothing: the warning that an asker would be guessing (D34). */
export const Undeclared: Story = { args: { registration: UNDECLARED, counts: undefined } };

/** The game: drivable, with no feed, since this deployment reads its own checkouts. */
export const AnotherAdopter: Story = { args: { registration: GAME, counts: COUNTS.game, driving: driving({ drivable: true }) } };

/**
 * Not adopted, with a root here (INT3c): *not adopted* in its head, no *Manage*, the driving row, and the steps to
 * adopt as text with the reasoning on the info glyph.
 */
export const NotAdopted: Story = { args: { registration: NEWBIE, counts: COUNTS.newbie, onManage: undefined } };

/** Not adopted, on a machine whose door is direct: the row says a quest there sits. */
export const NotAdoptedOnADirectDoor: Story = {
  args: {
    registration: NEWBIE, counts: COUNTS.newbie, onManage: undefined,
    driving: driving({ note: 'This machine drives on a direct agent, so a quest here sits, saying why, until it drives on a protocol one.' }),
  },
};

/** Not adopted, with no root here: nowhere to start it, so no driving row. */
export const NotAdoptedNoRoot: Story = {
  args: { registration: ELSEWHERE, counts: undefined, here: false, driving: null, onManage: undefined },
};

/** A browser's page: no *Manage*, no driving row and no standing, which only a shell's driver says (D47 §4). */
export const InABrowser: Story = { args: { here: undefined, driving: null, onManage: undefined } };

/** A name in 中文: the title and the summary are content, shown as they are. */
export const ChineseName: Story = { args: { registration: CJK, counts: COUNTS['渲染管线'], driving: driving({ drivable: true }) } };

/** Nothing chosen: how to choose, and the list's ＋ on a shell. */
export const NothingChosen: Story = {
  render: () => <ProjectsMainNotice state="none" action={{ label: 'Add repository', onAct: nothing }} />,
};

/** A repository the list chose and the registry no longer holds: retired, or in a workspace not shown. */
export const Gone: Story = { render: () => <ProjectsMainNotice state="gone" /> };

/** The chosen repository on its way: skeleton rows, never the empty state. */
export const Loading: Story = { render: () => <ProjectsMainNotice state="loading" /> };

/** A registry that never answered: its sentence in place, never a blank page. */
export const ErrorNoAnswer: Story = {
  render: () => <ProjectsMainNotice state="unanswered" sentence={'Daoris could not reach this machine\'s host. Is the service running?'} />,
};
