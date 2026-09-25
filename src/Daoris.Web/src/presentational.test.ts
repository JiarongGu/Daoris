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
  // The asks' organism (INT4c): it holds the queries so the card, the record and the composer do not.
  './asks/AsksSection.tsx',
  './work/AttentionBand.tsx',
  // The conversation's organism (D76, CONV2): it holds the record and the scroll, so the view,
  // the tool card and the Markdown below it hold neither.
  './work/SessionConversation.tsx',
  './work/DetachedSession.tsx',
  './work/DiffPane.tsx',
  './work/MonitorWindow.tsx',
  './work/SessionRail.tsx',
  './work/WorkFrame.tsx',
]);

/**
 * An organism, imported by a molecule, is the data reached one step removed (REV3): the output panel
 * rendered the console organism itself, and every check above stayed green. A TYPE from an organism
 * is only a shape, and is allowed.
 */
const organismRules = [...ORGANISMS].map((path) => {
  const name = path.replace(/^.*\//, '').replace(/\.tsx?$/, '');
  return {
    what: `the organism ${name}`,
    pattern: new RegExp(`^import\\s+(?!type\\s)[^;]*?from\\s+'(?:\\.\\.?\\/)+(?:[\\w-]+\\/)*${name}'`, 'm'),
  };
});

export function offenders(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    [...FORBIDDEN, ...organismRules]
      .filter((rule) => rule.pattern.test(source))
      .map((rule) => `${path} imports ${rule.what}`));
}

// `map/` since MAP2: the map's drawing and detail are molecules, and MapView above them is the view.
// `asks/` and `compose/` since INT4c: the ask's molecules, and the carry fields both composers share.
// `settings/` since AGT6: Daoris's own AI, drawn from props — SettingsView above it holds the queries.
// `projects/` since INT3c: the driver's row, drawn from props — ProjectsView above it holds the driver.
const sources = import.meta.glob('./{ui.tsx,work/**/*.{ts,tsx},map/**/*.{ts,tsx},asks/**/*.{ts,tsx},compose/**/*.{ts,tsx},settings/**/*.{ts,tsx},projects/**/*.{ts,tsx}}', {
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
    // One step removed: an organism holds the hook, and importing it reaches the data all the same.
    expect(offenders([['./work/frame.tsx', "import { SessionConsole } from '../SessionConsole';\n"]]))
      .toEqual(['./work/frame.tsx imports the organism SessionConsole']);
    expect(offenders([['./work/Row.tsx', "import type { AttentionDoors } from './AttentionBand';\n"]]))
      .toEqual([]);
  });

  it('is looking at files at all — a vacuous boundary is a boundary that has stopped working', () => {
    expect(presentational.length).toBeGreaterThan(0);
  });

  it('holds: no presentational component reaches the service or the shell', () => {
    expect(offenders(presentational)).toEqual([]);
  });
});
