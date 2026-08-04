import { mkdirSync, rmSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const FIXTURE_ROOT = join(dirname(dirname(fileURLToPath(import.meta.url))), '_fixtures');

/**
 * Return the error a function throws, so its exitCode and message can be
 * asserted. `assert.throws` returns undefined, so it cannot be used for this.
 */
export function captureError(fn) {
  try {
    fn();
  } catch (error) {
    return error;
  }
  throw new Error('expected a throw, but the call returned normally');
}

/** A scratch repo under _fixtures/ — never OS temp. */
export function makeFixture(name) {
  const root = join(FIXTURE_ROOT, name);
  rmSync(root, { recursive: true, force: true });
  mkdirSync(root, { recursive: true });
  return {
    root,
    write(rel, text) {
      const file = join(root, rel);
      mkdirSync(dirname(file), { recursive: true });
      writeFileSync(file, text, 'utf8');
      return file;
    },
    read: (rel) => readFileSync(join(root, rel), 'utf8'),
    exists: (rel) => existsSync(join(root, rel)),
    cleanup: () => rmSync(root, { recursive: true, force: true }),
  };
}
