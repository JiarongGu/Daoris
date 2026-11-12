/**
 * The seams `flow.ts` runs on, made from what the merge tool hands in (its gate runner, its reach table, its failure reader and
 * its stores) and from git and the toolchain, which are read here. One cached listing of a tree serves every gate of a run.
 */
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { spawnGit } from '../fsx.mjs';
import { BuildLedger } from './build-once.ts';
import type { FlowDeps, GateResult, PlanGate } from './flow.ts';
import { findingReds, verdictReds, type MainRed } from './main-red.ts';
import { MERGE_RECORD, readRecords } from './merge-record.ts';
import { killTree, listProcesses } from './processes.ts';
import { budgetMs } from './timeout.ts';
import type { Slow } from './timing.ts';
import type { StoredVerdict, Toolchain, TreeEntry } from './verdict-store.ts';

type Signed = { signature: unknown; unit?: string; test?: string };
type Finding = { state: string; gate?: string; test: string; key: string; lastSeen?: { commit?: string | null } | null; at?: string };

/** What the merge tool hands in. */
export type Seams = {
  runGate: FlowDeps['runGate'];
  pathsReaching: (gate: string, paths: string[], lanes: unknown[]) => string[];
  table: unknown;
  lanesOf: (root: string) => unknown[];
  slowestClasses: (trx: string) => Slow[];
  failuresOf: (where: string, result: GateResult, trx: string | null) => Signed[];
  signatureKey: (signature: unknown) => string;
  isProcessGate: (gate: PlanGate) => boolean;
  readVerdicts: (home: string) => { verdicts: StoredVerdict[] };
  readFindings: (home: string) => { findings: Finding[] };
  expired: (finding: Finding) => boolean;
};

const git = (cwd: string, args: string[]): string => {
  const ran = spawnGit(args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
  if (ran.status !== 0) throw new Error(`git ${args[0]} failed: ${String(ran.stderr).trim()}`);
  return ran.stdout;
};

const listings = new Map<string, TreeEntry[]>();
const entriesOf = (where: string, tree: string): TreeEntry[] => {
  const kept = listings.get(tree);
  if (kept) return kept;
  const entries = git(where, ['ls-tree', '-r', '-z', '--full-tree', tree]).split('\0').filter(Boolean).flatMap((line) => {
    const found = /^\d+ \w+ ([0-9a-f]+)\t(.*)$/s.exec(line);
    return found ? [{ blob: found[1] as string, path: found[2] as string }] : [];
  });
  listings.set(tree, entries);
  return entries;
};

/** The installed .NET SDKs, read from the SDK folder and never by running `dotnet`, which a stand-in on the path would answer. */
const sdksOf = (): string => {
  const root = process.env['DOTNET_ROOT'] ?? (process.platform === 'win32' ? join(process.env['ProgramFiles'] ?? 'C:\\Program Files', 'dotnet') : '/usr/share/dotnet');
  try {
    return readdirSync(join(root, 'sdk')).filter((name) => /^\d/.test(name)).sort().join('+') || 'none';
  } catch {
    return 'none';
  }
};

let toolchain: Toolchain | null = null;
export const toolchainOf = (): Toolchain => (toolchain ??= { node: process.version, dotnet: sdksOf() });

/** The deps of one run in the checkout `root`, whose verdict store is `home`'s, with a build ledger of its own. */
export function flowDepsOf(seams: Seams, root: string, home: string, kind: string | null): FlowDeps {
  const lanes = seams.lanesOf(root);
  return {
    timeoutMs: (gate) => budgetMs(gate, kind),
    lastSlowest: (name) => readRecords(join(home, MERGE_RECORD)).reverse().flatMap((record) => record.gates.filter((each) => each.name === name && each.slowest.length)).at(0)?.slowest ?? [],
    runGate: seams.runGate,
    reaching: (gate, paths) => seams.pathsReaching(gate, paths, lanes),
    reachSource: createHash('sha1').update(JSON.stringify([seams.table, lanes])).digest('hex'),
    readVerdicts: () => seams.readVerdicts(home).verdicts,
    entriesOf,
    diffOf: (where) => git(where, ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', 'HEAD']).split('\0').filter(Boolean),
    toolchain: toolchainOf(),
    slowestOf: (trx) => seams.slowestClasses(trx),
    failuresOf: (where, result, trx) => seams.failuresOf(where, result, trx)
      .map((each) => ({ unit: each.unit ?? each.test ?? result.gate.name, key: seams.signatureKey(each.signature) })),
    isProcessGate: seams.isProcessGate,
    ledger: new BuildLedger(),
    listProcesses,
    killTree,
  };
}

/** Main's standing for the line at `root`: its open reds the findings record, and its failing verdicts on its own tree. */
export function mainRedsOf(seams: Seams, root: string, home: string): MainRed[] {
  let mainTree: string | null = null;
  try {
    mainTree = git(root, ['rev-parse', 'HEAD^{tree}']).trim();
  } catch {
    mainTree = null;
  }
  const findings = seams.readFindings(home).findings.map((each) => ({ ...each, expired: seams.expired(each) }));
  return [...findingReds(findings), ...verdictReds(seams.readVerdicts(home).verdicts, mainTree)];
}

/** A command run in a shell at `root` with `env`, as a regeneration is. */
export const shellRunner = (root: string, env: NodeJS.ProcessEnv) => (command: string): { status: number | null; output: string } => {
  const ran = spawnSync(command, { cwd: root, encoding: 'utf8', shell: true, windowsHide: true, env });
  return { status: ran.status, output: `${ran.stdout}${ran.stderr}` };
};
