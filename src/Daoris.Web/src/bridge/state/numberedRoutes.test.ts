import { describe, expect, it } from 'vitest';
import { desktopSources, HELD, heldRoutes, pageSources, PLAIN, said, terminalReads, terminalSources, through, type Source, type TerminalRead } from '../../test/census/routeMarks';

// A numbered read is never refused: each answer, its reply or its event, carries its number (`at`), and a failure is an answer of
// its own (`failed`). The page reads one only where its events go to its number rule (`heldNumbered`), a terminal's verb reads its
// failure, and no ground reads one, since a ground takes every answer but nothing held yet for what it read.

const numbered = () => heldRoutes(true);

/** Each page read of `routes` outside a number rule: through a held reader, or plain in a file that hands no events to `heldNumbered`. */
const unfenced = (routes: readonly string[], sources: readonly Source[]) => [
  ...said(through(HELD, sources).filter(({ route }) => routes.includes(route)), 'through a held reader'),
  ...said(through(PLAIN, sources.filter(({ text }) => !text.includes('heldNumbered('))).filter(({ route }) => routes.includes(route)), 'beside no number rule'),
];

/** Each terminal read in a file that never reads a `Failed`. */
const failureUnread = (reads: readonly TerminalRead[], sources: readonly Source[]) =>
  said(reads.filter(({ file }) => !/\bFailed\b/.test(sources.find((each) => each.file === file)?.text ?? '')), 'reads no failure');

/** Each of `routes` a ground reads: the first argument of a read `PipeReads` makes (`Plan("…")`, `Wait("…")`). */
const groundReads = (routes: readonly string[], sources: readonly Source[]) => said(sources
  .filter(({ file }) => file.endsWith('/PipeReads.cs'))
  .flatMap(({ file, text }) => [...text.matchAll(/\b\w+\("([A-Z][A-Z0-9_]*)"/g)].map((found) => ({ file, route: found[1]! })))
  .filter(({ route }) => routes.includes(route)), 'read by a ground');

const PAGE: Source = { file: 'planted.ts', text: "askHeld('SESSION_AT_WORK', client, key, { session }, readAtWork);\ncall('SESSION_AT_WORK', { session });" };

const TERMINAL: Source = { file: 'Planted.cs', text: [
  'public static class PlantedRoutes',
  '{',
  '    public static InstanceRoute AtWork { get; } = new(TreesRoutes.DriverModule, "SESSION_AT_WORK", RouteClass.Open);',
  '}',
  'await InstanceDoor.TryReadAsync(PlantedRoutes.AtWork, new { session }, data => data.TryGetProperty("pending", out _) ? 2 : 0, world, ct);',
].join('\n') };

const GROUNDS: Source = { file: 'Daoris.Desktop/Planted/PipeReads.cs', text: 'public static PipeGround Attended() => Wait("SESSION_AT_WORK", ["session"], _ => null, (a, _) => "");' };

describe('the numbered reads', () => {
  it('are found among the reads told by event', () => {
    expect(numbered()).toContain('SESSION_AT_WORK');
    expect(heldRoutes(false)).not.toContain('SESSION_AT_WORK');
  });

  it('are read by the page only beside their number rule', () => {
    expect(said(through(PLAIN, pageSources()).filter(({ route }) => numbered().includes(route))).length).toBeGreaterThan(0);
    expect(unfenced(numbered(), pageSources())).toEqual([]);
  });

  it('are read by the terminal where a failure is an answer', () => {
    expect(terminalReads(numbered()).length).toBeGreaterThan(0);
    expect(failureUnread(terminalReads(numbered()), terminalSources())).toEqual([]);
  });

  it('are read by no ground', () => {
    expect(groundReads(heldRoutes(false), desktopSources())).toContain('Daoris.Desktop/Daoris.Desktop.Modules/Platform/PipeReads.cs: PLACE_PLAN read by a ground');
    expect(groundReads(numbered(), desktopSources())).toEqual([]);
  });

  it('find each wrong read in a planted source', () => {
    expect(unfenced(numbered(), [PAGE])).toEqual(['planted.ts: SESSION_AT_WORK through a held reader', 'planted.ts: SESSION_AT_WORK beside no number rule']);
    expect(failureUnread(terminalReads(numbered(), [TERMINAL]), [TERMINAL])).toEqual(['Planted.cs: SESSION_AT_WORK reads no failure']);
    expect(groundReads(numbered(), [GROUNDS])).toEqual(['Daoris.Desktop/Planted/PipeReads.cs: SESSION_AT_WORK read by a ground']);
  });
});
