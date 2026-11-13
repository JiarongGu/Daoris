/**
 * The adapters' fingerprint pin and `daoris.adapters.lock` (the ports-and-adapters design, §4). An adapter's version is its fingerprint:
 * one hash over its folder's tree, its dependency suite's tree, the contract cases it is held to, the tree of each Ports folder it
 * implements, the fingerprint of each adapter it is built over, the pinned version of each outside thing it drives, and the build files
 * that reach every project. A row of the lock is written only by a green run of the package's suite at that fingerprint, and the merge's
 * gate `adapters-lock` reads it in milliseconds: equal runs nothing, different names the run the package needs.
 *
 *   node tools/merge/adapters.ts check              the gate: every package's row holds its fingerprint (exit 0 kept, 1 stale)
 *   node tools/merge/adapters.ts release <name>     run the package's suite and, green, write its row
 *   node tools/merge/adapters.ts show               each package's fingerprint, for a reader
 *
 * The fingerprint is taken at a commit and never of a dirty tree, so a row means what the commit holds.
 */
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync, realpathSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ADAPTERS_ROOT = 'src/Daoris.Adapters';
export const LOCK_FILE = 'daoris.adapters.lock';
export const PORTS_ROOT = 'src/Daoris.Ports';

/** The build files that reach every project: a move in one runs every suite, the price of a fingerprint that never misses one. */
export const BUILD_FILES = ['Directory.Build.props', 'src/Daoris.Desktop/Directory.Build.props', 'src/Layers.props'];

/** Where each kind of outside pin is written; a package names another versions file in its manifest's `pins`. */
const NUGET_PINS = 'src/Daoris.Desktop/Directory.Packages.props';
const TOOL_PINS = 'src/Daoris.Desktop/Daoris.Desktop.Driver/resources.json';

export type Manifest = {
  /** The name the package's suite goes by in the gates and the lock. */
  suite: string;
  /** The folder of the dependency test project beside the adapter. */
  tests: string;
  /** The folders of the shared contract cases and the fakes the real side is held to. */
  contract: string[];
  /** The Ports folders the package implements or calls, by their folder name under src/Daoris.Ports. */
  ports: string[];
  /** The outside things it drives, each read where it is pinned: `nuget:<package>` or `tool:<name>`. */
  outside: string[];
  /** The adapters it is built over, by name. */
  over?: string[];
  /** The central versions file its `nuget:` pins are read from, where it builds on another tree's than the desktop's. */
  pins?: string;
};

export type LockRow = {
  fingerprint: string;
  tree: string;
  ports: Record<string, string>;
  outside: Record<string, string>;
  suite: string;
  toolchain: string;
  passed: string;
};

export type Lock = { schema: 1; adapters: Record<string, LockRow> };

/** What a fingerprint reads of a commit: the tree and blob hashes of paths, and the text of a file. */
export interface Sight {
  tree(path: string): string | null;
  blob(path: string): string | null;
  text(path: string): string | null;
  dirty(paths: string[]): string[];
  adapters(): string[];
}

const sha = (text: string): string => createHash('sha256').update(text).digest('hex');

const escaped = (text: string): string => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** <value> with each `$(Property)` replaced by what <props> sets it to, as the build reads the committed file; null where one is unset. */
function expanded(value: string, props: string, depth = 0): string | null {
  if (!value.includes('$(')) return value;
  if (depth > 8) return null;
  let unset = false;
  const next = value.replace(/\$\((\w+)\)/g, (_, property: string) => {
    const set = new RegExp(`<${escaped(property)}\\b[^>]*>([^<]*)</${escaped(property)}>`).exec(props);
    if (!set) unset = true;
    return set?.[1] ?? '';
  });
  return unset ? null : expanded(next, props, depth + 1);
}

/** The version a `nuget:` or `tool:` pin names, read where it is written (a tool's is the versions its table holds); null where the pin is not there. */
export function pinOf(pin: string, sight: Sight, versions: string = NUGET_PINS): string | null {
  const [kind, name] = pin.split(':', 2);
  if (kind === 'nuget') {
    const props = sight.text(versions) ?? '';
    const found = new RegExp(`<PackageVersion Include="${escaped(name ?? '')}" Version="([^"]+)"`).exec(props);
    return found?.[1] === undefined ? null : expanded(found[1], props);
  }
  if (kind === 'tool') {
    const table = JSON.parse(sight.text(TOOL_PINS) ?? '{}') as { tools?: Record<string, { versions?: Record<string, unknown> } | undefined> };
    const held = Object.keys(table.tools?.[name ?? '']?.versions ?? {}).sort();
    return held.length === 0 ? null : held.join(',');
  }
  return null;
}

