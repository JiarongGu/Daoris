/**
 * The seams `flow.ts` runs on, made from what the merge tool hands in (its gate runner, its reach table, its failure reader and
 * its stores) and from git and the toolchain, which are read here. One cached listing of a tree serves every gate of a run.
 */
import { spawn, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { isStamped, NPM_CI, stampDependencies } from '../merge-branch/dependencies.mjs';
import { BuildLedger } from './build-once.ts';
import type { FlowDeps, GateResult, PlanGate } from './flow.ts';
import { git } from './git.ts';
import { LockInstalls } from './lock-install.ts';
import { findingReds, verdictReds, type MainRed } from './main-red.ts';
import { MERGE_RECORD, readRecords } from './merge-record.ts';
import { killTree, listProcesses } from './processes.ts';
import { budgetMs } from './timeout.ts';
import type { Slow } from './timing.ts';
import type { StoredVerdict, Toolchain, TreeEntry } from './verdict-store.ts';

type Signed = { signature: unknown; names?: string; unit?: string; test?: string };
type Finding = { state: string; gate?: string; test: string; key: string; since?: string; lastSeen?: { commit?: string | null; at?: string } | null };

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
  privateOf: (where: string) => string;
};

const listings = new Map<string, TreeEntry[]>();
const entriesOf = (where: string, tree: string): TreeEntry[] => {
  const kept = listings.get(tree);
  if (kept) return kept;
  const entries = git(where, ['ls-tree', '-r', '-z', '--full-tree', tree]).out.split('\0').filter(Boolean).flatMap((line) => {
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

/** The paths the tree at `where` holds changed against its HEAD: in a merge not yet committed, what the merge changed. */
const diffOf = (where: string): string[] => git(where, ['--no-optional-locks', 'diff', '--name-only', '--no-renames', '-z', 'HEAD']).out.split('\0').filter(Boolean);

const npmCi = (where: string, folder: string): Promise<{ ok: boolean; note: string }> => new Promise((done) => {
  const child = spawn(NPM_CI, { cwd: join(where, folder), shell: true, stdio: ['ignore', 'inherit', 'inherit'], windowsHide: true });
  child.on('error', (error) => done({ ok: false, note: error.message }));
  child.on('close', (code) => {
    if (code === 0) stampDependencies(where, folder);
    done({ ok: code === 0, note: `exit ${code}` });
  });
});

/** The deps of one run in the checkout `root`, whose verdict store is `home`'s, with a build ledger of its own. */
export function flowDepsOf(seams: Seams, root: string, home: string, kind: string | null, replayAll = false): FlowDeps {
  const lanes = seams.lanesOf(root);
  return {
    replayAll,
    privateOf: seams.privateOf,
    timeoutMs: (gate) => budgetMs(gate, kind),
    lastSlowest: (name) => readRecords(join(home, MERGE_RECORD)).reverse().flatMap((record) => record.gates.filter((each) => each.name === name && each.slowest.length)).at(0)?.slowest ?? [],
    runGate: seams.runGate,
    reaching: (gate, paths) => seams.pathsReaching(gate, paths, lanes),
    reachSource: createHash('sha1').update(JSON.stringify([seams.table, lanes])).digest('hex'),
    readVerdicts: () => seams.readVerdicts(home).verdicts,
    entriesOf,
    diffOf,
    toolchain: toolchainOf(),
    slowestOf: (trx) => seams.slowestClasses(trx),
    failuresOf: (where, result, trx) => seams.failuresOf(where, result, trx)
      .map((each) => ({
        unit: each.unit ?? each.test ?? result.gate.name, key: seams.signatureKey(each.signature), test: each.names === 'test' ? each.test ?? null : null,
      })),
    isProcessGate: seams.isProcessGate,
    ledger: new BuildLedger(),
    listProcesses,
    killTree,
    installs: new LockInstalls({ changed: diffOf, current: isStamped, install: npmCi, say: (line) => console.log(line) }),
  };
}

/** Main's standing for the line at `root`: its open reds the findings record, and its failing verdicts on its own tree. */
export function mainRedsOf(seams: Seams, root: string, home: string): MainRed[] {
  let mainTree: string | null = null;
  try {
    mainTree = git(root, ['rev-parse', 'HEAD^{tree}']).out.trim();
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
