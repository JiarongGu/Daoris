import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import '../../locales/i18n';
import { RunWaitsHead } from './RunWaitsHead';
import type { StepWaitRow } from '../attention/attentionPushed';

// The session page's head:each step of its run waiting on a role the person holds reads "<node>: waiting on <role>: you",
// with the step's own presses beside it, the same presses *What needs you*'s row offers; a plugin's wait with no page offers
// *Done outside Daoris*. A notification about the step opens this session, so it opens at the step's line and press.

const SIGN: StepWaitRow = { run: 'q7', node: 'sign', why: 'approval', role: 'approver', holder: 'person', presses: ['approve', 'reject', 'skip'] };
const RELEASE: StepWaitRow = {
  run: 'q7', node: 'release', why: 'actor', role: 'release manager', holder: 'person', presses: ['done-outside'],
  held: { unit: 'run:q7', attempt: 2, words: ['done-outside'] },
};

describe('RunWaitsHead', () => {
  it('reads each wait on its role, held by you, with the step’s presses beside it', async () => {
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'sign', act: 'approve', done: true });
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'sign', why: 'approval', role: 'approver' }]} steps={[SIGN]} onWord={onWord} />);

    const line = screen.getByRole('group', { name: 'sign: waiting on approver: you' });
    expect(within(line).getByRole('button', { name: 'Approve…' })).toBeInTheDocument();
    expect(within(line).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();
    expect(within(line).queryByRole('button', { name: 'End this work…' })).toBeNull();

    await userEvent.click(within(line).getByRole('button', { name: 'Approve…' }));
    await userEvent.click(within(line).getByRole('button', { name: 'Approve' }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'sign', act: 'approve', words: null });
  });

  it('offers Done outside Daoris on a plugin’s wait with no page, said on its attempt with the link', async () => {
    const onHeld = vi.fn().mockResolvedValue({ done: true, message: '' });
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'release', why: 'actor', role: 'release manager' }]} steps={[RELEASE]} onHeld={onHeld} />);

    const line = screen.getByRole('group', { name: 'release: waiting on release manager: you' });
    await userEvent.click(within(line).getByRole('button', { name: 'Done outside Daoris…' }));
    await userEvent.type(within(line).getByRole('textbox', { name: /link/i }), 'https://dev.example/release/90');
    await userEvent.click(within(line).getByRole('button', { name: 'Done outside Daoris' }));
    expect(onHeld).toHaveBeenCalledWith({ unit: 'run:q7', node: 'release', attempt: 2, resolution: 'done-outside', link: 'https://dev.example/release/90' });
  });

  it('names the holder its row reads, passes the epoch the row read, and draws the row’s holder presses', async () => {
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'sign', act: 'approve', done: true });
    const row: StepWaitRow = { ...SIGN, holder: 'agent', holderName: 'beta', epoch: 3, holds: ['take', 'hand', 'give-back'] };
    render(
      <RunWaitsHead waits={[{ run: 'q7', node: 'sign', why: 'approval', role: 'approver' }]} steps={[row]} onWord={onWord}
        hold={(held) => <button type="button">{`hold ${held.holds?.join(' ')}`}</button>} />,
    );

    const line = screen.getByRole('group', { name: 'sign: waiting on approver: beta' });
    expect(within(line).getByRole('button', { name: 'hold take hand give-back' })).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'Approve…' }));
    await userEvent.click(within(line).getByRole('button', { name: 'Approve' }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'sign', act: 'approve', epoch: 3, words: null });
  });

  it('says nothing where no step of its run waits on the person', () => {
    const { container } = render(<RunWaitsHead waits={[]} steps={[SIGN]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
