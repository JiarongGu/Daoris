/**
 * What a preflight keeps of its run: each gate's result as a receipt, the run's measure, and the input of the run's summary
 * (`run-summary.ts`) read from git and the code map. A receipt is built from a result the run recorded, never from a word.
 */
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { writeAtomic } from '../fsx.mjs';
import { censusFirst, receiptsFolder, writeRun, type CheckRun, type GateReceipt, type ReceiptVerdict, type Required } from './receipts.ts';
import { buildSummary, summaryLines, type AreaDocument, type Change, type Deferred, type Module, type RunSummary, type SummaryInput } from './run-summary.ts';
import type { ScopedSet } from './fast-plan.ts';

export type Git = (cwd: string, args: string[], options?: { allowFail?: boolean }) => { status: number | null; out: string };

type Result = { gate: { name: string; run?: string }; verdict: string; ms?: number; note?: string };

const VERDICTS: Record<string, ReceiptVerdict> = { PASS: 'pass', FLAKE: 'pass', ENDED: 'ended' };

/** A gate's result as a receipt: a pass or a flake passes, an ended run proves nothing, any other verdict fails with its first line. */
export function receiptOf(result: Result, { at, waitMs }: { at: string; waitMs: number }): GateReceipt {
  const verdict = VERDICTS[result.verdict] ?? 'fail';
  const first = (result.note ?? '').split('\n')[0]?.trim();
  return {
    unit: result.gate.name, command: result.gate.run ?? result.gate.name, verdict, ms: result.ms ?? 0, waitMs, replayed: false, at,
    ...(first && verdict !== 'pass' ? { note: first } : {}),
  };
}

/** The run's record: the results with the time each waited, and each kept gate as a pass replayed from the one standing on this tree. */
export function checkRunOf({ id, trigger, branch, tip, tree, base, startedAt, wallMs, at, results, waits, kept }: {
  id: string; trigger: string; branch: string; tip: string; tree: string; base: string; startedAt: string; wallMs: number; at: string;
  results: Result[]; waits: Record<string, number>; kept: { name: string; why: string; run?: string }[];
}): CheckRun {
  const ran = results.map((result) => receiptOf(result, { at, waitMs: waits[result.gate.name] ?? 0 }));
  const replayed = kept.map((each): GateReceipt => ({ unit: each.name, command: each.run ?? each.name, verdict: 'pass', ms: 0, waitMs: 0, replayed: true, at, note: each.why }));
  return { schema: 1, id, kind: 'preflight', trigger, branch, tip, tree, base, startedAt, wallMs, gates: [...ran, ...replayed] };
}

/** The gates with the declaration's `verifyBudget.preflightFirst` names first; a declaration that does not read leaves the order. */
export function firstGates<T extends { name: string }>(gates: T[], declaration: string | null): T[] {
  try {
    const first = (JSON.parse(declaration ?? '{}') as { verifyBudget?: { preflightFirst?: unknown } }).verifyBudget?.preflightFirst;
    return censusFirst(gates, Array.isArray(first) ? first.filter((each): each is string => typeof each === 'string') : []);
  } catch {
    return gates;
  }
}

/** The files changed from `base` to `tip` with their lines; a binary file counts none, and a diff git cannot make reads as no change. */
export function changesOf(root: string, base: string, tip: string, git: Git): Change[] {
  const ran = git(root, ['--no-optional-locks', 'diff', '--numstat', `${base}...${tip}`], { allowFail: true });
  if (ran.status !== 0) return [];
  return ran.out.split('\n').filter(Boolean).flatMap((line) => {
    const [added, removed, ...path] = line.split('\t');
    return path.length ? [{ path: path.join('\t'), added: Number(added) || 0, removed: Number(removed) || 0 }] : [];
  });
}

const show = (root: string, git: Git, commit: string, path: string): string | null => {
  const ran = git(root, ['--no-optional-locks', 'show', `${commit}:${path}`], { allowFail: true });
  return ran.status === 0 ? ran.out : null;
};

/** The code map's modules at `tip`; none where it does not read, so a path is never placed by a guess. */
function modulesAt(root: string, git: Git, tip: string): Module[] {
  try {
    const map = JSON.parse(show(root, git, tip, 'docs/code-map.json') ?? '{}') as { modules?: { id?: unknown; path?: unknown }[] };
    return (map.modules ?? []).flatMap((each) => (typeof each.id === 'string' && typeof each.path === 'string' ? [{ id: each.id, path: each.path }] : []));
  } catch {
    return [];
  }
}

const AREA = /^docs\/areas\/[^/]+\.md$/;

/** What the summary is built from: the change, the area documents on both sides, the modules, the receipts and what the run deferred. */
export function summaryInputOf({ root, branch, base, tip, git, receipts, required, deferred }: {
  root: string; branch: string; base: string; tip: string; git: Git; receipts: GateReceipt[]; required: Required[]; deferred: Deferred[];
}): SummaryInput {
  const changes = changesOf(root, base, tip, git);
  const areas: AreaDocument[] = changes.filter((each) => AREA.test(each.path)).map((each) => ({ path: each.path, before: show(root, git, base, each.path), after: show(root, git, tip, each.path) }));
  return { branch, tip, base, changes, modules: modulesAt(root, git, tip), areas, receipts, required, deferred };
}

/**
 * A preflight's run kept: its receipts and measure in the home (the main checkout's `local/` where there is none, so every worktree of the repository shares it), and its summary record
 * beside the brief as `summary.json`. Answers the record, the summary and the summary's lines for the run to say.
 */
export function keepRun({ root, checkout, home, scratch, git, id, trigger, branch, base, baseTip, tip, tree, startedAt, now, results, waits, plan, set }: {
  root: string; checkout: string; home: string | null; scratch: string; git: Git; id: string; trigger: string; branch: string; base: string; baseTip: string; tip: string;
  tree: string; startedAt: number; now: () => number; results: Result[]; waits: Record<string, number>; plan: { name: string; run: string }[]; set: ScopedSet;
}): { run: CheckRun; summary: RunSummary; lines: string[] } {
  const at = new Date(now()).toISOString();
  const commandOf = (name: string): string => plan.find((gate) => gate.name === name)?.run ?? name;
  const run = checkRunOf({
    id, trigger, branch, tip, tree, base: baseTip, startedAt: new Date(startedAt).toISOString(), wallMs: now() - startedAt, at, results, waits,
    kept: set.kept.map((each) => ({ ...each, run: commandOf(each.name) })),
  });
  writeRun(receiptsFolder(home, checkout), run);
  const required = [...set.run, ...set.kept].map((each) => ({ unit: each.name, command: commandOf(each.name) }));
  const summary = buildSummary(summaryInputOf({ root, branch, base, tip, git, receipts: run.gates, required, deferred: set.deferred }));
  mkdirSync(scratch, { recursive: true });
  writeAtomic(join(scratch, 'summary.json'), `${JSON.stringify(summary, null, 1)}
`);
  return { run, summary, lines: summaryLines(summary) };
}
