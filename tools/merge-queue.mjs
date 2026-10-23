#!/usr/bin/env node
/**
 * The scoped run, the merge queue and the admission between the runners (CISCOPE1e, D207; design
 * `docs/2026-10-24-ci-scope-design.md` §4 and §5.1, §8 row 5). The merge tool's queue verbs (`<branch> --preview`,
 * `<branch> --message <file>`, `--queue`, `--queue --recheck`) and its detached runner (`--queue-run`) read and write these
 * records; each is kept in the main checkout's ignored `local/`.
 *
 * ## Why
 *
 * A merge used to block on the cheap checks alone, in the main checkout, and the CI beside gated main's tip after it. A merge now
 * waits on a scoped run of what it reaches, on a candidate commit in a tree of its own (`local/scope-tree`), so no dispatch waits
 * on the main checkout and main moves only by a fast-forward onto a candidate that passed. The full set stays the background
 * run's (CISCOPE1d), which the install waits for.
 *
 * ## The queue, `local/merge-queue.json`
 *
 * - **What the parent hands it.** A branch is previewed first (`--preview`: the merge made in the scope tree's spare index
 *   against main's tip, its diff and gates printed, nothing committed), after the records commit on the branch; then queued with
 *   its message file (`--message <file>`). The request is frozen at enqueue: the branch's tip and the message's bytes. A tip that
 *   differs from the branch's last preview, and a branch with no message, are refused (`enqueue`).
 * - **The candidate.** The runner merges every queued branch, in arrival order, at most `CANDIDATE_MAX`, one merge commit each
 *   on main's tip; a branch whose merge the resolvers cannot make is set aside and handed back (`formCandidate`). One member at a
 *   time where `--one-at-a-time` asked it, or where an unresolved failure split a candidate.
 * - **Its states.** `forming`, `running`, `bisecting`, `green`, `promoting`, `promoted`, `held` (on memory, on a checkout that is
 *   not clean), and the queue's `stopped`. A member handed back is `pinned`, `pinned with` or `unresolved`.
 * - **Promotion.** Only onto a clean main checkout whose tip is still the candidate's base; otherwise held, or re-formed on the
 *   new tip, the re-form after a records-only move running the whole cheap tier and deferring `service` alone (`recordsOnly`). A
 *   candidate re-formed `HELD_REFORMS` times running is *held by direct commits*. After a crash, a candidate in `promoting` or
 *   `green` whose tip main already holds has landed (`resumeCandidate`).
 *
 * ## Judging a red candidate (§4.1 point 4)
 *
 * `judgeFailure` places one failure, every run asking whether a selection fails *with this signature*. A death of memory is the
 * machine's (`memoryStep`) and never reaches it. A failing test the base shares is inherited and filed as known red, unless its
 * known red has expired, which stops the queue; a gate that names no test failing on the base stops the queue at once, lifted by
 * a direct commit, `--queue --recheck` or the runner's own recheck `RECHECK_MS` on. Otherwise the selection runs on every prefix,
 * and the member at the last transition into the signature is handed back (`placeOnPrefixes`), earlier transitions named as
 * interacting; a prefix that cannot run, a second run that disagrees, or a whole candidate that does not fail again, leaves it
 * unresolved. `candidateOutcome` turns the judgements into what the queue does next.
 *
 * ## The scoped set (§4.2)
 *
 * `scopedSet`: the cheap tier always; a reached .NET fast half whole; the TypeScript suites; the Process halves by class (their
 * sweep the background's); the git half within its budget (`gitBudget`), a known-red class first; browser specs and rehearsals
 * deferred. A gate whose pass stands on the candidate's tree is kept. Each run is recorded in `local/scope-runs.jsonl`.
 *
 * ## Admission, `local/admission.json` (§5.1)
 *
 * One record both runners ask before every heavy gate (`admit`): the Process lane is one resource, so the background starts no
 * Process-lane gate or rehearsal while a scoped tier runs or waits, and a scoped tier never waits for a slot. Memory is reserved in
 * physical memory and in commit, a reservation's unused part charged against what is free; the background leaves
 * `FOREGROUND_HEADROOM_GB` free. A reservation is released only on a read of its job that finds it empty, or on a name no job
 * answers; a read that fails keeps the whole charge. A background gate kept from its slot `BACKGROUND_SLOT_WAIT_MS` takes it
 * between scoped tiers.
 *
 * Exit codes: 0 · 2 a tool error. `node tools/merge-queue.mjs` prints the queue and the admission record.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { isMain, writeAtomic } from './fsx.mjs';
import { signatureKey } from './test-failures.mjs';

export const MERGE_QUEUE = 'local/merge-queue.json';
export const MERGE_QUEUE_LOG = 'local/merge-queue.log';
export const SCOPE_TREE = 'local/scope-tree';
export const SCOPE_RUNS_LOG = 'local/scope-runs.jsonl';
export const ADMISSION = 'local/admission.json';

/** At most this many branches in one candidate (§4.1 point 1). */
export const CANDIDATE_MAX = 8;
/** The runner's acknowledgement of its start, waited for this long before the absence is said (§4.1). */
export const RUNNER_ACK_MS = 30_000;
/** The runner's own recheck of a stop on the base (§4.1 point 4). */
export const RECHECK_MS = 60 * 60_000;
/** A candidate re-formed this many times running is held by direct commits (§4.1 point 3). */
export const HELD_REFORMS = 3;
/** The git half's budget, unless the gate declares `"scope": { "budgetMinutes": n }` (§4.2 point 5). */
export const GIT_BUDGET_MINUTES = 8;
/** The git half's lanes (§4.2 point 5). */
export const GIT_LANES = 4;
/** The memory the background leaves unreserved for the scoped peak (§5.1). */
export const FOREGROUND_HEADROOM_GB = 26;
/** A background Process-lane gate kept this long takes the slot between scoped tiers (§5.1). */
export const BACKGROUND_SLOT_WAIT_MS = 60 * 60_000;
/** The candidate's states, and a member's (§4.1, *Durable state*). */
export const CANDIDATE_STATES = Object.freeze(['forming', 'running', 'bisecting', 'green', 'promoting', 'promoted', 'held', 'stopped']);
export const HANDED_BACK = Object.freeze(['pinned', 'pinned with', 'unresolved', 'conflict']);

