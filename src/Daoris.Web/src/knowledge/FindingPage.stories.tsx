import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Convergence } from '../api';
import { BODIES, CJK_FINDING, CONVERGENT, IDENTICAL, RESTATEMENT } from './fixtures';
import { FindingMainNotice, FindingPage } from './FindingPage';
import type { EntryReading } from './records';

// A finding's page (FRAME1f, D118 §2) in Convergence's main area, where each of its entries was a drawer of its own: a
// drifted copy, a lesson learned in different words, a document pasted in three places, an entry still on its way, an
// entry the index let go, an entry whose read failed, a 中文 entry; and the main area with no finding: nothing chosen,
// gone, loading, an error with no answer ever.

/** Every entry of a finding read, from the fixtures' bodies; one not among them is gone. */
const read = (finding: Convergence): Record<string, EntryReading> => Object.fromEntries(finding.entries.map((entry) => {
  const body = BODIES[entry.id];
  return [entry.id, body ? { state: 'read', entry: body } : { state: 'gone' }];
}));

const meta: Meta<typeof FindingPage> = {
  title: 'Knowledge/FindingPage',
  component: FindingPage,
  args: { finding: RESTATEMENT, readings: read(RESTATEMENT) },
  // The main area's height, and a width between the side bar's two states.
  decorators: [(Story) => (
    <div className="flex h-[44rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof FindingPage>;

/** A copy that has drifted: the service's sentence, then both rules read whole, the second with what it added. */
export const Restatement: Story = {};

/** The same lesson in different words, the finding no text comparison can make. */
export const Convergent: Story = { args: { finding: CONVERGENT, readings: read(CONVERGENT) } };

/** A document pasted in three places: three repositories in its head, and each copy read whole (none here is read yet). */
export const Identical: Story = {
  args: {
    finding: IDENTICAL,
    readings: Object.fromEntries(IDENTICAL.entries.map((entry) => [entry.id, { state: 'loading' } as EntryReading])),
  },
};

/** One entry still on its way: skeleton rows in its place, and the other read. */
export const AnEntryLoading: Story = {
  args: { finding: CONVERGENT, readings: { ...read(CONVERGENT), [CONVERGENT.entries[1]!.id]: { state: 'loading' } } },
};

/** One entry the index let go since the finding was made: said in its place, and the other read. */
export const AnEntryGone: Story = {
  args: { finding: CONVERGENT, readings: { ...read(CONVERGENT), [CONVERGENT.entries[0]!.id]: { state: 'gone' } } },
};

/** One entry whose read failed: its sentence in its place. */
export const AnEntryFailed: Story = {
  args: {
    finding: CONVERGENT,
    readings: { ...read(CONVERGENT), [CONVERGENT.entries[0]!.id]: { state: 'unanswered', sentence: 'the service is not answering' } },
  },
};

/** A finding with a 中文 entry: its title and text are content, shown as they are. */
export const Chinese: Story = { args: { finding: CJK_FINDING, readings: read(CJK_FINDING) } };

/** Nothing chosen: how to choose. */
export const NothingChosen: Story = { render: () => <FindingMainNotice state="none" /> };

/** A finding the list chose and the answer no longer holds, at the similarity it was asked at. */
export const Gone: Story = { render: () => <FindingMainNotice state="gone" at={0.82} /> };

/** The chosen finding on its way: skeleton rows, never the empty state. */
export const Loading: Story = { render: () => <FindingMainNotice state="loading" /> };

/** A comparison that never answered: its sentence in place, never a blank page. */
export const ErrorNoAnswer: Story = {
  render: () => <FindingMainNotice state="unanswered" sentence={'Daoris could not reach this machine\'s host. Is the service running?'} />,
};
