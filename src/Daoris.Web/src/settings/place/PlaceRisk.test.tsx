import { describe, expect, it } from 'vitest';
import { PRODUCTION_RELEASES, riskValue, workspaceRisk } from './PlaceRisk';

describe('a place’s risk as the window reads it', () => {
  it('reads absent as low and a risk it does not know as high, the safe side', () => {
    expect(riskValue(undefined)).toBe('low');
    expect(riskValue('medium')).toBe('medium');
    expect(riskValue('critical')).toBe('high');
  });

  it('finds the workspace’s own row in any case, and none where nothing is set', () => {
    const rows = [{ place: 'workspace/Work', risk: 'low', marks: [PRODUCTION_RELEASES] }, { place: 'machine', risk: 'low', marks: ['line'] }];
    expect(workspaceRisk(rows, 'work')?.marks).toEqual([PRODUCTION_RELEASES]);
    expect(workspaceRisk(rows, 'home')).toBeUndefined();
  });
});
