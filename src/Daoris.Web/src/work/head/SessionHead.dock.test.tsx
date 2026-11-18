import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import '../../locales/i18n';
import type { Session } from '../../data/api';
import type { PermissionWait } from '../waits/PermissionAsks';
import { SessionHead } from './SessionHead';

// One place for what waits on the person (D320): the dock above the composer presses every wait, so the session's head draws
// none of its cards: no park card with Finish and Decline, no held tool call, no go-ahead, no rule, no work to accept.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q7', repository: 'engine', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-11-18T08:00:00Z', updated: '2026-11-18T09:30:00Z', note: 'Which table holds the totals?', ...over,
});

const HELD: PermissionWait = {
  session: 's1a2b3c4', request: 'p1', call: null, since: '2026-11-18T09:50:00Z', until: null, title: 'Bash', kind: 'execute',
  choices: [{ id: 'once', kind: 'allow_once', name: 'Allow once' }],
};

describe('SessionHead, with the dock below', () => {
  it('draws none of the cards the dock presses', () => {
    const { container } = render(
      <SessionHead
        session={session()}
        headed
        onResolve={vi.fn()}
        onAnswerSession={vi.fn()}
        permissions={{ waits: [HELD], onAnswer: vi.fn(), busy: false }}
        lands={{ branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 2, uncommitted: 0 }}
        landing={{ session: 's1a2b3c4', form: 'merge', target: 'main' } as never}
        onReviewRun={vi.fn()}
      />,
    );
    expect(screen.queryByRole('button', { name: /^Finish/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Decline/ })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Allow once' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Accept/ })).toBeNull();
    expect(container.querySelector('[data-wait-section]')).toBeNull();
  });
});
