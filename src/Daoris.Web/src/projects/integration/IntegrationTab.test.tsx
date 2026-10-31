import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { IntegrationLook } from '../../bridge/repositories/integrate';
import { IntegrationTab } from './IntegrationTab';

// INTEGRATE1h (the integration design §5.8): the Integration tab draws the queue, a candidate's fixed strip of steps with what holds it,
// what went back, the superseded commits and the history, in both languages.

const STEPS = ['form', 'replay', 'resolve', 'conflict', 'gates', 'judge', 'promote', 'settle', 'tidy', 'close']
  .map((step) => ({ step, state: step === 'form' ? 'done' : 'waiting' }));

const LOOK: IntegrationLook = {
  repository: 'engine',
  queue: [{ request: 'r1', line: 'main', branch: 'feature/a', tip: '1111111aaaa', since: '2026-10-30T09:00:00Z' }],
  candidates: [{
    candidate: 'c1', line: 'main', base: '0000000', since: '2026-10-30T09:01:00Z', steps: STEPS, at: 'replay',
    members: [{ request: 'r2', branch: 'feature/b', state: 'held', said: 'it conflicts in a.txt' }],
    holds: 'conflict', release: 'decide', by: 'owner',
  }],
  handedBack: [{ request: 'r3', line: 'main', branch: 'feature/c', tip: '2222222bbbb', since: '2026-10-30T09:02:00Z', said: 'its gate failed' }],
  superseded: [{ request: 'r2', commit: '3333333cccc', subject: 'old work', by: '4444444dddd', files: 2 }],
  promoted: [{ request: 'r4', line: 'main', branch: 'feature/d', tip: '5555555eeee', since: '2026-10-30T09:03:00Z' }],
};

describe("a repository's Integration tab", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws the queue, the candidate with its strip and what holds it, what went back, the superseded and the history', () => {
    render(<IntegrationTab repository="engine" look={LOOK} />);

    expect(screen.getByRole('heading', { name: 'Integration' })).toBeInTheDocument();
    expect(screen.getByText('feature/a')).toBeInTheDocument();
    expect(within(screen.getByRole('list', { name: 'Steps' })).getAllByRole('listitem')).toHaveLength(10);
    expect(screen.getByText(/A member is held for its conflict/)).toHaveTextContent('releases it, and you may press it');
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
    expect(screen.getByText('已退回')).toBeInTheDocument();
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

  it('says it is reading, says nothing is asked, and says a refusal in place', () => {
    const { rerender } = render(<IntegrationTab repository="engine" reading />);
    expect(screen.getByText('Reading the integration.')).toBeInTheDocument();

    rerender(<IntegrationTab repository="engine" look={{ ...LOOK, queue: [], candidates: [], handedBack: [], superseded: [], promoted: [] }} />);
    expect(screen.getByText(/Nothing is asked into this line/)).toBeInTheDocument();

    rerender(<IntegrationTab repository="engine" refusal="the driver is coming up" />);
    expect(screen.getByText('the driver is coming up')).toBeInTheDocument();
  });
});
