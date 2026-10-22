// A session's pieces (COAGENT1b, D199; the co-agent design §6.1): another agent's work on one piece, in a clone of its own, as the
// driver's look left it. Pure, so the Pieces section, the head's line and their stories read one table.

/** One piece a session asked, as `SESSION_PARTNERS` answers it, read defensively. */
export type PieceRow = {
  id: string;
  /** The piece as the person wrote it: their words, never translated. */
  piece: string;
  paths: string[];
  /** The agent the ask named. */
  agent: string | null;
  askedBy: string;
  at: string;
  /** The driver's state (`PartnerStates`), `asked` where its look has not judged it yet. */
  state: string;
  /** Why it is refused, held, uncertain or waits (`PartnerWhy`). */
  code: string | null;
  /** How its partner's turn ended (`PartnerEnds`), once it did. */
  end: string | null;
  /** The partner's own record, once it opened. */
  partner: string | null;
  /** The agent it started on. */
  startedOn: string | null;
  /** The one commit Daoris kept of its work, frozen at `ref`. */
  commit: string | null;
  ref: string | null;
  /** The files Daoris read in that commit, and those beyond the piece's paths. */
  files: string[] | null;
  beyond: string[] | null;
};

/** Where a piece stands as the section says it: one of the design's states, by its key under `work.pieces.state`. */
export type PieceShown =
  | 'asked' | 'waits' | 'working' | 'answered' | 'takenIn' | 'notTakenIn' | 'stopped' | 'outOfTime' | 'limited' | 'held' | 'refused'
  | 'uncertain' | 'discarded';

/** The driver's states a piece is working in: its slot reserved, its copy made, its partner at work, its work being kept. */
const WORKING = new Set(['approved', 'preparing', 'started', 'ending']);

/** The ends that are not an answer: its work is kept, and the section says how it ended. */
const ENDS: Readonly<Record<string, PieceShown>> = { stopped: 'stopped', 'out-of-time': 'outOfTime', limited: 'limited' };

/** Where a piece stands, in the section's words (the design §6.1's table): one the page has no word for reads as asked. */
export function shownOf(row: Pick<PieceRow, 'state' | 'end'>): PieceShown {
  if (WORKING.has(row.state)) return 'working';
  switch (row.state) {
    case 'waiting': return 'waits';
    case 'imported': case 'answered': case 'handed': return (row.end && ENDS[row.end]) || 'answered';
    case 'taken-in': return 'takenIn';
    case 'not-taken-in': return 'notTakenIn';
    case 'held': return 'held';
    case 'refused': return 'refused';
    case 'uncertain': return 'uncertain';
    case 'discarded': return 'discarded';
    default: return 'asked';
  }
}

/** Whether a piece's partner is at work, which its *Stop…* ends: started, with its record named. */
export const stoppable = (row: Pick<PieceRow, 'state' | 'partner'>): boolean => row.state === 'started' && Boolean(row.partner);

/**
 * The head's partners (the design §6.1): the agents its pieces went to, each once in the order first asked, by the roster's name,
 * and how many are working. Null where the session asked none.
 */
export function partnersOf(
  rows: readonly PieceRow[], nameOf: (agent: string) => string,
): { agents: string[]; working: number } | null {
  if (rows.length === 0) return null;
  const agents = [...new Set(rows.map((row) => row.startedOn ?? row.agent).filter((agent): agent is string => Boolean(agent)))];
  return { agents: agents.map(nameOf), working: rows.filter((row) => shownOf(row) === 'working').length };
}

const text = (value: unknown): string | null => (typeof value === 'string' && value ? value : null);
const texts = (value: unknown): string[] | null =>
  Array.isArray(value) ? value.filter((each): each is string => typeof each === 'string') : null;

/** The rows an answer holds, read defensively: one with no id or no piece is left out rather than guessed at. */
export function readPieces(answer: unknown): PieceRow[] {
  const rows = (answer as { pieces?: unknown } | null)?.pieces;
  if (!Array.isArray(rows)) return [];
  return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
    .filter((row) => text(row.id) !== null && typeof row.piece === 'string')
    .map((row) => ({
      id: row.id as string,
      piece: row.piece as string,
      paths: texts(row.paths) ?? [],
      agent: text(row.agent),
      askedBy: text(row.askedBy) ?? 'person',
      at: typeof row.at === 'string' ? row.at : '',
      state: text(row.state) ?? 'asked',
      code: text(row.code),
      end: text(row.end),
      partner: text(row.partner),
      startedOn: text(row.startedOn),
      commit: text(row.commit),
      ref: text(row.ref),
      files: texts(row.files),
      beyond: texts(row.beyond),
    }));
}
