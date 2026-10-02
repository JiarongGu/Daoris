import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { Button, EmptyState } from '../ui';
import type { SessionActId } from './acts';
import {
  cutEnded, ENDED_SHOWN, type SessionArrangement, type SessionGrouping, sessionsByRepository, sessionsByState, stripSessions,
} from './groups';
import { isHelp, isIntake } from './identity';
import { ListGroup } from './ListPane';
import { RepositoryGroup } from './RepositoryGroup';
import { SessionRow, SessionStripRow, type SessionWhere } from './SessionRow';

/** What one session's row is drawn from: its record, and what the rail above holds of it. */
export type SessionRowFacts = {
  session: Session;
  /** The quest it serves, where the list holds it. */
  quest?: Quest | null;
  /** What the person first said in it (RAIL1). */
  opening?: string | null;
  /** Whether a turn is in flight, as the driver says (UX5 U17). */
  taking?: boolean;
  /** When its last turn ended here (RAIL2). */
  lastTurn?: string | null;
  /** Its repository's checkout, to tell a tree of its own from the root. */
  root?: string | null;
  /** Where its work is now (LOOK2b). */
  where?: SessionWhere | null;
};

/** A repository group's facts by repository (`RepositoryGroup`): what the driver and the registry answer of it. */
export type RepositoryFacts = {
  label?: string;
  drivable?: boolean;
  held?: boolean;
  busy?: string | true | null;
  adopted?: boolean;
  hasCheckout?: boolean;
};

/** A row's choice and its menu, the rail's (RAIL1; SESSUX1d, D126 §3.1): each told the session it was pressed for. */
type RowActs = {
  onSelect?: (id: string) => void;
  /** The acts the one rule offers each session (`offeredActs`), in their order; absent, the rows have no menu. */
  actsFor?: (session: Session) => readonly SessionActId[];
  onAct?: (act: SessionActId, id: string) => void;
};

/**
 * **Sessions' list** (SESSUX1c, D126 §2.1, §4): by state, the default, in the groups the driver's one reader answers,
 * or by repository, the arrangement it had before.
 *
 * @remarks
 * **A molecule: every state is reached by its props** (components plan §2). The rail above it holds the queries and
 * hands it the records, the reader's answer and what each row needs; this file only draws them.
 *
 * - **By state** (§4.2): *Waiting on you*, *To review*, *Working*, *Resumes later* and *Ended*, each a `ListGroup` with
 *   its count, in the reader's order, and *Archived* last where it is shown. No group header names a repository, so a
 *   row's line does, and the headers carry no repository facts (*drives here*, *held*, *busy*): those belong to a
 *   repository, on the view by repository and on Repositories' page.
 * - **By repository** (§4.3): a group per repository with its facts, *waiting on you* first and *parked* with it, and
 *   *Ended* beneath. Live (the monitor's), it lists only what is running (UX5 U70).
 * - **Ended** shows twelve and then *Show N more* (§4.4), and never cuts the attended session out of its group.
 * - **Archived** (SESSUX1e, §5.2) is drawn while *Show archived* is ticked, and says so where nothing is archived. An
 *   archived row under no Archived heading (by repository) says it is archived on its line.
 */
