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
    const row: StepWaitRow = { ...SIGN, holder: 'agent', holderName: 'beta', epoch: 3, holds: ['hand', 'give-back'] };
    render(
      <RunWaitsHead waits={[{ run: 'q7', node: 'sign', why: 'approval', role: 'approver' }]} steps={[row]} onWord={onWord}
        hold={(held) => <button type="button">{`hold ${held.holds?.join(' ')}`}</button>} />,
    );

    const line = screen.getByRole('group', { name: 'sign: waiting on approver: beta' });
    expect(within(line).getByRole('button', { name: 'hold hand give-back' })).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'Approve…' }));
    await userEvent.click(within(line).getByRole('button', { name: 'Approve' }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'sign', act: 'approve', epoch: 3, words: null });
  });

  it('reads an agent-held task step as the agent’s with Take alone, and none of the person’s presses', () => {
    const row: StepWaitRow = {
      run: 'q7', node: 'config-change', why: 'actor', role: 'config-changer', holder: 'agent', holderName: 'builder-1', presses: [],
      epoch: 2, holds: ['take'],
    };
    render(
      <RunWaitsHead waits={[{ run: 'q7', node: 'config-change', why: 'actor', role: 'config-changer' }]} steps={[row]} onWord={vi.fn()}
        hold={(held) => <button type="button">{`hold ${held.holds?.join(' ')}`}</button>} />,
    );

    const line = screen.getByRole('group', { name: 'config-change: waiting on config-changer: builder-1' });
    expect(within(line).getByRole('button', { name: 'hold take' })).toBeInTheDocument();
    for (const name of ['Done…', 'Not needed this run…', 'End this work…']) expect(within(line).queryByRole('button', { name })).toBeNull();
  });

  it('draws the presses the driver sends on an agent-held step, withholding none itself', () => {
    const row: StepWaitRow = {
      run: 'q7', node: 'config-change', why: 'actor', role: 'config-changer', holder: 'agent', holderName: 'builder-1', presses: ['skip'], epoch: 2, holds: ['take'],
    };
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'config-change', why: 'actor', role: 'config-changer' }]} steps={[row]} onWord={vi.fn()} />);

    const line = screen.getByRole('group', { name: 'config-change: waiting on config-changer: builder-1' });
    expect(within(line).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();
  });

  it('says what the agent is blocked on the person for, linking that wait in the dock, never offering the task’s presses', async () => {
    const row: StepWaitRow = {
      run: 'q7', node: 'config-change', why: 'actor', role: 'config-changer', holder: 'agent', holderName: 'builder-1', presses: [], epoch: 2, holds: ['take'],
    };
    const onTo = vi.fn();
    render(
      <RunWaitsHead waits={[{ run: 'q7', node: 'config-change', why: 'actor', role: 'config-changer' }]} steps={[row]}
        blocked={[{ key: 'intake:a1', name: 'question' }]} onTo={onTo} />,
    );

    const line = screen.getByRole('group', { name: 'config-change: waiting on config-changer: builder-1' });
    expect(line).toHaveTextContent('builder-1 is waiting on you');
    await userEvent.click(within(line).getByRole('button', { name: 'question' }));
    expect(onTo).toHaveBeenCalledWith('intake:a1');
  });

  it('draws no blocked line on a step the person holds', () => {
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'sign', why: 'approval', role: 'approver' }]} steps={[SIGN]} blocked={[{ key: 'intake:a1', name: 'question' }]} onTo={vi.fn()} />);
    expect(screen.queryByText(/is waiting on you/)).toBeNull();
  });

  it('offers the step a run ended failed at its Try again, Done outside Daoris and End this work, whatever its quest', async () => {
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'push', act: 'again', done: true });
    const row: StepWaitRow = { run: 'q7', node: 'push', why: 'left', role: 'lead', holder: 'person', presses: ['again', 'done-outside', 'stop'] };
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'push', why: 'left', role: 'lead' }]} steps={[row]} onWord={onWord} />);

    const line = screen.getByRole('group', { name: 'push: waiting on lead: you' });
    expect(within(line).getByRole('button', { name: 'Done outside Daoris…' })).toBeInTheDocument();
    expect(within(line).getByRole('button', { name: 'End this work…' })).toBeInTheDocument();
    await userEvent.click(within(line).getByRole('button', { name: 'Try again…' }));
    await userEvent.click(within(line).getByRole('button', { name: 'Try again' }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'push', act: 'again', words: null });
  });

  it('draws a release step’s own doors the line is handed beside its held presses', () => {
    const row: StepWaitRow = { run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', holder: 'person', presses: ['skip', 'stop'] };
    const release = vi.fn((wait: { node: string }) => <button type="button">{`approval of ${wait.node}`}</button>);
    render(
      <RunWaitsHead waits={[{ run: 'q7', node: 'release-approve', why: 'actor', role: 'approver' }]} steps={[row]} onWord={vi.fn()} release={release} />,
    );

    const line = screen.getByRole('group', { name: 'release-approve: waiting on approver: you' });
    expect(within(line).getByRole('button', { name: 'approval of release-approve' })).toBeInTheDocument();
    expect(within(line).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();
    expect(within(line).getByRole('button', { name: 'End this work…' })).toBeInTheDocument();
  });

  it('says nothing where no step of its run waits on the person', () => {
    const { container } = render(<RunWaitsHead waits={[]} steps={[SIGN]} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('offers the pass of an unchecked done at holds with your words', async () => {
    const onWord = vi.fn().mockResolvedValue({ run: 'q7', node: 'holds', act: 'skip', done: true });
    const row: StepWaitRow = { run: 'q7', node: 'holds', why: 'pass', role: 'acceptor', holder: 'person', presses: ['skip', 'stop'] };
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'holds', why: 'pass', role: 'acceptor' }]} steps={[row]} onWord={onWord} />);

    const line = screen.getByRole('group', { name: 'holds: waiting on acceptor: you' });
    await userEvent.click(within(line).getByRole('button', { name: 'Not needed this run…' }));
    await userEvent.type(within(line).getByRole('textbox'), 'It does what I asked.');
    await userEvent.click(within(line).getByRole('button', { name: 'Not needed this run' }));
    expect(onWord).toHaveBeenCalledWith({ run: 'q7', node: 'holds', act: 'skip', words: 'It does what I asked.' });
  });
});

