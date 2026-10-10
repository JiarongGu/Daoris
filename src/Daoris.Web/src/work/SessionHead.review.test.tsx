import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import type { Session } from '../api';
import { STEP_REVIEWED, STEP_SHOWN } from './reviewFixtures';
import { SessionHead } from './SessionHead';

// REVIEWENV1g (D154 point 7; the review environment design §3.1): where the review's gate holds a session's work, the head
// says *Review in `<environment>`* in the gate's state where *Accept…* would be, and offers no *Accept…*.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q1', repository: 'reports', adapter: 'claude-code', state: 'completed', kind: 'driven',
  created: '2026-10-09T08:00:00Z', updated: '2026-10-09T09:00:00Z', tree: 'C:/somewhere/reports', ...over,
});
const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 };
const PLAN = { session: 's1a2b3c4', form: 'merge', target: 'main' };
const gate = () => screen.getByRole('region', { name: /Review in|Review before/ });

describe("the head's review gate", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it.each(['kind-paths', 'cannot-start', 'unread'])('shows the workflow %s hold and offers no Accept', (state) => {
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()}
      landing={{ ...PLAN, workflow: { state, holds: true, says: 'Holds: keep this workflow in the terminal.' } }} />);
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(screen.getByRole('region', { name: 'Workflow before it lands' })).toHaveTextContent('keep this workflow');
  });

  it('withdraws an open Accept confirmation when a workflow hold arrives', async () => {
    const { rerender } = render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} landing={PLAN} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept…' }));
    rerender(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()}
      landing={{ ...PLAN, workflow: { state: 'unread', holds: true, says: 'Binding unread.' } }} />);
    expect(screen.queryByRole('button', { name: /Accept/ })).toBeNull();
  });

  it.each(['follows', 'kept'])('offers Accept when the workflow %s lets go', (state) => {
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()}
      landing={{ ...PLAN, workflow: { state, holds: false, says: 'Follows the bound workflow.' } }} />);
    expect(screen.getByRole('button', { name: 'Accept…' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Workflow before it lands' })).toBeNull();
  });

  it('translates the workflow hold chrome and preserves the driver words', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()}
      landing={{ ...PLAN, workflow: { state: 'unread', holds: true, says: 'Binding unread.' } }} />);
    expect(screen.getByRole('region', { name: '落地前的工作流' })).toHaveTextContent('Binding unread.');
    expect(screen.queryByRole('button', { name: /接受/ })).toBeNull();
  });

  it('stands where Accept… would while the gate holds, with the set-up shown and its verdict', async () => {
    const reviewed = vi.fn();
    const user = userEvent.setup();
    render(
      <SessionHead
        session={session()} lands={LANDS} onLand={vi.fn()}
        landing={{ ...PLAN, review: { state: 'shown', environment: 'local', level: 'set-up-step', quest: 'q2', says: 'Waits for your review in `local`: …' } }}
        review={{ step: STEP_SHOWN, served: true, acts: { reviewed, skip: vi.fn() } }}
      />,
    );

    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
    expect(gate()).toHaveTextContent('Review in local');
    expect(gate()).toHaveTextContent('Set-up step #q2 showed it at a1b2c3d4.');
    await user.click(within(gate()).getByRole('button', { name: 'Reviewed' }));
    expect(reviewed).toHaveBeenCalledWith({ machine: 'desk', sequence: 7 }, expect.any(Object));
  });

  it('offers Set it up and Skip… where nothing shows the work yet, and opens the step where the head draws none', async () => {
    const setUp = vi.fn();
    const user = userEvent.setup();
    render(
      <SessionHead
        session={session()} lands={LANDS} onLand={vi.fn()}
        landing={{ ...PLAN, review: { state: 'not-shown', environment: 'local', level: 'repository' } }}
        review={{ acts: { setUp, skip: vi.fn() } }}
      />,
    );

    await user.click(within(gate()).getByRole('button', { name: 'Set it up in local' }));
    expect(setUp).toHaveBeenCalledWith('local', expect.any(Object));
    expect(within(gate()).getByRole('button', { name: 'Skip the review for this work…' })).toBeInTheDocument();
  });

  it('says newer work since a review, with the door to the step', async () => {
    const open = vi.fn();
    const user = userEvent.setup();
    render(
      <SessionHead
        session={session()} lands={LANDS} onLand={vi.fn()}
        landing={{ ...PLAN, review: { state: 'not-held', environment: 'local', quest: 'q2' } }}
        review={{ step: STEP_REVIEWED, acts: { skip: vi.fn(), open } }}
      />,
    );

    expect(gate()).toHaveTextContent('newer work since');
    await user.click(within(gate()).getByRole('button', { name: 'Open #q2' }));
    expect(open).toHaveBeenCalled();
  });

  it('says an unread gate in its own words, with the driver\'s sentence beneath', () => {
    render(
      <SessionHead
        session={session()} lands={LANDS} onLand={vi.fn()}
        landing={{ ...PLAN, review: { state: 'unread', says: 'Whether this work waits for your review could not be read: connection refused.' } }}
      />,
    );

    expect(gate()).toHaveTextContent('Review before it lands');
    expect(gate()).toHaveTextContent('The driver says: Whether this work waits');
    expect(screen.queryByRole('button', { name: 'Accept…' })).toBeNull();
  });

  it('offers Accept… as before where nothing waits for a review', () => {
    render(<SessionHead session={session()} lands={LANDS} onLand={vi.fn()} landing={PLAN} />);

    expect(screen.getByRole('button', { name: 'Accept…' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: /Review in/ })).toBeNull();
  });

  it('says it in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(
      <SessionHead
        session={session()} lands={LANDS} onLand={vi.fn()}
        landing={{ ...PLAN, review: { state: 'shown', environment: 'local', quest: 'q2' } }}
        review={{ step: STEP_SHOWN, acts: { reviewed: vi.fn() } }}
      />,
    );

    expect(screen.getByRole('region', { name: /在 local 中审阅/ })).toHaveTextContent('已展示');
    expect(screen.getByRole('button', { name: '已审阅' })).toBeInTheDocument();
  });
});