const iso = (ms) => new Date(ms).toISOString();
const timeOf = (at) => (typeof at === 'string' ? Date.parse(at) : Number.NaN);

function readJson(file, empty) {
  if (!existsSync(file)) return empty();
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8'));
    return parsed && typeof parsed === 'object' ? parsed : empty();
  } catch {
    return empty();
  }
}

// ---------------------------------------------------------------------------------------------------
// The queue's record

export const emptyQueue = () => ({ schema: 1, queued: [], previews: {}, candidate: null, stop: null, handedBack: [], runner: null, recheck: null });

/** `local/merge-queue.json`, or an empty queue where there is none or it does not read. */
export function readQueue(home) {
  const record = readJson(join(home, MERGE_QUEUE), emptyQueue);
  return { ...emptyQueue(), ...record };
}

export function writeQueue(home, queue) {
  const file = join(home, MERGE_QUEUE);
  mkdirSync(dirname(file), { recursive: true });
  writeAtomic(file, `${JSON.stringify(queue, null, 2)}\n`);
}

/** The preview recorded: the tip the parent read, and when (§4.1, *The queue*). */
export function notePreview(queue, { branch, tip, base, at }) {
  return { ...queue, previews: { ...queue.previews, [branch]: { tip, base, at } } };
}

/**
 * A branch put on the queue, its request frozen: its tip and its message's bytes (base64, so they are merged verbatim). Refused,
 * with the reason, when it has no message, when its tip differs from its last preview (both said), or when it is queued already.
 * Answers `{ queue, entry }` or `{ refused }`.
 */
export function enqueue(queue, { branch, tip, message, at, requeue = false }) {
  if (typeof message !== 'string' || !message.length) {
    return { refused: `${branch} is queued with its message: --message <file>, a file under local/ holding the merge commit's message` };
  }
  const previewed = queue.previews?.[branch] ?? null;
  if (!previewed) return { refused: `${branch} has no preview: read its diff first, after its records commit (--preview), then queue the tip you read` };
  if (previewed.tip !== tip) {
    return { refused: `${branch}'s tip is ${tip.slice(0, 7)}, and its last preview showed ${previewed.tip.slice(0, 7)}: preview it again (a records commit comes before the preview), then queue it` };
  }
  if (queue.queued.some((each) => each.branch === branch) || queue.candidate?.members.some((each) => each.branch === branch && !each.handedBack)) {
    return { refused: `${branch} is queued already: --drop-batch ${branch} takes it off, then queue it again` };
  }
  const entry = { branch, tip, message: Buffer.from(message, 'utf8').toString('base64'), at, ...(requeue ? { requeue } : {}) };
  return { queue: { ...queue, queued: [...queue.queued, entry] }, entry };
}

/** A queued request's message, as its bytes were at enqueue. */
export const messageOf = (entry) => Buffer.from(entry.message ?? '', 'base64').toString('utf8');