export function SessionList({
  arrangement, rows, groupings, selected = null, archived = false, live = false, repositoryFacts, onSelect, actsFor, onAct,
}: RowActs & {
  arrangement: SessionArrangement;
  /** Every session the list holds, in the records' order. */
  rows: readonly SessionRowFacts[];
  /** The reader's answer, in its order; absent where none answered, and then each record speaks alone. */
  groupings?: readonly SessionGrouping[];
  /** The attended session, held by the frame. */
  selected?: string | null;
  /** Whether archived sessions are shown (§4.1). */
  archived?: boolean;
  /** The present tense only (the monitor's, UX5 U70): no ended section. By repository. */
  live?: boolean;
  /** Each repository group's facts, by repository; absent, the groups assert none. */
  repositoryFacts?: (repository: string, sessions: readonly Session[]) => RepositoryFacts;
}) {
  const { t } = useTranslation();
  const [allEnded, setAllEnded] = useState(false);
  const factsOf = new Map(rows.map((row): [string, SessionRowFacts] => [row.session.id, row]));
  const placed = new Map((groupings ?? []).map((row): [string, SessionGrouping] => [row.session, row]));
  const sessions = rows.map((row) => row.session);

  // A row's repository on its line, where no group header says it: Ask Daoris's by its name, unless that is already its
  // title, and none for an intake, whose title names its ask.
  const placeOf = (facts: SessionRowFacts) => {
    if (isIntake(facts.session)) return null;
    if (isHelp(facts.session)) return facts.opening ? t('help.title') : null;
    return facts.session.repository;
  };

  // `underArchived`: the row is drawn under the Archived heading, which already says what its line would (§4.5).
  const row = (session: Session, byState: boolean, underArchived = false) => {
    const facts = factsOf.get(session.id) ?? { session };
    const grouping = live ? null : placed.get(session.id);
    return (
      <SessionRow
        key={session.id}
        {...facts}
        grouping={grouping}
        place={byState ? placeOf(facts) : null}
        archived={Boolean(grouping?.archived) && !underArchived}
        selected={session.id === selected}
        onSelect={onSelect}
        acts={actsFor?.(session)}
        onAct={onAct}
      />
    );
  };

  // The rest of a long Ended group, on a press (§4.4): counted, never silently cut.
  const ended = (all: readonly Session[], byState: boolean, underArchived = false): ReactNode => {
    const { rows: shown, hidden } = cutEnded(all, { limit: ENDED_SHOWN, open: allEnded, keep: selected });
    return (
      <>
        {shown.map((session) => row(session, byState, underArchived))}
        {hidden > 0 && (
          <li className="px-1.5 py-1">
            <Button variant="ghost" onClick={() => setAllEnded(true)} className="text-small">
              {t('work.list.showMore', { count: hidden })}
            </Button>
          </li>
        )}
      </>
    );
  };

  if (!rows.length) {
    return <EmptyState icon="inbox" headline={t('work.rail.empty.headline')} body={t('work.rail.empty.body')} />;
  }

  if (arrangement === 'state') {
    const groups = sessionsByState(sessions, groupings, { selected, archived });
    return (
      <>
        {groups.map(({ group, sessions: members }) => (
          <ListGroup key={group} title={t(`work.group.${group}`, { count: members.length })}>
            {group === 'ended' || group === 'archived'
              ? ended(members, true, group === 'archived')
              : members.map((session) => row(session, true))}
          </ListGroup>
        ))}
        {/* Shown and empty (SESSUX1e): the group the tick asked for, saying it holds nothing, rather than no answer. Not a
            row, so the list's arrows pass it by. */}
        {archived && !groups.some(({ group }) => group === 'archived') && (
          <ListGroup title={t('work.group.archived', { count: 0 })}>
            <li className="px-2.5 pb-2 text-small text-ink-faint">{t('work.archived.none')}</li>
          </ListGroup>
        )}
      </>
    );
  }

  // By repository: the live sessions, grouped by the repository each runs in; the monitor's, without the parked.
  const { live: groups, ended: rest } = sessionsByRepository(sessions, live ? undefined : groupings, { selected, archived });
  return (
    <>
      {!groups.length && <p className="px-3 py-2 text-small text-ink-faint">{t('work.rail.empty.headline')}</p>}
      {groups.map(({ repository, sessions: members }) => (
        <RepositoryGroup key={repository} repository={repository} count={members.length} {...repositoryFacts?.(repository, members)}>
          {members.map((session) => row(session, false))}
        </RepositoryGroup>
      ))}

      {/* The ended sessions — the driver's record of what it actually did. No repository group headers here: those
          carry live facts (drivable, held, which tree is busy) and an ended session has none to state; the row's own
          derived title already names what it was for. Newest first, the rest a press away. */}
      {!live && rest.length > 0 && (
        <section aria-label={t('work.rail.ended')} className="mt-2 border-t border-line pt-2">
          <h3 className="px-3 py-1 text-meta font-semibold uppercase tracking-wide text-ink-faint">{t('work.rail.ended')}</h3>
          <ul className="m-0 list-none p-0">{ended(rest, false)}</ul>
        </section>
      )}
    </>
  );
}

