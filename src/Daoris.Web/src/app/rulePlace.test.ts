import { describe, expect, it } from 'vitest';
import { workspaceItem } from './opener';
import { ruleDoor } from './rulePlace';

describe('the place a rule is changed at', () => {
  it("opens This machine's page, a workspace's Supervisor section and a repository's Roles", () => {
    expect(ruleDoor('machine')).toEqual({ view: 'agents', item: null, part: { agentsMode: 'setup' } });
    expect(ruleDoor('workspace:aurora')).toEqual({ view: 'projects', item: workspaceItem('aurora'), part: { workspaceSection: 'supervisor' } });
    expect(ruleDoor('repository:engine')).toEqual({ view: 'agents', item: null, part: { placeOf: 'engine', placeSection: 'roles', placeRow: 'supervisor' } });
  });

  it('refuses a line place, which holds no rules of its own', () => {
    expect(ruleDoor('line:412')).toBeNull();
  });
});
