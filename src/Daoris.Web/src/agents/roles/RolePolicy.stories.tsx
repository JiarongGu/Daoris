import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { type Editing, RoleEditor } from './RoleEditor';

// The role editor with a staffing policy, in each rule, stale, with no store and refused,
// at the wide, half and narrow widths, in English, 中文 and dark. The molecule reads no bridge, so each state is its props.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const OFFERS = { 'agent-a': { models: ['opus', 'sonnet', 'haiku'], efforts: ['low', 'medium', 'high'] } };

const BASE: Editing = {
  id: 'build', isNew: false, revision: 0, name: 'Build', nameIn: [['zh', '开发']], takes: ['build'], work: 'build', place: 'machine', placeId: '',
  drafts: [
    { key: 1, agent: 'agent-a', model: 'sonnet', effort: 'high', weight: '3' },
    { key: 2, agent: 'agent-a', model: 'opus', effort: 'high', weight: '1', budgetKind: 'starts', budget: '6', works: ['design'] },
  ],
  policy: {
    state: 'set', revision: 4, why: null, preset: 'weighted',
  },
};

const ORDER: Editing = {
  ...BASE,
  drafts: BASE.drafts.map((draft) => ({ ...draft, weight: '1' })),
  policy: { ...BASE.policy!, preset: 'first' },
};

const NEXT = { choice: '1', by: 'weight', said: 'by weight: 3 of the last 4 were choice 1, aim 3 in 4', agent: 'agent-a', model: 'sonnet', effort: 'high' };

const meta: Meta<typeof RoleEditor> = {
  title: 'Agents/Roles/Policy',
  component: RoleEditor,
  args: {
    editing: BASE, onEditing: () => {}, title: 'Build', agents: [{ value: 'agent-a', label: 'Agent A' }], offers: OFFERS,
    works: [{ value: 'build', label: 'building' }, { value: 'design', label: 'design and decisions' }],
    busy: false, held: false, placed: 'Placed on this machine; the place is fixed and the id is made for you.',
    consequence: 'Saving puts this role to work at once: new work takes its choices, and running work keeps its own.',
    conversing: false, mayOff: false, isOff: false, mayClear: false, refused: null, next: NEXT,
    rules: ['Moves to another choice a day: at 3: Ask before the next (This Daoris)', 'The service did not answer: at 2: Go to the next by weight (daoris)'],
    onSave: () => {}, onClear: () => {}, onCancel: () => {},
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof RoleEditor>;

/** Spread by weight, the weights with their aim, a limit on the second choice, the rules naming the role, and the next new start. */
export const Weighted: Story = {};
export const WeightedHalf: Story = { decorators: [storyWidths.half] };
export const WeightedNarrow: Story = { decorators: [storyWidths.narrow] };
export const WeightedChinese: Story = { decorators: [chinese] };
export const WeightedChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const WeightedDark: Story = { decorators: [dark] };
export const WeightedChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/** First that can run: no weight or aim, today's sentences in the ⓘ. */
export const First: Story = { args: { editing: ORDER, next: { ...NEXT, by: 'first', said: 'choice 1 in the stored order' } } };
export const FirstHalf: Story = { ...First, decorators: [storyWidths.half] };
export const FirstNarrow: Story = { ...First, decorators: [storyWidths.narrow] };
export const FirstChinese: Story = { ...First, decorators: [chinese] };
export const FirstDark: Story = { ...First, decorators: [dark] };

/** Its policy was set for other choices: its fields kept, the role running in order until saved again. */
const STALE: Editing = { ...BASE, policy: { ...BASE.policy!, state: 'stale', why: 'its choices changed' } };
export const Stale: Story = { args: { editing: STALE } };
export const StaleHalf: Story = { args: { editing: STALE }, decorators: [storyWidths.half] };
export const StaleNarrow: Story = { args: { editing: STALE }, decorators: [storyWidths.narrow] };
export const StaleChinese: Story = { args: { editing: STALE }, decorators: [chinese] };
export const StaleDark: Story = { args: { editing: STALE }, decorators: [dark] };

/** The policy changed since it was read: the driver's sentence under Save, the draft kept. */
const REFUSED = { refused: 'the policy changed since you read it' };
export const Refused: Story = { args: REFUSED };
export const RefusedHalf: Story = { args: REFUSED, decorators: [storyWidths.half] };
export const RefusedNarrow: Story = { args: REFUSED, decorators: [storyWidths.narrow] };
export const RefusedChinese: Story = { args: REFUSED, decorators: [chinese] };
export const RefusedDark: Story = { args: REFUSED, decorators: [dark] };
