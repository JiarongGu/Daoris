import type { Meta, StoryObj } from '@storybook/react-vite';
import { atWidths, inDark, inLight } from '../../design/flowFrames';
import { chinese } from '../../design/storyLanguage';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import type { EntryPart, SessionEntry } from '../entries';
import type { EntryAcceptPlan } from '../review/EntryAccept';
import { PartsFlow } from './PartsFlow';

// A session's parts drawn as a flowchart (FLOWCHART1b; the flowchart design §2.3, §4.4): each part hung by what it followed and
// labelled by how it began, an asked part from a faint node, the part the page is on lit, and where Accept would land them as a
// dashed node. At the frame's widths, 300 px the side bar's floor (the parts strip, which is the flow's rail), 400 and 544
// top to bottom, 1200 left to right. Parts are lineage, never a cause or a concurrency. The first decorator is the innermost.

const meta: Meta<typeof PartsFlow> = {
  title: 'Work/PartsFlow',
  component: PartsFlow,
  args: { onShow: () => {} },
};
export default meta;

type Story = StoryObj<typeof PartsFlow>;

/** At every width, in light: a story's own decorators, so a story in another look draws no frame inside another. */
const light = [atWidths(), inLight];

const part = (session: string, quest: string | null, shown: string, group: EntryPart['group'], began: string, over: Partial<EntryPart> = {}): EntryPart => ({
  session, quest, shown, group, began, after: null, askedBy: null, ...over,
});

/** The install's line of work: a failed start, its retry, and a request the database repository sent back. */
const RETRY = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

/**
 * A fork and retries: the first part failed, and two parts follow it (a retry, and a conversation handed over from it), the retry
 * retried again; a request from another repository began a part of its own, and a part carried on from a chain's parent has no
 * `after`. Listed oldest first, which is the ledger's order and says nothing of when any of them ran.
 */
const FORK: SessionEntry = {
  entry: 'quest:q0000a/app', origin: 'quest:q0000a', repository: 'app', group: 'working', leading: 'p5',
  parts: [
    part('p1', 'q0000a', 'failed', 'ended', 'opened'),
    part('p2', 'q0000a', 'failed', 'ended', 'retried', { after: 'p1' }),
    part('p3', 'q0000a', 'completed', 'ended', 'retried', { after: 'p2' }),
    part('p4', null, 'working', 'working', 'handed-over', { after: 'p1' }),
    part('p5', 'q0000c', 'working', 'working', 'asked', { askedBy: 'db' }),
    part('p6', 'q0000e', 'stopped', 'ended', 'carried-on'),
  ],
};
const forkRows = Object.fromEntries(['p1', 'p2', 'p3'].map((id, index) => [id, {
  session: { ...EVIDENCE_SESSIONS[2 + index]!, id }, root: 'C:/work/app',
}]));

/** Where Accept would land the install's three parts: one held by a gate, one leaving out a commit the line holds. */
const PLAN: EntryAcceptPlan = {
  entry: RETRY.entry, repository: 'app', form: 'branch', target: 'daoris/t-one',
  parts: [
    { session: 'ap000001' },
    { session: 'ap000002', leftOut: [{ commit: 'abc1234', subject: 'already on the line', why: 'content' }] },
    { session: 'ap000003', held: 'a gate holds it' },
  ],
};

/** A failed start, its retry, and a request the database repository sent back: the parts' lineage by what each followed. */
export const Retry: Story = { args: { entry: RETRY, attended: 'ap000002', rows }, decorators: light };
/** The same in dark. */
export const RetryDark: Story = { args: { entry: RETRY, attended: 'ap000002', rows }, decorators: [atWidths(), inDark] };
/** The same in 中文. */
export const RetryChinese: Story = { args: { entry: RETRY, attended: 'ap000002', rows }, decorators: [chinese, atWidths(), inLight] };
/** A fork and a retry of a retry: parts with no `after` between them branch from the start, never one after another. */
export const Fork: Story = { args: { entry: FORK, attended: 'p3', rows: forkRows }, decorators: light };
/** The same in 中文. */
export const ForkChinese: Story = { args: { entry: FORK, attended: 'p3', rows: forkRows }, decorators: [chinese, atWidths(), inLight] };
/** Where Accept would land the parts, read once its ask is open: a dashed node, with what holds or leaves out each on its edge. */
export const WithTheLandingPlan: Story = { args: { entry: RETRY, attended: 'ap000003', rows, plan: PLAN }, decorators: light };
/** The same in 中文. */
export const WithTheLandingPlanChinese: Story = {
  args: { entry: RETRY, attended: 'ap000003', rows, plan: PLAN }, decorators: [chinese, atWidths(), inLight],
};
