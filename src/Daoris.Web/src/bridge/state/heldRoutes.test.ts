import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// WEBHELD1: a route the shell tells by event (`[ByEvent("NAME")]`) answers `{ pending: true }` when its work is slow, and the plan comes
// later as an event. A page that reads one with a plain `call(` takes the pending answer for the answer, so every page read of such a route
// goes through `askHeld` (a query's own) or `askSettled` (a read with no query of its own). The numbered read keeps its own fence.

/** Told by event and read through its own numbered fence (`askAtWorkOf`), which keeps the number rule beside `heldNumbered`. */
const OWN_FENCE = ['SESSION_AT_WORK'];

const walk = (folder: string, keep: (name: string) => boolean): string[] =>
  readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) return ['obj', 'bin', 'node_modules', 'generated'].includes(entry.name) ? [] : walk(path, keep);
    return keep(entry.name) ? [path] : [];
  });

/** The route names the desktop's modules tell by event. */
const heldRoutes = (): string[] => {
  const root = join(process.cwd(), '..', 'Daoris.Desktop');
  const names = walk(root, (name) => name.endsWith('.cs')).flatMap((file) =>
    [...readFileSync(file, 'utf8').matchAll(/\[ByEvent\("([A-Z0-9_]+)"/g)].map((found) => found[1]!));
  return [...new Set(names)].filter((name) => !OWN_FENCE.includes(name)).sort();
};

/** Each place a page calls `route` with a plain `call(`, as `file: ROUTE`. */
const plainCalls = (routes: string[]): string[] => {
  const calls = new RegExp(`\\bcall(?:<[^>(]*>)?\\(\\s*(?:/\\*[^*]*\\*/\\s*)?['"\`](${routes.join('|')})['"\`]`, 'g');
  return walk(join(process.cwd(), 'src'), (name) => /\.(ts|tsx)$/.test(name) && !/\.(test|stories)\.tsx?$/.test(name)).flatMap((file) =>
    [...readFileSync(file, 'utf8').matchAll(calls)].map((found) => `${file.slice(process.cwd().length + 1)}: ${found[1]}`));
};

describe('the routes the shell tells by event (WEBHELD1)', () => {
  it('are found in the desktop modules', () => {
    const routes = heldRoutes();
    expect(routes.length).toBeGreaterThan(20);
    expect(routes).toContain('PLACE_PLAN');
  });

  it('are read by no page through a plain call', () => {
    expect(plainCalls(heldRoutes())).toEqual([]);
  });

  it('finds a plain call where one is made (the numbered read’s, which keeps its own fence)', () => {
    const calls = plainCalls(['SESSION_AT_WORK']);
    expect(calls.length).toBeGreaterThan(0);
  });
});