/** Queued branches taken off the queue; a candidate's branches end it and are handed back (`--drop-batch`). */
export function dropQueued(queue, branches, at = new Date().toISOString()) {
  const drop = (branch) => branches.length === 0 || branches.includes(branch);
  const ends = Boolean(queue.candidate?.members.some((each) => drop(each.branch)));
  const back = ends ? queue.candidate.members.map((each) => ({ ...each, handedBack: { how: 'dropped', at } })) : [];
  const dropped = [...queue.queued.filter((each) => drop(each.branch)).map((each) => each.branch), ...back.map((each) => each.branch)];
  return {
    queue: { ...queue, queued: queue.queued.filter((each) => !drop(each.branch)), candidate: ends ? null : queue.candidate, handedBack: [...(queue.handedBack ?? []), ...back] },
    dropped,
  };
}

/**
 * The next candidate's members: the queued requests in arrival order, at most `max`, one where `oneAtATime`. Answers the members
 * and the requests left queued.
 */
export function nextMembers(queued, { max = CANDIDATE_MAX, oneAtATime = false } = {}) {
  const take = oneAtATime ? 1 : max;
  return { members: queued.slice(0, take), rest: queued.slice(take) };
}

/**
 * A candidate formed on `base` from `members`, each merged on the last by `merge(member, onto)`, which answers `{ commit }` for
 * its merge commit or `{ conflict: [paths] }` for a merge the resolvers could not make (aborted by it). A conflicted member is set
 * aside and handed back with its conflict named, and the rest go on (§4.1 point 1). Answers the candidate and the set-aside.
 */
export async function formCandidate({ base, members, merge, at, reforms = 0, recordsOnly = false, split = false }) {
  let onto = base;
  const formed = [];
  const setAside = [];
  for (const member of members) {
    const merged = await merge(member, onto);
    if (merged.conflict) {
      setAside.push({ ...member, handedBack: { how: 'conflict', paths: merged.conflict, at } });
      continue;
    }
    formed.push({ ...member, commit: merged.commit });
    onto = merged.commit;
  }
  const candidate = formed.length
    ? { base, tip: onto, members: formed, state: 'running', formed: at, reforms, recordsOnly, split, runs: [] }
    : null;
  return { candidate, setAside };
}

/**
 * Whether a direct commit's paths move main by records only (§4.1 point 3): every path's own rule, the first that places it
 * (`ruleOf(path)`), is the records lane, `docs/**` and its neighbours, or `docs/index/**`. A path no rule places, or an earlier
 * rule naming more (`.claude/knowledge/adoption.md`, `daoris.lanes.json`), or a rule that names no gate (`src/Daoris.Cli/test/**`,
 * the tools lane), is not.
 */
export function recordsOnly(paths, ruleOf) {
  if (!paths.length) return false;
  return paths.every((path) => {
    const rule = ruleOf(path);
    if (!rule) return false;
    if (rule.lane === 'records') return true;
    return Array.isArray(rule.paths) && (rule.paths.includes('docs/**') || rule.paths.includes('docs/index/**'));
  });
}

/**
 * What the runner does with a candidate before anything is judged, after a start or a crash (§4.1, *Durable state*): `promoted`
 * when it was `promoting` or `green` and main holds its tip (`isAncestor(tip, mainTip)`); `reform` when main left its base;
 * `promote` when it is green on its base; `run` when its run's process is gone; else `wait`.
 */
export function resumeCandidate(candidate, { mainTip, isAncestor, runAlive = () => false }) {
  if (!candidate) return 'none';
  if (['promoting', 'green', 'promoted'].includes(candidate.state) && (candidate.tip === mainTip || isAncestor(candidate.tip, mainTip))) return 'promoted';
  if (candidate.base !== mainTip) return 'reform';
  if (candidate.state === 'green' || candidate.state === 'promoting') return 'promote';
  if (candidate.state === 'held') return candidate.held?.on === 'checkout' ? 'promote' : 'run';
  if (['running', 'bisecting', 'forming'].includes(candidate.state)) return runAlive(candidate.run?.pid) ? 'wait' : 'run';
  return 'wait';
}

/**
 * Whether a green candidate may fast-forward main now (§4.1 point 3): only onto a clean checkout (`clean`: no tracked change, no
 * merge in progress) whose tip is still its base. Answers `promote`, `held` with what holds it, or `reform`.
 */
export function promotion(candidate, { mainTip, clean }) {
  if (candidate.base !== mainTip) return { action: 'reform' };
  if (!clean) return { action: 'held', why: 'the main checkout has a tracked change or a merge in progress: promotion waits until it is clean, tried each minute and at --continue' };
  return { action: 'promote' };
}

/** A candidate re-formed on a moved main: its re-forms counted, and whether the move was records only (§4.1 point 3). */
export function reformCount(candidate, { recordsOnlyMove }) {
  const reforms = (candidate?.reforms ?? 0) + 1;
  return { reforms, recordsOnly: recordsOnlyMove, heldByDirect: reforms >= HELD_REFORMS };
}

