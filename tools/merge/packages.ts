/**
 * The packages' fingerprint pin and `daoris.packages.lock` (the ports-and-adapters design, §4). A package is a folder
 * holding `daoris.package.json` under a package root: the root itself where it holds one (a package of several projects side by side),
 * else each folder of the root but a test project (an adapter). Its version is its fingerprint: one hash over its sources' trees (its
 * folder, or the folders its manifest's `sources` names), its suite's tree, the contract cases it is held to, the tree of each Ports
 * folder it names, its evaluation cases' tree where it has them, the fingerprint of each package it is built over and the pinned version
 * of each outside thing it drives; no shared build file, since a package is completed and tested only in its own development. A row is
 * written only by a green run of the package's suite at that fingerprint; the gate `packages-lock` reads it: equal runs nothing,
 * different names the run the package needs.
 *
 *   node tools/merge/packages.ts check              the gate: every package's row holds its fingerprint (exit 0 kept, 1 stale)
 *   node tools/merge/packages.ts release <name>     run the package's suite and, green, write its row
 *   node tools/merge/packages.ts show               each package's fingerprint, for a reader
 *   node tools/merge/packages.ts carriers           the local branches and detached worktrees whose HEAD still holds the adapters' lock
 *
 * The fingerprint is taken at a commit and never of a dirty tree, so a row means what the commit holds.
 */
