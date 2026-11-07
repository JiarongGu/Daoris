import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { AGENTS } from '../../agents/setup/setupFixtures';
import type { RoleRow } from '../../data/wiring';
import { MERGE_GRAPH } from '../graphFixtures';
import type { EditorRole, StepStaffing } from './answers';
import { HOLDS, judgedFor, LIST, PALETTE } from './editFixtures';
import { WorkflowEditor } from './WorkflowEditor';

// A step's *Taken by*: the roles that take the step's work and cover this place, each named with its
// place; *Give engine its own role for …* opening the one role editor beside the step; a place that has a role of its own offered *Edit* in
// its place; and a role save the driver refused, with the editor open and the draft unchanged. In English and 中文, both themes, at the main
// area's wide, half and narrow widths (80, 39 and 25rem). A `play` presses what a person would.

const nothing = () => {};

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

/** The agent this machine has first, as the roles' fixtures name it. */
const AGENT = AGENTS[0]!.value;

const row = (id: string, name: string, takes: string[], scopeKind: string, scopeId: string | null, seeded = false): RoleRow => ({
  id, name: { en: name, zh: name }, seeded, state: seeded ? 'seed' : 'set', takes, scopeKind, scopeId, revision: 0, choices: [{ agent: AGENT, model: 'opus', effort: 'high' }],
});

const ROWS: RoleRow[] = [
  row('designer', 'Designer', ['design'], 'machine', null, true),
  row('build', 'Builder', ['build'], 'machine', null, true),
  row('aurora-build', 'Aurora builder', ['build'], 'workspace', 'aurora'),
];

const editorRole = (each: RoleRow): EditorRole => ({
  id: each.id, name: each.name ?? null, takes: each.takes, scope: { kind: each.scopeKind ?? 'machine', id: each.scopeId ?? null }, revision: each.revision, covers: true,
});

const staffing = (rows: RoleRow[], refuse: string | null = null): StepStaffing => ({
  catalogue: { roles: rows, works: ['build', 'design', 'review'], kept: true },
  read: 'ok',
  actors: [],
  agents: AGENTS,
  offers: { [AGENT]: { models: ['opus', 'sonnet'], efforts: ['high', 'medium'] } },
  busy: false,
  onSet: (_press, answered) => (refuse === null ? answered.done() : answered.refused(refuse)),
});

const meta: Meta<typeof WorkflowEditor> = {
  title: 'Workflow/WorkflowEditor/Staffing',
  component: WorkflowEditor,
  args: {
    list: LIST, palette: PALETTE, error: null, roles: ROWS.map(editorRole), staffing: staffing(ROWS), shown: null, onShow: nothing, judged: HOLDS, judging: false,
    onJudge: nothing, saving: false, onSave: async () => HOLDS, using: false,
    onUse: async () => ({ done: true, refused: null, message: null, changed: true, reach: [] }),
    place: { repository: 'engine' }, onClose: nothing, startFrom: { graph: MERGE_GRAPH, name: 'Merge workflow' },
  },
  render: (args) => <WorkflowEditor {...args} judgedOf={args.judgedOf ?? (args.startFrom ? judgedFor(args.startFrom.graph) : null)} />,
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof WorkflowEditor>;

const settle = () => new Promise((resolve) => setTimeout(resolve, 120));
const press = (root: ParentNode, name: RegExp, selector = 'button') => {
  const found = [...root.querySelectorAll<HTMLElement>(selector)].find((each) => name.test(each.getAttribute('aria-label') ?? each.textContent ?? ''));
  found?.click();
  return found;
};

/** The work's step selected: *Taken by* lists the roles that cover the place, and the door to give it one of its own. */
const selected: Story['play'] = async ({ canvasElement }) => {
  press(canvasElement, /^(The work|工作本身)/, '[data-flow-node]');
  await settle();
};
export const TakenBy: Story = { play: selected };
export const TakenByHalf: Story = { play: selected, decorators: [storyWidths.half] };
export const TakenByNarrow: Story = { play: selected, decorators: [storyWidths.narrow] };
export const TakenByChinese: Story = { play: selected, decorators: [storyWidths.wide, chinese] };
export const TakenByChineseNarrow: Story = { play: selected, decorators: [storyWidths.narrow, chinese] };
export const TakenByDark: Story = { play: selected, decorators: [storyWidths.wide, dark] };

/** *Give engine its own role…* pressed: the role editor beside the step, its consequence said before *Save*. */
const giving: Story['play'] = async (context) => {
  await selected?.(context);
  press(document.body, /^(Give engine its own role|让 engine 有自己的角色)/);
  await settle();
};
export const Giving: Story = { play: giving };
export const GivingHalf: Story = { play: giving, decorators: [storyWidths.half] };
export const GivingNarrow: Story = { play: giving, decorators: [storyWidths.narrow] };
export const GivingChinese: Story = { play: giving, decorators: [storyWidths.wide, chinese] };
export const GivingChineseNarrow: Story = { play: giving, decorators: [storyWidths.narrow, chinese] };
export const GivingDark: Story = { play: giving, decorators: [storyWidths.wide, dark] };
export const GivingChineseDarkHalf: Story = { play: giving, decorators: [storyWidths.half, chinese, dark] };

/** **A role the place already has for the work**: *Edit* in place of giving it another. */
const OWN = [...ROWS, row('designer-engine', 'Designer', ['design'], 'repository', 'engine')];
export const HasItsOwn: Story = { play: selected, args: { roles: OWN.map(editorRole), staffing: staffing(OWN) } };
export const HasItsOwnNarrow: Story = { ...HasItsOwn, decorators: [storyWidths.narrow] };
export const HasItsOwnChinese: Story = { ...HasItsOwn, decorators: [storyWidths.wide, chinese] };

/** **A role save the driver refused**: the editor stays open under the driver's sentence, and the draft is unchanged. */
const refusing: Story['play'] = async (context) => {
  await giving?.(context);
  press(document.body, /^Save$|^保存$/);
  await settle();
};
export const Refused: Story = { play: refusing, args: { staffing: staffing(ROWS, 'The role door refused it: another role of engine already takes this work.') } };
export const RefusedNarrow: Story = { ...Refused, decorators: [storyWidths.narrow] };
export const RefusedChineseDark: Story = { ...Refused, decorators: [storyWidths.half, chinese, dark] };