export type Computed = { fingerprint: string; tree: string; ports: Record<string, string>; outside: Record<string, string> };

/** The fingerprint of an adapter at the sight's commit; throws where an input is not there, which the declaration is wrong for. */
export function fingerprintOf(name: string, sight: Sight, seen: string[] = []): Computed {
  if (seen.includes(name)) throw new Error(`${name} is built over itself: ${[...seen, name].join(' -> ')}`);
  const manifest = manifestOf(name, sight);
  const folder = `${ADAPTERS_ROOT}/${name}`;
  const tree = need(sight.tree(folder), folder);
  const ports: Record<string, string> = {};
  for (const port of [...manifest.ports].sort()) ports[port] = need(sight.tree(`${PORTS_ROOT}/${port}`), `${PORTS_ROOT}/${port}`);
  const outside: Record<string, string> = {};
  for (const pin of [...manifest.outside].sort()) outside[pin] = need(pinOf(pin, sight, manifest.pins), `the pin of ${pin}`);
  const over: Record<string, string> = {};
  for (const other of [...(manifest.over ?? [])].sort()) over[other] = fingerprintOf(other, sight, [...seen, name]).fingerprint;
  const suite = need(sight.tree(manifest.tests), manifest.tests);
  const contract = Object.fromEntries([...manifest.contract].sort().map((path) => [path, need(sight.tree(path), path)]));
  const builds = Object.fromEntries(BUILD_FILES.map((path) => [path, need(sight.blob(path), path)]));
  const fingerprint = sha(JSON.stringify({ name, tree, suite, contract, ports, over, outside, builds }));
  return { fingerprint, tree, ports, outside };
}

function need<T>(value: T | null, what: string): T {
  if (value === null) throw new Error(`${what} is not in the commit`);
  return value;
}

/** An adapter's manifest, `adapter.json` in its folder. */
export function manifestOf(name: string, sight: Sight): Manifest {
  const path = `${ADAPTERS_ROOT}/${name}/adapter.json`;
  const text = sight.text(path);
  if (text === null) throw new Error(`${name} has no ${path}: a package declares its suite, its ports and the outside things it drives`);
  const manifest = JSON.parse(text) as Partial<Manifest>;
  for (const key of ['suite', 'tests'] as const) if (typeof manifest[key] !== 'string') throw new Error(`${path} names no ${key}`);
  for (const key of ['contract', 'ports', 'outside'] as const) if (!Array.isArray(manifest[key])) throw new Error(`${path} lists no ${key}`);
  return manifest as Manifest;
}

export type Verdict = { name: string; state: 'kept' | 'stale' | 'missing'; fingerprint: string; row: string | null };

/** Each package's fingerprint against its row: kept where equal, stale where different, missing where there is no row. */
export function judge(lock: Lock, sight: Sight): Verdict[] {
  return sight.adapters().sort().map((name) => {
    const { fingerprint } = fingerprintOf(name, sight);
    const row = lock.adapters[name]?.fingerprint ?? null;
    return { name, fingerprint, row, state: row === null ? 'missing' : row === fingerprint ? 'kept' : 'stale' };
  });
}

/** The lock with a row written for <name>; rows are kept in name order so the file is a stable diff. */
export function withRow(lock: Lock, name: string, row: LockRow): Lock {
  const adapters = Object.fromEntries(Object.entries({ ...lock.adapters, [name]: row }).sort(([a], [b]) => (a < b ? -1 : 1)));
  return { schema: 1, adapters };
}

export const emptyLock = (): Lock => ({ schema: 1, adapters: {} });

