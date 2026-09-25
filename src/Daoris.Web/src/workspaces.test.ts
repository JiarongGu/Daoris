import { describe, expect, it } from 'vitest';
import { workspaceOf, workspacesOf } from './workspaces';

describe('workspaces', () => {
  it('counts a row that names no circle as the default one', () => {
    expect(workspaceOf({})).toBe('default');
    expect(workspaceOf({ workspace: null })).toBe('default');
    expect(workspaceOf({ workspace: 'studio' })).toBe('studio');
  });

  it('lists each circle once, sorted, the unnamed ones included', () => {
    expect(workspacesOf([{ workspace: 'studio' }, {}, { workspace: 'aurora' }, { workspace: 'studio' }]))
      .toEqual(['aurora', 'default', 'studio']);
  });
});
