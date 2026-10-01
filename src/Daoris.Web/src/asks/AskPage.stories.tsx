import type { Meta, StoryObj } from '@storybook/react-vite';
import { AskPage } from './AskPage';
import {
  BY_INTAKE, CLOSED, DONE, INTAKE_ASKED, INTAKE_PARKED, INTAKE_SESSION, LONG_CJK, NAMED, PROPOSED, PUBLISHED, REFUSED,
  UNKNOWN_TIER, UNMATCHED,
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
