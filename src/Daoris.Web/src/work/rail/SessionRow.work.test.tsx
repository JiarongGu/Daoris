import { afterEach, describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import type { SessionGrouping } from '../groups';
import { SessionRow } from './SessionRow';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's2', quest: 'q1', repository: 'engine', door: 'claude-code', state: 'awaiting-step', kind: 'driven',
  created: '2026-11-15T09:00:00Z', updated: '2026-11-15T09:30:00Z',
  turns: [
    { number: 1, cause: 'start', at: '2026-11-15T09:00:00Z', part: 's1', agent: 'claude-code', account: 'work' },
    { number: 2, cause: 'limit', at: '2026-11-15T09:20:00Z', part: 's2', agent: 'claude-code', account: 'home' },
  ],
  ...over,
});

const grouping: SessionGrouping = {
  session: 's2', group: 'working', shown: 'awaiting-step', archived: false, teammate: false,
  passage: {
    run: 'q1', workflow: 'integrated', version: 3,
    steps: [{ node: 'content', kind: 'work', mark: 'passed' }, { node: 'check', kind: 'check', mark: 'current' }, { node: 'landing', kind: 'land', mark: 'next' }],
  },
};

/** One row per session, as work: its run's steps, whom it waits on in one sentence, and its turns beneath it. */
describe('a session row as work', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en');
  });

  it('draws the steps passed, current and to come, the sentence, and each turn with its cause, agent and account', () => {
    render(<ul><SessionRow session={session()} grouping={grouping} /></ul>);

    expect(screen.getByRole('list', { name: 'Steps of integrated' })).toBeTruthy();
    expect(screen.getByText('content: passed')).toBeTruthy();
    expect(screen.getByText('check: now')).toBeTruthy();
    expect(screen.getByText('landing: to come')).toBeTruthy();
    expect(screen.getByText('Waits on its step check, step 2 of 3.')).toBeTruthy();
    const turns = screen.getByRole('list', { name: 'Turns' });
    expect([...turns.querySelectorAll('[role="listitem"]')].map((item) => item.textContent)).toEqual([
      'Turn 1 · started · claude-code · work', 'Turn 2 · account limit · claude-code · home',
    ]);
  });

  it('draws no turns for a session of one turn, and no steps where it stands for no run', () => {
    render(<ul><SessionRow session={session({ state: 'working', turns: [session().turns![0]] })} /></ul>);

    expect(screen.queryByRole('list', { name: 'Turns' })).toBeNull();
    expect(screen.queryByRole('list', { name: /^Steps of/ })).toBeNull();
    expect(screen.getByText('Its agent is working.')).toBeTruthy();
  });
});
