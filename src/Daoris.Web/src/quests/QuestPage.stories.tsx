import type { Meta, StoryObj } from '@storybook/react-vite';
import { buildChain } from '../map/chain';
import {
  CHAINED, CJK, CONFLICTED, DECLINED, DELETABLE, DONE, EXHAUSTED, FAILED, HELD_BY_PERSON, LANED, OPEN, QUESTION, STOPPED,
  TAKEN, TRUST, WAITING, WAITS_FOR_ACCOUNT, WORKING,
} from './fixtures';
import { QuestPage, QuestsMainNotice } from './QuestPage';

// A quest's page (FRAME1d, D118 §3d) in Quests' main area, where it was a drawer: open, taken, sitting for the
// person's hold, held for the agent's trust with its question open, parked by its strikes, waiting on a question, a
// conflict, carrying links and files, a chain, a driven session's record, declined, done, a delete asking, a 中文
// title; and the main area with no record: nothing chosen, gone, loading.

const nothing = () => {};

const meta: Meta<typeof QuestPage> = {
  title: 'Quests/QuestPage',
  component: QuestPage,
  args: {
    quest: OPEN,
    onRespond: nothing, onDelete: nothing, onDismiss: nothing, onRetry: nothing, onTrusting: nothing, onGrant: nothing,
    onOpenQuest: nothing,
  },
  // The main area's height, and a width between the side bar's two states.
  decorators: [(Story) => (
    <div className="flex h-[44rem] w-[52rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof QuestPage>;

/** Open: *Take* is the loud act in its header, then *Mark done* and *Decline…*; it carries a link and two files. */
export const Open: Story = {};

/** Taken: *Mark done* is the loud act now (UX5 U31). */
export const Taken: Story = { args: { quest: TAKEN } };

/** Sitting because the person holds its repository: the driver's sentence, in the page's language. */
export const Sitting: Story = { args: { sitting: HELD_BY_PERSON } };

/** Held for the agent's trust where it would run (D73), its question opened on the press. */
export const HeldForTrust: Story = { args: { hold: TRUST, trusting: true } };

/** Parked by its strikes (RETRY1): *Try again* where its sentence is read. */
export const ParkedByStrikes: Story = { args: { sitting: EXHAUSTED } };

/** Waits for an account (TOOL4g, D125 §4): its pill and the hold's sentence, and no *Try again*, since nothing needs you. */
export const WaitsForAnAccount: Story = { args: { sitting: WAITS_FOR_ACCOUNT } };

/** Held by the person's stop (SESSUX1b, D126 §3.4): its sentence, and *Try again*, which releases it. */
export const HeldByAStop: Story = { args: { sitting: STOPPED } };

/** Taken, and waiting on a question its taker asked another repository (D79). */
export const WaitingOnAQuestion: Story = { args: { quest: WAITING, question: { id: QUESTION.id, quest: QUESTION } } };

/** A move that reached the remote second (D68 §5), kept for a person with its dismissal. */
export const Conflict: Story = { args: { quest: CONFLICTED } };

/** Addressed to two of the engine's lanes, each named where its registration says (D115 §2.2). */
export const Lanes: Story = { args: { quest: LANED, lanes: 'assets (Assets), core (Core)' } };

/** A step of a chain (MAP1): the quest it follows, unseen, this one, and the step its close will publish. */
export const AChain: Story = { args: { quest: CHAINED, chain: buildChain(CHAINED.id, [CHAINED], []) } };

/**
 * A driven session works it: its record and the door into Sessions, where its stop is (D126 §3.6): a quest is decided
 * on its page, and its sessions are managed on theirs.
 */
export const WithItsSession: Story = {
  args: {
    session: WORKING, onAttend: nothing,
    chain: buildChain(OPEN.id, [OPEN, { ...OPEN, id: 'n3n3n3', parent: OPEN.id, title: 'Next' }], [FAILED, WORKING]),
  },
};

/** The same session read in a browser: the record, and no door. */
export const WithItsSessionInABrowser: Story = { args: { session: WORKING } };

/** Declined, with the reason the asker can act on. */
export const Declined: Story = { args: { quest: DECLINED } };

/** Done: nothing left to do here. */
export const Done: Story = { args: { quest: DONE } };

/** Nobody has started on it, so the service says it may go (D95): *Delete…* beside the acts. */
export const Deletable: Story = { args: { quest: DELETABLE } };

/** A title in 中文, which the header wraps. */
export const ChineseTitle: Story = { args: { quest: CJK } };

/** Nothing chosen: how to choose, and the ＋'s two kinds, Ask first. */
export const NothingChosen: Story = {
  render: () => <QuestsMainNotice state="none" actions={[{ id: 'ask', label: 'Ask' }, { id: 'quest', label: 'New quest' }]} />,
};

/** A quest chosen that is no longer here. */
export const Gone: Story = { render: () => <QuestsMainNotice state="gone" gone="quest" /> };

/** An ask chosen that is no longer here. */
export const AskGone: Story = { render: () => <QuestsMainNotice state="gone" gone="ask" /> };

/** The chosen record on its way: skeleton rows, never the empty state. */
export const Loading: Story = { render: () => <QuestsMainNotice state="loading" /> };
