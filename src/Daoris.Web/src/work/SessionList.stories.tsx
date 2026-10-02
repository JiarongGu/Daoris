import type { Meta, StoryObj } from '@storybook/react-vite';
import type { Quest, Session } from '../api';
import { offeredActs } from './acts';
import { endedToArchive, type SessionArrangement, type SessionGrouping } from './groups';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from './layout';
import { ListMore, ListPane } from './ListPane';
import { ArchiveEndedAsk, type SessionRowFacts, SessionList, SessionStrip } from './SessionList';

// Sessions' list (SESSUX1c, D126 §2.1, §4) on its list pane, in every state the design names: by state with every
// group, by repository, one group only, a long Ended group and its press, archived shown, empty, loading, 中文 titles,
// the strip with what waits on the person first, and laid over the main area. A molecule, so each is reached by
// passing it: no driver, no service.

/** Relative to now, so a story reads as a duration rather than as the age of this file. */
const at = (minutesAgo: number) => new Date(Date.now() - minutesAgo * 60_000).toISOString();

const open = (width: number): ListLayout => ({ mode: 'open', width, beside: width, auto: false });
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };

const session = (over: Partial<Session> & Pick<Session, 'id' | 'state'>): Session => ({
  repository: 'engine', adapter: 'claude-code', kind: 'driven', created: at(95), updated: at(6), ...over,
});

const quest = (id: string, title: string, to = 'engine'): Quest => ({
  id, from: 'game', to, title, body: '', status: 'Taken', filed: at(4000), updated: at(60),
});

const QUESTS = new Map<string, Quest>([
  ['7a82cc', quest('7a82cc', 'Expose a streaming budget on the chunk API')],
  ['5e7a11', quest('5e7a11', 'Read the media field names from config', 'game')],
  ['b4dc0d', quest('b4dc0d', 'Cap hydration per frame in the streamer')],
  ['q9q9q9', quest('q9q9q9', 'Publish the save format version', 'game')],
  ['c0de42', quest('c0de42', '把区块加载预算暴露给世界流式系统')],
]);

const SESSIONS: Session[] = [
  session({ id: 'p4rk3d00', state: 'awaiting-person', quest: '7a82cc', updated: at(40) }),
  session({ id: 'f41led00', state: 'failed', quest: 'b4dc0d', updated: at(25) }),
  session({ id: 'st0pp3d0', state: 'stopped', quest: '5e7a11', repository: 'game', tree: 'trees/default/game/st0pp3d0', updated: at(18) }),
  session({ id: 'w0rk1ng0', state: 'working', quest: 'q9q9q9', repository: 'game', created: at(50) }),
  session({ id: 'c4a7c4a7', state: 'working', kind: 'chat', repository: 'tools', created: at(12) }),
  session({ id: 'qu3u3d00', state: 'queued', quest: 'c0de42', created: at(2), updated: at(2) }),
  session({ id: 'aw41t000', state: 'completed', quest: '7a82cc', repository: 'game', updated: at(70) }),
  session({ id: 'd0ne0000', state: 'completed', updated: at(130) }),
  session({ id: 'd0ne0001', state: 'declined', repository: 'tools', updated: at(200) }),
];

/** The driver's one reader on SESSIONS (`SessionGroups.Read`), in its order. */
const GROUPINGS: SessionGrouping[] = [
  { session: 'f41led00', group: 'you', shown: 'parked', archived: false, teammate: false, strikes: 3 },
  { session: 'p4rk3d00', group: 'you', shown: 'awaiting-person', archived: false, teammate: false },
  { session: 'st0pp3d0', group: 'review', shown: 'stopped', archived: false, teammate: false, work: { commits: 3, uncommitted: 0 } },
  { session: 'qu3u3d00', group: 'working', shown: 'queued', archived: false, teammate: false },
  { session: 'w0rk1ng0', group: 'working', shown: 'working', archived: false, teammate: false },
  { session: 'c4a7c4a7', group: 'working', shown: 'working', archived: false, teammate: false },
  { session: 'aw41t000', group: 'later', shown: 'awaiting-reply', archived: false, teammate: false, awaits: 'q9q9q9', awaitsOf: 'engine' },
  { session: 'd0ne0000', group: 'ended', shown: 'completed', archived: false, teammate: false },
  { session: 'd0ne0001', group: 'ended', shown: 'declined', archived: false, teammate: false },
];

