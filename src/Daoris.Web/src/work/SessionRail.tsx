import { Fragment, type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY, type Quest, type Session } from '../api';
import { useQuests, useRegistry, useSessions } from '../queries';
import { searchable } from '../searchable';
import {
  type SessionHit, useDriver, useSessionGroups, useSessionOpenings, useSessionSearch, useSessionWhere,
} from '../shell';
import { Icon, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { cn } from '../lib/cn';
import { isComposing } from '../lib/composing';
import { useDebounced } from '../lib/useDebounced';
import { offeredActs, type SessionActId } from './acts';
import { endedToArchive, type SessionArrangement } from './groups';
import { SessionRow } from './SessionRow';
import { ArchiveEndedAsk, type RepositoryFacts, SessionList, type SessionRowFacts, SessionStrip } from './SessionList';
import { isIntake, ownTree, sessionTitle } from './identity';
import { byName, marked, readable } from './railSearch';
import { type SessionDoors, useSessionActs } from './sessionActs';

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
  arrangement = 'state', archived = false, archiveEnded = false, onArchiveEnded,
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
}) {
  const { t } = useTranslation();
  // Closed records included, then filtered here: the rail needs the attended one whatever state it
  // reached, and this is the query key the quest view already warms.
  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const driver = useDriver();
  // Each act on a row, by the one owner the page header calls too (SESSUX1d).
  const actions = useSessionActs({ notify, doors });
  // Where each session is listed by state (SESSUX1a): the driver's one reader, asked under the sessions' key, so every
  // tick that asks the records again asks this too. The monitor's list draws no group and asks nothing.
  const groups = useSessionGroups(undefined, { enabled: !live });
  // A query that is not asking and has never answered is one with no driver to ask: a browser (D47 §4).
  const noReader = live || (groups.fetchStatus === 'idle' && groups.status === 'pending');
  const drawn: SessionArrangement = noReader ? 'repository' : arrangement;
  const groupings = live ? undefined : groups.data;
  // A conversation's name is what was first said in it (RAIL1), from this machine's record.
  const openings = useSessionOpenings(sessions.data);
  // What the list holds: every record, or where the rail is live, what runs and the attended session, whatever state it
  // reached.
  const listed = (sessions.data ?? []).filter((session) => !live || SESSION_ACTIVE.has(session.state) || session.id === selected);
  // Where each row's work is now (LOOK2b): its tree, or where its landing put the work — asked once, for the rows listed.
  const where = useSessionWhere(listed);
  // Searching (RAIL1): by name at once, and by what was said once the typing settles.
  const [query, setQuery] = useState('');
  const settled = useDebounced(query, 250);
  const said = useSessionSearch(settled);

  useErrorNotify(sessions.error, notify);

  const questFor = new Map((quests.data ?? []).map((quest): [string, Quest] => [quest.id, quest]));
  const registered = new Map((registry.data ?? []).map((row) => [row.repository, row]));
  const titleOf = (session: Session) =>
    sessionTitle(session, session.quest ? questFor.get(session.quest) : null, openings[session.id]);

  // What a search may find: everything the list holds, or only what is running where the rail is live.
  const pool = live ? (sessions.data ?? []).filter((session) => SESSION_ACTIVE.has(session.state)) : (sessions.data ?? []);
  // Each row the list holds, with what it is drawn from besides its record.
  const rows: SessionRowFacts[] = listed.map((session) => ({
    session,
    quest: session.quest ? questFor.get(session.quest) : null,
    opening: openings[session.id],
    taking: taking[session.id],
    lastTurn: lastTurns[session.id],
    root: registered.get(session.repository)?.root,
    where: where[session.id],
  }));
  const placed = new Map((groupings ?? []).map((row) => [row.session, row]));

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
          asking={said.isFetching}
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
          {...acts}
        />
      )}
    </nav>
  );
}

/**
 * What a search of the rail found (RAIL1): sessions by name first, then the rest by what was said in
 * them, with the words marked. A session its name already found is not listed twice.
 *
 * @remarks
 * By name answers as the person types; by content answers once the typing settles, for the words it
 * was asked about — never a stale answer beside a newer query. The host's bounds are said, not hidden.
 */
function SearchResults({ query, settled, named, hits, cut, asking, sessionOf, titleOf, row, selected, onSelect }: {
  query: string;
  /** The query the content answer is for — the typed one, once it stops changing. */
  settled: string;
  named: Session[];
  hits?: SessionHit[];
  cut: boolean;
  asking: boolean;
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

  const heading = 'px-3 py-1 text-meta font-semibold uppercase tracking-wide text-ink-faint';
  return (
    <div>
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
      {current && cut && <p className="m-0 px-3 py-1.5 text-meta text-ink-faint">{t('work.rail.search.cut')}</p>}
    </div>
  );
}