/** The real sight: a commit of the repository at <root>, read through git. */
export function gitSight(root: string, ref = 'HEAD'): Sight {
  const git = (...args: string[]): string | null => {
    try {
      return execFileSync('git', args, { cwd: root, encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] }).trim();
    } catch {
      return null;
    }
  };
  return {
    tree: (path) => git('rev-parse', `${ref}:${path}`),
    blob: (path) => git('rev-parse', `${ref}:${path}`),
    text: (path) => {
      try {
        return execFileSync('git', ['show', `${ref}:${path}`], { cwd: root, encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] });
      } catch {
        return null;
      }
    },
    dirty: (paths) => (git('status', '--porcelain', '--', ...paths) ?? '').split('\n').filter(Boolean),
    // Every folder that is not a test project is a package, and one that declares no adapter.json fails its fingerprint, naming it.
    adapters: () => (git('ls-tree', '--name-only', ref, `${ADAPTERS_ROOT}/`) ?? '')
      .split('\n')
      .map((path) => path.slice(ADAPTERS_ROOT.length + 1))
      .filter((name) => name.length > 0 && !name.endsWith('.Tests')),
  };
}

export function readLock(root: string): Lock {
  try {
    return JSON.parse(readFileSync(join(root, LOCK_FILE), 'utf8')) as Lock;
  } catch {
    return emptyLock();
  }
}

export function writeLock(root: string, lock: Lock): void {
  const path = join(root, LOCK_FILE);
  writeFileSync(`${path}.tmp`, `${JSON.stringify(lock, null, 2)}\n`, 'utf8');
  renameSync(`${path}.tmp`, path);
}

const toolchain = (): string => {
  const dotnet = execFileSync('dotnet', ['--version'], { encoding: 'utf8', windowsHide: true }).trim();
  return sha(`${process.version}\0${dotnet}`);
};

/** The gate: a line per package, and failure where a row is stale or missing, naming the run the package needs. */
export function check(root: string, sight: Sight = gitSight(root)): number {
  const lock = readLock(root);
  let stale = 0;
  for (const verdict of judge(lock, sight)) {
    if (verdict.state === 'kept') console.log(`adapters-lock: ${verdict.name} kept (${verdict.fingerprint.slice(0, 12)}): its suite runs nothing`);
    else {
      stale += 1;
      console.error(`adapters-lock: ${verdict.name} is ${verdict.state} (${verdict.fingerprint.slice(0, 12)} against ${verdict.row?.slice(0, 12) ?? 'no row'}): run node tools/merge/adapters.ts release ${verdict.name}`);
    }
  }
  return stale === 0 ? 0 : 1;
}

/** Runs the package's suite at the commit and, only on a complete green run, writes its row. */
export function release(root: string, name: string, sight: Sight = gitSight(root)): number {
  const manifest = manifestOf(name, sight);
  const dirty = sight.dirty([`${ADAPTERS_ROOT}/${name}`, manifest.tests, ...manifest.contract, ...BUILD_FILES, ...manifest.ports.map((port) => `${PORTS_ROOT}/${port}`)]);
  if (dirty.length > 0) {
    console.error(`adapters-lock: ${name}'s inputs hold uncommitted changes, and a row names a commit:\n${dirty.join('\n')}`);
    return 1;
  }

  const computed = fingerprintOf(name, sight);
  const ran = spawnSync('dotnet', ['test', manifest.tests, '-nodeReuse:false', '--nologo'], { cwd: root, stdio: 'inherit', windowsHide: true });
  if (ran.status !== 0) {
    console.error(`adapters-lock: ${name}'s suite did not pass, so its row is not written`);
    return 1;
  }

  const row: LockRow = { ...computed, suite: manifest.suite, toolchain: toolchain(), passed: new Date().toISOString() };
  writeLock(root, withRow(readLock(root), name, row));
  console.log(`adapters-lock: ${name} written at ${computed.fingerprint.slice(0, 12)}`);
  return 0;
}

function main(args: string[]): number {
  const root = dirname(dirname(dirname(fileURLToPath(import.meta.url))));
  const [command, name] = args;
  try {
    if (command === 'check') return check(root);
    if (command === 'release' && name) return release(root, name);
    if (command === 'show') {
      const sight = gitSight(root);
      for (const each of sight.adapters().sort()) console.log(`${each} ${fingerprintOf(each, sight).fingerprint}`);
      return 0;
    }
    console.error('usage: node tools/merge/adapters.ts check | release <name> | show');
    return 2;
  } catch (error) {
    console.error(`adapters-lock: ${(error as Error).message}`);
    return 2;
  }
}

const invoked = process.argv[1];
if (invoked && (() => {
  try {
    return realpathSync(invoked) === realpathSync(fileURLToPath(import.meta.url));
  } catch {
    return false;
  }
})()) process.exit(main(process.argv.slice(2)));
