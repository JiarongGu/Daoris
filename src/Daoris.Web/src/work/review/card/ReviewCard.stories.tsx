import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../../design/storyLanguage';
import { InTheme } from '../../../plugins/storyIcons';
import type { NodeWordAnswer, NodeWordAsk } from '../../../workflow/run';
import { BRANCH_OUTPUTS, BRANCH_REVIEW } from '../run/runReviewFixtures';
import { ReviewCard } from './ReviewCard';

// The Accept card in the session's conversation, in English, 中文 and dark: waiting, accepted with what follows, superseded by a newer
// revision, held by the supervisor. A press is answered kept, or `moved` in its own story.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Work/Review/Accept card',
  component: ReviewCard,
  args: {
    answer: BRANCH_OUTPUTS,
    viewed: { isViewed: (path: string) => path === 'notes/summary.md', mark: () => {} },
    onWord: (ask: NodeWordAsk): Promise<NodeWordAnswer> => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: true }),
    onOpenOutputs: () => {},
  },
  decorators: [(Story) => <div className="w-[640px] p-3"><Story /></div>],
} satisfies Meta<typeof ReviewCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Waiting: Story = {};
export const WaitingChinese: Story = { decorators: [chinese] };
export const WaitingDark: Story = { decorators: [dark] };
export const Moved: Story = {
  args: { onWord: (ask: NodeWordAsk): Promise<NodeWordAnswer> => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: false, refused: 'moved' }) },
};
export const Accepted: Story = {
  args: { answer: { ...BRANCH_OUTPUTS, state: 'reached', presses: [], follows: [{ node: 'land', kind: 'land', state: 'active', attempt: 1, offers: [] }] } },
};
export const Superseded: Story = { args: { supersededBy: 3 } };
export const Held: Story = { args: { answer: BRANCH_REVIEW, onOverrule: () => {} } };
