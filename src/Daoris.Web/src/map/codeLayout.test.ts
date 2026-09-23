import { describe, expect, it } from 'vitest';
import { layerModules } from './codeLayout';

// MAP3a: a repository's modules in layers by dependency — what uses a module above it, what it
// depends on below — as a pure function, so the picture does not move between two looks.

const dep = (from: string, to: string) => ({ from, to, kind: 'imports' });

describe('the code map layout', () => {
  it('puts what depends on nothing below everything that depends on it', () => {
    const layers = layerModules(['core', 'service', 'web'], [dep('web', 'service'), dep('service', 'core')]);

    expect(layers).toEqual([['web'], ['service'], ['core']]);
  });

  /** The longest way down decides: a module used from two depths sits below the deeper one. */
  it('places a module under the deepest thing that uses it', () => {
    const layers = layerModules(
      ['app', 'lib', 'util'],
      [dep('app', 'lib'), dep('lib', 'util'), dep('app', 'util')],
    );

    expect(layers).toEqual([['app'], ['lib'], ['util']]);
  });

  it('keeps unconnected modules on the top row, and every row in name order', () => {
    const layers = layerModules(['zeta', 'alpha', 'mid', 'base'], [dep('mid', 'base')]);

    expect(layers).toEqual([['alpha', 'mid', 'zeta'], ['base']]);
  });

  /** A producer may record a cycle; the picture still draws every module once. */
  it('survives a cycle, placing every module exactly once', () => {
    const layers = layerModules(['a', 'b', 'c'], [dep('a', 'b'), dep('b', 'c'), dep('c', 'a')]);

    expect(layers.flat().sort()).toEqual(['a', 'b', 'c']);
  });

  it('is nothing for nothing', () => {
    expect(layerModules([], [])).toEqual([]);
  });
});