// ---------------------------------------------------------------------------------------------------
// Judging a red candidate (§4.1 point 4)

const same = (outcome, signature) => outcome?.result === 'fail' && signatureKey(outcome.signature) === signatureKey(signature);
const cannot = (outcome, signature) => outcome?.result === 'cannot' && signatureKey(outcome.signature) !== signatureKey(signature);

/**
 * The prefixes' answers to one question, does it fail with this signature (`seq[k]` for the base plus the first *k* members,
 * `seq[0]` the base and `seq[n]` the whole candidate), placed: the member at the **last** transition into the signature,
 * earlier transitions named as interacting; any prefix that cannot run the selection with another signature makes it
 * unresolved, and so does a whole candidate that does not fail with it. Answers `{ outcome: 'pinned', member, interacting }`
 * (members by their number from 1) or `{ outcome: 'unresolved', why }`.
 */
export function placeOnPrefixes(seq, signature) {
  const n = seq.length - 1;
  const blocked = seq.findIndex((outcome) => cannot(outcome, signature));
  if (blocked >= 0) return { outcome: 'unresolved', why: `prefix ${blocked} cannot run the selection (${blocked === 0 ? 'the base' : `the base and ${blocked} member${blocked === 1 ? '' : 's'}`}): it fails another way` };
  if (!same(seq[n], signature)) return { outcome: 'unresolved', why: 'the whole candidate does not fail again with its signature' };
  const transitions = [];
  for (let k = 1; k <= n; k += 1) if (!same(seq[k - 1], signature) && same(seq[k], signature)) transitions.push(k);
  if (!transitions.length) return { outcome: 'unresolved', why: 'no prefix places it' };
  const member = transitions.at(-1);
  return { outcome: 'pinned', member, interacting: transitions.slice(0, -1), monotonic: transitions.length === 1 };
}

/**
 * One failure judged (§4.1 point 4). `failure`: `{ unit, test?, gate, signature, names: 'test' | 'gate' }`. `run(failure, k)` runs
 * its class, file or gate selection on the prefix of the base and the first `k` members (`k` 0 the base, `members` the whole
 * candidate) and answers `{ result: 'pass' }`, `{ result: 'fail', signature }`, or `{ result: 'cannot', signature }` for a
 * prefix that does not build, discover or host. `alone(failure, k)` runs it on the base with member `k` alone. `known` is the
 * open known red its signature matches (`{ finding, expired, lastSeen }`), or null; `baseCommit` the candidate's base.
 *
 * Answers `{ outcome, ... }`: `inherited` (filed as known red, pinned to none), `stop` (`why`: `base` or `expired`), `pinned`
 * (`member`, `with` the members it needs, `interacting`), or `unresolved` (`why`), with `runs`, every run it made.
 */
export async function judgeFailure(failure, { members, run, alone = null, known = null, baseCommit = null }) {
  const runs = [];
  const at = async (k) => {
    const outcome = await run(failure, k);
    runs.push({ prefix: k, ...outcome });
    return outcome;
  };
  if (failure.names === 'test' && known && !known.expired && known.lastSeen === baseCommit) {
    return { outcome: 'inherited', why: `known red, last seen failing on the base ${String(baseCommit).slice(0, 7)}: its base run is skipped`, runs };
  }
  const base = await at(0);
  if (same(base, failure.signature)) {
    if (failure.names === 'gate') {
      return { outcome: 'stop', why: 'base', said: `${failure.gate} fails on the base with the same signature: main is broken, the queue stops until a direct commit or --queue --recheck`, runs };
    }
    if (known?.expired) {
      return { outcome: 'stop', why: 'expired', said: `${failure.test} is known red past 72 hours: no candidate inherits it, and the queue stops until a direct commit fixes it or skips it under its row`, runs };
    }
    return { outcome: 'inherited', why: 'it fails on the base with the same signature: filed as known red, pinned to none', runs };
  }
  const seq = [base];
  const whole = await at(members);
  for (let k = 1; k < members; k += 1) seq.push(await at(k));
  seq.push(whole);
  const placed = placeOnPrefixes(seq, failure.signature);
  if (placed.outcome !== 'pinned') return { ...placed, runs };
  // A flake mid-bisection: the two prefixes either side of the transition run once more, and a result that differs leaves it unresolved.
  const before = await at(placed.member - 1);
  const after = await at(placed.member);
  if (same(before, failure.signature) !== same(seq[placed.member - 1], failure.signature)
    || same(after, failure.signature) !== same(seq[placed.member], failure.signature)) {
    return { outcome: 'unresolved', why: `the prefixes either side of member ${placed.member} disagree on a second run`, runs };
  }
  let needs = [];
  if (placed.monotonic && placed.member > 1 && alone) {
    const single = await alone(failure, placed.member);
    runs.push({ alone: placed.member, ...single });
    if (!same(single, failure.signature)) needs = Array.from({ length: placed.member - 1 }, (_, i) => i + 1).filter((k) => !placed.interacting.includes(k));
  }
  return { outcome: 'pinned', member: placed.member, with: needs, interacting: placed.interacting, runs };
}

