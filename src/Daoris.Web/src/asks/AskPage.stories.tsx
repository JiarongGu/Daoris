import type { Meta, StoryObj } from '@storybook/react-vite';
import type { WorkDoor } from '../work/pausing';
import {
  ABANDON_ANSWER, ABANDONED_ASK, ABANDONED_ENTRY, MIXED_ASK, PAUSABLE_ASK, PAUSED_ASK, SPENT_ASK,
} from '../work/pausingFixtures';
import { AskPage } from './AskPage';
import {
  BY_INTAKE, CLOSED, DONE, INTAKE_ASKED, INTAKE_PARKED, INTAKE_SESSION, LONG_CJK, NAMED, PROPOSED, PUBLISHED, REFUSED,
  UNKNOWN_TIER, UNMATCHED, WITH_GO_AHEADS,
} from './fixtures';

// An ask's page (INT4c; FRAME1d, D118 §3d): its record in Quests' main area, where it was a drawer, with *Close ask*
// and *Delete…* in its header. Every state a real machine reaches, and a sentence long enough and CJK enough to try
// the layout.

const nothing = () => {};
const TITLES = { '9a8b7c6d5e4f': 'The chunk streamer stalls on a cold cache — cap its hydration per frame.' };

const meta: Meta<typeof AskPage> = {
  title: 'Asks/AskPage',
  component: AskPage,
  args: {
    ask: PROPOSED, receivers: ['engine', 'game', 'lantern'], questTitles: TITLES,
    onPublish: nothing, onClose: nothing, onDelete: nothing, onOpenQuest: nothing,
  },
  // The main area's height, and a width between the side bar's two states.
  decorators: [(Story) => (
    <div className="flex h-[40rem] w-[46rem] max-w-full border border-line bg-page">
      <Story />
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof AskPage>;

/** Proposed by the declarations: each repository it proposed is a publish, and any other in its circle can be named. */
export const Proposed: Story = {};
/** Nothing in the circle's declarations shares its words, and the page says so. */
export const Unmatched: Story = { args: { ask: UNMATCHED } };
/** Published to two repositories, each a door into its quest's page where the page holds it. */
export const Published: Story = { args: { ask: PUBLISHED } };
/** The asker named the receiver, which published at once. */
export const Named: Story = { args: { ask: NAMED } };
/** A named receiver that could not be asked: the service's sentence, and the proposal kept to go on with. */
export const RefusedReceiver: Story = { args: { ask: REFUSED } };
/** Every quest it became has closed: done. */
export const Done: Story = { args: { ask: DONE } };
/** Nothing stands on its quests, so the service says it may go (D95): *Delete…* beside *Close ask*. */
export const Deletable: Story = { args: { ask: { ...PUBLISHED, deletable: true } } };
/** Closed with its reason: nothing more to do. */
export const Closed: Story = { args: { ask: CLOSED } };
/** A tier this page has no word for, shown as the service wrote it. */
export const UnknownTier: Story = { args: { ask: UNKNOWN_TIER } };
/** A long CJK sentence, which the title wraps. */
export const LongCjk: Story = { args: { ask: LONG_CJK } };
/** Its intake read it and published it — on the desktop, the session is a door. */
export const ByIntake: Story = { args: { ask: BY_INTAKE, intake: INTAKE_SESSION, onAttend: nothing } };
/** The same page in a browser: the session is named, and is not a door. */
export const ByIntakeInABrowser: Story = { args: { ask: BY_INTAKE, intake: INTAKE_SESSION } };
/** Its intake could not settle whose it is and parked asking — the proposal is still the person's. */
export const IntakeAsked: Story = { args: { ask: INTAKE_ASKED, intake: INTAKE_PARKED, onAttend: nothing } };
/** An intake the page has not loaded: named by its id, and no door that would open nothing. */
export const IntakeUnloaded: Story = { args: { ask: BY_INTAKE, intake: null, onAttend: nothing } };
/**
 * Its sessions asked for go-aheads (KNOWUSE1a): one approved with the person's words, one refused, one waiting that two
 * sessions asked, the second where its words could not be told from the first's, and a kind this page has no word for.
 */
export const GoAheads: Story = { args: { ask: WITH_GO_AHEADS, onAnswerGoAhead: nothing } };
/** The same go-aheads where there is no door to answer them: what became of each, and nothing offered. */
export const GoAheadsWithNoDoor: Story = { args: { ask: WITH_GO_AHEADS } };

// ——— Its work on this machine (PAUSE1e, D132 §7.1): *Pause…*, *Resume* and *Abandon…* where each applies, from the
// driver's plan.

const work = (over: Partial<WorkDoor> = {}): WorkDoor => ({
  plan: PAUSABLE_ASK, wired: true, busy: false, onPause: nothing, onResume: nothing, onAbandon: nothing, ...over,
});
const WORKING = { ...PUBLISHED, id: 'a1b2c3', deletable: true };

/** In flight on this machine: *Pause…* beside *Close ask*, *Abandon…* quiet beside *Delete…*. */
export const WorkInFlight: Story = { args: { ask: WORKING, work: work() } };
/** Paused here: *paused* beside its state, and *Resume* its loud act. */
export const WorkPaused: Story = { args: { ask: WORKING, work: work({ plan: PAUSED_ASK }) } };
/** The plan still on its way: none of the three is offered until it answers. */
export const WorkReading: Story = { args: { ask: WORKING, work: work({ plan: null }) } };
/** Abandoned: closed with the reason, when, what went and what stayed, from this machine's record. */
export const WorkAbandoned: Story = { args: { ask: { ...CLOSED, id: 'a1b2c3', note: ABANDONED_ENTRY.reason }, work: work({ plan: ABANDONED_ASK }) } };
/** Just abandoned: the second press's answer, said at once. */
export const WorkJustAbandoned: Story = {
  args: { ask: { ...CLOSED, id: 'a1b2c3', note: ABANDONED_ENTRY.reason }, work: work({ plan: SPENT_ASK, outcome: { answer: ABANDON_ANSWER, at: ABANDONED_ENTRY.at } }) },
};
/** Some of its work is not Daoris's here, so the abandon keeps it: *Abandon…* still offered for the rest. */
export const WorkPartlyElsewhere: Story = { args: { ask: WORKING, work: work({ plan: MIXED_ASK }) } };
/** In a browser: none of the three, and the terminal's commands named. */
export const WorkInABrowser: Story = { args: { ask: WORKING } };
