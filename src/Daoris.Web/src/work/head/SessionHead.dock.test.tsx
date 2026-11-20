import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', async () => {
  const actual = await vi.importActual<typeof import('@shenora/react')>('@shenora/react');
  return {
    ...actual,
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
    useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
    useShenoraEvent: () => {},
    useWindowMaximized: () => false,
  };
});

import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { dockOf } from '../waits/dock';
import type { ParkQuestion } from '../waits/parkQuestion';
import { stateLineOf, stateLineSaid } from './stateLine';
import { SessionHead } from './SessionHead';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q7', repository: 'engine', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-11-18T08:00:00Z', updated: '2026-11-18T09:30:00Z', note: 'Which table holds the totals?', ...over,
});

describe('SessionHead, with the dock below', () => {
  it('draws none of the cards the dock presses', () => {
    const { container } = render(
      <SessionHead
        session={session()}
        headed
        onResolve={vi.fn()}
        onAnswerSession={vi.fn()}
      />,
    );
    expect(screen.queryByRole('button', { name: /^Finish/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Decline/ })).toBeNull();
    expect(container.querySelector('[data-wait-section]')).toBeNull();
  });
});

const SIGN_IN: ParkQuestion = {
  id: 'q-sign', question: 'The production identity (ops) expired: sign in so I can restore the settings.', choices: [], recommend: null,
  why: null, by: null, owner: 'sign-in', site: null, at: '2026-11-19T09:00:00Z',
};

describe('the state line, at a park waiting on the person’s answer', () => {
  it('names its question alone where the question names no site', () => {
    const parked = session({ note: 'It waits on your sign-in at the portal.' });
    const docked = dockOf({ session: parked, question: SIGN_IN, onAnswerSession: vi.fn() });
    expect(stateLineSaid(i18n.t, stateLineOf({ session: parked, grouping: null, docked })).rest).toMatch(/its question$/);
  });
});
