import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { InTheme } from '../../plugins/storyIcons';
import { BRANCH_NEWER, BRANCH_OUTPUTS, FOLDER_REVIEW, PLAN_REVIEW } from '../review/run/runReviewFixtures';
import { OutputsView } from './OutputsView';

// A run's outputs, in English, 中文 and dark: the change set first (commits and every file's hunks), the handbacks as documents, a newer
// revision announced by the outputs that moved, an output of an unknown display drawn raw. The organism reads no bridge.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const meta = {
  title: 'Work/Outputs/Outputs view',
  component: OutputsView,
  args: { answer: BRANCH_OUTPUTS, viewed: { isViewed: () => false, mark: () => {} }, onRead: () => {} },
  decorators: [(Story) => <div className="flex h-[720px] w-[520px] flex-col"><Story /></div>],
} satisfies Meta<typeof OutputsView>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Branch: Story = {};
export const BranchChinese: Story = { decorators: [chinese] };
export const BranchDark: Story = { decorators: [dark] };
export const NewerRevision: Story = { args: { answer: BRANCH_NEWER } };
export const Folder: Story = { args: { answer: { ...FOLDER_REVIEW, outputs: [{ id: 'changes', type: 'changes', display: 'change-set', digest: 'f' }] } } };
export const Raw: Story = { args: { answer: { ...PLAN_REVIEW, outputs: [{ id: 'chart', type: 'chart', display: 'chart', title: 'Load chart', digest: 'x' }] } } };
