import { Fragment, type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { HELP_REPOSITORY, type Quest, type Session } from '../api';
import { useQuests, useRegistry, useSessions } from '../queries';
import { partition, waitingFirst } from './rail';
import { type SessionHit, useDriver, useOpenWindow, useSessionOpenings, useSessionSearch } from '../shell';
import { EmptyState, Icon, type Notify, SESSION_ACTIVE, SkeletonRows, useErrorNotify } from '../ui';
import { cn } from '../lib/cn';
import { useDebounced } from '../lib/useDebounced';
import { RepositoryGroup } from './RepositoryGroup';
import { SessionRow, SessionStripRow } from './SessionRow';
import { isIntake, ownTree, sessionTitle } from './identity';
import { byName, marked, readable } from './railSearch';
import { sessionWindowName } from './window';

/**
 * The rail: everything running, grouped by the repository it runs in (design §3).
 *
 * @remarks
 * **An organism — it holds the hooks so the molecules below it hold none** (components plan §2).
 * That is the whole arrangement: a row and a group header are reviewable in every state without a
 * service or a driver, and this file is the one place that knows where their props come from.
 *
 * **Selection is not held here.** The rail is told which session is attended and reports a choice,
 * because one selection binds every region of the Work frame (IDE study §3) and a frame cannot
 * bind a selection its rail keeps to itself.
 *
 * **What it lists is what is still running** — plus the attended session, whatever state it reached.
 * A session that finished while its person was reading it must not vanish out from under them, and
 * reviewing finished work is a surface of its own (SURF6), not a growing list here.
 */
export function SessionRail({ selected = null, onSelect, notify, compact = false, onReview, taking = {}, lastTurns = {}, live = false }: {
  /** The attended session's id, held by the frame. */
  selected?: string | null;
  onSelect?: (id: string) => void;
  notify: Notify;
  /**
   * The rail closed to its 56px strip (FRAME6): every running session as its initial and its mark,
   * in the open rail's order. What ended is left to the open rail.
   */
  compact?: boolean;
  /** Review a session's work — the frame's, since the dock is (RAIL1's row menu). */
  onReview?: (id: string) => void;
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
   * to a tile that is not there (UX5 U70).
   */
  live?: boolean;
}) {
  const { t } = useTranslation();
  // Closed records included, then filtered here: the rail needs the attended one whatever state it
  // reached, and this is the query key the quest view already warms.
  const sessions = useSessions(null, true);
  const quests = useQuests(null, true);
  const registry = useRegistry();
  const driver = useDriver();
  const openWindow = useOpenWindow();
  // A conversation's name is what was first said in it (RAIL1), from this machine's record.
  const openings = useSessionOpenings(sessions.data);
  // Searching (RAIL1): by name at once, and by what was said once the typing settles.
  const [query, setQuery] = useState('');
  const settled = useDebounced(query, 250);
  const said = useSessionSearch(settled);

  useErrorNotify(sessions.error, notify);

  const questFor = new Map((quests.data ?? []).map((quest): [string, Quest] => [quest.id, quest]));
  const registered = new Map((registry.data ?? []).map((row) => [row.repository, row]));
  const titleOf = (session: Session) =>
    sessionTitle(session, session.quest ? questFor.get(session.quest) : null, openings[session.id]);

  // What every row of the open rail carries: its name, and its menu.
  const row = (session: Session) => (
    <SessionRow
      key={session.id}
      session={session}
      quest={session.quest ? questFor.get(session.quest) : null}
      opening={openings[session.id]}
      taking={taking[session.id]}
      lastTurn={lastTurns[session.id]}
      root={registered.get(session.repository)?.root}
      selected={session.id === selected}
      onSelect={onSelect}
      onDetach={(id) => openWindow.mutate(sessionWindowName(id))}
      onReview={onReview}
      onCopy={(id) => {
        void navigator.clipboard?.writeText(id).then(() => notify(t('work.rail.menu.copied', { id })), () => {});
      }}
    />
  );

  // Live sessions grouped by repository, and beneath them the ones that ended — reachable after a
  // restart, which §7 of the working-surface design promises and which this rail did not keep until
  // the deployed application showed four empty-state sentences over four real records (`rail.ts`).
  const { active: shown, ended, hiddenEnded } = partition(sessions.data ?? [], selected);
  // What a search may find: everything the list holds, or only what is running where the rail is live.
  const pool = live ? (sessions.data ?? []).filter((session) => SESSION_ACTIVE.has(session.state)) : (sessions.data ?? []);

  const groups = new Map<string, Session[]>();
  for (const session of shown) {
    const rows = groups.get(session.repository) ?? [];
    rows.push(session);
    groups.set(session.repository, rows);
  }

  // Repository order is the name, so the rail does not reshuffle itself as states change. Inside a
  // group, the session that needs a person comes first — the one ordering that earns its keep.
  const ordered = [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));
  for (const [, rows] of ordered) {
    rows.sort(waitingFirst);
  }

  if (compact) {
    if (sessions.isPending) return null;
    return (
      <nav aria-label={t('work.rail.label')}>
        <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
          {ordered.flatMap(([, rows]) => rows).map((session) => (
            <SessionStripRow
              key={session.id}
              session={session}
              quest={session.quest ? questFor.get(session.quest) : null}
              opening={openings[session.id]}
              taking={taking[session.id]}
              selected={session.id === selected}
              onSelect={onSelect}
            />
          ))}
        </ul>
      </nav>
    );
  }

  if (sessions.isPending) return <div className="px-2.5 py-2"><SkeletonRows rows={5} /></div>;

  // The full empty state only when there is truly nothing — a machine with no live session but
  // four ended ones is not empty, and saying so at the top of a list of records reads as "these
  // records are nothing", which is the misreading this whole change removes.
  if (!shown.length && !ended.length) {
    return (
      <EmptyState
        icon="inbox"
        headline={t('work.rail.empty.headline')}
        body={t('work.rail.empty.body')}
      />
    );
  }

  const searching = query.trim().length > 0;

  return (
    <nav aria-label={t('work.rail.label')}>
      {/* Searching (RAIL1): on the open rail only — the strip has no room to type in. Escape clears it,
          and the rail's own list comes back. */}
      <div className="px-2 pb-1 pt-1.5">
        <label className="flex items-center gap-1.5 rounded-control border border-line-strong bg-raised px-2 py-1 text-ink-faint focus-within:border-accent">
          <Icon name="search" size={12} className="shrink-0" />
          <input
            type="search"
            value={query}
            aria-label={t('work.rail.search.label')}
            placeholder={t('work.rail.search.placeholder')}
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={(event) => { if (event.key === 'Escape') { event.preventDefault(); setQuery(''); } }}
            className="min-w-0 flex-1 bg-transparent text-small text-ink outline-none placeholder:text-ink-faint"
          />
        </label>
      </div>

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

      {!searching && !shown.length && (
        <p className="px-3 py-2 text-small text-ink-faint">{t('work.rail.empty.headline')}</p>
      )}
      {!searching && ordered.map(([repository, rows]) => {
        // A parked session holds its working tree, and a parked INTAKE holds nothing: it has asked
        // and ended, and the room's lock is the process (INT4b), so it claims no busy (INT4g).
        const holding = rows.find((session) => SESSION_ACTIVE.has(session.state)
          && !(isIntake(session) && session.state === 'awaiting-person'));
        const registration = registered.get(repository);
        return (
          <RepositoryGroup
            key={repository}
            repository={repository}
            // Ask Daoris's records are kept in `daoris:help`, which is no repository and no name (HELP1a).
            label={repository === HELP_REPOSITORY ? t('help.title') : undefined}
            count={rows.length}
            // The driver answers only where a shell is attached (D46 §6); with no answer these stay
            // undefined, and the header asserts nothing rather than reading silence as "no".
            drivable={driver.data && driver.data.drivable.includes(repository)}
            held={driver.data && driver.data.holds.includes(repository)}
            busy={holding ? (ownTree(holding, registration?.root) ?? true) : null}
            adopted={registration?.adopted}
            // A root is answered only to a caller on the machine that holds it (D48 §7), so the
            // question is only ASKED where a driver answered — otherwise every registration would
            // look like a teammate's, which is a claim rather than an absence.
            hasCheckout={driver.data && registration ? Boolean(registration.root) : undefined}
          >
            {rows.map(row)}
          </RepositoryGroup>
        );
      })}

      {/* 🔴 The ended sessions — the driver's record of what it actually did. No repository group
          headers here: those carry live facts (drivable, held, which tree is busy) and an ended
          session has none to state; the row's own derived title already names its repository.
          Newest first, capped, and the remainder COUNTED rather than silently cut. */}
      {!live && !searching && ended.length > 0 && (
        <section aria-label={t('work.rail.ended')} className="mt-2 border-t border-line pt-2">
          <h3 className="px-3 py-1 text-meta font-semibold uppercase tracking-wide text-ink-faint">
            {t('work.rail.ended')}
          </h3>
          {ended.map(row)}
          {hiddenEnded > 0 && (
            <p className="px-3 py-1.5 text-small text-ink-faint">
              {t('work.rail.endedMore', { count: hiddenEnded })}
            </p>
          )}
        </section>
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
  const current = settled.trim() === typed && typed.length >= 2;
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
              <li key={id}>
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
      {!asking && named.length === 0 && found.size === 0 && (current || typed.length < 2) && (
        <p className="m-0 px-3 py-1.5 text-small text-ink-faint">{t('work.rail.search.none', { query: typed })}</p>
      )}
      {current && cut && <p className="m-0 px-3 py-1.5 text-meta text-ink-faint">{t('work.rail.search.cut')}</p>}
    </div>
  );
}
