import { Fragment, type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { Quest, Session } from '../../data/api';
import { Button, EmptyState } from '../../design/ui';
import type { SessionActId } from '../acts';
import {
  cutEnded, ENDED_SHOWN, type NestedRow, nestServing, type SessionArrangement, type SessionGrouping, sessionsByRepository,
  sessionsByState, stripSessions,
} from '../groups';
import { type EntryFacts, entryOfSession, type SessionEntry, sessionsByAsk } from '../entries';
import type { HeavyPart } from '../heavySlot';
import { isHelp, isIntake, sessionTitle } from '../identity';
import { type Answered, InlineConfirm } from '../InlineConfirm';
import { groupHeading, ListGroup } from '../frame/ListPane';
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
  /** Whose work a second opinion's reviewer reads, by that session's title (XAGENT1g4c). */
  reads?: string | null;
  /** The session it continues, by that session's title, where the rail joined them (MIXAGENT3a-i). */
  continues?: string | null;
  /** The session a helper helps, by that session's title, where the rail joined them (MIXAGENT3a-i). */
  helps?: string | null;
  /** The entry's name where it is not the record's own: the ask's (SESSONE1c, D167). Not drawn by ask, whose heading says it. */
  name?: string | null;
  /** The entry this row stands for (SESSONE1c): how many parts it holds, and all their ids, so any part attended marks the row. */
  entry?: EntryFacts | null;
  /** Its part in its repository's heavy-run slot (HEAVYSLOT1), where it holds it or waits for it. */
  heavy?: HeavyPart | null;
  /** When a line its account's limit cut off goes on by itself, and on which account (LIMITRESUME1d2, `limitWaitLines`' first). */
  limitWait?: string | null;
  /** The title of the session its wait is behind, where the rail holds it (HEAVYSLOT1); absent, its id. */
  heavyBehind?: string | null;
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
 * - **A helper and a reviewer under the work they serve** (MIXAGENT3a-ii, the mixed-agents design §4), in every arrangement:
 *   moved there out of their own group (`nestServing`), so a group counts the rows it draws as its own, and their lines leave
 *   whose work it is to the row above. Where that row is not drawn, or the person must act on them, they keep their place.
 */
export function SessionList({
  arrangement, rows, groupings, selected = null, archived = false, live = false, repositoryFacts, onSelect, actsFor, onAct,
  entries, askNames = {}, unread = {}, askAccept, serving = {},
}: RowActs & {
  /**
   * The session whose work each helper and reviewer serves, by the helper's or reviewer's id, as the rail joined them
   * (MIXAGENT3a-ii): a helper's `serves`, a reviewer's opinion's `working`. Absent, every row is its own.
   */
  serving?: Readonly<Record<string, string>>;
  /** What is unread on each ask's channel, by its id (SESSONE2c, D167), said in the heading *by ask* draws; none where no driver says. */
  unread?: Readonly<Record<string, number>>;
  /**
   * An ask's one Accept under its heading *by ask* (ACCEPTALL1, D167's 2026-10-15 amendment), drawn by the rail, which holds its
   * plan and press; null where the ask offers none. Not a row, so the list's arrows pass it by.
   */
  askAccept?: (ask: string) => ReactNode;
  arrangement: SessionArrangement;
  /** The reader's entries (SESSONE1c, D167): what *by ask* groups by. Absent, the list by ask is the list by state. */
  entries?: readonly SessionEntry[];
  /** Each ask's name by its id (`askName`), for the headings *by ask* draws; an ask the page does not hold is named by its id. */
  askNames?: Readonly<Record<string, string>>;
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

  // `underArchived`: the row is drawn under the Archived heading, which already says what its line would (§4.5). `parent`: the
  // row it is drawn under (MIXAGENT3a-ii), which says whose work it serves and, where it is the same, the repository.
  const row = (session: Session, byState: boolean, underArchived = false, byAsk = false, parent?: Session) => {
    const facts = factsOf.get(session.id) ?? { session };
    const grouping = live ? null : placed.get(session.id);
    return (
      <SessionRow
        key={session.id}
        {...facts}
        reads={parent ? null : facts.reads}
        helps={parent ? null : facts.helps}
        // The ask's heading says whose it is, so each row there is the line of work's own name (its leading part's).
        name={byAsk ? null : facts.name}
        parts={facts.entry?.parts}
        grouping={grouping}
        place={byState && parent?.repository !== session.repository ? placeOf(facts) : null}
        archived={Boolean(grouping?.archived) && !underArchived}
        // The entry is attended while any of its parts is: the strip on its page picks which one (SESSONE1c).
        selected={session.id === selected || Boolean(selected && facts.entry?.ids.includes(selected))}
        onSelect={onSelect}
        acts={actsFor?.(session)}
        onAct={onAct}
      />
    );
  };

  // Each helper and reviewer under the row of the work it serves (MIXAGENT3a-ii): that work's entry's row where entries are
  // answered, and never one the reader says waits on the person or is to review.
  const nest = (drawn: readonly (readonly Session[])[]) => nestServing(drawn, serving, {
    standsFor: (id) => entryOfSession(entries, id)?.leading ?? id,
    keepOwn: (id) => !live && ['you', 'review'].includes(placed.get(id)?.group ?? ''),
  });

  // A row the list draws as its own, then what is drawn under it, a list of its own named for whose work it is: rows of the
  // list all the same, so its arrows move through them.
  const drawRow = ({ session, under }: NestedRow, byState: boolean, underArchived = false, byAsk = false): ReactNode => {
    if (!under.length) return row(session, byState, underArchived, byAsk);
    const facts = factsOf.get(session.id) ?? { session };
    const title = (byAsk ? null : facts.name) || sessionTitle(session, facts.quest, facts.opening);
    return (
      <Fragment key={session.id}>
        {row(session, byState, underArchived, byAsk)}
        <li>
          <ul aria-label={t('work.rail.under', { title })} className="m-0 ml-3 list-none border-l border-line p-0">
            {under.map((child) => row(child, byState, underArchived, byAsk, session))}
          </ul>
        </li>
      </Fragment>
    );
  };

  // The rest of a long Ended group, on a press (§4.4): counted, never silently cut, and never the row the attended one is under.
  const ended = (all: readonly NestedRow[], byState: boolean, underArchived = false): ReactNode => {
    const keep = all.find((each) => each.under.some((child) => child.id === selected))?.id ?? selected;
    const { rows: shown, hidden } = cutEnded(all, { limit: ENDED_SHOWN, open: allEnded, keep });
    return (
      <>
        {shown.map((each) => drawRow(each, byState, underArchived))}
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

  // By ask (D167 point 5): each ask a heading, its entries under it with their repositories, what no ask began last.
  if (arrangement === 'ask' && entries && !live) {
    const byAsk = sessionsByAsk(sessions, entries, { selected, archived });
    const nested = nest(byAsk.map((group) => group.sessions));
    const asks = byAsk.map(({ ask }, index) => ({ ask, members: nested[index]! })).filter((group) => group.members.length > 0);
    return (
      <>
        {asks.map(({ ask, members }) => {
          const accept = ask === null ? null : askAccept?.(ask);
          return (
            // What is unread on the ask's channel is said in its heading (SESSONE2c, D167): the person's count, not any session's.
            <ListGroup
              key={ask ?? 'none'}
              title={ask === null
                ? t('work.group.noAsk')
                : `${askNames[ask] ?? `#${ask}`}${unread[ask] ? ` · ${t('asks.channel.unread', { count: unread[ask] })}` : ''}`}
            >
              {accept && <li className="grid justify-items-start gap-1 px-2.5 pb-1.5">{accept}</li>}
              {members.map((each) => drawRow(each, true, false, true))}
            </ListGroup>
          );
        })}
        {asks.length === 0 && <p className="px-3 py-2 text-small text-ink-faint">{t('work.rail.empty.headline')}</p>}
      </>
    );
  }

  if (arrangement !== 'repository') {
    const byState = sessionsByState(sessions, groupings, { selected, archived });
    const nested = nest(byState.map((group) => group.sessions));
    const groups = byState.map(({ group }, index) => ({ group, members: nested[index]! })).filter((group) => group.members.length > 0);
    return (
      <>
        {groups.map(({ group, members }) => (
          // Its heading's id is what a door into the group brings into view (ENTRY1b).
          <ListGroup key={group} id={groupHeading('sessions', group)} title={t(`work.group.${group}`, { count: members.length })}>
            {group === 'ended' || group === 'archived'
              ? ended(members, true, group === 'archived')
              : members.map((each) => drawRow(each, true))}
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
  const byRepository = sessionsByRepository(sessions, live ? undefined : groupings, { selected, archived });
  const nested = nest([...byRepository.live.map((group) => group.sessions), byRepository.ended]);
  const rest = nested.at(-1)!;
  const groups = byRepository.live
    .map(({ repository }, index) => ({ repository, members: nested[index]! }))
    .filter((group) => group.members.length > 0);
  return (
    <>
      {!groups.length && <p className="px-3 py-2 text-small text-ink-faint">{t('work.rail.empty.headline')}</p>}
      {groups.map(({ repository, members }) => (
        // Its facts read every session of this repository it draws, those under another's row among them, as before they moved.
        <RepositoryGroup
          key={repository}
          repository={repository}
          count={members.length}
          {...repositoryFacts?.(repository, members.flatMap((each) => [each.session, ...each.under])
            .filter((session) => session.repository === repository))}
        >
          {members.map((each) => drawRow(each, false))}
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
  /** The second press, sending what the first listed, and how to tell the ask how it ended (UXFIX2b2b). */
  onArchive: (ids: readonly string[], answered: Answered) => void;
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
    // Nothing is destroyed (an archive is shown again from *Show archived*), so the move wears the primary's hue (UXFIX2b2b).
    // Nothing to archive offers only *Close*, as the shared ask does where it has no move.
    <InlineConfirm
      className="mx-2 mb-1 mt-1.5"
      tone="primary"
      label={t('work.list.archiveEnded')}
      says={keeps ? t('work.archive.join', { first: takes, second: keeps }) : takes}
      meanIt={count > 0 ? t('work.list.archiveMeanIt', { count }) : undefined}
      busy={busy}
      onConfirm={(answered) => onArchive(listed.going, answered)}
      onClose={onCancel}
    />
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
