import type { Meta, StoryObj } from '@storybook/react-vite';
import { INTAKE_ASKED, PROPOSED, PUBLISHED } from '../asks/fixtures';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from '../work/layout';
import { ListMore, ListPane } from '../work/ListPane';
import { CHAINED, CJK, CONFLICTED, DECLINED, DONE, HELD, HELD_BY_PERSON, LANED, OPEN, SITTING, TAKEN, WAITING } from './fixtures';
import { type AskRowFacts, QuestList, type QuestRowFacts } from './QuestList';

// Quests' list (FRAME1d, D118 §2) on its list pane, in every state the design names: the asks then the quests by
// state, a row chosen, the closed group, a receiver filter said, a held repository's row with its resume, a row
// waiting on a question and one a session works, only asks, empty, loading, an error with no answer ever, 中文 titles,
// the strip, and laid over the main area.

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };

const ASKS: AskRowFacts[] = [{ ask: PROPOSED }, { ask: INTAKE_ASKED, intake: 'awaiting-person' }, { ask: PUBLISHED }];
const QUESTS: QuestRowFacts[] = [
  { quest: OPEN, session: 'working' },
  { quest: SITTING },
  { quest: LANED },
  { quest: CHAINED },
  { quest: TAKEN },
  { quest: WAITING, waits: 'q2q2q2' },
  { quest: CONFLICTED },
];
const CLOSED_QUESTS: QuestRowFacts[] = [{ quest: DONE }, { quest: DECLINED }];

type Args = {
  asks: AskRowFacts[];
  quests: QuestRowFacts[];
  closed?: boolean;
  chosen?: string | null;
  filteredTo?: string | null;
  layout?: ListLayout;
  loading?: boolean;
  unanswered?: string;
};

/** The list as the view hands it to its pane: its ＋ with two kinds, its ⋯ of filters, its body. */
function QuestsListPane({ asks, quests, closed = false, chosen = null, filteredTo = null, layout = open(LIST_BOUNDS.quests.initial), loading = false, unanswered }: Args) {
  const empty = !loading && !unanswered && asks.length === 0 && quests.length === 0;
  return (
    <ListPane
      name="Quests"
      labels={{ open: 'Show the quest list', close: 'Hide the quest list', resize: 'quest list width' }}
      layout={layout}
      bounds={LIST_BOUNDS.quests}
      make={{ label: 'New ask or quest', kinds: [{ id: 'ask', label: 'Ask' }, { id: 'quest', label: 'New quest' }], onMake: () => {} }}
      more={(
        <ListMore
          label="Filter the list"
          choice={{
            label: 'Receiver', value: filteredTo ?? '*', onChoose: () => {},
            options: [{ value: '*', label: 'Everyone' }, { value: 'engine', label: 'engine' }, { value: 'game', label: 'game' }],
          }}
          items={[{ id: 'closed', label: 'Include closed', checked: closed }]}
          onChoose={() => {}}
        />
      )}
      loading={loading}
      empty={empty ? {
        headline: filteredTo ? `Nothing asked of ${filteredTo}` : 'No open quests anywhere',
        body: 'The family owes itself nothing right now. When a repository needs something from a sibling, it is asked for here — never edited across.',
      } : undefined}
      onOpen={() => {}}
      onClose={() => {}}
      onDismiss={() => {}}
      onResize={() => {}}
    >
      <QuestList
        asks={asks}
        quests={quests}
        closed={closed}
        filteredTo={filteredTo}
        empty={filteredTo ? `Nothing asked of ${filteredTo}` : 'No open quests anywhere'}
        unanswered={unanswered}
        chosen={chosen}
        onChoose={() => {}}
        onResume={() => {}}
      />
    </ListPane>
  );
}

const meta: Meta<typeof QuestsListPane> = {
  title: 'Quests/QuestList',
  component: QuestsListPane,
  args: { asks: ASKS, quests: QUESTS, chosen: OPEN.id },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[40rem] w-[60rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the chosen quest's page, or an ask's.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof QuestsListPane>;

/** The asks first, then the quests by where they are in their life, each group with its count; a quest chosen. */
export const AsksThenQuests: Story = {};

/** An ask chosen: its row wears the list's choice. */
export const AnAskChosen: Story = { args: { chosen: `ask:${PROPOSED.id}` } };

/** Closed quests included from the ⋯: a group of their own, read as finished. */
export const ClosedIncluded: Story = { args: { closed: true, quests: [...QUESTS, ...CLOSED_QUESTS] } };

/** Filtered to one receiver from the ⋯: said at the list's head, since the ⋯ that set it is a press away. */
export const FilteredToOneReceiver: Story = { args: { filteredTo: 'engine', quests: QUESTS.filter(({ quest }) => quest.to === 'engine') } };

/** A held repository's quest says why it waits, and its resume sits beside its row's door (USE1). */
export const HeldWithItsResume: Story = { args: { quests: [{ quest: OPEN, sitting: HELD_BY_PERSON }, ...QUESTS.slice(1)], chosen: null } };

/**
 * A done a departure holds for the person's yes (DRIFT1d2): first among the quests, with closed quests not shown, its row
 * saying it awaits them and never dimmed as ended.
 */
export const AwaitingYourYes: Story = { args: { quests: [{ quest: HELD }, ...QUESTS], chosen: HELD.id } };

/** Asks and no quests: the asks, then the line that says no quest is open. */
export const OnlyAsks: Story = { args: { quests: [] } };

/** Nothing at all: the empty state, with the ＋'s two kinds, Ask first. */
export const Empty: Story = { args: { asks: [], quests: [] } };

/** Nothing asked of the receiver it is filtered to. */
export const EmptyForAReceiver: Story = { args: { asks: [], quests: [], filteredTo: 'game' } };

/** A first load: skeleton rows, never the empty state. */
export const Loading: Story = { args: { loading: true } };

/** An error with no answer ever: the list says the sentence in place, and is never blank. */
export const ErrorNoAnswer: Story = {
  args: { asks: [], quests: [], unanswered: 'Daoris could not reach this machine\'s host. Is the service running?' },
};

/** Titles in 中文: content, never translated, cut to one line with the whole in its tip. */
export const ChineseTitles: Story = { args: { quests: [{ quest: CJK }, ...QUESTS.slice(0, 3)], chosen: CJK.id } };

/** Closed by the person: its strip holds its controls alone, the open and the ＋. */
export const Strip: Story = { args: { layout: CLOSED } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.quests.initial, beside: LIST_STRIP, auto: true } } };