const facts = (sessions: readonly Session[]): SessionRowFacts[] => sessions.map((row) => ({
  session: row,
  quest: row.quest ? QUESTS.get(row.quest) : null,
  opening: row.id === 'c4a7c4a7' ? 'Why does the loader block on the first chunk?' : null,
  // A chat between turns reads idle, as the driver says (UX5 U17).
  taking: row.kind === 'chat' ? false : undefined,
}));

/** Fifteen that ended, newest first: past the twelve a group shows before its press (§4.4). */
const MANY_ENDED = Array.from({ length: 15 }, (_, index) =>
  session({ id: `e${String(index).padStart(7, '0')}`, state: index % 4 === 0 ? 'failed' : 'completed', updated: at(30 + index * 45) }));

type Args = {
  arrangement: SessionArrangement;
  sessions: readonly Session[];
  groupings?: readonly SessionGrouping[];
  selected?: string | null;
  archived?: boolean;
  layout?: ListLayout;
  loading?: boolean;
  /** *Archive what ended…*'s first press, under the list's header (SESSUX1e), listed from these sessions. */
  asking?: boolean;
};

/** The list as Sessions hands it to its pane: its ＋, its ⋯ with *Group by* and the archive's items, its strip and its body. */
function SessionsListPane({
  arrangement, sessions, groupings, selected = null, archived = false, layout = open(LIST_BOUNDS.sessions.initial), loading = false,
  asking = false,
}: Args) {
  const rows = facts(sessions);
  return (
    <ListPane
      name="Sessions"
      labels={{ open: 'Show the session list', close: 'Hide the session list', resize: 'session list width' }}
      layout={layout}
      bounds={LIST_BOUNDS.sessions}
      make={{ label: 'Start a session', onMake: () => {} }}
      more={(
        <ListMore
          label="More actions"
          choice={{
            label: 'Group by', value: arrangement, onChoose: () => {},
            options: [{ value: 'state', label: 'State' }, { value: 'repository', label: 'Repository' }],
          }}
          items={[
            { id: 'archived', label: 'Show archived', checked: archived },
            { id: 'archiveEnded', label: 'Archive what ended…', rule: true },
          ]}
        />
      )}
      strip={<SessionStrip arrangement={arrangement} rows={rows} groupings={groupings} selected={selected} archived={archived} label="Sessions" />}
      loading={loading}
      onOpen={() => {}}
      onClose={() => {}}
      onDismiss={() => {}}
      onResize={() => {}}
    >
      <nav aria-label="Sessions">
        {asking && <ArchiveEndedAsk {...endedToArchive(sessions, groupings)} onArchive={() => {}} onCancel={() => {}} />}
        <SessionList
          arrangement={arrangement}
          rows={rows}
          groupings={groupings}
          selected={selected}
          archived={archived}
          repositoryFacts={(repository) => ({ drivable: repository !== 'tools', held: repository === 'tools', busy: repository === 'game' ? true : null })}
          onSelect={() => {}}
          // Each row's acts as the one rule offers them (SESSUX1d), as the rail hands them.
          actsFor={(row) => offeredActs({ session: row, grouping: groupings?.find((each) => each.session === row.id) }, 'row')}
          onAct={() => {}}
        />
      </nav>
    </ListPane>
  );
}

