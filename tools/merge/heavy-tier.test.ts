import { describe, expect, it } from 'vitest';
import { deferredLines, isHeavyUnit, splitTier, tierNamed } from './heavy-tier.ts';

const processSuites = new Set(['driver-process', 'modules-process']);
const units = [
  { suite: 'driver', unit: 'A' }, { suite: 'driver-process', unit: 'B' }, { suite: 'modules-process', unit: 'C' },
  { suite: 'driver-rehearsals', unit: 'D' }, { suite: 'web-unit', unit: 'E' }, { suite: 'cli', unit: 'F' },
];

describe('the tiers of a reached run', () => {
  it('runs a unit class in the fast tier and defers a Process or rehearsal class', () => {
    const { kept, deferred } = splitTier(units, 'fast', processSuites);
    expect(kept.map((unit) => unit.unit)).toEqual(['A', 'E', 'F']);
    expect(deferred.map((unit) => unit.unit)).toEqual(['B', 'C', 'D']);
  });

  it('runs only the heavy classes in the heavy tier, and every class for a builder', () => {
    expect(splitTier(units, 'heavy', processSuites).kept.map((unit) => unit.unit)).toEqual(['B', 'C', 'D']);
    expect(splitTier(units, 'heavy', processSuites).deferred).toEqual([]);
    expect(splitTier(units, 'all', processSuites)).toEqual({ kept: units, deferred: [] });
  });

  it('knows a heavy class by its suite, not by its name', () => {
    expect(isHeavyUnit({ suite: 'driver-process-git' }, new Set(['driver-process-git']))).toBe(true);
    expect(isHeavyUnit({ suite: 'driver-process-git' }, new Set())).toBe(false);
  });

  it("defers the tools' end-to-end specs as it defers a Process class, and keeps the tools' unit tier fast (TOOLTIER1)", () => {
    expect(isHeavyUnit({ suite: 'tool-e2e' }, new Set())).toBe(true);
    expect(isHeavyUnit({ suite: 'tool-tests' }, new Set())).toBe(false);
    const tools = [{ suite: 'tool-tests', unit: 'U' }, { suite: 'tool-e2e', unit: 'E' }];
    expect(splitTier(tools, 'fast', new Set())).toEqual({ kept: [tools[0]], deferred: [tools[1]] });
    expect(splitTier(tools, 'heavy', new Set()).kept).toEqual([tools[1]]);
  });

  it('reads a tier by its name only', () => {
    expect(tierNamed('heavy')).toBe('heavy');
    expect(tierNamed('slow')).toBeNull();
    expect(tierNamed(null)).toBeNull();
  });

  it('says what it deferred, by suite, and nothing where none was', () => {
    expect(deferredLines([])).toEqual([]);
    expect(deferredLines(splitTier(units, 'fast', processSuites).deferred)).toEqual([
      'reached: 3 heavy-tier classes deferred to the run after the landing (driver-process 1, modules-process 1, driver-rehearsals 1)',
    ]);
  });
});
