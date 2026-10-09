import type { FileOpen } from './preview';

/**
 * Where the second opinion's part of a landing's gate stands (XAGENT1f, D155 points 9–10; the second-agent design §8.5), as the
 * driver spells it (`OpinionGateStates`). `none`, `settled`, `anyway`, `myself` and `answered` let the work go, and so does
 * `unavailable` where the rule does not require one; the rest hold it. A newer driver may name another, which the page words as
 * unread, holding, with the driver's sentence beneath.
 */
export type OpinionState =
  | 'none' | 'waits-chain' | 'not-asked' | 'reading' | 'with-session' | 'read-again' | 'disputed' | 'unavailable'
  | 'commits-since' | 'settled' | 'anyway' | 'myself' | 'answered' | 'unread';

const KNOWN: ReadonlySet<string> = new Set<OpinionState>([
  'none', 'waits-chain', 'not-asked', 'reading', 'with-session', 'read-again', 'disputed', 'unavailable', 'commits-since',
  'settled', 'anyway', 'myself', 'answered', 'unread',
]);

/** A state the driver named, as the page words it: one it does not know is `unread`, as the driver's own rule holds it. */
export const opinionState = (state: string | null | undefined): OpinionState =>
  state && KNOWN.has(state) ? state as OpinionState : 'unread';

/**
 * What the second opinion's gate says beside a landing's plan or press, and after each of its presses (XAGENT1f,
 * `DriverModule.Opinion`): its state, whether it holds the work, whether the rule requires one, the reviewer by its product and
 * maker and how it stands to the work (`label`), its session, the findings and the disputes counted, the commits nobody read,
 * why none could be had and until when, the later step of the chain, the person's answer, the token a press sends back to answer
 * what it showed, and the driver's English. Absent where the level asks no opinion, so a landing no rule touches reads as it did.
 */
export type OpinionGate = {
  state: string;
  holds: boolean;
  required?: boolean;
  opinion?: string | null;
  reviewer?: string | null;
  product?: string | null;
  maker?: string | null;
  label?: string | null;
  /** The reviewer's own session, which Sessions lists beside the work it read. */
  reviewing?: string | null;
  findings?: number | null;
  disputes?: number | null;
  since?: number | null;
  code?: string | null;
  until?: string | null;
  later?: string | null;
  person?: string | null;
  /** The token *Accept…* sends back to answer what it showed (§8.2–§8.3); absent where no press answers the state. */
  answers?: string | null;
  says?: string | null;
};

/** The working session's answer to one finding as the host keeps it (§6.4): its own claim. */
export type OpinionAnswer = { said: string; commit?: string | null; evidence?: string | null; why?: string | null };

/** How the driver read that answer as the turn ended (§6.4): what counts, and why it does not count as said. */
export type OpinionCounts = { counts: string; why?: string | null; fix?: string | null };

/** One finding (§6.1), a claim and never a fact (§6.6), with what stands beside it. */
export type OpinionFinding = {
  number: number;
  weight: string;
  where: string;
  claim: string;
  consequence?: string | null;
  reproduce?: string | null;
  sure?: string | null;
  proposal?: string | null;
  beside: {
    answer?: OpinionAnswer | null;
    counts?: OpinionCounts | null;
    /** The recheck's word on a first-pass finding: `stands` or `withdrawn`. */
    rechecked?: string | null;
    disputed?: boolean;
  };
};

/** One pass (§6.2): the first, or the recheck. */
export type OpinionPass = {
  id: string;
  occasion?: string;
  pass?: string;
  state?: string;
  why?: string | null;
  base?: string;
  tip?: string;
  commits?: number;
  minutes?: number | null;
  reviewer?: string;
  product?: string | null;
  maker?: string | null;
  label?: string;
  reviewing?: string;
  handedTo?: string | null;
  read?: string | null;
  limits?: string | null;
  /** Null until it said its opinion. */
  findings?: OpinionFinding[] | null;
};

/** The second opinion as the review draws it (XAGENT1g, `DriverModule.OpinionDetail`; the second-agent design §9). */
export type OpinionDetail = {
  /** `agent`, `person` where the person read it themselves, `none` where no agent could (§10). */
  tier: string;
  /** Whether what was read covers what would land; null where git could not say. */
  covers?: boolean | null;
  passes?: number;
  /** Why the findings went to the person rather than the working session (§6.7), a code. */
  toPerson?: string | null;
  answered?: string | null;
  person?: { said: string; at: string; words?: string | null; door?: string | null } | null;
  first?: OpinionPass | null;
  recheck?: OpinionPass | null;
};

/** One work whose gate waits on the person (XAGENT1g, `OPINION_WAITS`): *What needs you*'s row. */
export type OpinionWait = {
  session: string;
  quest: string;
  repository: string;
  workspace?: string;
  since: string;
  /** The work lands by itself (D145): no press of the person's is coming to answer what is unsettled. */
  auto: boolean;
  opinion: OpinionGate;
};

