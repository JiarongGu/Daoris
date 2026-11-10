import { describe, expect, it } from 'vitest';
import { MACHINE, holds, homeFocus, placeSaid, readPlace, samePlace } from './focus';

/**
 * An Ask Daoris conversation's focus, the place (ASKSCOPE2b): one shape on every side. The spelling table is line for line the
 * driver's `HelpFocusTests`, its twin `Help/HelpFocus.cs`.
 */
describe('a place', () => {
  it.each([
    ['{"kind":"machine"}', 'machine'],
    ['{"kind":"workspace","workspace":"Games"}', 'Games'],
    ['{"kind":"repository","workspace":"Games","repository":"engine"}', 'Games/engine'],
  ])('is spelled one way on the wire and in the terminal: %s', (wire, said) => {
    const place = readPlace(JSON.parse(wire));
    expect(place).not.toBeNull();
    expect(placeSaid(place!)).toBe(said);
    expect(JSON.stringify(place)).toBe(wire);
  });

  it.each([
    '{"kind":"room"}',
    '{"kind":"workspace"}',
    '{"kind":"repository","workspace":"Games"}',
    '{"kind":"workspace","workspace":" "}',
    '"Games"',
  ])('reads as none for a shape that is no place: %s', (wire) => {
    expect(readPlace(JSON.parse(wire))).toBeNull();
  });

  it('holds itself and what is inside it, names compared whatever their case', () => {
    const engine = { kind: 'repository', workspace: 'Games', repository: 'engine' } as const;
    expect(holds(MACHINE, engine)).toBe(true);
    expect(holds({ kind: 'workspace', workspace: 'games' }, engine)).toBe(true);
    expect(holds({ kind: 'workspace', workspace: 'Work' }, engine)).toBe(false);
    expect(holds(engine, { kind: 'workspace', workspace: 'Games' })).toBe(false);
    expect(samePlace(engine, { kind: 'repository', workspace: 'GAMES', repository: 'Engine' })).toBe(true);
  });

  it("reads a record from before as its home's, and default's as this machine", () => {
    expect(homeFocus('Games')).toEqual({ kind: 'workspace', workspace: 'Games' });
    expect(homeFocus('default')).toEqual(MACHINE);
    expect(homeFocus(undefined)).toEqual(MACHINE);
  });
});
