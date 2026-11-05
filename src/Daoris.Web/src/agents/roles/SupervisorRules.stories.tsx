import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import type { SupervisorRulesRead } from '../../data/wiring';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { InTheme } from '../../plugins/storyIcons';
import { SupervisorRulesFold } from './SupervisorRulesFold';

// CONTEXTDRIVE1g (the context counters design §7.1): a repository's supervisor rules at the wide, half and narrow widths, in English, 中文
// and dark: its own rules saying what they replace, the machine's rules offered, an empty place and a refused press. No bridge: every state is props.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const READ: SupervisorRulesRead = {
  counts: [
    { count: 'answers-alone', window: 'in-a-row', actions: ['hold', 'refer', 'off'], least: 1, most: 20 },
    { count: 'failures', window: 'since-try-again', actions: ['hold', 'refer', 'off'], least: 1, most: 20 },
    { count: 'tries', window: 'episode', actions: ['hold', 'ask', 'next', 'weighted', 'refer'], least: 1, most: 5 },
    { count: 'moves', window: '24-hours', actions: ['ask', 'refer', 'off'], least: 1, most: 8 },
  ],
  sets: [
    { place: 'machine', revision: 3, rules: [
      { id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 2, do: 'hold' }, { id: 'r-2', count: 'failures', window: 'since-try-again', at: 3, do: 'hold' },
      { id: 'r-3', count: 'tries', window: 'episode', at: 5, do: 'hold' },
    ] },
    { place: 'repository:daoris', revision: 1, rules: [
      { id: 'r-1', count: 'answers-alone', window: 'in-a-row', at: 3, do: 'hold' },
      { id: 'r-2', count: 'tries', window: 'episode', at: 2, do: 'weighted', roles: ['build'] },
      { id: 'r-3', count: 'moves', window: '24-hours', at: 3, do: 'ask', roles: ['build'] },
    ] },
  ],
};

const meta: Meta<typeof SupervisorRulesFold> = {
  title: 'Agents/Roles/Supervisor rules',
  component: SupervisorRulesFold,
  args: {
    place: 'repository:daoris', placeName: 'daoris', read: READ, roles: [{ value: 'build', label: 'Build' }, { value: 'review', label: 'Review' }],
    busy: false, refused: null, onSave: () => {},
  },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof SupervisorRulesFold>;

export const Rules: Story = {};
export const RulesHalf: Story = { decorators: [storyWidths.half] };
export const RulesNarrow: Story = { decorators: [storyWidths.narrow] };
export const RulesChinese: Story = { decorators: [chinese] };
export const RulesChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const RulesDark: Story = { decorators: [dark] };
export const RulesChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, dark] };

/** A place holding none: the machine's rules offered with *Set here instead…*. */
const EMPTY = { read: { ...READ, sets: [READ.sets[0]!] }, place: 'workspace:acme', placeName: 'acme' };
export const Empty: Story = { args: EMPTY };
export const EmptyNarrow: Story = { args: EMPTY, decorators: [storyWidths.narrow] };
export const EmptyChinese: Story = { args: EMPTY, decorators: [chinese] };
export const EmptyDark: Story = { args: EMPTY, decorators: [dark] };

/** The machine's set: its tries rule is never taken out. */
export const Machine: Story = { args: { place: 'machine', placeName: 'This Daoris' } };
export const MachineNarrow: Story = { args: { place: 'machine', placeName: 'This Daoris' }, decorators: [storyWidths.narrow] };

/** The driver's sentence under Save, the draft kept. */
const REFUSED = { refused: 'repository:daoris would leave build lines moving to another choice with no moves rule.' };
export const Refused: Story = { args: REFUSED };
export const RefusedNarrow: Story = { args: REFUSED, decorators: [storyWidths.narrow] };
export const RefusedDark: Story = { args: REFUSED, decorators: [dark] };
