import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import '../../locales/i18n';
import type { Session } from '../../data/api';
import type { StepWaitRow } from '../attention/attentionPushed';
import type { PermissionWait } from './PermissionAsks';
import { WaitStrip } from './WaitStrip';

// Every wait the person settles stands in the dock with its own press, oldest first under a count, never folded; a teammate's
// session draws its waits as lines with no press.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q7', repository: 'engine', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-11-18T08:00:00Z', updated: '2026-11-18T09:30:00Z', note: 'Which table holds the totals?', ...over,
});

const SIGN: StepWaitRow = { run: 'q7', node: 'sign', why: 'approval', role: 'approver', holder: 'person', presses: ['approve', 'reject', 'skip'] };
const HELD: PermissionWait = {
  session: 's1a2b3c4', request: 'p1', call: null, since: '2026-11-18T09:50:00Z', until: null, title: 'Bash', kind: 'execute',
  choices: [{ id: 'once', kind: 'allow_once', name: 'Allow once' }],
};

const dock = () => screen.getByRole('region', { name: /waiting on you/i });

describe('WaitStrip, the dock', () => {
  it('holds every wait with its own press, the run\'s step and the park\'s Finish among them, oldest first under a count', () => {
    render(
      <WaitStrip
        session={session()}
        onResolve={vi.fn()}
        permissions={{ waits: [HELD], onAnswer: vi.fn(), busy: false }}
        runWaits={{ waits: [{ run: 'q7', node: 'sign', why: 'approval', role: 'approver', since: '2026-11-18T09:05:00Z' }], steps: [SIGN], onWord: vi.fn() }}
      />,
    );

    const region = dock();
    expect(within(region).getByRole('heading', { name: '3 waiting on you' })).toBeInTheDocument();
    // Never folded: no press hides a wait.
    expect(within(region).queryByRole('button', { name: 'Fold' })).toBeNull();
    // The run's step, with the press its row offers.
    const step = within(region).getByRole('group', { name: 'sign: waiting on approver: you' });
    expect(within(step).getByRole('button', { name: 'Approve…' })).toBeInTheDocument();
    // The park's own presses, moved from the head.
    expect(within(region).getByRole('button', { name: /^Finish/ })).toBeInTheDocument();
    // The held tool call, with its choice.
    expect(within(region).getByRole('button', { name: 'Allow once' })).toBeInTheDocument();
    // Oldest first: the step (09:05), the park (09:30), the tool call (09:50).
    const lines = [...region.querySelectorAll('[data-dock-line]')].map((line) => line.getAttribute('data-dock-line'));
    expect(lines).toEqual(['step:q7/sign', 'question', 'permission:p1']);
  });

  it('tags a subagent\'s wait with its subagent, nested ones by the way to them, and opens its focus from the tag', () => {
    const open = vi.fn();
    const lifetimes = [
      { seq: 1, kind: 'child' as const, at: '2026-11-18T09:40:00Z', id: 'c1', call: 'c1', name: 'find tests', depth: 1 },
      { seq: 2, kind: 'child' as const, at: '2026-11-18T09:45:00Z', id: 'c2', parent: 'c1', name: 'read fixtures', depth: 2 },
    ];
    render(
      <WaitStrip
        session={session({ state: 'working' })}
        permissions={{ waits: [{ ...HELD, child: 'c2' }, { ...HELD, request: 'p2', child: 'gone' }, { ...HELD, request: 'p3' }], onAnswer: vi.fn(), busy: false }}
        beside={{ session: 's1a2b3c4', lifetimes, act: null, open }}
      />,
    );
    const line = (key: string) => document.querySelector(`[data-dock-line="${key}"]`) as HTMLElement;
    expect(within(line('permission:p1')).getByText('From subagent read fixtures')).toBeInTheDocument();
    within(line('permission:p1')).getByRole('button', { name: 'Open read fixtures' }).click();
    expect(open).toHaveBeenCalledWith({ child: ['c1', 'c2'] });
    // One the record does not hold yet is still said as a subagent's, with nothing to open.
    expect(within(line('permission:p2')).getByText('From a subagent')).toBeInTheDocument();
    expect(within(line('permission:p2')).queryByRole('button', { name: /^Open/ })).toBeNull();
    // The session's own agent's is untagged.
    expect(line('permission:p3')).not.toHaveTextContent(/subagent/i);
  });

  it('draws a teammate\'s waits as a line with no press', () => {
    render(<WaitStrip session={session({ id: 'teammate-machine/s1a2b3c4' })} />);
    const region = screen.getByRole('region', { name: /waits on its owner/i });
    expect(region).toHaveTextContent('Waits on its owner: its question');
    expect(within(region).queryAllByRole('button')).toEqual([]);
  });
});
