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

  it('says one line however many steps and turns it has, the steps and turns in that line\'s tip', () => {
    const many: SessionGrouping = {
      ...grouping,
      passage: { ...grouping.passage!, steps: Array.from({ length: 30 }, (_, at) => ({ node: `n${at}`, kind: 'work', mark: at < 10 ? 'passed' : at === 10 ? 'current' : 'next' })) },
    };
    const turns = Array.from({ length: 12 }, (_, at) => ({ number: at + 1, cause: 'step', at: '2026-11-15T09:00:00Z', part: 's2', agent: 'claude-code', account: 'work' }));
    const { container } = render(<ul><SessionRow session={session({ turns })} grouping={many} /></ul>);

    const line = screen.getByText('Waits on its step n10, step 11 of 30.');
    expect(container.querySelectorAll('[role="list"], [role="listitem"]')).toHaveLength(0);
    // The state, the title, the facts and the one status line, whatever runs.
    expect(line.parentElement!.parentElement!.children).toHaveLength(4);
    const tip = line.getAttribute('title')!;
    expect(tip.split('\n')).toHaveLength(30 + 12);
    expect(tip).toContain('n0: passed');
    expect(tip).toContain('Turn 12 · woken by a step · claude-code · work');
  });

  it('keeps a session of one turn and no run to its one sentence', () => {
    render(<ul><SessionRow session={session({ state: 'working', turns: [session().turns![0]] })} /></ul>);

    expect(screen.getByText('Its agent is working.').getAttribute('title')).toBeNull();
  });
});
