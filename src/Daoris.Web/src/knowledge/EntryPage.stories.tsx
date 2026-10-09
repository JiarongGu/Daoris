import type { Meta, StoryObj } from '@storybook/react-vite';
import { EntryMainNotice, EntryPage } from './EntryPage';
import { CJK_ENTRY, ENTRY, INDEX_ENTRY, LONG_LINES, RULE } from './fixtures';
import { chinese } from '../storyLanguage';
import { InTheme } from '../plugins/storyIcons';

// An entry's page (FRAME1f, D118 §2) in Search's main area, where it was the reader drawer: a knowledge document with
// its frontmatter, a canonical rule, a source whose lines run long, a 中文 entry; and the main area with no entry:
// nothing chosen, gone, loading, an error with no answer ever.

const meta: Meta<typeof EntryPage> = {
  title: 'Knowledge/EntryPage',
  component: EntryPage,
  args: { entry: ENTRY },
  // The main area's height, and a width between the side bar's two states.
  decorators: [(Story) => (
    <div className="flex h-[44rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof EntryPage>;

/** A knowledge document, as it is written: its frontmatter and its Markdown's markers, unrendered. */
export const Knowledge: Story = {};

export const Index: Story = { args: { entry: INDEX_ENTRY } };
export const IndexChinese: Story = { args: { entry: INDEX_ENTRY }, decorators: [chinese] };
export const IndexDark: Story = {
  args: { entry: INDEX_ENTRY },
  decorators: [(Story) => <InTheme theme="dark" className="flex min-h-0 min-w-0 flex-1 p-0"><Story /></InTheme>],
};
export const IndexChineseDark: Story = {
  args: { entry: INDEX_ENTRY },
  decorators: [chinese, (Story) => <InTheme theme="dark" className="flex min-h-0 min-w-0 flex-1 p-0"><Story /></InTheme>],
};

/** A canonical rule, as every adopter holds it: *canonical* beside its kind. */
export const CanonicalRule: Story = { args: { entry: RULE } };

/** A source whose lines run past a hundred characters: it wraps at the main area's width, never a drawer's. */
export const LongLines: Story = { args: { entry: LONG_LINES } };

/** An entry in 中文: its title and its text are content, shown as they are. */
export const Chinese: Story = { args: { entry: CJK_ENTRY } };

/** Nothing chosen: how to choose. */
export const NothingChosen: Story = { render: () => <EntryMainNotice state="none" /> };

/** An entry the index no longer holds: moved, renamed, deleted, or its repository retired. */
export const Gone: Story = { render: () => <EntryMainNotice state="gone" /> };

/** The chosen entry on its way: skeleton rows, never the empty state. */
export const Loading: Story = { render: () => <EntryMainNotice state="loading" /> };

/** A read that failed: its sentence in place, never a blank page. */
export const ErrorNoAnswer: Story = {
  render: () => <EntryMainNotice state="unanswered" sentence={'Daoris could not reach this machine\'s host. Is the service running?'} />,
};
