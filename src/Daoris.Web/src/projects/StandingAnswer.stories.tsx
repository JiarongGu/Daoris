import type { Meta, StoryObj } from '@storybook/react-vite';
import { StandingAnswer } from './StandingAnswer';

// A repository's standing answer on this machine (KNOWUSE1b, D135 §3): the person's words, handed to every session there,
// on the repository's page under the driver's other choices.

const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();

const meta = {
  title: 'Projects/StandingAnswer',
  component: StandingAnswer,
  args: { says: null, onSave: () => {} },
  decorators: [(Story) => <div className="w-[40rem] max-w-full bg-page p-4"><Story /></div>],
} satisfies Meta<typeof StandingAnswer>;
export default meta;

type Story = StoryObj<typeof meta>;

/** None kept: what one is for, and *Add*. */
export const None: Story = {};

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
