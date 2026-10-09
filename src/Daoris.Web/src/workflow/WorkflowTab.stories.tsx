import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../storyLanguage';
import { FULL, NOTHING_SET, PLUGIN_UNREADY, WORK, WORKSPACE } from './fixtures';
import { WorkflowTab } from './WorkflowTab';

// The Workflow tab (WORKFLOW1b; D157 point 12, the workflow design §6.1, §6.2, §6.4), every state the design names drawn
// from props: nothing set anywhere, a repository with everything at once, a step whose plugin is not ready, a workspace with
// its repositories, its registry unread, the answer on its way and refused, a kind and a limit a newer driver draws, 中文,
// dark, and the main area at a 680 px window and at its 400 px floor.

const nothing = () => {};

/** The main area at a width: 600 px at a 680 px window, the list a strip beside it, and 400 px, its floor. */
const mainAt = (width: number): Decorator => (Story) => (
  <div className="border border-line bg-page p-6" style={{ width }}><Story /></div>
);
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof WorkflowTab> = {
  title: 'Workflow/WorkflowTab',
  component: WorkflowTab,
  args: { page: 'repository', name: 'engine', current: FULL, doors: { setup: nothing, workspaceSetup: nothing } },
  // A repository's page's main area beside its list, at a common width.
  decorators: [(Story) => <div className="max-w-full border border-line bg-page p-6" style={{ width: 880 }}><Story /></div>],
};
export default meta;

type Story = StoryObj<typeof WorkflowTab>;

/**
 * **Everything at once**: a plugin that may hold a start, the work with the standing answer, the workspace's second opinion
 * (in part, since XAGENT1f's gate), the repository's look in `dev` (in part), an automatic landing on a branch, and the pull request its plugin
 * opens, which the person merges. Each step's door opens the Setup that sets it.
 */
export const FullRepository: Story = {};

/** **Nothing set anywhere** (design §2.8): the work, then a merge the person accepts, by Daoris's default; nothing after it lands. */
export const NothingSet: Story = { args: { current: NOTHING_SET } };

/** **A step with a limit**: a branch rule whose plugin is not on this machine, said whole on the landing and on its pull request. */
export const AStepWithALimit: Story = { args: { current: PLUGIN_UNREADY } };

/**
 * **A workspace**: its Current, read from its rules for each repository there that sets none of its own, each step's door
 * opening its own Setup; then its repositories, one with rules of its own a door to its own workflow.
 */
export const AWorkspace: Story = {
  args: { page: 'workspace', name: 'aurora', current: WORKSPACE, doors: { setup: nothing, repository: nothing } },
};

/** A workspace drawn before the driver's service is up: its Current from the file, which repositories set their own not known. */
export const AWorkspaceRegistryUnread: Story = {
  args: { page: 'workspace', name: 'aurora', current: { ...WORKSPACE, repositories: null }, doors: { setup: nothing } },
};

/** A repository the registry does not hold: read as in no workspace, and said so in its head. */
export const NotRegistered: Story = {
  args: { name: 'stray', current: { ...NOTHING_SET, repository: 'stray', registered: false } },
};

/** The answer on its way: its words, and static rows in its place. */
export const Reading: Story = { args: { current: undefined, reading: true } };

/** The read refused: the driver's sentence, whole, in the tab's place. */
export const Refused: Story = {
  args: { current: undefined, refusal: 'the driver is still coming up — its service is not answering yet. A moment.' },
};

/** A newer driver's kind and limit: drawn in the driver's words, marked as shown as recorded, never dropped. */
export const ANewerDriversWords: Story = {
  args: {
    current: {
      ...NOTHING_SET,
      steps: [{ ...WORK(), limit: 'a-new-limit' }, { ...NOTHING_SET.steps[1]!, id: 'stage', kind: 'stage' }],
      limits: { 'a-new-limit': 'A sentence a newer driver says of a step this build does not know.' },
    },
  },
};

/** In 中文: 现行工作流, 落地之前 and 落地之后, each part and runtime its glossary word, the person's own words as written. */
export const Chinese: Story = { decorators: [chinese] };

/** A workspace in 中文. */
export const AWorkspaceChinese: Story = { ...AWorkspace, decorators: [chinese] };

/** In dark: the rail and its marks in ink, the limits on their warning rail, the chips outlined. */
export const Dark: Story = { decorators: [dark] };

/** At a 680 px window: the main area 600 px wide, every line wrapping at its edge. */
export const At680: Story = { decorators: [mainAt(600)] };

/** At the main area's 400 px floor: a step's chip and who acts go under its title, nothing shrinks. */
export const AtTheFloor: Story = { decorators: [mainAt(400)] };

/** At the floor, in 中文 and dark. */
export const AtTheFloorChineseDark: Story = { decorators: [mainAt(400), chinese, dark] };
