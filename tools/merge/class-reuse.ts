/**
 * The reuse of a test class's pass inside the `reached` gate. A class's key is the digest of every tree file that reaches it (its
 * own files, the source areas it names, the build files above it), its suite's declaration and the toolchain; a class whose key
 * passed before is not run again, and a change to a widely referenced project invalidates exactly the classes that name it.
 * The pass is recorded per class once it passes, in a store of its own (`local/reached-classes.json`).
 */
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

export type Unit = { suite: string; unit: string };
/** What the census says of a tree, seen from here: the files, whether a unit reaches one, and its text for the digest. */
export type KeyContext = {
  /** The files that can reach this unit's suite at all, so a unit asks only of those. */
  candidates: (unit: Unit) => string[];
  reaches: (unit: Unit, path: string) => boolean;
  digestOf: (path: string) => string;
  declaration: (suite: string) => string;
  toolchain: string;
};

const sha = (text: string): string => createHash('sha1').update(text).digest('hex');
export const unitId = (unit: Unit): string => `${unit.suite}|${unit.unit}`;

export function classKey(unit: Unit, context: KeyContext): string {
  const reached = context.candidates(unit).filter((path) => context.reaches(unit, path)).map((path) => `${path}\0${context.digestOf(path)}`).sort();
  return sha([unitId(unit), context.declaration(unit.suite), context.toolchain, ...reached].join('\n'));
}

type Pass = { key: string; at: string; run?: string };
type Passes = Record<string, Pass[]>;
const KEPT_PER_CLASS = 3;

export class PassedClasses {
  private passes: Passes = {};
  private readonly file: string;
  constructor(file: string) {
    this.file = file;
    try {
      if (existsSync(file)) {
        const parsed = JSON.parse(readFileSync(file, 'utf8')) as { passes?: Passes };
        if (parsed.passes && typeof parsed.passes === 'object') this.passes = parsed.passes;
      }
    } catch {
      this.passes = {};
    }
  }

  has(id: string, key: string): boolean {
    return (this.passes[id] ?? []).some((each) => each.key === key);
  }

  standing(id: string, key: string): Pass | undefined {
    return (this.passes[id] ?? []).find((each) => each.key === key);
  }

  add(id: string, key: string, at: string, run?: string): void {
    const rest = (this.passes[id] ?? []).filter((each) => each.key !== key);
    this.passes[id] = [...rest, { key, at, ...(run ? { run } : {}) }].slice(-KEPT_PER_CLASS);
  }

  save(): void {
    mkdirSync(dirname(this.file), { recursive: true });
    const temporary = `${this.file}.${process.pid}.tmp`;
    writeFileSync(temporary, `${JSON.stringify({ schema: 1, passes: this.passes }, null, 1)}\n`);
    renameSync(temporary, this.file);
  }
}

/** The units split by whether their key stands as passed: those to run, and those replayed. `keys` holds every unit's key by id. */
export function splitByReuse<U extends Unit>(units: U[], keyFor: (unit: U) => string, store: PassedClasses): { run: U[]; replayed: U[]; keys: Map<string, string> } {
  const run: U[] = [];
  const replayed: U[] = [];
  const keys = new Map<string, string>();
  for (const unit of units) {
    const key = keyFor(unit);
    keys.set(unitId(unit), key);
    (store.has(unitId(unit), key) ? replayed : run).push(unit);
  }
  return { run, replayed, keys };
}
