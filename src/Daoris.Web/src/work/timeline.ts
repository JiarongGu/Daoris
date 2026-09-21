import type { Quest, Session, SessionState } from '../api';

// The observed audit layer (design §3), derived from the record and nothing else.

/**
 * One thing that was observed about a session.
 *
 * @remarks
 * **Observed, never inferred, and never parsed out of the stream.** Step-parsing was rejected by
 * name (D52): the only way to get structured progress from a harness is to read another program's
 * stdout, which turns the adapter seam into a screen-scraper that breaks the first time a harness
 * rewords a line (D23/D24). What Daoris actually has is process lifetime, the quest's transitions
 * and what landed in git — which is thin, true, and the whole point.
 *
 * **The record carries no event log**, so this is what the record's own fields can honestly say: it
 * opened, it reached a state, its quest moved, and this is what came out. When the record grows a
 * history, this function is the one place that changes.
 */
export type TimelineEvent =
  | { kind: 'opened'; at: string }
  | { kind: 'state'; at: string; state: SessionState; note?: string | null }
  | { kind: 'quest'; at: string; status: Quest['status']; note?: string | null }
  | { kind: 'evidence'; at: string; text: string; commits: Commit[] };

export type Commit = { sha: string; subject: string };

/** A `git log --oneline` line. Anchored, so a wrapped body line is not mistaken for a commit. */
const COMMIT_LINE = /^([0-9a-f]{7,40}) +(\S.*)$/i;

/**
 * Split an evidence bundle into the driver's own sentence and the commits inside it.
 *
 * @remarks
 * **No phrase is matched, deliberately.** The evidence string is built on the driving machine
 * (`WorkingTree.CommitsSinceAsync`) and reaches here as data — so this reads its *shape*, not its
 * words: anything that looks like a `git log --oneline` line is a commit, and everything else is
 * the driver's sentence, rendered verbatim like every other system sentence. That is what keeps two
 * languages from having to agree on a literal, and what makes a reworded header harmless rather
 * than a silently empty list.
 */
export function readEvidence(evidence: string): { text: string; commits: Commit[] } {
  const commits: Commit[] = [];
  const rest: string[] = [];

  for (const line of evidence.split('\n')) {
    const match = COMMIT_LINE.exec(line.trim());
    if (match) commits.push({ sha: match[1], subject: match[2] });
    else if (line.trim()) rest.push(line.trim());
  }

  return { text: rest.join('\n'), commits };
}

/**
 * What is known to have happened to this session, oldest first.
 *
 * @remarks
 * A session that has only just been queued has exactly one entry, and that is correct rather than
 * incomplete: the alternative is inventing the steps nobody observed.
 *
 * The quest's transition appears only when it moved **after** the session opened — before that it
 * is the quest's own history, which the quest view already holds, and putting it here would make
 * every session's timeline start with news that predates it.
 */
export function sessionTimeline(session: Session, quest?: Quest | null): TimelineEvent[] {
  const events: TimelineEvent[] = [{ kind: 'opened', at: session.created }];

  if (session.updated !== session.created) {
    events.push({ kind: 'state', at: session.updated, state: session.state, note: session.note });
  }

  if (quest && quest.updated > session.created) {
    events.push({ kind: 'quest', at: quest.updated, status: quest.status, note: quest.note });
  }

  if (session.evidence) {
    const { text, commits } = readEvidence(session.evidence);
    events.push({ kind: 'evidence', at: session.updated, text, commits });
  }

  // Stable by design: two things stamped at the same instant keep the order above, which is the
  // order they happen in — a state moves, and the evidence is what that move carried.
  return events
    .map((event, index) => ({ event, index }))
    .sort((a, b) => a.event.at.localeCompare(b.event.at) || a.index - b.index)
    .map(({ event }) => event);
}
