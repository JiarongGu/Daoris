import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../api';
import { Button, EmptyState } from '../ui';
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

/** The row's menu, the rail's (RAIL1): each told the session it was pressed for. */
type RowActs = {
  onSelect?: (id: string) => void;
  onDetach?: (id: string) => void;
  onReview?: (id: string) => void;
  onCopy?: (id: string) => void;
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
 */
export function SessionList({
  arrangement, rows, groupings, selected = null, archived = false, live = false, repositoryFacts, ...acts
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

  const row = (session: Session, byState: boolean) => {
    const facts = factsOf.get(session.id) ?? { session };
    return (
      <SessionRow
        key={session.id}
        {...facts}
        grouping={live ? null : placed.get(session.id)}
        place={byState ? placeOf(facts) : null}
        selected={session.id === selected}
        {...acts}
      />
    );
  };

  // The rest of a long Ended group, on a press (§4.4): counted, never silently cut.
  const ended = (all: readonly Session[], byState: boolean): ReactNode => {
    const { rows: shown, hidden } = cutEnded(all, { limit: ENDED_SHOWN, open: allEnded, keep: selected });
    return (
      <>
        {shown.map((session) => row(session, byState))}
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
            {group === 'ended' || group === 'archived' ? ended(members, true) : members.map((session) => row(session, true))}
          </ListGroup>
        ))}
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
