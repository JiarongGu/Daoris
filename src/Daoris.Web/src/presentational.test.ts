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
  // Its domains, which `./shell` only re-exports since MOD3: without this row the rule is side-stepped
  // by importing the file the barrel names.
  { what: "a bridge domain ('./bridge/…')", pattern: /\bfrom\s+'(?:\.\.?\/)+bridge\// },
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
  // The asks' organism (INT4c; FRAME1d): it holds the queries so the row, the page and the composer do not.
  './asks/AsksPart.tsx',
  './work/AttentionBand.tsx',
  // The conversation's organism (D76, CONV2): it holds the record and the scroll, so the view,
  // the tool card and the Markdown below it hold neither.
  './work/SessionConversation.tsx',
  './work/DetachedSession.tsx',
  './work/DiffPane.tsx',
  './work/MonitorWindow.tsx',
  './work/SessionRail.tsx',
  // The one owner of each act on a session (SESSUX1d, D126 §3.1): it holds the bridge's hooks, so the row, the list and
  // the page header, which each press it, hold none.
  './work/sessionActs.ts',
  './work/WorkFrame.tsx',
  // Ask Daoris's organism (HELP1): it reads the machine, so the panel and the starters do not.
  './help/AskDaoris.tsx',
  // Ask Daoris's conversation (HELP1a): the newest help session's record and its composer.
  './help/AskConversation.tsx',
  // The machine as the queries answer it (SETUP1a, D97): one reading for the starters and the setup
  // guide, so the panel, the guide and the steps below them hold no query.
  './help/useMachine.ts',
  // The Plugins view's organism (PLUGUI1b, D119): it holds the catalogue and the plugin's acts, so the list, the
  // strip, the page and the offer's page below it hold none.
  './plugins/PluginsView.tsx',
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
}).concat([
  // Settings' domains, organisms by their name (below), and the list that imports every one of them.
  { what: 'a Settings domain', pattern: /^import\s+(?!type\s)[^;]*?from\s+'(?:\.\.?\/)+(?:[\w-]+\/)*\w+Domain'/m },
  { what: "the Settings domains' list", pattern: /^import\s+(?!type\s)[^;]*?from\s+'(?:\.\.?\/)+(?:settings\/)?domains'/m },
]);

/**
 * **A Settings domain is an organism by its name** (MOD4): `settings/<Name>Domain.tsx` holds its
 * domain's queries, as SettingsView did before each domain had a file, so the molecules beside it hold
 * none; and `settings/domains.ts`, the list, imports every one. By name rather than listed here, so a new
 * domain is added in the list alone — and `settings/domains.test.ts` holds that every file so named IS a
 * registered domain, so the name cannot carry a molecule past this boundary.
 */
const SETTINGS_ORGANISM = /^\.\/settings\/(?:\w+Domain\.tsx|domains\.ts)$/;

export function offenders(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    [...FORBIDDEN, ...organismRules]
      .filter((rule) => rule.pattern.test(source))
      .map((rule) => `${path} imports ${rule.what}`));
}

// `map/` since MAP2: the map's drawing and detail are molecules, and MapView above them is the view.
// `asks/` and `compose/` since INT4c: the ask's molecules, and the carry fields both composers share.
// `settings/` since AGT6: Daoris's own AI, drawn from props — SettingsView above it holds the queries,
// and since MOD4 each domain's own `<Name>Domain.tsx` does.
// `projects/` since INT3c: the driver's row, drawn from props — ProjectsView above it holds the driver; since FRAME1e
// the list and a repository's page too.
// `help/` since HELP1: Ask Daoris's panel and its starters, drawn from props — AskDaoris holds the machine.
// `links.tsx` since BRW7: the one place a link opens, told where by a context the application provides.
// `plugins/` since PLUGUI1b: the Plugins view's list, strip and pages, drawn from props — PluginsView holds the catalogue.
// `quests/` since FRAME1d: Quests' list, a quest's page and its composer, drawn from props — QuestsView holds the queries.
// `knowledge/` since FRAME1f: Search's and Convergence's lists and pages, drawn from props — SearchView and
// ConvergenceView hold the queries.
const sources = import.meta.glob('./{ui.tsx,links.tsx,work/**/*.{ts,tsx},map/**/*.{ts,tsx},asks/**/*.{ts,tsx},compose/**/*.{ts,tsx},settings/**/*.{ts,tsx},projects/**/*.{ts,tsx},help/**/*.{ts,tsx},plugins/**/*.{ts,tsx},quests/**/*.{ts,tsx},knowledge/**/*.{ts,tsx}}', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>;

const presentational = Object.entries(sources)
  .filter(([path]) => !ORGANISMS.has(path) && !SETTINGS_ORGANISM.test(path) && !/\.(test|stories)\.tsx?$/.test(path));

describe('the presentational boundary', () => {
  it('catches an import it is meant to catch — the check itself, sabotaged', () => {
    expect(offenders([['./work/SessionRow.tsx', "import { useSessions } from '../queries';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports the query layer ('./queries')"]);
    expect(offenders([['./work/SessionRow.tsx', "import { useDriver } from '../shell';\n"]]))
      .toHaveLength(1);
    // The road around the barrel: the domain file it re-exports.
    expect(offenders([['./work/SessionRow.tsx', "import { useDriver } from '../bridge/driver';\n"]]))
      .toEqual(["./work/SessionRow.tsx imports a bridge domain ('./bridge/…')"]);
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
    // A Settings domain is an organism by its name, and so is the list of them.
    expect(offenders([['./settings/Lines.tsx', "import { PluginsDomain } from './PluginsDomain';\n"]]))
      .toEqual(['./settings/Lines.tsx imports a Settings domain']);
    expect(offenders([['./settings/Lines.tsx', "import { SETTINGS_DOMAINS } from './domains';\n"]]))
      .toEqual(["./settings/Lines.tsx imports the Settings domains' list"]);
    expect(offenders([['./settings/Lines.tsx', "import type { SettingsDomainProps } from './domains';\n"]]))
      .toEqual([]);
    expect(SETTINGS_ORGANISM.test('./settings/PluginsDomain.tsx')).toBe(true);
    expect(SETTINGS_ORGANISM.test('./settings/Lines.tsx')).toBe(false);
  });

  it('is looking at files at all — a vacuous boundary is a boundary that has stopped working', () => {
    expect(presentational.length).toBeGreaterThan(0);
  });

  it('holds: no presentational component reaches the service or the shell', () => {
    expect(offenders(presentational)).toEqual([]);
  });
});
