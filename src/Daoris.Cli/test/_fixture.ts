import type { DaorisError } from '../src/errors.ts';
import { mkdirSync, rmSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { findRegion } from '../src/region.ts';
import { tierRuleBody } from '../src/tierrender.ts';

const FIXTURE_ROOT = join(dirname(dirname(fileURLToPath(import.meta.url))), '_fixtures');

/** A scratch repository under `_fixtures/` — never OS temp. */
export interface Fixture {
  root: string;
  write(rel: string, text: string): string;
  read(rel: string): string;
  exists(rel: string): boolean;
  /**
   * The doctrine region's body (D59), or null when this repository has none.
   *
   * The always-loaded tier is a span in `AGENTS.md` rather than files under `.claude/rules/`, so
   * "what did sync write for this rule" is a question about a region now, and every test that used
   * to read a path asks it here.
   */
  region(): string | null;
  /** One rule's body inside the region — what drift and `upstream` are measured per. */
  rule(source: string): string | null;
  cleanup(): void;
}

/**
 * Return the error a function throws, so its exitCode and message can be
 * asserted. `assert.throws` returns undefined, so it cannot be used for this.
 *
 * Typed as `DaorisError` because that is what every caller asserts on — the exit code is part of the
 * contract, so a test that could not reach it would not be testing the contract.
 */
export function captureError(fn: () => unknown): DaorisError {
  try {
    fn();
  } catch (error) {
    return error as DaorisError;
  }
  throw new Error('expected a throw, but the call returned normally');
}

/** A scratch repo under _fixtures/ — never OS temp. */
export function makeFixture(name: string): Fixture {
  const root = join(FIXTURE_ROOT, name);
  rmSync(root, { recursive: true, force: true });
  mkdirSync(root, { recursive: true });
  return {
    root,
    write(rel: string, text: string): string {
      const file = join(root, rel);
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, text, 'utf8');
      return file;
    },
    read: (rel: string) => readFileSync(join(root, rel), 'utf8'),
    exists: (rel: string) => existsSync(join(root, rel)),
    region(): string | null {
      const file = join(root, 'AGENTS.md');
      if (!existsSync(file)) return null;
      const held = findRegion(readFileSync(file, 'utf8'), 'rules');
      return held.kind === 'present' ? held.body : null;
    },
    rule(source: string): string | null {
      const body = this.region();
      return body === null ? null : tierRuleBody(body, source);
    },
    // A child the test started (a stub agent, a git) may still be letting go of the folder as the test ends; on Windows
    // that is EPERM, which failed passing tests in three suites on 2026-10-04 (FLAKE1). Waits as the rehearsals' cleans do.
    cleanup: () => rmSync(root, { recursive: true, force: true, maxRetries: 20, retryDelay: 250 }),
  };
}
