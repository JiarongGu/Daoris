import type { Decorator, Meta, StoryObj } from '@storybook/react-vite';
import { InTheme } from '../../plugins/storyIcons';
import { chinese } from '../../design/storyLanguage';
import { storyWidths } from '../../design/storyWidths';
import type { MilestoneEntry, RecoveryDropResult } from '../../work/head/milestone';
import { WorkspaceRecoveries } from './WorkspaceRecoveries';

// What Daoris keeps for recovery that no unit names (RECOVERYVIEW1, RECOVERYDROP1, CLEANUP1), at the foot of a workspace's
// Branches tab (`WorkspaceBranches.tsx`), on its own (STORYGAP1, the UI/UX design §1.2 item 6): a folder set aside until its date,
// a ref a branch of the person's holds, a ref nothing holds with its copyable command, the delete of those asked once, and what a
// press answered; at the main area's three widths, in 中文 and dark.

const dark: Decorator = (Story) => <InTheme theme="dark"><Story /></InTheme>;

const recovery = (over: Partial<MilestoneEntry>): MilestoneEntry => ({
  unit: '', kind: 'recovery-ref', resource: 'refs/daoris/discarded/daoris/s-gone', role: 'recovery', disposition: 'retained',
  detail: 'no branch or tag of yours holds its commit, so Daoris keeps it until you delete it: `git update-ref -d refs/daoris/discarded/daoris/s-gone` in `billing`',
  until: null, repository: 'billing', commit: '4f2a9c10b7e5d3a1c8f6e2b0d9a7c5e3f1b2a4c6', ...over,
});

const FOLDER = recovery({
  kind: 'moved-aside', resource: 'trees/.tidied/work/billing/s-1-20261017T090000000Z', commit: null,
  detail: 'moved aside 2026-10-17 09:00 UTC; deleted once it is left untouched until 2026-10-24 09:00 UTC', until: '2026-10-24T09:00:00Z',
});
const HELD = recovery({
  resource: 'refs/daoris/discarded/daoris/s-held', until: '2026-11-16T09:00:00Z',
  detail: "a branch or a tag of yours holds its commit, so the look's tidy removes it once it is 30 days old, from 2026-11-16 09:00 UTC",
});
const UNHELD = recovery({});
const UNHELD_LEDGER = recovery({
  resource: 'refs/daoris/discarded/daoris/s-ledger-reconcile-the-quarter-end', repository: 'ledger',
  detail: 'no branch or tag of yours holds its commit, so Daoris keeps it until you delete it: `git update-ref -d refs/daoris/discarded/daoris/s-ledger-reconcile-the-quarter-end` in `ledger`',
  commit: '9b8a7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e2f1a0b',
});

const EVERY = [FOLDER, HELD, UNHELD, UNHELD_LEDGER];
const REPOSITORIES = ['billing', 'ledger'];

/** The driver deleted one and kept the other, which a branch began to hold meanwhile. */
const PARTLY: RecoveryDropResult = {
  dropped: [{ repository: 'billing', ref: UNHELD.resource, commit: UNHELD.commit! }],
  skipped: [{ repository: 'ledger', ref: UNHELD_LEDGER.resource, why: 'a branch of yours holds its commit now' }],
  refused: [],
};

const meta = {
  title: 'Repositories/WorkspaceRecoveries',
  component: WorkspaceRecoveries,
  args: { recoveries: EVERY, repositories: REPOSITORIES, onDrop: () => Promise.resolve(PARTLY) },
  decorators: [storyWidths.wide],
} satisfies Meta<typeof WorkspaceRecoveries>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Each kind kept: a folder until its date, a held ref, two refs nothing holds with their commands and *Delete…*. */
export const Kept: Story = {};

/** A shell older than the delete: the copyable commands alone. */
export const CommandsOnly: Story = { args: { onDrop: undefined } };

/** Only what something keeps until a date: nothing to delete, so no press. */
export const NothingToDelete: Story = { args: { recoveries: [FOLDER, HELD] } };

/** A press the driver refused whole: its sentence is said inside the ask, which stays open (press *Delete…*, then confirm). */
export const Refused: Story = {
  args: { onDrop: () => Promise.resolve({ dropped: [], skipped: [], refused: ['This machine does not hold the lease on billing.'] }) },
};

/** Half: a 680 px window with the list a strip beside it. */
export const Half: Story = { decorators: [storyWidths.half] };

/** The main area's 400 px floor: a long ref and its command wrap anywhere, *Copy* beside them. */
export const Narrow: Story = { decorators: [storyWidths.narrow] };

/** In 中文, wide. */
export const Chinese: Story = { decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { decorators: [chinese, storyWidths.narrow] };

/** In dark. */
export const Dark: Story = { decorators: [dark] };

/** In 中文 and dark, half. */
export const ChineseDarkHalf: Story = { decorators: [chinese, dark, storyWidths.half] };