import { execFileSync, spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, realpathSync, renameSync, writeFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';

export const PACKAGE_ROOTS: readonly string[] = ['src/Daoris.Adapters', 'src/Daoris.Assistant'];
export const MANIFEST = 'daoris.package.json';
export const LOCK_FILE = 'daoris.packages.lock';
export const PORTS_ROOT = 'src/Daoris.Ports';

/**
 * The older readings, each kept for records the adapters' own lock and tool wrote, and each with its end:
 * - the adapters' tool hashed a package's folder with its manifest named `adapter.json`, and its rows came into this lock byte for byte,
 *   so a row is also kept where the folder, its manifest read under that name, still gives it. Each check names the rows kept so;
 *   when none is (each package released again), `CARRIED_MANIFEST`, `carriedTree` and `Sight.listing` are deleted. The comments of
 *   those packages' project files that name the old lock and manifest are rewritten at the same release, since an edit moves the row;
 * - a tree that holds `daoris.adapters.lock` is a branch made before the move, which the check refuses; `carriers` lists the refs
 *   that hold it, and when it lists none, `OLD_LOCK` and its reading are deleted.
 */
export const OLD_LOCK = 'daoris.adapters.lock';
const CARRIED_MANIFEST = 'adapter.json';

/** Where each kind of outside pin is written; a package names another versions file in its manifest's `pins`. */
const NUGET_PINS = 'src/Daoris.Desktop/Directory.Packages.props';
const TOOL_PINS = 'src/Daoris.Desktop/Daoris.Desktop.Driver/resources.json';

export type Manifest = {
  /** The name the package's suite goes by in the gates and the lock. */
  suite: string;
  /** The folder of its test project. */
  tests: string;
  /** The folders of the shared contract cases and the fakes the real side is held to. */
  contract: string[];
  /** The Ports folders the package implements or calls, by their folder name under src/Daoris.Ports. */
  ports: string[];
  /** The outside things it drives, each read where it is pinned: `nuget:<package>` or `tool:<name>`. */
  outside: string[];
  /** The packages it is built over, by name. */
  over?: string[];
  /** The central versions file its `nuget:` pins are read from, where it builds on another tree's than the desktop's. */
  pins?: string;
  /** The folders of its own sources, where they are not its folder alone: a package of several projects names its core and fakes. */
  sources?: string[];
  /** The folder of its evaluation cases, where it has them. */
  evals?: string;
};

/** What the package's evaluations scored at the row's release: the tier that ran, its cases and those that passed. */
export type EvalsRow = { tier: string; cases: number; passed: number };

export type LockRow = {
  fingerprint: string;
  tree: string;
  ports: Record<string, string>;
  outside: Record<string, string>;
  suite: string;
  toolchain: string;
  passed: string;
  evals?: EvalsRow;
};

export type Lock = { schema: 2; packages: Record<string, LockRow> };

export type Package = { name: string; folder: string };

/** A folder's entry as git lists it. */
export type Entry = { mode: string; tree: boolean; sha: string; name: string };

/** What a fingerprint reads of a commit: the tree hashes of paths, the text of a file, and the packages it holds. */
export interface Sight {
  tree(path: string): string | null;
  text(path: string): string | null;
  dirty(paths: string[]): string[];
  packages(): Package[];
  listing(path: string): Entry[] | null;
}

const sha = (text: string): string => createHash('sha256').update(text).digest('hex');

const escaped = (text: string): string => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** The id git gives a tree of these entries: its object's SHA-1, the entries in git's order (a folder's name as if it ended in `/`). */
export function treeHash(entries: readonly Entry[]): string {
  const key = (entry: Entry): Buffer => Buffer.from(entry.tree ? `${entry.name}/` : entry.name);
  const body = Buffer.concat([...entries].sort((a, b) => Buffer.compare(key(a), key(b)))
    .map((entry) => Buffer.concat([Buffer.from(`${entry.mode.replace(/^0+/, '')} ${entry.name}\0`), Buffer.from(entry.sha, 'hex')])));
  return createHash('sha1').update(Buffer.concat([Buffer.from(`tree ${body.length}\0`), body])).digest('hex');
}

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
export function pinOf(pin: string, sight: Pick<Sight, 'text'>, versions: string = NUGET_PINS): string | null {
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

function folderOf(name: string, sight: Sight): string {
  const found = sight.packages().find((each) => each.name === name);
  if (!found) throw new Error(`${name} is no package under ${PACKAGE_ROOTS.join(' or ')}`);
  return found.folder;
}

/** The tree of a package's folder as the adapters' tool hashed it, its manifest under that tool's name; null where it cannot be. */
function carriedTree(folder: string, sight: Sight): string | null {
  const entries = sight.listing(folder);
  if (!entries?.some((entry) => entry.name === MANIFEST)) return null;
  return treeHash(entries.map((entry) => (entry.name === MANIFEST ? { ...entry, name: CARRIED_MANIFEST } : entry)));
}

/** The fingerprint of a package at the sight's commit; throws where an input is not there, which the declaration is wrong for. */
export function fingerprintOf(name: string, sight: Sight, seen: string[] = []): Computed {
  return fingerprintBy(name, sight, seen, false)!;
}

/** `carried` takes it as the adapters' tool took it (its folder with the manifest under that tool's name), null where it cannot be. */
function fingerprintBy(name: string, sight: Sight, seen: string[], carried: boolean): Computed | null {
  if (seen.includes(name)) throw new Error(`${name} is built over itself: ${[...seen, name].join(' -> ')}`);
  const folder = folderOf(name, sight);
  const manifest = manifestAt(folder, sight);
  if (carried && (manifest.sources || manifest.evals)) return null;
  const tree = carried ? carriedTree(folder, sight) : manifest.sources
    ? sha(JSON.stringify(Object.fromEntries([...manifest.sources].sort().map((path) => [path, need(sight.tree(path), path)]))))
    : need(sight.tree(folder), folder);
  if (tree === null) return null;
  const ports: Record<string, string> = {};
  for (const port of [...manifest.ports].sort()) ports[port] = need(sight.tree(`${PORTS_ROOT}/${port}`), `${PORTS_ROOT}/${port}`);
  const outside: Record<string, string> = {};
  for (const pin of [...manifest.outside].sort()) outside[pin] = need(pinOf(pin, sight, manifest.pins), `the pin of ${pin}`);
  const over: Record<string, string> = {};
  for (const other of [...(manifest.over ?? [])].sort()) {
    const built = fingerprintBy(other, sight, [...seen, name], carried);
    if (built === null) return null;
    over[other] = built.fingerprint;
  }
  const suite = need(sight.tree(manifest.tests), manifest.tests);
  const contract = Object.fromEntries([...manifest.contract].sort().map((path) => [path, need(sight.tree(path), path)]));
  const evals = manifest.evals === undefined ? undefined : need(sight.tree(manifest.evals), manifest.evals);
  const fingerprint = sha(JSON.stringify({ name, tree, suite, contract, ports, over, outside, evals }));
  return { fingerprint, tree, ports, outside };
}

function need<T>(value: T | null, what: string): T {
  if (value === null) throw new Error(`${what} is not in the commit`);
  return value;
}

/** A package's manifest, `daoris.package.json` in its folder. */
export function manifestAt(folder: string, sight: Pick<Sight, 'text'>): Manifest {
  const path = `${folder}/${MANIFEST}`;
  const text = sight.text(path);
  if (text === null) throw new Error(`${posix.basename(folder)} has no ${path}: a package declares its suite, its ports and the outside things it drives`);
  const manifest = JSON.parse(text) as Partial<Manifest>;
  for (const key of ['suite', 'tests'] as const) if (typeof manifest[key] !== 'string') throw new Error(`${path} names no ${key}`);
  for (const key of ['contract', 'ports', 'outside'] as const) if (!Array.isArray(manifest[key])) throw new Error(`${path} lists no ${key}`);
  return manifest as Manifest;
}

export type Verdict = { name: string; state: 'kept' | 'stale' | 'missing'; fingerprint: string; row: string | null; carried?: true };

/** Each package's fingerprint against its row: kept where equal (or equal as the adapters' tool took it), stale where different, missing where there is no row. */
export function judge(lock: Lock, sight: Sight): Verdict[] {
  return sight.packages().map((each) => each.name).sort().map((name): Verdict => {
    const { fingerprint } = fingerprintOf(name, sight);
    const row = lock.packages[name]?.fingerprint ?? null;
    if (row === fingerprint) return { name, fingerprint, row, state: 'kept' };
    if (row !== null && row === fingerprintBy(name, sight, [], true)?.fingerprint) return { name, fingerprint, row, state: 'kept', carried: true };
    return { name, fingerprint, row, state: row === null ? 'missing' : 'stale' };
  });
}

/** The lock with a row written for <name>; rows are kept in name order so the file is a stable diff. */
export function withRow(lock: Lock, name: string, row: LockRow): Lock {
  const packages = Object.fromEntries(Object.entries({ ...lock.packages, [name]: row }).sort(([a], [b]) => (a < b ? -1 : 1)));
  return { schema: 2, packages };
}

export const emptyLock = (): Lock => ({ schema: 2, packages: {} });

function gitIn(root: string): (...args: string[]) => string | null {
  return (...args) => {
    try {
      return execFileSync('git', args, { cwd: root, encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] }).trim();
    } catch {
      return null;
    }
  };
}

/** The real sight: a commit of the repository at <root>, read through git. */
export function gitSight(root: string, ref = 'HEAD'): Sight {
  const git = gitIn(root);
  return {
    tree: (path) => git('rev-parse', `${ref}:${path}`),
    text: (path) => {
      try {
        return execFileSync('git', ['show', `${ref}:${path}`], { cwd: root, encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] });
      } catch {
        return null;
      }
    },
    dirty: (paths) => (git('status', '--porcelain', '--', ...paths) ?? '').split('\n').filter(Boolean),
    // A root holding a manifest is one package; else every folder of it that is not a test project is one, and one that declares no
    // manifest fails its fingerprint, naming it.
    packages: () => PACKAGE_ROOTS.flatMap((root): Package[] => {
      if (git('cat-file', '-e', `${ref}:${root}/${MANIFEST}`) !== null) return [{ name: posix.basename(root), folder: root }];
      return (git('ls-tree', '-d', '--name-only', ref, `${root}/`) ?? '').split('\n')
        .filter((path) => path.length > root.length + 1 && !path.endsWith('.Tests'))
        .map((path) => ({ name: path.slice(root.length + 1), folder: path }));
    }),
    listing: (path) => {
      const listed = git('ls-tree', '-z', `${ref}:${path}`);
      if (listed === null) return null;
      return listed.split('\0').filter(Boolean).map((line) => {
        const [meta, name] = line.split('\t') as [string, string];
        const [mode, type, object] = meta.split(' ') as [string, string, string];
        return { mode, tree: type === 'tree', sha: object, name };
      });
    },
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

/** The local branches, and the worktrees on no branch, whose HEAD still holds the adapters' lock. */
export function carriers(root: string): string[] {
  const git = gitIn(root);
  const branches = (git('for-each-ref', '--format=%(refname:short)', 'refs/heads') ?? '').split('\n').filter(Boolean);
  const detached = (git('worktree', 'list', '--porcelain') ?? '').split(/\n\n/).filter((block) => /^detached$/m.test(block))
    .map((block) => ({ at: /^worktree (.+)$/m.exec(block)?.[1] ?? '', head: /^HEAD (\w+)$/m.exec(block)?.[1] ?? '' }));
  return [
    ...branches.filter((branch) => git('cat-file', '-e', `${branch}:${OLD_LOCK}`) !== null),
    ...detached.filter(({ head }) => git('cat-file', '-e', `${head}:${OLD_LOCK}`) !== null).map(({ at, head }) => `${at} (${head.slice(0, 10)})`),
  ];
}

const toolchain = (): string => {
  const dotnet = execFileSync('dotnet', ['--version'], { encoding: 'utf8', windowsHide: true }).trim();
  return sha(`${process.version}\0${dotnet}`);
};

/** A branch made before the move holds the adapters' lock: its rows that differ from this lock's are what it released there. */
function refuseOldLock(root: string): number {
  let released: string[] = [];
  try {
    const old = JSON.parse(readFileSync(join(root, OLD_LOCK), 'utf8')) as { adapters?: Record<string, LockRow> };
    const kept = readLock(root).packages;
    released = Object.entries(old.adapters ?? {}).filter(([name, row]) => kept[name]?.fingerprint !== row.fingerprint).map(([name]) => name);
  } catch {
    released = [];
  }
  console.error(`packages-lock: this tree holds ${OLD_LOCK}, which ${LOCK_FILE} replaced: it is a branch made before the move, so merge main into it`
    + `${released.length ? `, and release again what it released there (${released.join(', ')}) with node tools/merge/packages.ts release <name>` : ''}`);
  return 1;
}

/** The gate: a line per package, and failure where a row is stale or missing, naming the run the package needs. */
export function check(root: string, sight: Sight = gitSight(root)): number {
  if (existsSync(join(root, OLD_LOCK))) return refuseOldLock(root);
  const lock = readLock(root);
  let stale = 0;
  for (const verdict of judge(lock, sight)) {
    const at = verdict.fingerprint.slice(0, 12);
    if (verdict.carried) console.log(`packages-lock: ${verdict.name} kept as the adapters' lock wrote it (${verdict.row?.slice(0, 12)}): its next release writes it at ${at}`);
    else if (verdict.state === 'kept') console.log(`packages-lock: ${verdict.name} kept (${at}): its suite runs nothing`);
    else {
      stale += 1;
      console.error(`packages-lock: ${verdict.name} is ${verdict.state} (${at} against ${verdict.row?.slice(0, 12) ?? 'no row'}): run node tools/merge/packages.ts release ${verdict.name}`);
    }
  }
  return stale === 0 ? 0 : 1;
}

/** Runs the package's suite at the commit and, only on a complete green run, writes its row. */
export function release(root: string, name: string, sight: Sight = gitSight(root)): number {
  const folder = folderOf(name, sight);
  const manifest = manifestAt(folder, sight);
  const inputs = [...manifest.sources ?? [folder], manifest.tests, ...manifest.contract, ...manifest.ports.map((port) => `${PORTS_ROOT}/${port}`), ...manifest.evals ? [manifest.evals] : []];
  const dirty = sight.dirty(inputs);
  if (dirty.length > 0) {
    console.error(`packages-lock: ${name}'s inputs hold uncommitted changes, and a row names a commit:\n${dirty.join('\n')}`);
    return 1;
  }

  const computed = fingerprintOf(name, sight);
  const ran = spawnSync('dotnet', ['test', manifest.tests, '-nodeReuse:false', '--nologo'], { cwd: root, stdio: 'inherit', windowsHide: true });
  if (ran.status !== 0) {
    console.error(`packages-lock: ${name}'s suite did not pass, so its row is not written`);
    return 1;
  }

  const row: LockRow = { ...computed, suite: manifest.suite, toolchain: toolchain(), passed: new Date().toISOString() };
  writeLock(root, withRow(readLock(root), name, row));
  console.log(`packages-lock: ${name} written at ${computed.fingerprint.slice(0, 12)}`);
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
      for (const each of sight.packages().map((one) => one.name).sort()) console.log(`${each} ${fingerprintOf(each, sight).fingerprint}`);
      return 0;
    }
    if (command === 'carriers') {
      const held = carriers(root);
      for (const ref of held) console.log(ref);
      console.log(held.length ? `packages-lock: ${held.length} ref(s) still hold ${OLD_LOCK}` : `packages-lock: no local branch or worktree holds ${OLD_LOCK}`);
      return 0;
    }
    console.error('usage: node tools/merge/packages.ts check | release <name> | show | carriers');
    return 2;
  } catch (error) {
    console.error(`packages-lock: ${(error as Error).message}`);
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