/**
 * A death of memory, the machine's (§4.1 point 4): never run on the base or over the prefixes, never a stop or a hand-back. Given
 * the gate's attempts so far (`{ died, peakGb? }` each, oldest first), whether the hold was released by an admission
 * (`afterHold`), and its reserved need, answers what happens next: `again` (run it once more, whole and alone, once its memory is
 * free), `pass` (a pass after a death, FLAKE of memory, counted as the gate's pass), `hold` (held as memory until the admission
 * reserves its need), or `judge` (on the run after the hold, its own job's peak past its need: the gate's failure, judged as one
 * that names no test).
 */
export function memoryStep(attempts, { afterHold = false, needGb = null } = {}) {
  const last = attempts.at(-1);
  if (!last) return { next: 'run' };
  if (!last.died) return attempts.length > 1 ? { next: 'pass', note: 'FLAKE of memory: it passed once its memory was free' } : { next: 'pass' };
  if (afterHold) {
    if (needGb !== null && typeof last.peakGb === 'number' && last.peakGb > needGb) {
      return { next: 'judge', note: `its own job's peak commit (${last.peakGb} GB) passed its reserved need (${needGb} GB): the gate's failure` };
    }
    return { next: 'hold', note: 'it died within its reserved need, under load from outside the runners: held again, not judged' };
  }
  if (attempts.length === 1) return { next: 'again', note: 'a death of memory: run again once, whole and alone, once its memory is free (MEMGATE1)' };
  return { next: 'hold', note: 'a second death of memory: the candidate is held as memory until the admission record reserves its need' };
}

/**
 * What the queue does with a red candidate's judgements (§4.1 point 4): `stop` when any stops the queue; else the members handed
 * back (pinned, pinned with, or, for a candidate of one, unresolved), the inherited failures to file, and either the members kept
 * to be judged again (`retain`) or, where an unresolved failure left a candidate of several, its members one at a time (`split`).
 * A pass on an unresolved failure's re-run never makes the candidate green.
 */
export function candidateOutcome(judgements, members) {
  const stop = judgements.find(({ judged }) => judged.outcome === 'stop');
  if (stop) return { action: 'stop', failure: stop.failure, why: stop.judged.why, said: stop.judged.said };
  const inherited = judgements.filter(({ judged }) => judged.outcome === 'inherited').map(({ failure }) => failure);
  const back = new Map();
  for (const { failure, judged } of judgements) {
    if (judged.outcome !== 'pinned') continue;
    const member = members[judged.member - 1];
    if (!member || back.has(member.branch)) continue;
    back.set(member.branch, {
      ...member,
      handedBack: {
        how: judged.with?.length ? 'pinned with' : 'pinned',
        failure: failure.test ?? failure.gate,
        with: (judged.with ?? []).map((k) => members[k - 1]?.branch).filter(Boolean),
        interacting: judged.interacting.map((k) => members[k - 1]?.branch).filter(Boolean),
      },
    });
  }
  const unresolved = judgements.filter(({ judged }) => judged.outcome === 'unresolved');
  if (unresolved.length && members.length === 1 && !back.size) {
    const [only] = members;
    return {
      action: 'hand back', inherited, flakes: unresolved.map(({ failure }) => failure),
      handedBack: [{ ...only, handedBack: { how: 'unresolved', failure: unresolved.map(({ failure }) => failure.test ?? failure.gate).join(', '), why: unresolved.map(({ judged }) => judged.why).join('; ') } }],
      retain: [],
    };
  }
  const kept = members.filter((member) => !back.has(member.branch));
  if (unresolved.length) return { action: 'split', inherited, flakes: unresolved.map(({ failure }) => failure), handedBack: [...back.values()], retain: kept };
  return { action: back.size ? 'hand back' : 'green', inherited, flakes: [], handedBack: [...back.values()], retain: back.size ? kept : members };
}

/** Whether a stopped queue is due its own recheck (§4.1 point 4): an hour after the stop or the last recheck. */
export function recheckDue(stop, now = Date.now(), every = RECHECK_MS) {
  if (!stop) return false;
  const last = stop.rechecks?.at(-1)?.at ?? stop.at;
  return now - timeOf(last) >= every;
}

