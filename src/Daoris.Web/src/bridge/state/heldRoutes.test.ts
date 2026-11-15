import { describe, expect, it } from 'vitest';
import { HELD, heldRoutes, pageSources, PLAIN, readersUnread, said, terminalReads, terminalSources, through, type Source, type TerminalRead } from '../../test/census/routeMarks';

// A read the shell tells by event answers `{ pending: true }` while nothing is held yet, its answer told later as an event: the page
// reads one through `askHeld` or `askSettled` with a reader that takes that for empty, and a terminal's verb asks again or reads it as
// no answer. The numbered reads keep their own number rule, held by `numberedRoutes.test.ts`.

const told = () => heldRoutes(false);
const every = () => [...told(), ...heldRoutes(true)];

/** A terminal's read that takes nothing held yet for its answer: through no door that reads an answer with no `said` as none, nor beside a read of `pending`. */
const terminalUnread = (reads: readonly TerminalRead[], sources: readonly Source[]) => reads
  .filter(({ file, door }) => door !== 'ThroughAsync' && !sources.find((each) => each.file === file)?.text.includes('"pending"'))
  .map(({ file, route, door }) => `${file}: ${route} ${door ? `through ${door}, reading no pending` : 'handed to no door'}`);

const PAGE: Source = { file: 'planted.ts', text: [
  "call('PLACE_PLAN', ask);",
  "getBridge().invoke<Plan>('DAORIS.DRIVER', 'PLACE_PLAN', { payload });",
  "askHeld('PLACE_PLAN', client, key, ask, readPlacePlan);",
  "askHeld('PLACE_PLAN', client, key, ask, (answer) => answer as Plan);",
  "askSettled('PLACE_PLAN', ask, (told: unknown): Plan => told as Plan, 1000);",
  "askSettled('LINES', ask, readLines, 1000);",
].join('\n') };

const TERMINAL: Source = { file: 'Planted.cs', text: [
  'public static class PlantedRoutes',
  '{',
  '    public static InstanceRoute Plan { get; } = new(TreesRoutes.DriverModule, "PLACE_PLAN", RouteClass.Open);',
  '    public static InstanceRoute Where { get; } = new(TreesRoutes.DriverModule, "SESSION_WHERE", RouteClass.Open);',
  '}',
  'var exit = await InstanceDoor.TryReadAsync(PlantedRoutes.Plan, new { }, data => 0, world, ct);',
].join('\n') };

describe('the routes the shell tells by event', () => {
  it('are found in the desktop modules', () => {
    expect(told().length).toBeGreaterThan(20);
    expect(told()).toContain('PLACE_PLAN');
  });

  it('are read by no page through a plain call', () => {
    expect(said(through(PLAIN, pageSources()).filter(({ route }) => told().includes(route)))).toEqual([]);
  });

  it('are read through readers that take nothing held yet for empty', () => {
    expect(readersUnread(pageSources())).toEqual([]);
  });

  it('are the only routes the held readers read', () => {
    expect(said(through(HELD, pageSources()).filter(({ route }) => !told().includes(route)))).toEqual([]);
  });

  it('are read by the terminal where nothing held yet is no answer', () => {
    expect(terminalReads(every()).map(({ route }) => route)).toEqual(expect.arrayContaining(['PLACE_PLAN', 'SESSION_AT_WORK', 'SUPERVISOR_WAITING']));
    expect(terminalUnread(terminalReads(every()), terminalSources())).toEqual([]);
  });

  it('find each wrong read in a planted source', () => {
    expect(said(through(PLAIN, [PAGE]).filter(({ route }) => told().includes(route)))).toEqual(['planted.ts: PLACE_PLAN', 'planted.ts: PLACE_PLAN']);
    expect(readersUnread([PAGE])).toEqual(['planted.ts: PLACE_PLAN hands its answer on as it came', 'planted.ts: PLACE_PLAN hands its answer on as it came']);
    expect(said(through(HELD, [PAGE]).filter(({ route }) => !told().includes(route)))).toEqual(['planted.ts: LINES']);
    expect(terminalUnread(terminalReads(told(), [TERMINAL]), [TERMINAL])).toEqual([
      'Planted.cs: PLACE_PLAN through TryReadAsync, reading no pending', 'Planted.cs: SESSION_WHERE handed to no door',
    ]);
  });
});
