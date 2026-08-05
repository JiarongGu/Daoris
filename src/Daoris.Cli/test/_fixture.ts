import type { DaorisError } from '../src/errors.ts';
import { mkdirSync, rmSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const FIXTURE_ROOT = join(dirname(dirname(fileURLToPath(import.meta.url))), '_fixtures');

/** A scratch repository under `_fixtures/` — never OS temp. */
export interface Fixture {
  root: string;
  write(rel: string, text: string): string;
  read(rel: string): string;
  exists(rel: string): boolean;
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
    cleanup: () => rmSync(root, { recursive: true, force: true }),
  };
}
