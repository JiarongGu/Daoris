/**
 * The receipts of a preflight: what Daoris itself proved of a tree before an agent's work is checked, written by code
 * and read by the run's check, by `done` and by the summary (`run-summary.ts`). A receipt is keyed by the git tree it judged, so it holds
 * across rebases and worktrees; a pass written by a person's word or an agent's is none, only a run's own counts. Each run also
 * appends its measure (gates, minutes, build, wait, what started it, the units replayed) to one file, so a week's check minutes per
 * landed run are read from records alone.
 *
 * Kept under the home's `checks` folder (`data/checks/receipts/`), or the checkout's ignored `local/receipts/` where there is no home.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { homeEntry } from '../../src/Daoris.Cli/src/platform/home.ts';
import { writeAtomic } from '../fsx.mjs';

/** How a unit ended: `pass` and `fail` are verdicts; `ended` is a run the bound or a stop cut short, which proves nothing. */
export type ReceiptVerdict = 'pass' | 'fail' | 'ended';

/** What one gate or unit of a preflight proved: its command, how it ended, its minutes and wait, and whether a standing pass stood for it. */
export type GateReceipt = {
  unit: string; command: string; verdict: ReceiptVerdict; ms: number; waitMs: number; replayed: boolean; at: string;
  /** The first failing line, or what a replay stood on. */
  note?: string;
  /** Minutes the unit spent building, where its run says. */
  buildMs?: number;
};

/** One check run's measure and receipts: what started it (`trigger`), the tree it judged and what it ran or replayed. */
export type CheckRun = {
  schema: 1; id: string; kind: 'preflight' | 'check' | 'merge'; trigger: string; branch: string; tip: string; tree: string; base: string;
  startedAt: string; wallMs: number; gates: GateReceipt[];
};

export type Required = { unit: string; command: string };

export function receiptsFolder(home: string | null, checkout: string): string {
  return home ? homeEntry(home, 'checks', 'receipts') : join(checkout, 'local', 'receipts');
}

export const runsFile = (folder: string): string => join(folder, 'runs.jsonl');

const treeFile = (folder: string, tree: string): string => join(folder, `${tree}.json`);

/** The receipts kept for a tree, one per unit, newest; none where the tree has none or its file does not read. */
export function readReceipts(folder: string, tree: string): GateReceipt[] {
  const file = treeFile(folder, tree);
  if (!existsSync(file)) return [];
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8')) as { units?: Record<string, GateReceipt> };
    return Object.values(parsed.units ?? {}).filter((each) => typeof each?.unit === 'string' && typeof each.verdict === 'string');
  } catch {
    return [];
  }
}

/** A run kept: its receipts merged into its tree's (a unit's newest replaces the one before), its measure appended whole. */
export function writeRun(folder: string, run: CheckRun): void {
  mkdirSync(folder, { recursive: true });
  const units: Record<string, GateReceipt> = {};
  for (const each of readReceipts(folder, run.tree)) units[each.unit] = each;
  for (const each of run.gates) units[each.unit] = each;
  writeAtomic(treeFile(folder, run.tree), `${JSON.stringify({ schema: 1, tree: run.tree, tip: run.tip, units }, null, 1)}\n`);
  appendFileSync(runsFile(folder), `${JSON.stringify(run)}\n`);
}

/** The required units split by what their receipts say: a pass covers (a replayed pass included), a fail is failed, anything else is missing. */
export function coverage(required: Required[], receipts: GateReceipt[]): { covered: Required[]; failed: Required[]; missing: Required[] } {
  const by = new Map(receipts.map((each) => [each.unit, each]));
  const covered: Required[] = [];
  const failed: Required[] = [];
  const missing: Required[] = [];
  for (const each of required) {
    const found = by.get(each.unit);
    if (found?.verdict === 'pass') covered.push(each);
    else if (found?.verdict === 'fail') failed.push(each);
    else missing.push(each);
  }
  return { covered, failed, missing };
}

/**
 * The refusal a `done` is met with in seconds: each command whose receipt is missing or failed, and the command that writes the
 * receipts (`runWith`); null where every required unit has a pass.
 */
export function refusalAtDone(required: Required[], receipts: GateReceipt[], runWith: string): string | null {
  const { failed, missing } = coverage(required, receipts);
  if (!failed.length && !missing.length) return null;
  const lines = [
    ...failed.map((each) => `\`${each.command}\` failed at this tip.`),
    ...missing.map((each) => `\`${each.command}\` has no receipt at this tip.`),
  ];
  return `${lines.join(' ')} Run \`${runWith}\` in your tree: it writes the receipts, and done is taken once every required command has a pass.`;
}

/** The gates with the named censuses first, in the order named; the rest keep theirs. A name no gate carries is passed over. */
export function censusFirst<T extends { name: string }>(gates: T[], first: readonly string[]): T[] {
  const named = first.flatMap((name) => gates.filter((gate) => gate.name === name));
  return [...named, ...gates.filter((gate) => !named.includes(gate))];
}
