import type { Meta, StoryObj } from '@storybook/react-vite';
import type { ComponentProps } from 'react';
import type { Quest } from '../data/api';
import { chinese } from '../design/storyLanguage';
import { MAIN_WIDTHS } from '../design/storyWidths';
import i18n from '../locales/i18n';
import { buildChain } from '../map/chain';
import { AttendedSession } from './AttendedSession';
import type { Channel } from './channel';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from './entries.fixtures';
import { ViewMain } from './frame/ViewMain';
import { sessionFacts } from './head/headFacts';
import type { MilestoneUnit } from './head/milestone';
import { SessionPageHead } from './head/SessionPageHead';
import { questName } from './identity';

// The record head with every block it can carry, drawn in a real browser by `measured/head.spec.ts` (MILESTONEUX1; the UI/UX
// design §3.7 and §9 row 6; UX7's budget, the UX7 design §6.2): the pinned page head, the head, the parts strip, the one Accept,
// the chain folded to its line, the relations, the channel, the milestone folded to its line and the trace folded, then the
// first lines of the conversation, which a marked box stands in for. The frame is the main area at each width the window gives
// it (`MAIN_WIDTHS`), with the page's own gutters: `ViewMain` is that area, so no `storyWidths` box is drawn around it.

const nothing = () => {};
const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const ENTRY = EVIDENCE_ENTRIES[0]!;
const ATTENDED = EVIDENCE_SESSIONS.find((session) => session.id === 'ap000003')!;
const ROWS = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

const quest = (over: Partial<Quest> & { id: string }): Quest => ({
  from: 'db', to: 'app', title: over.id, body: '', status: 'Taken', filed: at(400), updated: at(60), ...over,
});
const THEN = [{ to: 'db', title: 'Report the index back', body: '' }];
const QUEST = quest({ id: 'q0000c', title: 'Read the export table from the app', parent: 'q0000b', then: THEN });
const PARENT = quest({ id: 'q0000b', from: 'ask #c98a7e', to: 'db', title: 'Add the export table', status: 'Done' });

/** A person's channel kept to this line of work: one message to the app, so the fold carries a count. */
const CHANNEL: Channel = {
  ask: 'c98a7e', entry: 'person', unread: 0, mark: null,
  items: [{
    kind: 'message', at: at(30), id: 'm1', person: false, session: 'db000003', repository: 'db', agent: 'claude-code',
    entry: 'ask:c98a7e/db', to: 'app', text: 'Which column holds the export date?',
  }],
};

/** Every line the milestone's fold can say at once: the end, a blocked clean-up, a recovery with its date, and an attempt held. */
const MILESTONE: MilestoneUnit = {
  unit: 'u1', repository: 'app', branch: 'daoris/ap000003', session: 'ap000003',
  end: { state: 'ended', by: 'merged', on: 'main', revision: '3f9a12c', at: at(90), said: 'every commit of it is on `main`' },
  result: [{ unit: 'u1', kind: 'line', resource: 'main', role: 'result', disposition: 'kept', detail: "the branch's commits are on the line" }],
  cleanup: {
    state: 'pending',
    waits: [{ unit: 'u1', kind: 'tree', resource: 'ap000003', role: 'temporary', disposition: 'blocked', detail: 'a session still holds this tree' }],
    gone: [],
  },
  recovery: [{
    unit: 'u1', kind: 'moved-aside', resource: 'ap000003-20261103', role: 'recovery', disposition: 'retained', detail: 'set aside',
    until: '2026-11-10T14:02:00Z',
  }],
  entries: [],
  held: [{
    unit: 'u1', node: 'deploy', attempt: 2, plugin: 'acme.deploy', step: 'acme.deploy/deploy', nodeState: 'unknown', why: 'unknown',
    at: at(20), said: 'its plugin was asked and cannot say what became of it',
  }],
};

const ACCEPT: ComponentProps<typeof AttendedSession>['entryAccept'] = {
  scope: 'entry', offered: 2, open: false, onOpen: nothing, plans: null, onLand: nothing,
};

/** The main area at one width, as the frame draws a session: the pinned head, the record and the conversation's first lines. */
function RecordHead({ width, milestone = MILESTONE }: { width: keyof typeof MAIN_WIDTHS; milestone?: MilestoneUnit | null }) {
  const title = questName(QUEST);
  return (
    <div data-record-frame={MAIN_WIDTHS[width]} style={{ width: MAIN_WIDTHS[width] }} className="flex h-[47.5rem] max-w-full flex-col bg-page text-ink">
      <ViewMain
        gutters="session"
        header={(
          <SessionPageHead
            session={ATTENDED} title={title} shown="working" onAct={nothing}
            facts={sessionFacts(i18n.t.bind(i18n), {
              session: ATTENDED, shown: 'working',
              grouping: { session: ATTENDED.id, group: 'working', shown: 'working', archived: false, teammate: false, strikes: 3 },
            })}
            acts={['stop', 'review', 'openFolder', 'terminal', 'detach', 'copy']}
          />
        )}
      >
        <AttendedSession
          session={ATTENDED}
          quest={QUEST}
          headed
          parts={{ entry: ENTRY, attended: ATTENDED.id, rows: ROWS, onShow: nothing }}
          entryAccept={ACCEPT}
          chain={buildChain(QUEST.id, [QUEST, PARENT], EVIDENCE_SESSIONS)}
          relations={{
            askedBy: { id: 'db000003', session: EVIDENCE_SESSIONS.find((session) => session.id === 'db000003') ?? null },
            resumedAfter: null,
            asked: [{ quest: quest({ id: 'q0000d', to: 'db', title: 'Index the export table by date', status: 'Taken' }), question: false }],
          }}
          channel={{ channel: CHANNEL, only: { entry: 'ask:c98a7e/app', repository: 'app' }, onPost: nothing }}
          milestone={milestone}
          trace={{ open: false, onToggle: nothing }}
        />
        {/* The conversation (D76): below the record, in the same scroll; its first box is what the budget measures to. */}
        <div data-conversation className="mt-4 min-w-0">
          {Array.from({ length: 12 }, (_, line) => (
            <p key={line} className="m-0 py-1 text-body text-ink-soft">The agent’s words, line {line + 1}.</p>
          ))}
        </div>
      </ViewMain>
    </div>
  );
}

const meta = {
  title: 'Work/AttendedSession head',
  component: RecordHead,
  args: { width: 'wide' },
} satisfies Meta<typeof RecordHead>;
export default meta;

type Story = StoryObj<typeof meta>;

/** Every block present, at the main area's wide width. */
export const Wide: Story = {};

/** At half. */
export const Half: Story = { args: { width: 'half' } };

/** At the main area's 400 px floor. */
export const Narrow: Story = { args: { width: 'narrow' } };

/** In 中文, wide. */
export const ChineseWide: Story = { decorators: [chinese] };

/** In 中文, half. */
export const ChineseHalf: Story = { args: { width: 'half' }, decorators: [chinese] };

/** In 中文, at the floor. */
export const ChineseNarrow: Story = { args: { width: 'narrow' }, decorators: [chinese] };
