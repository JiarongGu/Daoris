import { describe, expect, it } from 'vitest';

/**
 * **A molecule imports no hook** (D52 as amended) — the rule that makes every state of the working
 * surface reachable by passing props, and therefore designable in Storybook and testable without a
 * bridge. A component that fetches its own data has states only a running system can produce, and
 * those are exactly the states nobody reviews.
 *
 * The rule is held here rather than by review because it breaks silently and one import at a time.
 * `src/work/` is covered by default and an organism is exempted by NAME: a list of what may reach
 * the data is a list someone must justify appending to, where a list of what may not is one someone
 * forgets to append to.
 */

/** Modules that reach the service or the shell — every other presentational file must not. */
const FORBIDDEN: { what: string; pattern: RegExp }[] = [
  { what: "the query layer ('./queries')", pattern: /\bfrom\s+'(?:\.\.?\/)+queries'/ },
  { what: "the shell bridge ('./shell')", pattern: /\bfrom\s+'(?:\.\.?\/)+shell'/ },
  { what: 'react-query directly', pattern: /\bfrom\s+'@tanstack\/react-query'/ },
  // The bridge ITSELF, not just this repository's wrapper around it (SURF7). Without this row the
  // rule is trivially side-stepped by importing the library `./shell` is built on.
  { what: "the bridge library ('@shenora/react')", pattern: /\bfrom\s+'@shenora\/react'/ },
  // The window's chrome hook — the other shell-layer module, and the one that caught this gap out.
  // The app strip briefly imported it for two constants and every check here stayed green, because
  // **this check is per-file, not transitive**: it reads each source for a forbidden import and
  // knows nothing about what that import's own module reaches. So the list has to name every
  // shell-layer module by hand, and adding one is part of writing one.
  { what: "the window chrome ('./windowChrome')", pattern: /\bfrom\s+'(?:\.\.?\/)+windowChrome'/ },
];

/**
 * Organisms — allowed to hold a hook, because holding one for the molecules below them is their job.
 * Adding a name here is the deliberate act; it is meant to be noticed in review.
 */
const ORGANISMS = new Set<string>([
  './SessionConsole.tsx',
  './work/AttentionBand.tsx',
  './work/SessionRail.tsx',
  './work/WorkFrame.tsx',
]);

export function offenders(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    FORBIDDEN.filter((rule) => rule.pattern.test(source)).map((rule) => `${path} imports ${rule.what}`));
}

const sources = import.meta.glob('./{ui.tsx,work/**/*.{ts,tsx}}', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>;

const presentational = Object.entries(sources)
  .filter(([path]) => !ORGANISMS.has(path) && !/\.(test|stories)\.tsx?$/.test(path));

describe('the presentational boundary', () => {
  it('catches an import it is meant to catch — the check itself, sabotaged', () => {
    expect(offenders([['./work/SessionRow.tsx', "import { useSessions } from '../queries';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports the query layer ('./queries')"]);
    expect(offenders([['./work/SessionRow.tsx', "import { useDriver } from '../shell';\n"]]))
      .toHaveLength(1);
    // The road around the wrapper: the library `./shell` is itself built on.
    expect(offenders([['./work/SessionRow.tsx', "import { useShenora } from '@shenora/react';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports the bridge library ('@shenora/react')"]);
    expect(offenders([['./work/SessionRow.tsx', "import type { Session } from '../api';\n"]]))
      .toEqual([]);
  });

  it('is looking at files at all — a vacuous boundary is a boundary that has stopped working', () => {
    expect(presentational.length).toBeGreaterThan(0);
  });

  it('holds: no presentational component reaches the service or the shell', () => {
    expect(offenders(presentational)).toEqual([]);
  });
});
