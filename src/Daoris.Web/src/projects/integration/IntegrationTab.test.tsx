import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { IntegrationLook } from '../../bridge/repositories/integrate';
import { IntegrationTab } from './IntegrationTab';

// The Integration tab draws the queue, a candidate's fixed strip of steps with what holds it,
// what went back, the superseded commits and the history, in both languages.

const STEPS = ['form', 'replay', 'resolve', 'conflict', 'gates', 'judge', 'promote', 'settle', 'tidy', 'close']
  .map((step) => ({ step, state: step === 'form' ? 'done' : 'waiting' }));

const LOOK: IntegrationLook = {
  repository: 'engine',
  queue: [{ request: 'r1', line: 'main', branch: 'feature/a', tip: '1111111aaaa', since: '2026-10-30T09:00:00Z' }],
  candidates: [{
    candidate: 'c1', line: 'main', base: '0000000', since: '2026-10-30T09:01:00Z', steps: STEPS, at: 'replay',
    members: [{ request: 'r2', branch: 'feature/b', state: 'held', said: 'it conflicts in a.txt' }],
    holds: 'conflict', by: 'owner',
  }],
  handedBack: [{ request: 'r3', line: 'main', branch: 'feature/c', tip: '2222222bbbb', since: '2026-10-30T09:02:00Z', said: 'its gate failed' }],
  superseded: [{ request: 'r2', commit: '3333333cccc', subject: 'old work', by: '4444444dddd', files: 2 }],
  promoted: [{ request: 'r4', line: 'main', branch: 'feature/d', tip: '5555555eeee', since: '2026-10-30T09:03:00Z' }],
  waiting: [],
};

/** A candidate the policy held for the owner's decide, the queue in its order, and a landing waiting for the card. */
const ACTS: IntegrationLook = {
  ...LOOK,
  queue: [
    { request: 'r5', line: 'main', branch: 'feature/e', tip: '6666666ffff', since: '2026-10-30T09:04:00Z', ordered: 'first' },
    { request: 'r1', line: 'main', branch: 'feature/a', tip: '1111111aaaa', since: '2026-10-30T09:00:00Z' },
  ],
  candidates: [{
    candidate: 'c2', line: 'main', base: '0000000', since: '2026-10-30T09:01:00Z', steps: STEPS, at: 'judge',
    members: [{ request: 'r6', branch: 'feature/f', state: 'replayed' }],
    holds: 'decide', release: 'decide', by: 'owner', why: 'an unconfirmed failure of unit',
  }],
  waiting: [{ unit: 'u1', branch: 'feature/g', tip: '7777777aaaa', code: 'awaits-owner', said: 'it waits for the owner’s card', since: '2026-10-30T09:05:00Z' }],
};

