/**
 * The merge's own record: one line per merge run in `local/merge-record.jsonl`, appended as the run ends. It carries the branch,
 * the run's wall time, each gate's time and slowest classes (`ms` sums them, so gates run side by side count twice), the tests
 * that passed only when run again, whether the run is a branch's first pass, the reds set down as main's and what ran over
 * budget; `tools/dev-metrics.mjs` reads it for the first-pass, wall-time and flake figures.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { GATE_BUDGET_MS, overBudget, type Slow } from './timing.ts';

export const MERGE_RECORD = 'local/merge-record.jsonl';

export type LeftProcess = { pid: number; created: string; stopped: boolean };
export type GateRecord = {
  name: string; verdict: string; ms: number; replayed: boolean; mainRed: boolean; abnormal: boolean; slowest: Slow[]; over: boolean; left: LeftProcess[];
  flakes?: string[];
  classes?: { ran: number; replayed: number; flaked: number; mainRed: number; red: number; deferred: number };
};
export type MainRedRecord = { gate: string; unit: string; key: string };
export type MergeRecord = {
  schema: 1; branch: string; at: string; ms: number; wallMs?: number; flakes?: number; attempt: number; firstPass: boolean; passed: boolean;
  gates: GateRecord[]; mainReds: MainRedRecord[]; overBudget: { gates: string[]; merge: boolean };
  heavy?: { deferred: string[] };
};

const passes = (gate: GateRecord): boolean => gate.verdict === 'PASS' || gate.verdict === 'FLAKE' || (gate.verdict === 'FAIL' && gate.mainRed);

export function buildRecord({ branch, at, ms, wallMs, gates, mainReds, previous, deferred = [] }: {
  branch: string; at: string; ms: number; wallMs: number; gates: GateRecord[]; mainReds: MainRedRecord[]; previous: MergeRecord[]; deferred?: string[];
}): MergeRecord {
  const attempt = previous.filter((each) => each.branch === branch).length + 1;
  const passed = gates.every(passes);
  return {
    schema: 1, branch, at, ms, wallMs, flakes: gates.reduce((n, gate) => n + (gate.flakes?.length ?? 0), 0), attempt, passed,
    firstPass: attempt === 1 && passed,
    gates: gates.map((gate) => ({ ...gate, over: gate.ms > GATE_BUDGET_MS })),
    mainReds, overBudget: overBudget(gates, ms), ...(deferred.length ? { heavy: { deferred } } : {}),
  };
}

const median = (values: number[]): number | null => {
  if (!values.length) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid]! : Math.round((sorted[mid - 1]! + sorted[mid]!) / 2);
};

export function appendRecord(file: string, record: MergeRecord): void {
  mkdirSync(dirname(file), { recursive: true });
  appendFileSync(file, `${JSON.stringify(record)}\n`);
}

export function readRecords(file: string): MergeRecord[] {
  if (!existsSync(file)) return [];
  return readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean).flatMap((line) => {
    try {
      return [JSON.parse(line) as MergeRecord];
    } catch {
      return [];
    }
  });
}

/** The figures a reader wants of a set of records. */
export function summarize(records: MergeRecord[]): {
  runs: number; firstPass: number; mainReds: number; replayed: number; gates: Record<string, number>; overBudget: string[];
  wall: { measured: number; medianMs: number | null; totalMs: number }; flakes: number; classes: { ran: number; replayed: number };
} {
  const classes = { ran: 0, replayed: 0 };
  const walls = records.flatMap((each) => (each.wallMs === undefined ? [] : [each.wallMs]));
  const gates: Record<string, number> = {};
  const over = new Set<string>();
  let replayed = 0;
  for (const record of records) {
    for (const gate of record.gates) {
      gates[gate.name] = (gates[gate.name] ?? 0) + gate.ms;
      if (gate.replayed) replayed += 1;
      classes.ran += gate.classes?.ran ?? 0;
      classes.replayed += gate.classes?.replayed ?? 0;
    }
    for (const name of record.overBudget.gates) over.add(name);
  }
  return {
    runs: records.length, firstPass: records.filter((each) => each.firstPass).length,
    mainReds: records.reduce((n, each) => n + each.mainReds.length, 0), replayed, gates, overBudget: [...over],
    wall: { measured: walls.length, medianMs: median(walls), totalMs: walls.reduce((n, ms) => n + ms, 0) },
    flakes: records.reduce((n, each) => n + (each.flakes ?? 0), 0), classes,
  };
}