/** The gate on a landing's plan where it asks anything; null where the level asks no opinion, as a plan from before says. */
export const opinionAsked = (landing: { opinion?: OpinionGate | null } | null | undefined): OpinionGate | null =>
  landing?.opinion && opinionState(landing.opinion.state) !== 'none' ? landing.opinion : null;

/** The gate on a landing's plan where it holds the work. */
export const opinionHolds = (landing: { opinion?: OpinionGate | null } | null | undefined): OpinionGate | null => {
  const gate = opinionAsked(landing);
  return gate?.holds ? gate : null;
};

/**
 * Whether *Accept…* is the press that answers the gate (§8.2–§8.3): a dispute or commits nobody read, drawn with the token the
 * press sends back. Then *Accept…* stands, showing what it answers; any other hold keeps it away, as the landing door would
 * refuse it.
 */
export const acceptAnswers = (gate: OpinionGate | null | undefined): boolean =>
  Boolean(gate?.holds && gate.answers && (gate.state === 'disputed' || gate.state === 'commits-since'));

/** A press at the gate (design §8.5), named as the page draws it. */
export type OpinionPress = 'askNow' | 'stop' | 'tryAgain' | 'sameAgent' | 'askAgain' | 'myself' | 'anyway' | 'sendBack';

/** The presses that ask once under the gate before they do anything: words to give, or a reading to end. */
export const OPINION_ASKS_ONCE: ReadonlySet<OpinionPress> = new Set(['stop', 'myself', 'anyway', 'sendBack']);

/**
 * What each state offers, in the order it shows them (the second-agent design §8.5's table): not asked, *Ask now*, and while the
 * chain's later step works, *Go on anyway…* too; being read, *Stop*; disputed, *Send back…* and *Ask again*, with *Go on anyway…*
 * only where no press is coming (`pressComing`: *Accept…* or D154's *Reviewed* answers it); unavailable and required, *Try again*,
 * *Ask the same agent, fresh*, *I looked myself…* and *Go on anyway…*; commits unread, *Ask again*, and *Go on anyway…* where no
 * press is coming. Being answered and read again offer the sessions' doors alone. One rule, read by the page and the tests.
 */
export function opinionPresses(
  state: OpinionState, { required = false, pressComing = false }: { required?: boolean; pressComing?: boolean } = {},
): OpinionPress[] {
  switch (state) {
    case 'waits-chain': return ['askNow', 'anyway'];
    case 'not-asked': return ['askNow'];
    case 'reading': return ['stop'];
    case 'disputed': return pressComing ? ['sendBack', 'askAgain'] : ['anyway', 'sendBack', 'askAgain'];
    case 'unavailable': return required ? ['tryAgain', 'sameAgent', 'myself', 'anyway'] : [];
    case 'commits-since': return pressComing ? ['askAgain'] : ['anyway', 'askAgain'];
    default: return [];
  }
}

/** The hue a state wears: the person's (open) where it waits on them, taken's while an agent works, done's once settled. */
export function opinionTone(state: OpinionState, { holds = true }: { holds?: boolean } = {}): 'open' | 'taken' | 'done' | 'neutral' {
  switch (state) {
    case 'reading':
    case 'with-session':
    case 'read-again':
    case 'not-asked':
    case 'waits-chain': return 'taken';
    case 'disputed':
    case 'commits-since': return 'open';
    case 'unavailable': return holds ? 'open' : 'neutral';
    case 'settled':
    case 'answered':
    case 'myself': return 'done';
    default: return 'neutral';
  }
}

/** Who read it, as a person reads it: its product, else its adapter, and its maker where declared (`OpinionView.Who`). */
export function reviewerName(gate: Pick<OpinionGate, 'product' | 'reviewer' | 'maker'> | Pick<OpinionPass, 'product' | 'reviewer' | 'maker'>): string {
  const name = gate.product || gate.reviewer || '';
  return gate.maker ? `${name} (${gate.maker})` : name;
}

/**
 * Where a finding is, as a door: a repository-relative path with its line opens the file's preview at that line (PREVIEW1); a
 * commit or `general` is said as it is. Null for what no preview opens.
 */
export function findingPlace(where: string): FileOpen | null {
  const text = where.trim();
  if (!text || text === 'general' || /^[0-9a-f]{7,64}$/i.test(text)) return null;
  const at = /^(.+?):(\d+)(?:-(\d+))?$/.exec(text);
  if (at) {
    const from = Number(at[2]);
    const to = at[3] ? Number(at[3]) : from;
    return from >= 1 ? { path: at[1]!, lines: { from, to: Math.max(from, to) } } : { path: at[1]! };
  }
  return /[\\/]|\.\w+$/.test(text) ? { path: text } : null;
}

/** Each pass's findings in their order, the first pass's then the recheck's own. */
export function findingsOf(detail: OpinionDetail | null | undefined): { pass: 'first' | 'recheck'; finding: OpinionFinding }[] {
  return [
    ...(detail?.first?.findings ?? []).map((finding) => ({ pass: 'first' as const, finding })),
    ...(detail?.recheck?.findings ?? []).map((finding) => ({ pass: 'recheck' as const, finding })),
  ];
}