// Every `PersonWaits.All` kind in every run state (its census's `<kind>@<state>` past a run going on) has its card; a `carried` one's press is on the card that carries it.
const CENSUS: Record<string, { presses: string[]; held?: StepWaitRow['held'] } | 'carried'> = {
  'accept': 'carried',
  'approval': { presses: ['approve', 'reject', 'skip'] },
  'task': { presses: ['done', 'skip'] },
  'go-ahead': { presses: ['skip', 'stop'] },
  'held': 'carried',
  'choose': { presses: ['stop'] },
  'actor': { presses: ['done-outside'], held: { unit: 'run:q7', attempt: 1, words: ['done-outside'] } },
  'left': 'carried',
  'left@failed-held': { presses: ['again', 'done-outside', 'stop'] },
  'vacant': { presses: ['skip', 'stop'] },
  'pass': { presses: ['skip', 'stop'] },
};

describe('RunWaitsHead, every wait', () => {
  it.each(Object.entries(CENSUS))('draws `%s` with its press where a step row carries one', (kind, carries) => {
    const why = kind.split('@')[0];
    const steps: StepWaitRow[] = carries === 'carried' ? [] : [{ run: 'q7', node: 'step', why, role: 'someone', holder: 'person', ...carries }];
    render(<RunWaitsHead waits={[{ run: 'q7', node: 'step', why, role: 'someone' }]} steps={steps} onWord={vi.fn()} onHeld={vi.fn()} />);

    const line = screen.getByRole('group', { name: 'step: waiting on someone: you' });
    if (carries !== 'carried') expect(within(line).getAllByRole('button').length).toBeGreaterThan(0);
  });
});
