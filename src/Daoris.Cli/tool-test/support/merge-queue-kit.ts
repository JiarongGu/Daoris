/**
 * What the merge queue's tests share, the unit tier's (`merge-queue.test.ts`) and the end-to-end tier's (`merge-queue-*.e2e.ts`):
 * the shapes of the queue's records and the tools they import, read as untyped workspace tooling.
 */
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** The repository's root, from this kit's place. */
export const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..');

export interface Signature { test?: string; exception?: string; location?: { method: string; text: string; ordinal: number }; message?: string; gate?: string; step?: string; errors?: string[] }
export type Outcome = { result: 'pass' } | { result: 'fail'; signature: Signature } | { result: 'cannot'; signature: Signature };
export interface Failure { unit?: string; test?: string | undefined; gate: string; signature: Signature; names: 'test' | 'gate' }
interface ContainedIn { line?: boolean; branch?: string; request?: string | undefined; commit?: string }
export interface Member { branch: string; tip: string; request?: string; seq?: number; message?: string; commit?: string; at?: string; containedIn?: ContainedIn; handedBack?: { how: string; at?: string; failure?: string; with?: string[]; interacting?: string[]; paths?: string[]; why?: string } }
export interface Judged { outcome: string; member?: number; with?: number[]; interacting?: number[]; why?: string; said?: string; runs: unknown[]; confirm?: Confirm }
export interface Evidence { conclusive: boolean; doubts: { at: string; why: string }[]; waitedMs?: number | null; ids?: string[] }
export interface Confirm { result: string; why?: string; evidence?: Evidence | null; composition?: { units: string[] | null; digest: string | null } | null; failures?: Failure[]; error?: unknown }
export type Whole = { held: true; ids: string[]; end: () => Evidence } | { held: false; dropped: boolean; why: string; evidence: Evidence };
export interface Candidate { base: string; tip: string; members: Member[]; contained?: Member[]; state: string; reforms: number; recordsOnly: boolean; held?: { on: string; why: string }; run?: { pid: number } }
export interface Queue {
  schema: number; queued: Member[]; previews: Record<string, { tip: string }>; candidate: Candidate | null; stop: unknown; handedBack: Member[]; landed?: Member[]; drained?: Drained;
  forming?: { base: string; members: Member[] } | null; contained?: Member[]; runner?: unknown;
}
type Standing = { landed?: Member; contained?: Member; back?: Member; pending?: string; gone?: boolean };
interface Reservation { runner: string; gate: string; needGb: number; job: string }
interface Admission { schema: number; reservations: Reservation[]; scopedTier: { state: string; since: string } | null; waiting: unknown[] }
export interface Job { state: string; workingGb?: number; committedGb?: number; why?: string }
interface Rule { lane?: string; paths?: string[]; gates: string[] | '*' }
export interface Drained { tip: string; at: string; ended: string[]; left: string[]; probe: string | null; strays: number[] }
/** A confirm run as the scope run's record keeps it. */
interface ConfirmRecord { gate: string; result: string; why: string; composition: { units: string[] | null; digest: string | null } | null; admission: (Evidence & { capacity?: number }) | null; waitedMs: number | null; ms: number | null }
export interface ScopeRun {
  state: string; tip: string; tree: string; members: { branch: string }[]; outcome?: string; judged?: { test: string; outcome: string; member: number | null; why: string | null }[];
  confirms?: ConfirmRecord[]; policy?: { unconfirmed: string; said: string }; passedOnConfirm?: string[]; drained?: Drained;
}
export interface ProcessRow { pid: number; ppid: number; command: string }
interface Trees { ended: string | null; hold: (pid: number) => void; release: (pid: number) => boolean; end: (why: string) => void }
interface Watch { trees: Trees; check: () => boolean; start: () => Watch; stop: () => void }

