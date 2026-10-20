import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { Card, SettingList } from '../../design/ui';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import type { Answered } from '../../work/InlineConfirm';
import { type AccessEdit, AccessRows, type ResolvedPause } from './Access';

// A level's access rows (AUTONOMY1d, D184 points 1-3, 5; ACCESSASK1, the UI/UX design §3.3) at its three levels: a repository's
// Setup with each pause from where it was set, a workspace's, and this machine's on Driver; full access on in the warning colour;
// its ask open in the warning tone; and a change the driver refused, said under the row it was pressed in with the editor open
// (R3). At the frame's width, the 680 px window's main area and the main area's 400 px floor (R6), in both themes and in 中文.

/** The engine's setup: its permissions from its workspace, routing from this machine, landing Daoris's, its go-aheads its own. */
const MIXED: ResolvedPause[] = [
  { pause: 'permissions', value: 'asking', source: 'workspace' },
  { pause: 'routing', value: 'intake', source: 'machine' },
  { pause: 'landing', value: 'person', source: 'default' },
  { pause: 'goAheads', value: 'never', source: 'repository' },
  { pause: 'questions', value: 'claude-code', source: 'default' },
];

const OPEN = (source: string): ResolvedPause[] => [
  { pause: 'permissions', value: 'unrestricted', source },
  { pause: 'routing', value: 'intake', source },
  { pause: 'landing', value: 'rule', source },
  { pause: 'goAheads', value: 'never', source },
  { pause: 'questions', value: 'claude-code', source },
];

const MACHINE: ResolvedPause[] = [
  { pause: 'permissions', value: 'auto', source: 'default' },
  { pause: 'routing', value: 'person', source: 'default' },
  { pause: 'landing', value: 'person', source: 'default' },
  { pause: 'goAheads', value: 'named', source: 'machine' },
  { pause: 'questions', value: 'claude-code', source: 'default' },
];

/** The driver's sentence for a write it could not make, as a refused `SET_ACCESS` says it. */
const REFUSED = 'The driver could not write the access file: it is in use by another process.';

const meta = {
  title: 'Settings/AccessRows',
  component: AccessRows,
  args: {
    owner: 'repository',
    name: 'engine',
    workspace: 'aurora',
    pauses: MIXED,
    aboveFull: false,
    setHere: 'Set for this repository',
    onChange: (_edit: AccessEdit, _answered?: Answered) => {},
  },
  // In the card and on the label track its holders give it.
  decorators: [(Story) => <Card><SettingList><Story /></SettingList></Card>, storyWidths.wide],
} satisfies Meta<typeof AccessRows>;

export default meta;
type Story = StoryObj<typeof meta>;

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
/** The 680 px window's main area, the frame's half. */
const narrow = storyWidths.half;
/** The main area's floor, 400 px beside an open list and side bar. */
const atFloor = storyWidths.narrow;

/** A repository's Setup: each pause says where it came from, its own with *Clear*, one from above with *Set for this repository*. */
export const Repository: Story = {};

/** A workspace's Setup: the same rows one level up, its own routing among values from this machine and Daoris's default. */
export const Workspace: Story = {
  args: {
    owner: 'workspace',
    name: 'aurora',
    setHere: 'Set for this workspace',
    pauses: [
      { pause: 'permissions', value: 'asking', source: 'machine' },
      { pause: 'routing', value: 'intake', source: 'workspace' },
      { pause: 'landing', value: 'person', source: 'default' },
      { pause: 'goAheads', value: 'named', source: 'default' },
      { pause: 'questions', value: 'claude-code', source: 'default' },
    ],
  },
};

/** This machine's, on Settings → Driver: nothing stands above it. */
export const Machine: Story = {
  args: { owner: 'machine', name: undefined, setHere: 'Set for this machine', pauses: MACHINE },
};

/** Full access on for the repository, in the warning colour. */
export const FullAccess: Story = { args: { pauses: OPEN('repository') } };

/** Full access asked once, in the warning tone: the switch pressed opens its ask in the row. */
export const FullAccessAsked: Story = {
  play: async ({ canvasElement }) => {
    canvasElement.querySelector<HTMLElement>('[role="checkbox"], input[type="checkbox"]')?.click();
  },
};

/** The same in dark. */
export const FullAccessAskedDark: Story = { ...FullAccessAsked, decorators: [dark] };

/**
 * A pause's change the driver refused (ACCESSASK1, R3): said under its row in the driver's sentence, whole, with its editor still
 * open to press again, and no toast.
 */
export const PauseRefused: Story = {
  args: { onChange: (_edit, answered) => answered?.refused(REFUSED) },
  play: async ({ canvasElement }) => {
    const row = [...canvasElement.querySelectorAll<HTMLElement>('[data-setting-row]')]
      .find((each) => each.textContent?.includes('Permission prompts'));
    row?.querySelector<HTMLButtonElement>('button:not([role])')?.click();
    await new Promise((settled) => setTimeout(settled, 0));
    row?.querySelector<HTMLButtonElement>('[role="radio"][aria-checked="false"]')?.click();
  },
};

/** The same at this machine's level, on Driver. */
export const PauseRefusedMachine: Story = {
  ...PauseRefused,
  args: { ...Machine.args, onChange: PauseRefused.args!.onChange },
};

/** A refusal at the 680 px window's main area: the sentence breaks with its row. */
export const PauseRefusedNarrow: Story = { ...PauseRefused, decorators: [narrow] };

/** At the main area's 400 px floor, in dark: each value under its words, the refusal under the editor. */
export const PauseRefusedFloorDark: Story = { ...PauseRefused, decorators: [atFloor, dark] };

/** The workspace's rows in 中文 at the floor. */
export const WorkspaceChineseFloor: Story = { args: Workspace.args, decorators: [atFloor, chinese] };

/** A refusal in 中文: the chrome translated, the driver's sentence as it said it. */
export const PauseRefusedChinese: Story = { ...PauseRefused, decorators: [chinese] };