const meta: Meta<typeof SessionsListPane> = {
  title: 'Work/SessionList',
  component: SessionsListPane,
  args: { arrangement: 'state', sessions: SESSIONS, groupings: GROUPINGS, selected: 'w0rk1ng0' },
  // The frame's height, and a main area beside the list for it to sit beside or lie over.
  decorators: [(Story) => (
    <div className="relative flex h-[44rem] w-[60rem] max-w-full border border-line bg-page">
      <Story />
      <div className="min-w-0 flex-1 p-4 text-body text-ink-soft">The main area: the attended session.</div>
    </div>
  )],
};
export default meta;

type Story = StoryObj<typeof SessionsListPane>;

/**
 * By state, the default, with every group: *Waiting on you* (a session parked to ask, and a quest parked after three
 * failed sessions, in open's hue), *To review* (a stopped session whose tree holds three commits), *Working* (queued
 * now neutral, a driven session, a chat between turns), *Resumes later* (awaiting a reply) and *Ended*. Each row's
 * line names its repository, since no group header does.
 */
export const ByState: Story = {};

/** By repository, chosen in the ⋯: each repository's facts on its header, waiting on you and parked first, the ended beneath. */
export const ByRepository: Story = { args: { arrangement: 'repository' } };

/** One group only: everything is running, and only *Working* is drawn. */
export const OneGroup: Story = {
  args: { sessions: SESSIONS.slice(3, 6), groupings: GROUPINGS.slice(3, 6), selected: null },
};

/** A long *Ended*: twelve, then *Show 3 more* (§4.4). */
export const LongEnded: Story = {
  args: {
    sessions: MANY_ENDED, selected: null,
    groupings: MANY_ENDED.map((row) => ({ session: row.id, group: 'ended' as const, shown: row.state, archived: false, teammate: false })),
  },
};

/** Archived shown: last, after *Ended* (§4.1), each row's ⋯ offering *Unarchive*. Hidden unless shown. */
export const ArchivedShown: Story = {
  args: {
    archived: true,
    groupings: [...GROUPINGS.slice(0, 8), { session: 'd0ne0001', group: 'archived', shown: 'declined', archived: true, teammate: false }],
  },
};

/** *Show archived* ticked with nothing archived (SESSUX1e): the group says so, rather than the tick seeming to do nothing. */
export const ArchivedEmpty: Story = { args: { archived: true } };

/** By repository with archived shown: an archived row sits among the ended, and its line says it is archived (§4.5). */
export const ArchivedByRepository: Story = {
  args: {
    arrangement: 'repository',
    archived: true,
    groupings: [...GROUPINGS.slice(0, 8), { session: 'd0ne0001', group: 'archived', shown: 'declined', archived: true, teammate: false }],
  },
};

/**
 * *Archive what ended…*'s first press (§5.3), under the list's header: what the second press would archive, and what
 * stays because it needs the person: two waiting on you, one to review.
 */
export const ArchiveWhatEnded: Story = { args: { asking: true, selected: null } };

/** The first press with nothing ended to archive: it says so, keeps what needs you, and offers only to close. */
export const NothingToArchive: Story = {
  args: { asking: true, selected: null, sessions: SESSIONS.slice(0, 6), groupings: GROUPINGS.slice(0, 6) },
};

/** Nothing at all: the list's empty state. */
export const Empty: Story = { args: { sessions: [], groupings: [] } };

/** A first load: skeleton rows, never the empty state. */
export const Loading: Story = { args: { loading: true } };

/** Titles in 中文: content, never translated, cut to one line with the whole in its tip. */
export const ChineseTitle: Story = { args: { selected: 'qu3u3d00' } };

/** No reader has answered (a browser): every record by its own word, by repository, as the list always was. */
export const NoReader: Story = { args: { arrangement: 'repository', groupings: undefined } };

/** Closed by the person: its strip holds what waits on the person first, then what runs (§2.5). */
export const Strip: Story = { args: { layout: CLOSED } };

/** A strip the window drew, opened: the list over the main area, beside its strip. */
export const LaidOver: Story = { args: { layout: { mode: 'over', width: LIST_BOUNDS.sessions.initial, beside: LIST_STRIP, auto: true } } };