describe("a repository's Integration tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the queue, the candidate with its strip and what holds it, what went back, the superseded and the history', () => {
    render(<IntegrationTab repository="engine" look={LOOK} />);

    expect(screen.getByRole('heading', { name: 'Integration' })).toBeInTheDocument();
    expect(screen.getByText('feature/a')).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: 'Steps' })).getAllByRole('listitem')).toHaveLength(10);
    expect(screen.getByText(/A member is held for its conflict/)).toHaveTextContent('it waits for the steward’s act or yours');
    expect(screen.getByText('its gate failed')).toBeInTheDocument();
    expect(screen.getByText(/superseded by/)).toBeInTheDocument();
    expect(screen.getByText('feature/d')).toBeInTheDocument();
  });

  it('says the same in Chinese, with the glossary names', async () => {
    await i18n.changeLanguage('zh');
    render(<IntegrationTab repository="engine" look={LOOK} />);

    expect(screen.getByRole('heading', { name: '集成' })).toBeInTheDocument();
    expect(screen.getByText('main 的候选')).toBeInTheDocument();
    expect(screen.getByText('等待处置的已被取代的提交')).toBeInTheDocument();
    expect(screen.getByText('已打回')).toBeInTheDocument();
  });

  it('offers a drop on a queued request and a recheck, and nothing where no act is handed', async () => {
    const onDrop = vi.fn();
    const onRecheck = vi.fn();
    const { rerender } = render(<IntegrationTab repository="engine" look={LOOK} onDrop={onDrop} onRecheck={onRecheck} />);

    await userEvent.click(screen.getByRole('button', { name: 'Drop feature/a from the queue' }));
    await userEvent.click(screen.getByRole('button', { name: 'Check again' }));
    expect(onDrop).toHaveBeenCalledWith('r1');
    expect(onRecheck).toHaveBeenCalled();

    rerender(<IntegrationTab repository="engine" look={LOOK} />);
    expect(screen.queryByRole('button', { name: 'Check again' })).toBeNull();
  });

  /** A candidate held for a decide offers the owner's three, each pressed with its candidate; a recorded one says it waits for the run. */
  it('offers the owner’s decide on a held candidate and says a recorded one waits for the line’s run', async () => {
    const onDecide = vi.fn();
    const { rerender } = render(<IntegrationTab repository="engine" look={ACTS} onDecide={onDecide} />);

    expect(screen.getByText(/held for your decide/)).toHaveTextContent('an unconfirmed failure of unit');
    await userEvent.click(screen.getByRole('button', { name: 'Split' }));
    await userEvent.click(screen.getByRole('button', { name: 'Hand back' }));
    await userEvent.click(screen.getByRole('button', { name: 'Land' }));
    expect(onDecide.mock.calls).toEqual([['c2', 'split'], ['c2', 'hand-back'], ['c2', 'land']]);

    rerender(<IntegrationTab repository="engine" look={{ ...ACTS, candidates: [{ ...ACTS.candidates[0]!, release: null, decided: 'land' }] }} onDecide={onDecide} />);
    expect(screen.getByText(/You decided/)).toHaveTextContent('land');
    expect(screen.queryByRole('button', { name: 'Land' })).toBeNull();
  });

  /** The queue says each request's place and where an order moved it, and moves one first or last. */
  it('numbers the queue in its order, says a moved request, and moves one first or last', async () => {
    const onOrder = vi.fn();
    render(<IntegrationTab repository="engine" look={ACTS} onOrder={onOrder} />);

    const queue = within(screen.getByRole('list', { name: 'Queued' })).getAllByRole('listitem');
    expect(queue.map((row) => row.textContent)).toEqual([expect.stringMatching(/^1\. ?feature\/e.*moved first/), expect.stringMatching(/^2\. ?feature\/a/)]);
    await userEvent.click(screen.getByRole('button', { name: 'Move feature/a first' }));
    await userEvent.click(screen.getByRole('button', { name: 'Move feature/e last' }));
    expect(onOrder.mock.calls).toEqual([['r1', 'first'], ['r5', 'last']]);
  });

  /** A superseded commit's two answers, and a landing waiting for the owner's card with its Request. */
  it('answers a superseded commit and requests a landing that waits for the card', async () => {
    const onAnswer = vi.fn();
    const onRequest = vi.fn();
    render(<IntegrationTab repository="engine" look={ACTS} onAnswer={onAnswer} onRequest={onRequest} />);

    await userEvent.click(screen.getByRole('button', { name: 'Superseded' }));
    await userEvent.click(screen.getByRole('button', { name: 'Rebase it' }));
    expect(onAnswer.mock.calls).toEqual([['r2', '3333333cccc', 'superseded'], ['r2', '3333333cccc', 'rebase']]);
    expect(screen.getByRole('heading', { name: 'Waiting for your request' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Request feature/g into the line' }));
    expect(onRequest).toHaveBeenCalledWith('feature/g');
  });

  it('says the acts in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<IntegrationTab repository="engine" look={ACTS} onDecide={vi.fn()} onAnswer={vi.fn()} onRequest={vi.fn()} onOrder={vi.fn()} />);

    expect(screen.getByRole('button', { name: '拆分' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '打回' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: '等你请求' })).toBeInTheDocument();
  });

  it('says it is reading, says nothing is asked, and says a refusal in place', () => {
    const { rerender } = render(<IntegrationTab repository="engine" reading />);
    expect(screen.getByText('Reading the integration.')).toBeInTheDocument();

    rerender(<IntegrationTab repository="engine" look={{ ...LOOK, queue: [], candidates: [], handedBack: [], superseded: [], promoted: [] }} />);
    expect(screen.getByText(/Nothing is asked into this line/)).toBeInTheDocument();

    rerender(<IntegrationTab repository="engine" refusal="the driver is coming up" />);
    expect(screen.getByText('the driver is coming up')).toBeInTheDocument();
  });
});
