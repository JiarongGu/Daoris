import type { Meta, StoryObj } from '@storybook/react-vite';
import { storyWidths } from '../../design/storyWidths';
import { StandingAnswer } from './StandingAnswer';

// A repository's standing answer on this machine (KNOWUSE1b, D135 §3): the person's words, handed to every session there,
// a row of the repository's Setup tab (UX6f), its terminal twin as its hint.

const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();

const meta = {
  title: 'Projects/StandingAnswer',
  component: StandingAnswer,
  args: { repository: 'engine', says: null, onSave: () => {} },
  // The main area at its wide width, as the frame gives it (the UI/UX design R6, UIFIT1).
  decorators: [storyWidths.wide],
} satisfies Meta<typeof StandingAnswer>;
export default meta;

type Story = StoryObj<typeof meta>;

/** None kept: what one is for, and *Add*. */
export const None: Story = {};

/** Kept, at the 400 px floor: the words under the row's label. */
export const KeptNarrow: Story = {
  args: { says: 'dev writes allowed; test locally against dev; prod only on a yes', at: hoursAgo(26) },
  decorators: [storyWidths.narrow],
};

/** Kept, in the person's words, with when they set them: *Edit* and *Clear*. */
export const Kept: Story = {
  args: { says: 'dev writes allowed; test locally against dev; prod only on a yes', at: hoursAgo(26) },
};

/** Several lines of words, quoted as written, and a time the file does not say. */
export const SeveralLines: Story = {
  args: {
    says: 'Dev writes are allowed: the local app points at dev.\nTest against dev before anything reaches production.\n'
      + 'A production write, a release or a push waits for my yes.',
  },
};

/** Words in Chinese, kept as written: content, never translated. */
export const InChinese: Story = { args: { says: '允许写入开发环境；在本地对开发环境测试；生产环境须经我同意。', at: hoursAgo(3) } };
