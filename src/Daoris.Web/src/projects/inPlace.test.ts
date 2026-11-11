import { describe, expect, it } from 'vitest';
import { choosesInPlace } from './inPlace';

describe('the add choosing Work in place', () => {
  it('chooses it for a folder and a system always, and for a repository where the person ticks it', () => {
    expect([choosesInPlace('folder', false), choosesInPlace('system', false), choosesInPlace('git', false), choosesInPlace('git', true)])
      .toEqual([true, true, false, true]);
  });
});
