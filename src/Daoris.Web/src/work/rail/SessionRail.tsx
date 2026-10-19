import { type ComponentProps, Fragment, type ReactNode, useLayoutEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY, type Quest, type Session } from '../../data/api';
import { useAsks, useOpinions, useQuests, useRegistry, useSessions } from '../../data/queries';
import { searchable } from '../../lib/searchable';
import {
  type SessionHit, useAskLanding, useChannelUnreads, useDriver, useLandAsk, useSessionEntries, useSessionGroups, useSessionOpenings,
  usePushedCalls, useSessionSearch, useSessionWhere, useUnderWaySubject, withCall,
} from '../../bridge/shell';
import { askOfEntry, entryFacts, isIntakeEntry, listedSessions, nameOfEntry, type SessionEntry } from '../entries';
import { EntryAccept } from '../review/EntryAccept';
import { sentence } from '../../locales/format';
import { Button, Icon, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../../design/ui';
import { cn } from '../../lib/cn';
import { isComposing } from '../../lib/composing';
import { useDebounced } from '../../lib/useDebounced';
import { offeredActs, type SessionActId } from '../acts';
import type { OpenGroup } from '../../app/opener';
import { endedToArchive, type SessionArrangement } from '../groups';
import { groupHeading, useBringGroup } from '../frame/ListPane';
import { SessionRow } from './SessionRow';
import { Refused } from '../InlineConfirm';
import { ArchiveEndedAsk, type RepositoryFacts, SessionList, type SessionRowFacts, SessionStrip } from './SessionList';
import { askName, isIntake, isOpinion, linkedSessions, ownTree, sessionTitle } from '../identity';
import { byName, marked, readable } from './railSearch';
import { type SessionDoors, useSessionActs } from '../sessionActs';

/**
 * The rail: Sessions' list, by state or by repository (SESSUX1c, D126 §4), and its search (RAIL1).
 *
 * @remarks
 * **An organism — it holds the hooks so the molecules below it hold none** (components plan §2).
 * That is the whole arrangement: `SessionList`, its rows and its group headers are reviewable in every
 * state without a service or a driver, and this file is the one place that knows where their props come from.
 *
 * **Selection is not held here.** The rail is told which session is attended and reports a choice,
 * because one selection binds every region of the Work frame (IDE study §3) and a frame cannot
 * bind a selection its rail keeps to itself.
 *
 * **By state, a session is where the driver's one reader places it** (`useSessionGroups`, D126 §2.4): the page asks
 * and never derives a group of its own. A record the reader has not answered for yet, or every record while no answer
 * has come, is placed by its record alone (`groups.ts`) until the next answer. **A browser has no driver to ask**, so
 * its list is the arrangement it always had, by repository (D47 §4); and the monitor's list, the present tense only,
 * asks nothing and keeps it too.
 *
 * **A row's acts are the one owner's** (SESSUX1d, D126 §3.1): each row is offered what the one rule offers it
 * (`offeredActs`), and a press goes to `useSessionActs`, which the page header calls too. What only the frame can do
 * (attend and answer, attend and ask to stop, review, a terminal there) goes through the frame's `doors`. The monitor's
 * list, the present tense on a second screen, offers only its window and its id: the moves stay in the main window.
 * *Archive what ended…*'s second press is the owner's too (SESSUX1e, §5.3).
 */
export function SessionRail({
  selected = null, onSelect, notify, compact = false, doors, taking = {}, lastTurns = {}, live = false,
  arrangement = 'state', archived = false, archiveEnded = false, onArchiveEnded, group = null, onGroupBrought,
}: {
  /** The attended session's id, held by the frame. */
  selected?: string | null;
  onSelect?: (id: string) => void;
  notify: Notify;
  /**
   * The rail closed to its 56px strip (FRAME6): what waits on the person, then what runs, as its initial and its mark,
   * in the open rail's order (D126 §2.5). What ended is left to the open rail.
   */
  compact?: boolean;
  /**
   * What only the frame can do with a session (SESSUX1d): attend it and open its answer's box, attend it and ask to stop
   * it, review it in the dock, open a terminal in its folder. Absent, a row offers none of them.
   */
  doors?: SessionDoors;
  /**
   * Whether each live conversation has a turn in flight, as the driver says — the frame's, which
   * follows them for its composer too (UX5 U17). A chat between turns reads idle; one absent here
   * keeps its record's word.
   */
  taking?: Record<string, boolean>;
  /** When each live conversation's last turn ended here (RAIL2): a row's *moved* reads the later of it and the record. */
  lastTurns?: Record<string, string>;
  /**
   * The present tense only: no ended section, and a search that finds only what is running. The
   * monitor's, whose tiles are the running sessions a press scrolls to; an ended row there scrolled
   * to a tile that is not there (UX5 U70). By repository, and asking no reader.
   */
  live?: boolean;
  /** By state, the default, or by repository (D126 §4.1): the list's ⋯, remembered by the frame. */
  arrangement?: SessionArrangement;
  /** Whether archived sessions are shown (D126 §4.1). */
  archived?: boolean;
  /**
   * *Archive what ended…* pressed in the list's ⋯ (SESSUX1e, D126 §5.3): its first press, listed under the list's header
   * until it archives or the person never minds, which `onArchiveEnded` tells the frame.
   */
  archiveEnded?: boolean;
  onArchiveEnded?: () => void;
  /**
   * The group a door into what waits on the person names (ENTRY1b): *Waiting on you* or *To review*, brought into view once
   * the records and the reader have answered, and `onGroupBrought` told, whether this rail drew it or not.
   */
  group?: OpenGroup | null;
  onGroupBrought?: () => void;
}) {
  const { t } = useTranslation();
  // Closed records included, then filtered here: the rail needs the attended one whatever state it
  // reached, and this is the query key the quest view already warms.
  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const driver = useDriver();
  // Each act on a row, by the one owner the page header calls too (SESSUX1d).
  // Where each session is listed by state (SESSUX1a): the driver's one reader, asked under the sessions' key, so every
  // tick that asks the records again asks this too. The monitor's list draws no group and asks nothing.
  const groups = useSessionGroups(undefined, { enabled: !live });
  // The entries beside them (SESSONE1c, D167 point 5): one row per line of work in one repository, its records its parts.
  // The same answer, so nothing more is asked. The monitor's list is the present tense, by record; a host before entries
  // answers none and the list is the records it always was.
  const entryAnswer = useSessionEntries(undefined, { enabled: !live });
  const entries = live ? undefined : entryAnswer.data;
  // Each act on a row, by the one owner the page header calls too (SESSUX1d); an archive reaches every part of its entry.
  const actions = useSessionActs({ notify, doors, entries });
  // A query that is not asking and has never answered is one with no driver to ask: a browser (D47 §4).
  const noReader = live || (groups.fetchStatus === 'idle' && groups.status === 'pending');
  const drawn: SessionArrangement = noReader ? 'repository' : arrangement;
  const groupings = live ? undefined : groups.data;
  // A conversation's name is what was first said in it (RAIL1), from this machine's record.
  const openings = useSessionOpenings(sessions.data);
  // What the list holds: every record, or where the rail is live, what runs and the attended session, whatever state it
  // reached.
  // One record per entry, its leading part (D167); the others are its page's strip.
  const listed = listedSessions(
    (sessions.data ?? []).filter((session) => !live || SESSION_ACTIVE.has(session.state) || session.id === selected),
    entries,
  );
  // The asks the entries came from, named as the ask's page names them (`askName`): asked only where an entry has one.
  const asked = (entries ?? []).some((entry) => askOfEntry(entry) !== null);
  const asks = useAsks(true, asked);
  const askNames = Object.fromEntries((asks.data ?? []).map((ask): [string, string] => [ask.id, askName(ask)]));
  // What is unread on each such ask's channel (SESSONE2c, D167), said in the headings of the list *by ask*: asked only there, the
  // same reads the ask's page and its row hold under one key. None in a browser, which has no driver.
  const unreads = useChannelUnreads(
    [...new Set((entries ?? []).map(askOfEntry).filter((ask): ask is string => ask !== null))],
    { enabled: arrangement === 'ask' && !live },
  );
  // Where each row's work is now (LOOK2b): its tree, or where its landing put the work — asked once, for the rows listed.
  const where = useSessionWhere(listed);
  // Searching (RAIL1): by name at once, and by what was said once the typing settles.
  const [query, setQuery] = useState('');
  const settled = useDebounced(query, 250);
  const said = useSessionSearch(settled);

  useErrorNotify(sessions.error, notify);

  // ENTRY1b: a door's group, looked for once the records and the reader have answered, since a parked quest's session waits
  // on the person by the reader's word alone. A list that draws none (nothing waits there, no reader, the strip) lets it go.
  // A search typed here hides every group, and is this rail's alone and never kept, so a go clears it, in the render the
  // go arrives in (ENTRY1g); the frame shows the list by state for it, and lays its strip over.
  const bringing = group === 'you' || group === 'review';
  if (bringing && !compact && query) setQuery('');
  const answered = !sessions.isPending && (noReader || groups.data !== undefined || groups.isError);
  useBringGroup(bringing ? groupHeading('sessions', group) : null, answered, onGroupBrought);

  const questFor = new Map((quests.data ?? []).map((quest): [string, Quest] => [quest.id, quest]));
  const registered = new Map((registry.data ?? []).map((row) => [row.repository, row]));
  const titleOf = (session: Session) =>
    sessionTitle(session, session.quest ? questFor.get(session.quest) : null, openings[session.id]);

  // Whose work each second opinion's reviewer reads (XAGENT1g4c): the opinions the service lists, asked only when the list holds
  // a reviewer, joined to the sessions it already has. A 404 or an error leaves the map empty and no row claims anything.
  const opinions = useOpinions((sessions.data ?? []).some(isOpinion));
  const byId = new Map((sessions.data ?? []).map((session): [string, Session] => [session.id, session]));
  const reads: Record<string, string> = {};
  for (const opinion of opinions.data ?? []) {
    const working = byId.get(opinion.working);
    if (working) reads[opinion.session] = titleOf(working);
  }
  // Whom each session continues and whom each helper helps (MIXAGENT3a-i): the records' own links, joined to the sessions the
  // list holds, as the reviewers' are. A link to one it does not hold claims nothing.
  const continues: Record<string, string> = {};
  const helps: Record<string, string> = {};
  for (const session of sessions.data ?? []) {
    const linked = linkedSessions(session, byId);
    if (linked.continues) continues[session.id] = titleOf(linked.continues);
    if (linked.helps) helps[session.id] = titleOf(linked.helps);
  }
  // The work each helper and reviewer serves (MIXAGENT3a-ii, closing XAGENT1g4d): the list draws it under that work's row where
  // that row is drawn. The same two joins: a helper's own `serves`, and the reviewer's opinion's `working`.
  const serving: Record<string, string> = {};
  for (const opinion of opinions.data ?? []) {
    if (byId.has(opinion.session)) serving[opinion.session] = opinion.working;
  }
  for (const session of sessions.data ?? []) {
    if (session.serves) serving[session.id] = session.serves;
  }

  // What a search may find: everything the list holds, or only what is running where the rail is live.
  const pool = live ? (sessions.data ?? []).filter((session) => SESSION_ACTIVE.has(session.state)) : (sessions.data ?? []);
  // Each row the list holds, with what it is drawn from besides its record.
  const factsOfEntry = entryFacts(entries);
  const entryOf = new Map((entries ?? []).map((entry): [string, SessionEntry] => [entry.leading, entry]));
  const rows: SessionRowFacts[] = listed.map((session) => ({
    session,
    // A line of work an ask began is named by its ask, whose page names it the same way (D167, design §1.6); an intake
    // keeps its own name, which already names its ask.
    name: nameOfEntry(entryOf.get(session.id), askNames),
    entry: factsOfEntry.get(session.id),
    quest: session.quest ? questFor.get(session.quest) : null,
    opening: openings[session.id],
    taking: taking[session.id],
    lastTurn: lastTurns[session.id],
    root: registered.get(session.repository)?.root,
    where: where[session.id],
    reads: reads[session.id],
    continues: continues[session.id],
    helps: helps[session.id],
  }));
  // The call each session is making, from the shell's push where it is live (WAITSAY1c2): laid over the pull's `calling`.
  const calls = usePushedCalls();
  const placed = new Map((groupings ?? []).map((row) => [row.session, withCall(row, calls) ?? row]));

  // ACCEPTALL1 (D167's 2026-10-15 amendment point 4): an ask's one Accept, under its heading in the list by ask, where its lines of
  // work hold more than one part with work to land. Its plan reads git for every part, so it is asked only once its ask is open.
  const [askAsking, setAskAsking] = useState<string | null>(null);
  const askPlan = useAskLanding(askAsking, askAsking !== null);
  const landAsk = useLandAsk();
  const askEntries = (ask: string) => (entries ?? []).filter((entry) => askOfEntry(entry) === ask && !isIntakeEntry(entry));
  const askOffered = (ask: string) => askEntries(ask).flatMap((entry) => entry.parts).filter((part) => placed.get(part.session)?.lands).length;
  const askAccept = drawn === 'ask' && !live ? (ask: string) => {
    const offered = askOffered(ask);
    if (offered < 2) return null;
    const open = askAsking === ask;
    return (
      <AskAccept
        ask={ask}
        scope="ask"
        offered={offered}
        open={open}
        onOpen={(opened) => setAskAsking(opened ? ask : null)}
        plans={open ? askPlan.data?.entries ?? null : null}
        reading={open && askPlan.isFetching && !askPlan.data}
        // A part is named by its number in its own line of work's strip.
        numberOf={(session) => {
          const parts = askEntries(ask).find((entry) => entry.parts.some((part) => part.session === session))?.parts ?? [];
          return parts.findIndex((part) => part.session === session) + 1 || null;
        }}
        // Told back to its ask: every repository landed, or each one's sentence, a refusal holding only its own (point 4).
        onLand={(leaveOut, answered) => landAsk.mutate({ ask, parts: offered, leaveOut }, {
          onSuccess: (result) => {
            const said = result.repositories.map((each) => `${each.repository}: ${each.message}`).join('\n');
            if (!result.done) {
              answered.refused(said || t('work.accept.nothing'));
              return;
            }
            notify(said);
            answered.done();
          },
          onError: (error) => answered.refused(sentence(error)),
        })}
      />
    );
  } : undefined;

  // What decides each row's acts: its record, where the reader placed it, its checkout, where its work is (D126 §3.1).
  const factsFor = (session: Session) => ({
    session,
    // Whether an ask asked its quest decides *Pause ask…* (PAUSE1e).
    quest: session.quest ? questFor.get(session.quest) : null,
    grouping: live ? null : placed.get(session.id),
    root: registered.get(session.repository)?.root,
    where: where[session.id],
  });
  // The acts each row is offered, as the one rule offers them and this door can carry out. The monitor's list is read
  // only: its window and its id (D55 §b).
  const actsFor = (session: Session): SessionActId[] => offeredActs(factsFor(session), 'row')
    .filter((act) => actions.can(act) && (!live || act === 'detach' || act === 'copy'));
  const acts = {
    onSelect,
    actsFor,
    onAct: (act: SessionActId, id: string) => {
      const session = (sessions.data ?? []).find((row) => row.id === id);
      if (session) actions.run(act, factsFor(session));
    },
  };

  // A repository group's facts (by repository): the driver answers only where a shell is attached (D46 §6); with no
  // answer these stay undefined, and the header asserts nothing rather than reading silence as "no".
  const repositoryFacts = (repository: string, members: readonly Session[]): RepositoryFacts => {
    // A parked session holds its working tree, and a parked INTAKE holds nothing: it has asked and ended, and the room's
    // lock is the process (INT4b), so it claims no busy (INT4g).
    const holding = members.find((session) => SESSION_ACTIVE.has(session.state)
      && !(isIntake(session) && session.state === 'awaiting-person'));
    const registration = registered.get(repository);
    return {
      // Ask Daoris's records are kept in `daoris:help`, which is no repository and no name (HELP1a).
      label: repository === HELP_REPOSITORY ? t('help.title') : undefined,
      drivable: driver.data && driver.data.drivable.includes(repository),
      held: driver.data && driver.data.holds.includes(repository),
      busy: holding ? (ownTree(holding, registration?.root) ?? true) : null,
      adopted: registration?.adopted,
      // A root is answered only to a caller on the machine that holds it (D48 §7), so the question is only ASKED where a
      // driver answered — otherwise every registration would look like a teammate's, which is a claim rather than an absence.
      hasCheckout: driver.data && registration ? Boolean(registration.root) : undefined,
    };
  };

  if (compact) {
    if (sessions.isPending) return null;
    return (
      <SessionStrip
        arrangement={drawn}
        rows={rows}
        groupings={groupings}
        selected={selected}
        archived={archived}
        live={live}
        label={t('work.rail.label')}
        onSelect={onSelect}
      />
    );
  }

  if (sessions.isPending) return <div className="px-2.5 py-2"><SkeletonRows rows={5} /></div>;

  const searching = query.trim().length > 0;

  // What every row a search found carries: its name, its reader's word, and its menu.
  const row = (session: Session) => (
    <SessionRow
      key={session.id}
      session={session}
      quest={session.quest ? questFor.get(session.quest) : null}
      opening={openings[session.id]}
      taking={taking[session.id]}
      lastTurn={lastTurns[session.id]}
      root={registered.get(session.repository)?.root}
      where={where[session.id]}
      reads={reads[session.id]}
      continues={continues[session.id]}
      helps={helps[session.id]}
      grouping={live ? null : placed.get(session.id)}
      // A search finds archived sessions too, marked, since it is how one is found without the tick (D126 §4.5).
      archived={!live && Boolean(placed.get(session.id)?.archived)}
      selected={session.id === selected}
      onSelect={onSelect}
      acts={actsFor(session)}
      onAct={acts.onAct}
    />
  );

  // *Archive what ended…*'s first press (§5.3), once the reader has answered: what it says is what it lists, and a list
  // read before the answer would say nothing ended.
  const asking = archiveEnded && !live && groupings !== undefined;

  return (
    <nav aria-label={t('work.rail.label')}>
      {asking && (
        <ArchiveEndedAsk
          {...endedToArchive(listed, groupings)}
          busy={actions.archiving}
          // What the first press listed are entries (D167): every part of each goes, and is counted once.
          onArchive={(ids, answered) => actions.archiveListed(ids, answered)}
          onCancel={() => onArchiveEnded?.()}
        />
      )}

      {/* Searching (RAIL1): on the open rail only — the strip has no room to type in. Escape clears it,
          and the rail's own list comes back. The list's full empty state has no search above it. */}
      {rows.length > 0 && (
        <div className="px-2 pb-1 pt-1.5">
          <label className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised px-2 py-1 text-ink-faint focus-within:border-accent">
            <Icon name="search" size={12} className="shrink-0" />
            <input
              type="search"
              value={query}
              aria-label={t('work.rail.search.label')}
              placeholder={t('work.rail.search.placeholder')}
              onChange={(event) => setQuery(event.target.value)}
              // Escape clears a search, and one with nothing in it is left to the list laid over (D118 §3a).
              onKeyDown={(event) => { if (!isComposing(event) && event.key === 'Escape' && query) { event.preventDefault(); setQuery(''); } }}
              className="min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
            />
          </label>
        </div>
      )}

      {searching && (
        <SearchResults
          query={query}
          settled={settled}
          // Every session the list holds, ended ones past the rail's cap included, newest first.
          named={byName([...pool].sort((a, b) => b.updated.localeCompare(a.updated)), query, titleOf)}
          hits={said.data?.hits?.filter((hit) => pool.some((session) => session.id === hit.session))}
          cut={said.data?.cut ?? false}
          total={said.data?.total ?? null}
          covered={new Set((said.data?.hits ?? []).map((hit) => hit.session)).size}
          // The next page's wait is the foot's own to say, not the rail's search.
          asking={said.isFetching && !said.isFetchingNextPage}
          onMore={said.hasNextPage ? () => { void said.fetchNextPage(); } : undefined}
          loadingMore={said.isFetchingNextPage}
          moreError={said.isFetchNextPageError && !said.isFetching ? sentence(said.error) : null}
          sessionOf={(id) => pool.find((session) => session.id === id)}
          titleOf={titleOf}
          row={row}
          selected={selected}
          onSelect={onSelect}
        />
      )}

      {!searching && (
        <SessionList
          arrangement={drawn}
          rows={rows}
          groupings={groupings}
          selected={selected}
          archived={archived}
          live={live}
          repositoryFacts={repositoryFacts}
          entries={entries}
          askNames={askNames}
          unread={unreads}
          askAccept={askAccept}
          serving={serving}
          {...acts}
        />
      )}
    </nav>
  );
}

/**
 * An ask's one Accept, waiting while its landing is under way in any window (ACCEPTALL1e): this window's own press, or the driver's
 * telling of another's, read where the molecule is drawn, since a hook cannot be called where the list draws each ask.
 */
function AskAccept({ ask, ...accept }: { ask: string } & Omit<ComponentProps<typeof EntryAccept>, 'busy'>) {
  return <EntryAccept {...accept} busy={useUnderWaySubject('LAND_ASK', ask)} />;
}

/**
 * What a search of the rail found (RAIL1): sessions by name first, then the rest by what was said in
 * them, with the words marked. A session its name already found is not listed twice.
 *
 * @remarks
 * By name answers as the person types; by content answers once the typing settles, for the words it
 * was asked about — never a stale answer beside a newer query. The host's bounds are said, not hidden.
 */
export function SearchResults({
  query, settled, named, hits, cut, total, covered, asking, onMore, loadingMore, moreError, sessionOf, titleOf, row, selected, onSelect,
}: {
  query: string;
  /** The query the content answer is for — the typed one, once it stops changing. */
  settled: string;
  named: Session[];
  hits?: SessionHit[];
  /** Whether the host left some out; said only by a shell that gives no `total`, which cannot page (the older line). */
  cut: boolean;
  /** How many sessions the search found in what was said, as the host counts; null from a shell before pages. */
  total: number | null;
  /** How many of them the pages read so far hold. */
  covered: number;
  asking: boolean;
  /** Read the next page of sessions; absent on the last (RAILPAGE1b, UX §4's load-more rule). */
  onMore?: () => void;
  loadingMore?: boolean;
  /** Why the next page could not be read, in the sentence the person reads; null when it was. */
  moreError?: string | null;
  sessionOf: (id: string) => Session | undefined;
  titleOf: (session: Session) => string;
  row: (session: Session) => ReactNode;
  selected: string | null;
  onSelect?: (id: string) => void;
}) {
  const { t } = useTranslation();
  const typed = query.trim();
  const current = settled.trim() === typed && searchable(typed);
  const listed = new Set(named.map((session) => session.id));

  // The hits by session, in the host's order — its newest records first.
  const found = new Map<string, SessionHit[]>();
  for (const hit of current ? hits ?? [] : []) {
    if (listed.has(hit.session) || !sessionOf(hit.session)) continue;
    found.set(hit.session, [...(found.get(hit.session) ?? []), hit]);
  }

  // *Show more* pressed: how many sessions were listed and for which words, so the first one the next page brings takes the focus.
  const pressed = useRef<{ words: string; at: number } | null>(null);
  const root = useRef<HTMLDivElement>(null);
  const foot = useRef<HTMLDivElement>(null);
  const ids = [...found.keys()];
  const idsKey = ids.join(' ');

  // The next page arrived: the reader goes on from its first session, unless they went elsewhere meanwhile (the search changed,
  // or the focus is on something outside the foot). The press leaves with the last page, so the focus may be nowhere.
  useLayoutEffect(() => {
    const was = pressed.current;
    if (!was) return;
    if (was.words !== settled) { pressed.current = null; return; }
    if (ids.length <= was.at) return;
    pressed.current = null;
    const focused = document.activeElement;
    if (focused && focused !== document.body && !foot.current?.contains(focused)) return;
    const first = ids[was.at]!;
    [...(root.current?.querySelectorAll<HTMLElement>('[data-found]') ?? [])].find((door) => door.dataset.found === first)?.focus();
    // `idsKey` stands for `ids`, which is a new array every render.
  }, [idsKey, settled]);

  const more = () => {
    // Pressed again while it reads, it asks nothing more: the press stays a press, and the focus is never taken from it.
    if (loadingMore || !onMore) return;
    pressed.current = { words: settled, at: ids.length };
    onMore();
  };

  const heading = 'px-3 py-1 text-meta font-semibold uppercase tracking-wide text-ink-faint';
  return (
    <div ref={root}>
      {named.length > 0 && (
        <section aria-label={t('work.rail.search.byName')}>
          <h3 className={heading}>{t('work.rail.search.byName')}</h3>
          <ul className="m-0 list-none p-0">{named.map(row)}</ul>
        </section>
      )}

      {found.size > 0 && (
        <section aria-label={t('work.rail.search.byContent')} className={named.length > 0 ? 'mt-2 border-t border-line pt-2' : undefined}>
          <h3 className={heading}>{t('work.rail.search.byContent')}</h3>
          <ul className="m-0 list-none p-0">
            {[...found].map(([id, words]) => (
              <li key={id} data-list-row="">
                <button
                  type="button"
                  data-found={id}
                  aria-current={id === selected || undefined}
                  onClick={() => onSelect?.(id)}
                  className={cn(
                    'block w-full border-l-[3px] px-2.5 py-1.5 text-left transition-colors duration-(--speed) hover:bg-accent-soft/50',
                    id === selected ? 'border-l-accent bg-accent-soft' : 'border-l-transparent',
                  )}
                >
                  <span className="block truncate text-body text-ink">{titleOf(sessionOf(id)!)}</span>
                  {words.map((hit) => (
                    <span key={hit.seq} className="mt-0.5 line-clamp-2 block text-small text-ink-soft">
                      {/* Plain text around each mark, not spans: a span's edge dropped the space before a
                          match from the button's accessible name ("in thestreamer"). */}
                      {marked(readable(hit.snippet), settled).map((part, index) => (part.match
                        ? <mark key={index} className="-mx-0.5 rounded-sm bg-accent-soft px-0.5 text-ink">{part.text}</mark>
                        : <Fragment key={index}>{part.text}</Fragment>))}
                    </span>
                  ))}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {asking && <p className="m-0 px-3 py-1.5 text-small text-ink-faint">{t('work.rail.search.searching')}</p>}
      {!asking && named.length === 0 && found.size === 0 && (current || !searchable(typed)) && (
        <p className="m-0 px-3 py-1.5 text-small text-ink-faint">{t('work.rail.search.none', { query: typed })}</p>
      )}
      {/* A shell before pages says only that it left some out; one that counts says how many, and reads the rest on a press. */}
      {current && total === null && cut && <p className="m-0 px-3 py-1.5 text-meta text-ink-faint">{t('work.rail.search.cut')}</p>}
      {current && onMore && total !== null && (
        <div ref={foot} className="grid grid-cols-[minmax(0,1fr)] justify-items-start gap-1.5 px-3 py-1.5">
          <p className="m-0 text-meta text-ink-faint">{t('work.rail.search.listed', { shown: covered, total })}</p>
          {moreError && <Refused sentence={t('work.rail.search.moreFailed', { why: moreError })} />}
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <Button className="px-2.5 text-small" onClick={more}>{t('work.rail.search.more')}</Button>
            {/* There from the first, so a reader hears it speak when the wait begins. */}
            <span role="status" className="text-small text-ink-soft">{loadingMore ? t('work.rail.search.loadingMore') : ''}</span>
          </div>
        </div>
      )}
    </div>
  );
}
