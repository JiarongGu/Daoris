import type { Meta, StoryObj } from '@storybook/react-vite';
import { chinese } from '../../design/storyLanguage';
import { entryFacts, listedSessions } from '../entries';
import { EVIDENCE_ASKS, EVIDENCE_ENTRIES, EVIDENCE_GROUPINGS, EVIDENCE_QUESTS, EVIDENCE_SESSIONS } from '../entries.fixtures';
import { PartsStrip } from '../head/PartsStrip';
import { SessionList, type SessionRowFacts } from './SessionList';
import type { SessionArrangement } from '../groups';

// Sessions as entries (SESSONE1c, D167 point 5): the owner's two asks, which the list showed as eleven records, as the three
// lines of work they are, and the parts strip on a session's page. Molecules, so each is reached by passing it: no driver,
// no service. English and 中文, since the strip's and the list's words are chrome.

const quests = new Map(EVIDENCE_QUESTS.map((quest) => [quest.id, quest]));
const leading = listedSessions(EVIDENCE_SESSIONS, EVIDENCE_ENTRIES);
const factsOfEntry = entryFacts(EVIDENCE_ENTRIES);

/** The rows the rail draws: each entry's leading record, named by its ask, with its parts counted. */
const rows: SessionRowFacts[] = leading.map((session) => {
  const entry = EVIDENCE_ENTRIES.find((each) => each.leading === session.id)!;
  return {
    session,
    quest: session.quest ? quests.get(session.quest) : null,
    name: EVIDENCE_ASKS[entry.origin.slice('ask:'.length)],
    entry: factsOfEntry.get(session.id),
  };
});

function Rail({ arrangement, selected = null }: { arrangement: SessionArrangement; selected?: string | null }) {
  return (
    <nav aria-label="Sessions" className="w-72 border border-line bg-page">
      <SessionList
        arrangement={arrangement}
        rows={rows}
        groupings={EVIDENCE_GROUPINGS}
        entries={EVIDENCE_ENTRIES}
        askNames={EVIDENCE_ASKS}
        selected={selected}
        onSelect={() => {}}
      />
    </nav>
  );
}

const meta: Meta<typeof Rail> = { title: 'Work/SessionEntries', component: Rail };
export default meta;

type Story = StoryObj<typeof Rail>;

/** Three rows for the two asks: the app's and the database's lines of the first, the app's line of the second, each saying its parts. */
export const ByState: Story = { args: { arrangement: 'state', selected: 'ap000001' } };
export const ByStateChinese: Story = { ...ByState, decorators: [chinese] };

/** *Group by ask*: each ask a heading, its lines of work under it with their repositories. */
export const ByAsk: Story = { args: { arrangement: 'ask' } };
export const ByAskChinese: Story = { ...ByAsk, decorators: [chinese] };

const rowsOfParts = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

function Strip({ attended }: { attended: string }) {
  return (
    <div className="w-[34rem] max-w-full">
      <PartsStrip entry={EVIDENCE_ENTRIES[0]!} attended={attended} rows={rowsOfParts} onShow={() => {}} />
    </div>
  );
}

/** The parts strip under a session's head: a failed start, its retry and a request from the database repository's line. */
export const Parts: StoryObj<typeof Strip> = { render: (args) => <Strip {...args} />, args: { attended: 'ap000003' } };
export const PartsChinese: StoryObj<typeof Strip> = { ...Parts, decorators: [chinese] };
export const PartsMiddle: StoryObj<typeof Strip> = { ...Parts, args: { attended: 'ap000002' } };