/**
 * *Archive what ended…*'s first press (SESSUX1e, D126 §5.3): under the list's header, what the second press would
 * archive and what stays because it needs the person, listed before anything is archived, as the clean-up lists (D88).
 *
 * @remarks
 * **The second press archives what the first listed.** A list is a fact about a moment, and the reader answers again on
 * every tick, so what this said when it opened is held and sent, never a later answer's list; the host judges each
 * again as it goes. Nothing to archive is said, with no press that would archive nothing (D119 §3.2).
 */
export function ArchiveEndedAsk({ going, kept, busy = false, onArchive, onCancel }: {
  /** The sessions it would archive, as `endedToArchive` lists them. */
  going: readonly string[];
  /** How many stay under *Waiting on you* and *To review*. */
  kept: { you: number; review: number };
  /** An archive on its way: the presses wait for it. */
  busy?: boolean;
  onArchive: (ids: readonly string[]) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const [listed] = useState(() => ({ going: [...going], kept: { ...kept } }));
  const count = listed.going.length;
  const { you, review } = listed.kept;
  const takes = count > 0 ? t('work.archive.ask', { count }) : t('work.archive.none');
  const keeps = you > 0 && review > 0 ? t('work.archive.keptBoth', { you, review })
    : you > 0 ? t('work.archive.keptYou', { count: you })
      : review > 0 ? t('work.archive.keptReview', { count: review })
        : null;

  return (
    <div
      role="group"
      aria-label={t('work.list.archiveEnded')}
      className="mx-2 mb-1 mt-1.5 grid gap-2 rounded-control border border-line bg-sunken px-2.5 py-2"
    >
      <p className="m-0 text-small text-ink-soft">{keeps ? t('work.archive.join', { first: takes, second: keeps }) : takes}</p>
      <div className="flex flex-wrap gap-2">
        {count > 0 && (
          <Button variant="primary" disabled={busy} onClick={() => onArchive(listed.going)}>
            {t('work.list.archiveMeanIt', { count })}
          </Button>
        )}
        <Button variant="ghost" disabled={busy} onClick={onCancel}>{t(count > 0 ? 'common.cancel' : 'common.close')}</Button>
      </div>
    </div>
  );
}

/**
 * Sessions' list closed to its strip (FRAME6, D126 §2.5): what waits on the person first, then what runs, in the open
 * list's order, each its initial and its mark, named in words. What ended is the open list's.
 */
export function SessionStrip({ arrangement, rows, groupings, selected = null, archived = false, live = false, label, onSelect }: {
  arrangement: SessionArrangement;
  rows: readonly SessionRowFacts[];
  groupings?: readonly SessionGrouping[];
  selected?: string | null;
  archived?: boolean;
  /** The monitor's: what is running, by repository, and no parked row with no tile to go to. */
  live?: boolean;
  /** The strip's name: the list's. */
  label: string;
  onSelect?: (id: string) => void;
}) {
  const factsOf = new Map(rows.map((row): [string, SessionRowFacts] => [row.session.id, row]));
  const placed = new Map((groupings ?? []).map((row): [string, SessionGrouping] => [row.session, row]));
  const shown = stripSessions(rows.map((row) => row.session), live ? undefined : groupings, { arrangement, selected, archived });

  return (
    <nav aria-label={label}>
      <ul className="m-0 grid list-none justify-items-center gap-1 px-0 py-1.5">
        {shown.map((session) => {
          const facts = factsOf.get(session.id) ?? { session };
          return (
            <SessionStripRow
              key={session.id}
              session={session}
              quest={facts.quest}
              opening={facts.opening}
              taking={facts.taking}
              grouping={live ? null : placed.get(session.id)}
              selected={session.id === selected}
              onSelect={onSelect}
            />
          );
        })}
      </ul>
    </nav>
  );
}