/** A stop lifted, or kept, by a recheck of its gate's selection on the base. */
export function noteRecheck(stop, { at, passed, by }) {
  return passed ? null : { ...stop, rechecks: [...(stop.rechecks ?? []), { at, passed, by }] };
}

// ---------------------------------------------------------------------------------------------------
// The scoped set (§4.2)

/**
 * The git half's classes within the budget (§4.2 point 5): its predicted wall, the classes' measured times laid on `lanes`, never
 * less than the longest, plus the half's overhead; a class with no measured time counts as the longest. When the reached set
 * fits, all of it runs. Otherwise a known-red class runs first and is never deferred, then the classes whose subject a change
 * touched, then the rest, each group by recent failures and then shortest first, until the budget is spent. Answers `run`,
 * `deferred`, `predictedMs` and `overrun`.
 */
export function gitBudget({ classes, overheadMs = 0, budgetMs = GIT_BUDGET_MINUTES * 60_000, lanes = GIT_LANES }) {
  const longest = Math.max(0, ...classes.map((each) => (typeof each.ms === 'number' ? each.ms : 0)));
  const costOf = (each) => (typeof each.ms === 'number' ? each.ms : longest);
  const wall = (set) => (set.length ? Math.max(Math.max(...set.map(costOf)), set.reduce((sum, each) => sum + costOf(each), 0) / lanes) + overheadMs : 0);
  if (wall(classes) <= budgetMs) return { run: classes.map((each) => each.name), deferred: [], predictedMs: wall(classes), overrun: false };
  const order = (a, b) => (b.failures ?? 0) - (a.failures ?? 0) || costOf(a) - costOf(b) || a.name.localeCompare(b.name);
  const red = classes.filter((each) => each.knownRed);
  const subject = classes.filter((each) => !each.knownRed && each.subject).sort(order);
  const rest = classes.filter((each) => !each.knownRed && !each.subject).sort(order);
  const chosen = [...red];
  const overrun = wall(red) > budgetMs;
  if (!overrun) for (const each of [...subject, ...rest]) if (wall([...chosen, each]) <= budgetMs) chosen.push(each);
  const names = new Set(chosen.map((each) => each.name));
  return { run: chosen.map((each) => each.name), deferred: classes.filter((each) => !names.has(each.name)).map((each) => each.name), predictedMs: wall(chosen), overrun };
}

/**
 * What a candidate's scoped run runs, of the plan (§4.2): `gates` each `{ name, kind }` with kind one of `cheap`, `fast` (a .NET
 * fast half), `node` (a TypeScript suite), `process` (a Process half run by class), `git` (the git half), `browser`, `rehearsal`
 * or `other`; `reached` the gates the candidate's paths reach; `standing` the gates whose pass stands on its tree; `knownRed`
 * the gates holding a known-red unit the scoped run may run, which run whatever is reached; `recordsOnly` for a re-form after a
 * records-only move, which defers `service` alone. Answers `run`, `kept` and `deferred` (each `{ name, why }`).
 */
export function scopedSet({ gates, reached, standing = new Set(), knownRed = new Set(), recordsOnly = false }) {
  const run = [];
  const kept = [];
  const deferred = [];
  for (const { name, kind } of gates) {
    const isReached = reached.has(name) || knownRed.has(name);
    if (kind === 'cheap') {
      if (standing.has(name) && !knownRed.has(name)) kept.push({ name, why: 'its pass stands on this tree' });
      else run.push({ name, why: 'the cheap tier runs in every candidate' });
      continue;
    }
    if (!isReached) continue;
    if (kind === 'browser' || kind === 'rehearsal') {
      deferred.push({ name, why: 'a browser spec or a rehearsal: deferred to the background run, which the install waits for' });
      continue;
    }
    if (recordsOnly && name === 'service') {
      deferred.push({ name, why: "a base moved by records only: the service suite's scan of the documents is deferred to the background run" });
      continue;
    }
    if (standing.has(name) && !knownRed.has(name)) {
      kept.push({ name, why: 'its pass stands on this tree' });
      continue;
    }
    const why = knownRed.has(name) && !reached.has(name) ? 'it holds a known red the scoped run may run, so it runs in every candidate'
      : kind === 'fast' ? 'a reached .NET fast half runs whole'
        : kind === 'process' ? 'a reached Process half, by class'
          : kind === 'git' ? 'the git half, within its budget'
            : 'reached';
    run.push({ name, why });
  }
  return { run, kept, deferred };
}

/** A scoped run's line in `local/scope-runs.jsonl`: its base, members, tree, and the units it selected, ran, deferred and their verdicts. */
export function appendScopeRun(home, entry) {
  const file = join(home, SCOPE_RUNS_LOG);
  mkdirSync(dirname(file), { recursive: true });
  appendFileSync(file, `${JSON.stringify(entry)}\n`);
}

