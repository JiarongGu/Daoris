import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import { HistoryEdit, type HistoryPlanShown } from './HistoryEdit';

// Edit history on a branch of the person's own: closed, the edit and its target asked, the
// driver's plan shown before Confirm, done with where the original commit is kept, refused in the driver's sentence, and not known. At the
// main area's three widths (80, 39 and 25rem) in English and 中文, in both themes.

const nothing = async () => ({ refused: false, said: '' });
const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;
const BRANCH = 'spike/renderer';
const LONG = 'feature/a-branch-name-long-enough-to-meet-the-floor-of-the-main-area';
const PLAN: HistoryPlanShown = {
  tip: '5d2e77a0b1c2d3e4f5a6b7c8d9e0f1a2b3c4d5e6',
  onto: 'c4f1a2e0b7d94c3a8f56e1d20b9a7c4e3f8d1a65',
  commits: [
    { id: 'a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0', subject: 'Try a forward renderer' },
    { id: 'b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1', subject: 'Draw the shadows in a pass of their own, after the lights, so a long subject wraps' },
  ],
};

const meta: Meta<typeof HistoryEdit> = {
  title: 'Repositories/HistoryEdit',
  component: HistoryEdit,
  args: { branch: BRANCH, line: 'main', edits: { plan: nothing, edit: nothing } },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof HistoryEdit>;

/** Closed: one press, above the branch's commits. */
export const Closed: Story = {};

/** At 80rem: rebase onto its line, the first choice. */
export const Wide: Story = { args: { initial: {} } };
export const WideChinese: Story = { args: { initial: {} }, decorators: [chinese] };
export const WideDark: Story = { args: { initial: {} }, decorators: [dark] };
export const WideChineseDark: Story = { args: { initial: {} }, decorators: [chinese, dark] };

/** At 39rem, a 680 px window: a pick's range typed. */
export const Half: Story = { args: { initial: { edit: 'pick', target: 'c4f1a2e..9be03d1' } }, decorators: [storyWidths.half] };
export const HalfChinese: Story = { args: { initial: { edit: 'pick', target: 'c4f1a2e..9be03d1' } }, decorators: [storyWidths.half, chinese] };
export const HalfDark: Story = { args: { initial: { edit: 'pick', target: 'c4f1a2e..9be03d1' } }, decorators: [storyWidths.half, dark] };
export const HalfChineseDark: Story = { args: { initial: { edit: 'pick', target: 'c4f1a2e..9be03d1' } }, decorators: [storyWidths.half, chinese, dark] };

/** At 25rem, the main area's floor: the segments and the presses wrap. */
export const Narrow: Story = { args: { branch: LONG, initial: { edit: 'revert', target: 'a1b2c3d' } }, decorators: [storyWidths.narrow] };
export const NarrowChinese: Story = { args: { branch: LONG, initial: { edit: 'revert', target: 'a1b2c3d' } }, decorators: [storyWidths.narrow, chinese] };
export const NarrowDark: Story = { args: { branch: LONG, initial: { edit: 'revert', target: 'a1b2c3d' } }, decorators: [storyWidths.narrow, dark] };
export const NarrowChineseDark: Story = { args: { branch: LONG, initial: { edit: 'revert', target: 'a1b2c3d' } }, decorators: [storyWidths.narrow, chinese, dark] };

/** The driver's plan, before the press that sends the edit. */
export const PlanRebase: Story = { args: { initial: { stage: 'plan', plan: PLAN } } };
export const PlanRebaseChineseDark: Story = { args: { initial: { stage: 'plan', plan: PLAN } }, decorators: [chinese, dark] };
export const PlanPickNarrow: Story = { args: { branch: LONG, initial: { stage: 'plan', edit: 'pick', target: 'a1b2c3d..b2c3d4e', plan: PLAN } }, decorators: [storyWidths.narrow] };
export const PlanRevertNarrowChinese: Story = {
  args: { initial: { stage: 'plan', edit: 'revert', target: 'a1b2c3d', plan: { ...PLAN, commits: PLAN.commits.slice(0, 1) } } },
  decorators: [storyWidths.narrow, chinese],
};

/** Done: where the branch stands now, and where its old commit is kept. */
export const Done: Story = { args: { initial: { stage: 'done', tip: 'e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8', kept: `refs/daoris/discarded/${BRANCH}` } } };
export const DoneNarrowDark: Story = {
  args: { branch: LONG, initial: { stage: 'done', tip: 'e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8', kept: `refs/daoris/discarded/${LONG}` } },
  decorators: [storyWidths.narrow, dark],
};

/** Refused in the driver's own sentence, what is typed kept to change. */
export const Refused: Story = {
  args: {
    initial: {
      stage: 'said', target: 'main',
      sentence: 'picking a1b2c3d4e5f6 (Try a forward renderer) onto c4f1a2e0b7d9 conflicts in `src/render.cpp`; only objects were written, so nothing was changed',
    },
  },
};

/** Not known: said, and nothing retried. */
export const Unknown: Story = { args: { initial: { stage: 'unknown' } } };
export const UnknownChineseDark: Story = { args: { initial: { stage: 'unknown' } }, decorators: [chinese, dark] };
