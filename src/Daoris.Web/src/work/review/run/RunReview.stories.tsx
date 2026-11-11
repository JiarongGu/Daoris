import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../../design/storyLanguage';
import { storyWidths } from '../../../design/storyWidths';
import { InTheme } from '../../../plugins/storyIcons';
import type { NodeWordAsk } from '../../../workflow/run';
import { DiffLines } from '../PatchView';
import { lineDiff } from './lineDiff';
import { RunReview } from './RunReview';
import { ANSWER_REVIEW, BRANCH_REVIEW, COPY_REVIEW, FOLDER_REVIEW, NEWER_REVIEW, PLAN_REVIEW } from './runReviewFixtures';

// A run's review, one story per kind, in English, 中文 and dark: the decision bar and its presses, the changes by kind, a newer
// revision announced. The organism's body reads no bridge, so each state is its props; a press is answered kept.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const SUMMARY = lineDiff('# Budget\n\nTotal: 410\nRows: 12\n', '# Budget\n\nTotal: 420\nRows: 9\nChecked: yes\n');

const meta = {
  title: 'Work/Review/Run review',
  component: RunReview,
  args: {
    answer: FOLDER_REVIEW,
    viewed: { isViewed: (path: string) => path === 'notes/old-rows.csv', mark: () => {} },
    onWord: (ask: NodeWordAsk) => Promise.resolve({ run: ask.run, node: ask.node, act: ask.act, done: true }),
    onRead: () => {},
    fileBody: () => <div className="px-3 pb-2"><DiffLines lines={SUMMARY.lines} language="markdown" /></div>,
  },
  decorators: [(Story) => <div className="flex h-[640px] flex-col"><Story /></div>],
} satisfies Meta<typeof RunReview>;

export default meta;
type Story = StoryObj<typeof meta>;

/** A folder worked in place: its files with their versions, one changed outside its turns. */
export const FolderInPlace: Story = {};
export const FolderInPlaceChinese: Story = { decorators: [chinese] };
export const FolderInPlaceDark: Story = { decorators: [dark] };

/** A copy brought back after the review, one file changed in the folder too. */
export const Copy: Story = { args: { answer: COPY_REVIEW } };
export const CopyChinese: Story = { args: { answer: COPY_REVIEW }, decorators: [chinese] };
export const CopyDark: Story = { args: { answer: COPY_REVIEW }, decorators: [dark] };

/** A system's plan, its operations, applied after the Accept. */
export const Plan: Story = { args: { answer: PLAN_REVIEW } };
export const PlanChinese: Story = { args: { answer: PLAN_REVIEW }, decorators: [chinese] };
export const PlanDark: Story = { args: { answer: PLAN_REVIEW }, decorators: [dark] };

/** An answer that changed nothing, its handback rendered as Markdown. */
export const Answer: Story = { args: { answer: ANSWER_REVIEW } };
export const AnswerChinese: Story = { args: { answer: ANSWER_REVIEW }, decorators: [chinese] };
export const AnswerDark: Story = { args: { answer: ANSWER_REVIEW }, decorators: [dark] };

/** A chain of two parts on a branch, the supervisor's Accept held on the bar. */
export const BranchChain: Story = { args: { answer: BRANCH_REVIEW, onOverrule: () => {} } };
export const BranchChainChinese: Story = { args: { answer: BRANCH_REVIEW, onOverrule: () => {} }, decorators: [chinese] };
export const BranchChainDark: Story = { args: { answer: BRANCH_REVIEW, onOverrule: () => {} }, decorators: [dark] };

/** A newer revision ready over an older one read: announced, never swapped in. */
export const NewerReady: Story = { args: { answer: NEWER_REVIEW } };
export const NewerReadyChinese: Story = { args: { answer: NEWER_REVIEW }, decorators: [chinese] };
export const NewerReadyDarkNarrow: Story = { args: { answer: NEWER_REVIEW }, decorators: [storyWidths.narrow, dark] };