export function readScopeRuns(home) {
  const file = join(home, SCOPE_RUNS_LOG);
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean).flatMap((line) => {
    try {
      return [JSON.parse(line)];
    } catch {
      return [];
    }
  });
}

// ---------------------------------------------------------------------------------------------------
// Admission between the runners (§5.1)

export const emptyAdmission = () => ({ schema: 1, reservations: [], scopedTier: null, waiting: [] });

export function readAdmission(home) {
  return { ...emptyAdmission(), ...readJson(join(home, ADMISSION), emptyAdmission) };
}

export function writeAdmission(home, record) {
  const file = join(home, ADMISSION);
  mkdirSync(dirname(file), { recursive: true });
  writeAtomic(file, `${JSON.stringify(record, null, 2)}\n`);
}

/**
 * Each reservation read through its job (`readJob(name)`: `{ state: 'active', workingGb, committedGb }`, `{ state: 'empty' }`,
 * `{ state: 'gone' }` for a name no job answers, or `{ state: 'error', why }`). A job read empty or gone releases its
 * reservation; one that cannot be read keeps the whole charge, said. Answers the reservations kept with their unused charge in
 * each measure, those released, and the lines to say.
 */
export function chargeReservations(reservations, readJob) {
  const kept = [];
  const released = [];
  const said = [];
  for (const each of reservations) {
    let job;
    try {
      job = readJob(each.job);
    } catch (error) {
      job = { state: 'error', why: error?.message ?? String(error) };
    }
    if (job?.state === 'empty' || job?.state === 'gone') {
      released.push({ ...each, released: job.state });
      continue;
    }
    if (job?.state === 'active' && typeof job.workingGb === 'number' && typeof job.committedGb === 'number') {
      kept.push({ ...each, unused: { physicalGb: Math.max(0, each.needGb - job.workingGb), commitGb: Math.max(0, each.needGb - job.committedGb) } });
      continue;
    }
    said.push(`${each.gate} (${each.runner}): its job ${each.job} could not be read (${job?.why ?? 'no answer'}), so its whole charge is kept`);
    kept.push({ ...each, unused: { physicalGb: each.needGb, commitGb: each.needGb } });
  }
  return { kept, released, said };
}

/**
 * Whether a gate is admitted now (§5.1). `request`: `{ runner: 'scoped' | 'background', gate, lane: 'process' | 'rehearsal' |
 * 'light', needGb, waitingSince? }`; `free`: the sampled `{ physicalGb, commitGb }`; `betweenTiers` whether the scope runner is
 * between Process tiers. The background starts no Process-lane gate or rehearsal while a scoped tier runs or waits, but one kept
 * `BACKGROUND_SLOT_WAIT_MS` takes the slot between tiers; a scoped gate never waits for a slot. Each measure's free figure, less
 * every live reservation's unused part, covers the need (the background's beside `FOREGROUND_HEADROOM_GB`). Answers `{ admit,
 * wait: 'slot' | 'memory' | null, why, record, said }` with the record's reservations read through their jobs.
 */
export function admit({ record, request, free, readJob, now = Date.now(), betweenTiers = false, headroomGb = FOREGROUND_HEADROOM_GB }) {
  const charged = chargeReservations(record.reservations ?? [], readJob);
  const next = { ...record, reservations: charged.kept.map(({ unused, ...rest }) => rest) };
  const unused = (measure) => charged.kept.reduce((sum, each) => sum + each.unused[measure], 0);
  const heavyLane = request.lane === 'process' || request.lane === 'rehearsal';
  if (request.runner === 'background' && heavyLane && next.scopedTier) {
    const waited = request.waitingSince ? now - timeOf(request.waitingSince) : 0;
    if (!(waited >= BACKGROUND_SLOT_WAIT_MS && betweenTiers)) {
      return { admit: false, wait: 'slot', why: `a scoped Process tier ${next.scopedTier.state}: the background starts no Process-lane gate or rehearsal`, record: next, said: charged.said };
    }
  }
  const reserve = request.runner === 'background' ? headroomGb : 0;
  const physical = free.physicalGb - unused('physicalGb') - reserve;
  const commit = free.commitGb - unused('commitGb') - reserve;
  if (physical < request.needGb || commit < request.needGb) {
    return {
      admit: false,
      wait: 'memory',
      why: `${request.gate} needs ${request.needGb} GB; free less the live reservations${reserve ? ` and the ${reserve} GB foreground headroom` : ''}: ${Math.max(0, physical).toFixed(1)} GB physical, ${Math.max(0, commit).toFixed(1)} GB commit`,
      record: next,
      said: charged.said,
    };
  }
  return { admit: true, wait: null, why: 'admitted', record: next, said: charged.said };
}