export const q = await import(
  // @ts-expect-error — untyped workspace tooling; the same seam merge-branch.test.ts documents
  '../../../../tools/merge-queue.mjs') as {
  CANDIDATE_MAX: number;
  RECHECK_MS: number;
  HELD_REFORMS: number;
  MERGE_QUEUE: string;
  FOREGROUND_HEADROOM_GB: number;
  BACKGROUND_SLOT_WAIT_MS: number;
  emptyQueue: () => Queue;
  readQueue: (home: string) => Queue;
  writeQueue: (home: string, queue: Queue) => void;
  notePreview: (queue: Queue, preview: { branch: string; tip: string; base: string; at: string }) => Queue;
  enqueue: (queue: Queue, request: { branch: string; tip: string; message: string | null; at: string; requeue?: boolean }) => { queue?: Queue; entry?: Member; refused?: string };
  messageOf: (entry: Member) => string;
  dropQueued: (queue: Queue, branches: string[], at?: string) => { queue: Queue; dropped: string[]; ended: Candidate | null };
  requestStanding: (queue: Queue, request: { branch: string; tip: string; request?: string }) => Standing;
  drainedRecord: (candidate: Candidate, what: { ended?: string[]; left?: string[]; probe?: string | null; strays?: number[] }, at?: string) => Drained;
  drainedLine: (drained: Drained) => string;
  sameCandidate: (current: Candidate | null, expected: Candidate) => boolean;
  changeCandidate: (queue: Queue, expected: Candidate, change: (queue: Queue) => { queue: Queue }) => { queue?: Queue; obsolete?: boolean };
  nextMembers: (queued: Member[], options?: { max?: number; oneAtATime?: boolean }) => { members: Member[]; rest: Member[] };
  formCandidate: (input: { base: string; members: Member[]; merge: (member: Member, onto: string, formed: Member[]) => Promise<{ commit?: string; conflict?: string[]; contained?: ContainedIn }>; at: string }) => Promise<{ candidate: Candidate | null; setAside: Member[]; contained: Member[] }>;
  IN_THE_LINE: ContainedIn;
  inArrival: (requests: Member[]) => Member[];
  requestsOf: (candidate: Candidate | null, requests?: Member[]) => Member[];
  settleContained: (candidate: Candidate | null, at: string) => { branch: string; tip: string; request?: string; containedIn: ContainedIn; at: string }[];
  recordsOnly: (paths: string[], ruleOf: (path: string) => Rule | null) => boolean;
  resumeCandidate: (candidate: Candidate | null, facts: { mainTip: string; isAncestor: (a: string, b: string) => boolean; runAlive?: (pid?: number) => boolean }) => string;
  promotion: (candidate: Candidate, facts: { mainTip: string; clean: boolean }) => { action: string; why?: string };
  reformCount: (candidate: Candidate, facts: { recordsOnlyMove: boolean }) => { reforms: number; recordsOnly: boolean; heldByDirect: boolean };
  placeOnPrefixes: (seq: Outcome[], signature: Signature) => { outcome: string; member?: number; interacting?: number[]; why?: string };
  judgeFailure: (failure: Failure, context: { members: number; run: (failure: Failure, k: number) => Promise<Outcome>; alone?: (failure: Failure, k: number) => Promise<Outcome>; known?: { expired: boolean; lastSeen: string } | null; baseCommit?: string; confirm?: Confirm | (() => Confirm | Promise<Confirm>) }) => Promise<Judged>;
  memoryStep: (attempts: { died: boolean; peakGb?: number }[], options?: { afterHold?: boolean; needGb?: number }) => { next: string; note?: string };
  candidateOutcome: (judgements: { failure: Failure; judged: Judged }[], members: Member[], policy?: { unconfirmed?: string | undefined }) => {
    action: string; handedBack?: Member[]; retain?: Member[]; inherited?: Failure[]; flakes?: Failure[]; why?: string; on?: string;
    sightings?: { failure: Failure; confirm: Confirm }[]; inconclusive?: Failure[]; passedOnConfirm?: string[];
  };
  confirmStep: (input: { take: (request: { what: string; halted: () => boolean }) => Promise<Whole>; run: () => Promise<unknown>; halted?: () => boolean; what?: string }) => Promise<Confirm>;
  confirmReadings: (confirm: Confirm, failures: Failure[]) => { failure: Failure; confirm: Confirm }[];
  INTEGRATION_POLICY: string;
  INTEGRATION_SIGHTINGS: string;
  readIntegrationPolicy: (root: string) => { unconfirmed: string; said: string };
  sightingOf: (input: { failure: Failure; candidate: { base: string; tip: string; members: Member[] }; confirm: Confirm; policy: string; at: string }) => Record<string, unknown>;
  appendSighting: (root: string, sighting: object) => void;
  readSightings: (root: string) => Record<string, unknown>[];
  recheckDue: (stop: { at: string; rechecks?: { at: string }[] } | null, now: number) => boolean;
  noteRecheck: (stop: { at: string }, recheck: { at: string; passed: boolean; by: string }) => unknown;
  gitBudget: (input: { classes: { name: string; ms?: number; subject?: boolean; failures?: number; knownRed?: boolean }[]; overheadMs?: number; budgetMs?: number; lanes?: number }) => { run: string[]; deferred: string[]; predictedMs: number; overrun: boolean };
  scopedSet: (input: { gates: { name: string; kind: string }[]; reached: Set<string>; standing?: Set<string>; knownRed?: Set<string>; recordsOnly?: boolean }) => { run: { name: string; why: string }[]; kept: { name: string }[]; deferred: { name: string; why: string }[] };
  appendScopeRun: (home: string, entry: object) => void;
  readScopeRuns: (home: string) => object[];
  emptyAdmission: () => Admission;
  admit: (input: { record: Admission; request: { runner: string; gate: string; lane: string; needGb: number; waitingSince?: string }; free: { physicalGb: number; commitGb: number }; readJob: (name: string) => Job; now?: number; betweenTiers?: boolean }) => { admit: boolean; wait: string | null; why: string; record: Admission; said: string[] };
  reserve: (record: Admission, input: { runner: string; run: string; gate: string; lane: string; needGb: number; pid: number; at: string }) => { record: Admission; job: string };
  scopedTier: (record: Admission, state: string | null, at: string) => Admission;
  overdueWaits: (record: Admission, now: number) => string[];
  queueLines: (queue: Queue, options?: { branchTip?: (branch: string) => string | null; now?: number }) => string[];
  runnerStanding: (queue: Queue, pid: number) => { stands: true } | { stands: false; why: string };
  censusTrees: (home: string) => string[];
  orphansUnder: (processes: ProcessRow[], options: { trees: string[]; ignore?: number[] }) => ProcessRow[];
  censusLine: (orphans: ProcessRow[], options?: { limit?: number; width?: number }) => string;
};
export const failures = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../../tools/test-failures.mjs') as { gateSignature: (gate: string, step: string, output: string) => Signature; signatureKey: (signature: Signature) => string };
export const machine = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../../tools/machine-admission.mjs') as {
  admissionFile: (home: string) => string;
  createAdmission: (options: { daorisHome: string; capacity?: number; say?: (line: string) => void }) => { take: (request: { what: string }) => Promise<{ id: string; release: () => void } | null> };
};

