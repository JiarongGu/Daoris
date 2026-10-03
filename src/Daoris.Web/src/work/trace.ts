import type { InstructionAccount } from './conversation';

/**
 * How a session or a quest came to be (TRACE1b, D143, D50): `daoris-driver trace`'s read, as the driver's `TRACE` route
 * answers it. Each link names the store it was read from, and a link nothing keeps carries why, as a code this page words
 * (`work.trace.json`). The driver declares every code (`Trace.Chain.cs`), and `trace.test.ts` holds both catalogues to it.
 *
 * @remarks
 * **No machine path is in it** (D47 §4): a tree is its folder's name, a file under the home is named by its store, and what a
 * failed read said is the terminal's alone. The person's words, a requirement's quote and a session's answer are someone's
 * words, shown as written (D142); everything Daoris says is a code.
 */

/** The kinds a trace starts from on a page. The terminal also starts from a commit. */
export type TraceKind = 'session' | 'quest';

/** The route's answer: the chain, or none where nothing names the id; and each store that did not answer. */
export type TraceAnswer = { chain: TraceChain | null; unread: TraceUnread[] };

export type TraceUnread = { store: string };

export type TraceChain = {
  kind: string;
  id: string;
  unread: TraceUnread[];
  links: TraceLink[];
};

export type TraceLink = {
  kind: 'ask' | 'quest' | 'session' | 'unrecorded' | string;
  ask?: TraceAskLink | null;
  quest?: TraceQuestLink | null;
  session?: TraceSessionLink | null;
  unrecorded?: { session: string; branches: TraceBranch[] } | null;
};

export type TraceNamedBy = { kind: string; id?: string | null };

export type TraceState = { state: string; limit?: boolean; interrupted?: boolean; answered?: boolean };

export type TraceAgent = { adapter?: string | null; harness?: string | null; account?: string | null; teammate?: boolean };

export type TraceWord = { kind: string; text: string; at: string; session?: string | null; quest?: string | null };

export type TraceGoAhead = {
  number: number;
  kind: string;
  on: string;
  act: string;
  answer?: { approved: boolean; words?: string | null; at: string } | null;
  firstAskedBy?: string | null;
  firstAskedAt?: string | null;
  near?: number | null;
};

export type TraceAskLink = {
  id: string;
  source: string;
  missing?: string | null;
  namedBy?: TraceNamedBy | null;
  workspace?: string | null;
  state?: string | null;
  tier?: string | null;
  intake?: { session: string; state?: TraceState | null; agent?: TraceAgent | null } | null;
  quests: string[];
  note?: string | null;
  sentence?: string | null;
  /** Null where the service answers none: a host from before they were kept, which is not the same as none said. */
  words?: TraceWord[] | null;
  wordsKeptFrom?: string | null;
  /** Null where the service answers none: a host from before them. */
  goAheads?: TraceGoAhead[] | null;
};

export type TraceRequirement = {
  number: number;
  quote: string;
  check: string;
  answer: string;
  met?: string | null;
  departed?: string | null;
  on?: string | null;
};

export type TraceQuestLink = {
  id: string;
  source: string;
  missing?: string | null;
  namedBy?: TraceNamedBy | null;
  address?: string | null;
  title?: string | null;
  status?: string | null;
  filed?: string | null;
  moved?: string | null;
  ask?: string | null;
  from?: string | null;
  parent?: string | null;
  publishedBy?: string | null;
  awaits?: string | null;
  held?: boolean;
  accepted?: string | null;
  note?: string | null;
  requirements: TraceRequirement[];
  then: { to: string; title: string }[];
  /** Null where the session records could not be read. */
  records?: { id: string; state: string }[] | null;
};

export type TraceBefore = {
  first: boolean;
  session?: string | null;
  state?: string | null;
  ended?: boolean;
  at?: string | null;
  tree?: string | null;
};

export type TraceInstruction = { seq: number; chars: number; kept?: number | null; account?: InstructionAccount | null };

export type TraceBranch = {
  branch: string;
  tip: string;
  at: string;
  line?: string | null;
  from?: string | null;
  plugin?: string | null;
  pushed?: boolean;
  pullRequest?: string | null;
  gone?: string | null;
  removedAs?: string | null;
  removedOn?: string | null;
  /** Who accepted it (LAND2b): `person` or `auto`; absent for a landing recorded before it was kept. */
  acceptedBy?: string | null;
  /** The landing rule it was made under, as it stood then: absent for one recorded before it was kept. */
  rule?: { plugin?: string | null; autoAccept: boolean; source: string } | null;
};

/** A session's entry on the due list (LAND2b): when it became due, when a try closed it, and each try by its code. */
export type TraceDue = {
  since: string;
  source: string;
  closed?: string | null;
  tries: { at: string; code: string; branch?: string | null; commits?: number | null; uncommitted?: number | null; tip?: string | null }[];
};

export type TraceSessionLink = {
  id: string;
  source: string;
  kind: string;
  state: TraceState;
  opened?: string | null;
  moved?: string | null;
  agent: TraceAgent;
  teammate?: boolean;
  /** Its tree's folder name, never its path. */
  tree?: string | null;
  baseCommit?: string | null;
  quest?: string | null;
  ask?: string | null;
  before?: TraceBefore | null;
  took?: boolean;
  note?: string | null;
  answer?: string | null;
  evidence?: { said: string; lines: string[]; commits: { sha: string; line: string }[] } | null;
  events?: {
    source: string;
    missing?: string | null;
    starts: string[];
    instructions: TraceInstruction[];
    accepted: { at: string; said: string }[];
  } | null;
  rules?: {
    source: string;
    missing?: string | null;
    allowed: number;
    asked: number;
    denied: number;
    hard: number;
    guarded: boolean;
  } | null;
  landing?: { source: string; missing?: string | null; branches: TraceBranch[]; due?: TraceDue | null } | null;
  stood?: {
    missing?: string | null;
    standing?: { repository: string; state: string; source: string; at?: string | null; says?: string | null } | null;
    goAheads: { number: number; state: string; at?: string | null; mine?: boolean }[];
    words?: { ask: string; before: number; of: number } | null;
  } | null;
};

/** The chain as one line, story first: the ask it came from, the quests it reached, how many sessions worked them. */
export type TraceStory = { asks: string[]; senders: string[]; quests: string[]; sessions: number };

/** The chain's story, for the folded line: each ask and quest in the order it is read, and the sessions counted. */
export function traceStory(chain: TraceChain): TraceStory {
  const story: TraceStory = { asks: [], senders: [], quests: [], sessions: 0 };
  for (const link of chain.links) {
    if (link.ask) story.asks.push(link.ask.id);
    if (link.quest) {
      story.quests.push(link.quest.id);
      if (!link.quest.missing && !link.quest.ask && link.quest.from) story.senders.push(link.quest.from);
    }
    if (link.session || link.unrecorded) story.sessions += 1;
  }
  return story;
}

/** A commit's first seven digits, as git abbreviates one. */
export const short = (sha: string) => sha.slice(0, 7);
