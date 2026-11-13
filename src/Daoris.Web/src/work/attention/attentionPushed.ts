import type { GoAhead, SetUpRef } from '../../data/api';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import type { Consideration, TrustHold } from '../../data/signals';
import type { SessionGrouping } from '../groups';
import type { AttentionKind, SecondCheckFlag } from './AttentionRow';
import type { PushedAccountFacts } from './accountWords';
import type { GraphEnd, HeldAttempt, MilestoneEntry, UnreadableRecord } from '../head/milestone';
import type { OpinionWait } from '../gates/opinion';
import type { PermissionWait } from '../waits/PermissionAsks';
import type { AnswerWait } from '../waits/AnswerAlone';
import type { NoteRecord } from '../noteLines';

/**
 * A stale go-ahead's mark, as .NET judges it: since when nothing on its ask could still use its answer, the last thing on
 * its ask moving; null where no moment reads.
 */
export type StaleSince = { since: string | null };

/**
 * A quiet row's watch: the minutes since its session last spoke, whether something it runs keeps the machine
 * busy (the *busy, nothing heard* row, offered no end), whether it is an intake, and whether the person's words wait to reach it.
 */
export type QuietWait = {
  minutes: number; busy: boolean; intake: boolean; words: boolean;
  /** It waits for its repository's heavy-run slot behind a holder: the holder's session, or empty for a run from a terminal. */
  heavy?: string | null;
};

/**
 * One row of *What needs you* as the shell pushes it: who needs the person is judged in .NET
 * (`NeedsAPerson`, `AccountAttention`), and the page orders and words its rows. Each carries its kind, its id, since when it has
 * waited (null where nothing says), the values its words are made of, and the workspace of the record it is judged from where a
 * window's scope filters it. What it is called is named here from the records the page holds (`attentionWorded`).
 */
export type PushedAttention = {
  kind: AttentionKind;
  id: string;
  since: string | null;
  /**
   * The obligation it is, by its exchange's id where that is not its kind and id: a go-ahead's `go-ahead:<ask>/<n>`, which
   * the park of each session that asked it waits on too. `obligationOf` reads it.
   */
  obligation?: string;
  /** The record's workspace, for a row judged from the service's sessions, quests or asks: a scoped window shows its own. */
  workspace?: string;
  /** Where it waits, where that is a value: a repository, a circle, an agent's product; absent or null where the page words it. */
  where?: string | null;
  /** A parked session's or a parked intake's note, its parts beside its English. */
  note?: NoteRecord;
  /** Authored words shown as written: a refused receiver's sentence, a go-ahead's why, a widening's why. */
  detail?: string | null;
  publishTo?: string[];
  choices?: string[];
  ask?: string;
  /** A `plan-question`'s question: what it is, what yes does, what waits on it and the default; the ask's page answers it. */
  question?: { key: string; kind: string; text: string; yes: string; blocks: string; default: string };
  number?: number;
  /** A go-ahead's act, which its title says. */
  goAhead?: Pick<GoAhead, 'kind' | 'on' | 'act'>;
  /**
   * A go-ahead nothing on its ask could still use: no session on the ask runs or waits, and no quest of it is open. Since
   * the last thing on its ask moved, which its line says as its age; absent from a go-ahead something still waits for, and from a shell
   * older than the field.
   */
  stale?: StaleSince;
  session?: string | null;
  /** A held done's first departed requirement, its reason, and how many it departed from. */
  departed?: { count: number; number: number; reason: string } | null;
  setUp?: SetUpRef;
  local?: boolean;
  /** A set-up's environment and what its newest set-up showed. */
  environment?: string;
  shows?: string | null;
  opinion?: OpinionWait;
  /** Work to review's place in Sessions' list, which its line says. */
  placed?: SessionGrouping;
  /** A parked quest's verdict, which says why it sits. */
  sitting?: Consideration;
  trust?: TrustHold;
  quest?: string;
  proposal?: RuleProposal;
  /** A title that is a value: a helper's question's first line, a hand-over ask's note's, a unit's branch or its tree's folder. */
  title?: string;
  /** The agent an ask to hand a session over goes to, as the driver's look named it. */
  agent?: string | null;
  /** Whether the session that asked to be handed over stands still on it, its turn no longer running. */
  stands?: boolean;
  permission?: PermissionWait;
  /** A third answer alone held for the person: its wait whole, the agent that answered among it. */
  answer?: AnswerWait;
  /**
   * A unit only the person's press settles: whether its owner is unknown, and the entries it is blocked on, each with
   * the code the page words it by and its facts, where the driver gave one.
   */
  blocked?: { unknown: boolean; entries: Pick<MilestoneEntry, 'kind' | 'resource' | 'detail' | 'code' | 'facts'>[] };
  held?: HeldAttempt;
  /** A node record that does not read: its file, the unit it names, and the driver's sentence. */
  unreadable?: UnreadableRecord;
  /** A closure graph closed only in part whose close node's stop is offered, its end whole. */
  ended?: GraphEnd;
  /** A start waiting on accounts that holds an intake, or quests in several repositories: `where` is its workspace. */
  wholeWorkspace?: boolean;
  /** What a start waiting on accounts holds, which its title says. */
  holding?: { quests: string[]; asks: string[] };
  /** When the read that found the account signed out happened. */
  read?: string | null;
  account?: PushedAccountFacts;
  /** A press a terminal sent that waits for the window's Confirm: {@link PushedConfirmation}. */
  confirmation?: PushedConfirmation;
  /**
   * A volume still below its free-space floor once the floor's acts ran: where it is mounted, its free space, size and floor in
   * bytes as read when the row is said, what the floor freed in this stretch, and what *Free up space…* could free (null where none is read).
   */
  space?: { root: string | null; free: number; total: number; floor: number | null; freed: number; could?: number | null };
  /** The old records every part's census holds while one has not ended, and their count by part. */
  convert?: { records: number; parts?: Record<string, number> };
  /** A git remote held because it could not authenticate: one row per held group, as .NET groups them. */
  git?: GitHeld;
  /** A git go-ahead a door holds while its act waits: its repository, remote, host, class and session, and whether its door has gone. */
  gitAsk?: GitAskWait;
  secondCheck?: SecondCheckFlag;
  /** A session the watcher brings to the person: its watch, which its words and its presses read. */
  quiet?: QuietWait;
  /** A run held at a declined request: its `work.requests` node, the quest declined and where it went. */
  declined?: DeclinedRequest;
  /** A run waiting on a person's task: its `human` node and the presses it waits for. */
  task?: HumanTaskWait;
  /** What memory waits on the person for a session: ends and replacements to approve, a stopped session's staged rows. */
  memory?: MemoryWait;
  /** An integration's wait on the owner: which, its line, and the candidate, commit or landing it is of. */
  integration?: IntegrationWait;
};