export const tool = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../../tools/merge-branch.mjs') as {
  REACH: Rule[];
  ruleFor: (path: string, rules: Rule[], lanes: unknown[]) => Rule | null;
  readLanes: (root: string) => { lanes: unknown[] } | null;
  endTree: (pid: number) => boolean;
  stepTrees: (options?: { end?: (pid: number) => void }) => Trees;
  watchCandidate: (candidate: Candidate, current: () => Candidate | null, trees: Trees, options?: { ms?: number; superseded?: () => string | null; taken?: () => void }) => Watch;
  ownerEnded: (environment?: Record<string, string | undefined>, alive?: (pid: number) => boolean) => string | null;
  prefixRunner: (tree: string, candidate: { base: string; members: Member[] }, plan: { name: string; run: string }[], dir: string, watch?: Watch | null) => (failure: Failure, k: number) => Promise<Outcome>;
  queueHomeRefusal: (facts: { root: string; home: string; tool?: string | null; sameRepository?: boolean; run?: boolean }) => string | null;
  treeBindingRefusal: (facts: { path: string; reported: string | null }) => string | null;
  gateEnvironment: (start?: Record<string, string | undefined>, options?: { pass?: readonly string[] }) => Record<string, string>;
  GATE_REMOVED: readonly (string | RegExp)[];
  waitSaid: (standing: Standing, facts: { branch: string; request?: string; holder?: string | boolean | null }) => { code: number; lines: string[] } | null;
  BESIDE_ENVIRONMENT: Record<string, string>;
  DRAIN_WAIT_MS: number;
};

export const procs = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../../tools/processes.mjs') as {
  processList: (options?: { platform?: string; helper?: string; run?: (command: string, args: string[], options: object) => { status: number | null; stdout?: string; stderr?: string } }) => ProcessRow[] | null;
  strandedRoots: (processes: (ProcessRow & { created: number })[], options: { folder: string; since: number; except?: number[] }) => (ProcessRow & { created: number })[];
  endStrandedUnder: (folder: string, since: number, options?: { except?: number[]; platform?: string; list?: () => (ProcessRow & { created: number })[]; end?: (pid: number) => void }) => number[];
};

export const AT = '2026-10-24T10:00:00.000Z';

export const sig = (assertion: string, test = 'Driver.Tests.SessionTests.Resumes'): Signature => ({ test, exception: 'Xunit.Sdk.EqualException', location: { method: 'Resumes', text: assertion, ordinal: 0 } });