/** A reservation held for an admitted gate, named by its job: the runner, the run and the gate. */
export function reserve(record, { runner, run, gate, lane, needGb, pid, at }) {
  const job = `daoris-${runner}-${run}-${gate}`.replace(/[^A-Za-z0-9._-]+/g, '-');
  return { record: { ...record, reservations: [...(record.reservations ?? []).filter((each) => each.job !== job), { runner, run, gate, lane, needGb, job, pid, at }] }, job };
}

/** The scoped tier's state on the record, `running`, `queued` or null. */
export const scopedTier = (record, state, at) => ({ ...record, scopedTier: state ? { state, since: record.scopedTier?.since ?? at } : null });

/** A background gate held on memory past `BACKGROUND_SLOT_WAIT_MS` is reported overdue (§5.1), with how long it has waited. */
export function overdueWaits(record, now = Date.now()) {
  return (record.waiting ?? []).filter((each) => each.runner === 'background' && now - timeOf(each.since) >= BACKGROUND_SLOT_WAIT_MS)
    .map((each) => `${each.gate} has waited on ${each.on ?? 'memory'} since ${each.since} (${Math.round((now - timeOf(each.since)) / 60_000)} min)`);
}

// ---------------------------------------------------------------------------------------------------
// What `--queue` says

/** The queue as lines: the stop, the candidate with its members and state, what is queued and whether its tip still is the branch's, what was handed back. */
export function queueLines(queue, { branchTip = () => null, now = Date.now() } = {}) {
  const out = [];
  if (queue.stop) {
    out.push(`stopped since ${queue.stop.at}: ${queue.stop.said ?? queue.stop.why}`);
    out.push(`  lifted by a direct commit on main, by --queue --recheck, or by the runner's own recheck (${recheckDue(queue.stop, now) ? 'due now' : `next at ${iso(timeOf(queue.stop.rechecks?.at(-1)?.at ?? queue.stop.at) + RECHECK_MS)}`})`);
  }
  const candidate = queue.candidate;
  if (candidate) {
    const held = candidate.state === 'held' ? ` (held on ${candidate.held?.on}: ${candidate.held?.why})` : '';
    out.push(`candidate on ${String(candidate.base).slice(0, 7)}: ${candidate.state}${held}${candidate.reforms ? `, re-formed ${candidate.reforms} time${candidate.reforms === 1 ? '' : 's'}` : ''}${candidate.reforms >= HELD_REFORMS ? ', held by direct commits' : ''}`);
    candidate.members.forEach((member, i) => out.push(`  ${i + 1}. ${member.branch} at ${String(member.tip).slice(0, 7)}${member.commit ? ` → ${String(member.commit).slice(0, 7)}` : ''}`));
  }
  if (queue.queued.length) {
    out.push(`queued: ${queue.queued.length}`);
    for (const entry of queue.queued) {
      const now = branchTip(entry.branch);
      out.push(`  ${entry.branch} at ${String(entry.tip).slice(0, 7)}${now && now !== entry.tip ? ` (the branch is now at ${now.slice(0, 7)}: the frozen tip is what merges)` : ''}${entry.requeue ? ' (a requeue of an unresolved member)' : ''}`);
    }
  }
  for (const back of (queue.handedBack ?? []).slice(-8)) {
    const detail = back.handedBack?.how === 'conflict' ? `a conflict in ${back.handedBack.paths?.join(', ')}`
      : `${back.handedBack?.how}${back.handedBack?.failure ? `: ${back.handedBack.failure}` : ''}${back.handedBack?.with?.length ? `, with ${back.handedBack.with.join(', ')}` : ''}${back.handedBack?.interacting?.length ? `; interacting: ${back.handedBack.interacting.join(', ')}` : ''}`;
    out.push(`handed back: ${back.branch} (${detail})`);
  }
  if (!out.length) out.push('the queue is empty');
  return out;
}

function main() {
  const home = process.cwd();
  for (const line of queueLines(readQueue(home))) console.log(line);
  const admission = readAdmission(home);
  console.log(`admission: ${admission.reservations.length} reservation${admission.reservations.length === 1 ? '' : 's'}${admission.scopedTier ? `, a scoped tier ${admission.scopedTier.state}` : ''}`);
  for (const each of admission.reservations) console.log(`  ${each.gate} (${each.runner}): ${each.needGb} GB, job ${each.job}`);
  for (const line of overdueWaits(admission)) console.log(`  overdue: ${line}`);
  return 0;
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = main();
  } catch (error) {
    console.error(`merge-queue: ${error.message}`);
    process.exitCode = 2;
  }
}