/** What memory waits on the person for: a session's ends, replacements and staged rows, or, with `end`, one end the sweep wrote, which names no session. */
export type MemoryWait = {
  ends: number; replacements: number; staged: number;
  end?: { op: string; repository: string; target: string; revision: number | null; reason: string | null };
};

/**
 * An integration's wait on the owner: a candidate the line's rule held for their decide,
 * with its members' branches and the judge's words; a superseded commit awaiting their answer, by its request and commit; or a landed branch
 * whose integration waits for their card; or a repository whose committed declaration waits for their acceptance, where `tip` is the
 * declaration's blob and `state` says whether none is accepted yet or the accepted one changed. Branches, subjects and words are content,
 * shown as written.
 */
export type IntegrationWait = {
  wait: 'decide' | 'superseded' | 'card' | 'declaration';
  /** A declaration's state against the accepted blob: `unaccepted`, or `changed`, where integration goes on under the last accepted blob. */
  state?: 'unaccepted' | 'changed' | null;
  repository: string;
  line?: string | null;
  branch?: string | null;
  branches?: string[];
  candidate?: string | null;
  request?: string | null;
  commit?: string | null;
  subject?: string | null;
  unit?: string | null;
  tip?: string | null;
  why?: string | null;
};

/**
 * A run held at a request the receiver declined: the `work.requests` node the person's *Pass over…* is said on, the
 * declined quest and the repository it addresses, and the run's newest work quest here (null where it has none), which its door reads.
 */
export type DeclinedRequest = { node: string; quest: string; to: string; work?: string | null; request?: boolean };

/**
 * A run waiting on a person's task: the run and its `human` node the press is said on, the presses it waits for (`done`, or
 * `approve` and `reject`), and the run's newest work quest here (null where it has none), which its door reads.
 */
export type HumanTaskWait = { run: string; node: string; acts: string[]; work?: string | null };

/**
 * A press a terminal sent that waits for the window's Confirm: its route, the terminal as the system names it, and when a
 * window first showed it. Never a value it carries, which its card alone shows. `expires` is null for it (it waits until
 * the person answers), a field kept so the slice and this type stay twins.
 */
export type PushedConfirmation = {
  module: string;
  route: string;
  asker?: { process?: string | null; pid?: number | null } | null;
  expires?: string | null;
  shownAt?: string | null;
};

/**
 * Why a git remote is held: refused, its push refused, its host key unknown, its host key changed, or
 * its repository's own ssh command, which Daoris's ssh door cannot stand in front of on this install.
 */
export type GitHeldClass = 'refused' | 'write-refused' | 'host-unknown' | 'host-changed' | 'own-input' | 'no-identity';

/**
 * A git go-ahead: the door holds one act on a remote whose class is ask, a push or a
 * fetch, until the person allows or refuses it; where its door has gone (`gone`), an Allow is kept for the identical retry. The
 * repository, remote and host are content.
 */
export type GitAskWait = {
  repository?: string | null;
  remote?: string | null;
  host?: string | null;
  class: 'read' | 'write' | (string & {});
  session?: string | null;
  gone: boolean;
};

/** What serves a held group's remote now, as the doors resolve it: an identity, the person's own sign-ins, or nothing. */
export type GitServed = 'identity' | 'own' | 'none';

/**
 * A held group of git remotes: the host, why, what it holds (reads and writes, or writes
 * alone), the remote, the repositories held in this group, and git's or ssh's own last line, already redacted. Its
 * way and what serves it, the identity by label and id where one does; the move's row (`no-identity`) names every host it covers and
 * the repositories whose own configuration names authentication. The host, the remote, the repositories, the label and git's words
 * are content, shown as written.
 */
export type GitHeld = {
  host: string;
  class: GitHeldClass;
  holds: 'all' | 'writes';
  remote: string;
  repositories: string[];
  words: string;
  way?: 'ssh' | 'https' | null;
  served?: GitServed | null;
  identity?: string | null;
  identityId?: string | null;
  /** The host's own page for SSH keys, where Daoris knows it (github.com, gitlab.com, Azure DevOps). */
  keyPage?: string | null;
  hosts?: readonly string[] | null;
  ownInput?: readonly string[] | null;
};

/**
 * The slice's answer composed (`attentionSlice`): the rows in the band's order, which the bell and each place count; the
 * keys of what parked on the person; and the keys of the sessions the driver runs, which the status bar counts and,
 * less the parks, Sessions' running mark reads.
 */
export type AttentionPushed = {
  rows: PushedAttention[];
  sessions: { key: string; workspace?: string | null }[];
  working: { key: string; workspace?: string | null }[];
};
