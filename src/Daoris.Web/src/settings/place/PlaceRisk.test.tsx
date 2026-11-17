import { describe, expect, it, vi } from 'vitest';
import { render as draw, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import * as Tooltip from '@radix-ui/react-tooltip';
import { PlaceRiskSetting, PRODUCTION_RELEASES, repositoryRisk, riskValue, workspaceRisk } from './PlaceRisk';

const render = (ui: ReactElement) => draw(<Tooltip.Provider>{ui}</Tooltip.Provider>);

describe('a repository’s risk row', () => {
  const rows = [
    { place: 'machine', risk: 'medium', marks: [] },
    { place: 'workspace/Work', risk: 'high', marks: [PRODUCTION_RELEASES] },
    { place: 'repository/reports', risk: 'low', marks: [] },
  ];

  it('takes its own row over its workspace’s, else its workspace’s, else the machine’s, else none', () => {
    expect(repositoryRisk(rows, 'Reports', 'work')).toEqual({ row: rows[2] });
    expect(repositoryRisk(rows, 'billing', 'work')).toEqual({ inherited: { row: rows[1], from: 'workspace' } });
    expect(repositoryRisk(rows, 'billing', 'home')).toEqual({ inherited: { row: rows[0], from: 'machine' } });
    expect(repositoryRisk([], 'billing', 'work')).toEqual({});
  });

  it('shows the value that stands and where it comes from, and a press sets the repository’s own', () => {
    const onChange = vi.fn();
    const { unmount } = render(<PlaceRiskSetting name="billing" twin="twin" {...repositoryRisk(rows, 'billing', 'work')} onChange={onChange} />);

    expect(screen.getByText(/From its workspace\./)).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'High' })).toBeChecked();
    screen.getByRole('radio', { name: 'Low' }).click();
    expect(onChange).toHaveBeenCalledWith({ risk: 'low' });
    unmount();

    render(<PlaceRiskSetting name="reports" twin="twin" {...repositoryRisk(rows, 'reports', 'work')} onChange={onChange} />);
    expect(screen.getByText(/Set for this place\./)).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: 'Low' })).toBeChecked();
  });
});

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
