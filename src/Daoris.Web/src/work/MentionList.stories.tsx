import type { Meta, StoryObj } from '@storybook/react-vite';
import { MentionList } from './MentionList';

// What `@` offers (CONV4d), in each state it can be in. The list floats above the box it belongs to,
// so the decorator stands a box beneath it and leaves room above.

const meta: Meta<typeof MentionList> = {
  title: 'Work/MentionList',
  component: MentionList,
  args: {
    id: 'mention-story', active: 0, query: 'de', listing: false, refusal: null, empty: false, unlisted: 0,
    options: ['docs/design.md', 'docs/deep file.md', 'src/engine/render-decisions.ts', 'README.md'],
    onPick: () => {}, onActive: () => {},
  },
  decorators: [(Story) => (
    <div className="relative mt-80 max-w-2xl">
      <Story />
      <div className="h-14 rounded-control border border-line-strong bg-raised" />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof MentionList>;

/** Offering: the name first, where it lives beside it, and the one Enter takes lit. */
export const Offering: Story = {};

/** The arrows moved: the second row is the one Enter or Tab writes. */
export const SecondChosen: Story = { args: { active: 1 } };

/** Asked for and not answered yet — said, never an empty panel. */
export const Listing: Story = { args: { options: [], listing: true } };

/** Nothing matches what was typed. Enter sends it as typed. */
export const NoMatch: Story = { args: { options: [], query: 'zebra' } };

/** A tree with nothing in it to name. */
export const EmptyTree: Story = { args: { options: [], query: '', empty: true } };

/** The host's own sentence, verbatim: the typed path still reaches the agent. */
export const Unlisted: Story = {
  args: {
    options: [],
    refusal: 'git cannot list this session\'s files here — its tree is not on this machine, is gone, or is not a '
      + 'repository of its own. A path you type after @ still reaches the agent.',
  },
};

/** The host's bound left files out, and says so beneath what it did offer. */
export const Bounded: Story = { args: { unlisted: 1_204 } };

/** A deep path and a long 中文 name: the folder gives way far faster than the name, and neither pushes the row sideways. */
export const LongNames: Story = {
  args: {
    query: '设计',
    options: [
      'docs/architecture/2026/the-working-surface/components/设计说明-会话与工作树-第二版.md',
      'src/Daoris.Desktop/Daoris.Desktop.Modules/very/deeply/nested/folder/structure/设计.cs',
    ],
  },
};
