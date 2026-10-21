import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../plugins/storyIcons';
import { chinese } from '../design/storyLanguage';
import { storyWidths } from '../design/storyWidths';
import {
  FULL, GATES_CHANGED, NO_VERIFICATION, NOTHING_SET, PLUGIN_UNREADY, ROUTED, START, WITH_A_FAILURE_PATH, WORK, WORKSPACE,
} from './fixtures';
import { WorkflowTab } from './WorkflowTab';

// The Workflow tab (WORKFLOW1b; D157 point 12, the workflow design §6.1, §6.2, §6.4), every state the design names drawn
// from props: nothing set anywhere, a repository with everything at once, a step whose plugin is not ready, a workspace with
// its repositories, its registry unread, the answer on its way and refused, a kind and a limit a newer driver draws, 中文,
// dark, and the main area at a 680 px window and at its 400 px floor.

const nothing = () => {};

// The main area at the frame's widths (the UI/UX design R6, UIFIT1): `storyWidths.half` is about 630 px, a 680 px
// window with the list a strip beside it, and `storyWidths.narrow` the 400 px floor.
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta: Meta<typeof WorkflowTab> = {
  title: 'Workflow/WorkflowTab',
  component: WorkflowTab,
  args: { page: 'repository', name: 'engine', current: FULL, doors: { setup: nothing, workspaceSetup: nothing } },
  // A repository's page's main area beside its list, at the frame's wide width.
  decorators: [storyWidths.wide],
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

/** At a 680 px window: the main area 600 px wide, the flowchart top to bottom, a step a rank, every line wrapping at its edge. */
export const At680: Story = { decorators: [storyWidths.half] };

/** At a 680 px window, in 中文 and dark. */
export const At680ChineseDark: Story = { decorators: [storyWidths.half, chinese, dark] };

/** At the main area's 400 px floor: still top to bottom, a step's 19rem fitting inside its gutters; nothing shrinks. */
export const AtTheFloor: Story = { decorators: [storyWidths.narrow] };

/** At the floor, in 中文 and dark. */
export const AtTheFloorChineseDark: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/**
 * **A failure path** (FLOWCHART1d): a newer driver's check after the pull request, which this build runs none of, so its kind has
 * no words here; its failure, sent back once, then you, a dashed edge to its outcome beside the end of the line. Wide, a rank of
 * two fits; at a 680 px window and the floor it is the rail, the outcome said under its step.
 */
export const AFailurePath: Story = { args: { current: WITH_A_FAILURE_PATH } };
export const AFailurePathAt680: Story = { args: { current: WITH_A_FAILURE_PATH }, decorators: [storyWidths.half] };
export const AFailurePathAtTheFloorChineseDark: Story = {
  args: { current: WITH_A_FAILURE_PATH }, decorators: [storyWidths.narrow, chinese, dark],
};

/**
 * **The start node first** (FABRIC-PIPELINE1a; the pipeline design §1.3): the default to Current, Documentation to its own workflow by
 * the workspace's choice, and the work that lands to `Verify the line`, its gates accepted at the line's tip; then the steps.
 */
export const Routed: Story = { args: { current: ROUTED, onAcceptGates: nothing } };

/** The declaration at the tip changed since it was accepted: shown whole, nothing runs, and *Accept the gates* is the person's. */
export const GatesChanged: Story = { args: { current: { ...ROUTED, start: GATES_CHANGED }, onAcceptGates: nothing } };

/** No landed route anywhere: the work that lands goes to *No verification here*, today's. */
export const NoVerification: Story = { args: { current: { ...ROUTED, start: NO_VERIFICATION } } };

/** A workspace's start: its own routes, and its landed route's gates accepted repository by repository. */
export const AWorkspaceRouted: Story = {
  args: {
    page: 'workspace', name: 'aurora', doors: { setup: nothing, repository: nothing },
    current: { ...WORKSPACE, start: { ...START, landed: { ...START.landed, level: 'workspace-landed', gates: null, state: 'unread', tip: null } } },
  },
};

/** The start node in 中文: 起点, 落地的工作, 采纳这些检查. */
export const RoutedChinese: Story = { ...GatesChanged, decorators: [chinese] };

/** The start node at the floor, in 中文 and dark: the flow becomes its rail, every route a row. */
export const RoutedAtTheFloorChineseDark: Story = { ...GatesChanged, decorators: [storyWidths.narrow, chinese, dark] };
