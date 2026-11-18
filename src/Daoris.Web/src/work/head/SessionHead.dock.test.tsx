import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

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
import { LinkOpener } from '../../design/links';
import { dockOf } from '../waits/dock';
import type { ParkQuestion } from '../waits/parkQuestion';
import { stateLineOf, stateLineSaid } from './stateLine';
import { ORIGIN, sentTo, wireRelease } from '../../test/doubles/releaseShell';
import { show } from '../../test/shellHarness';
import { APPROVAL_PAGE, APPROVAL_WORDS, GRAPH_RUN_APPROVAL } from '../../workflow/runFixtures';
import type { StepWaitRow } from '../attention/attentionPushed';
import { WaitStrip } from '../waits/WaitStrip';
import { SessionHead } from './SessionHead';
import { releaseDoorsOf } from './useRunWaitDoors';

// The dock above the composer presses every wait, a release's approval with its run's view's card, so the head draws none of its cards.

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

const WAIT = { run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', since: '2026-11-18T10:18:40Z' };
const ROW: StepWaitRow = {
  run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', holder: 'person', presses: ['skip', 'stop'], words: APPROVAL_WORDS, url: APPROVAL_PAGE,
};

const drawDock = () => show(
  <WaitStrip
    session={session({ state: 'working', note: undefined })}
    runWaits={{ waits: [WAIT], steps: [ROW], onWord: vi.fn(), release: releaseDoorsOf([GRAPH_RUN_APPROVAL]) }}
  />,
);

const SIGN_IN: ParkQuestion = {
  id: 'q-sign', question: 'The production identity (ops) expired: sign in so I can restore the settings.', choices: [], recommend: null,
  why: null, by: null, owner: 'sign-in', site: 'https://portal.example.com/settings', at: '2026-11-19T09:00:00Z',
};

describe('the dock, at a park waiting on the person’s sign-in', () => {
  const parked = session({ note: 'It waits on your sign-in at the portal.' });

  it('reads Waiting on you · Sign in, and carries Open the sign-in… with the site and Signed in, which answers the park', async () => {
    const opened: string[] = [];
    const onAnswerSession = vi.fn(() => Promise.resolve(null));
    render(
      <LinkOpener.Provider value={(address) => opened.push(address)}>
        <WaitStrip session={parked} question={SIGN_IN} onAnswerSession={onAnswerSession} onResolve={vi.fn()} />
      </LinkOpener.Provider>,
    );

    const docked = dockOf({ session: parked, question: SIGN_IN, onAnswerSession });
    const said = stateLineSaid(i18n.t, stateLineOf({ session: parked, grouping: null, docked }));
    expect(`${said.word} · ${said.rest.split(' · ').at(-1)}`).toBe('Waiting on you · Sign in');
    expect(screen.getByText(SIGN_IN.question)).toBeInTheDocument();
    const line = screen.getByRole('group', { name: 'Sign in at portal.example.com' });
    await userEvent.click(within(line).getByRole('link', { name: 'Open the sign-in…' }));
    expect(opened).toEqual(['https://portal.example.com/settings']);
    await userEvent.click(within(line).getByRole('button', { name: 'Signed in' }));
    expect(onAnswerSession).toHaveBeenCalledWith('Signed in');
  });

  it('names its question alone where the question names no site', () => {
    const docked = dockOf({ session: parked, question: { ...SIGN_IN, site: null }, onAnswerSession: vi.fn() });
    expect(stateLineSaid(i18n.t, stateLineOf({ session: parked, grouping: null, docked })).rest).toMatch(/its question$/);
  });
});

describe('the dock, at a release step waiting on its approver', () => {
  beforeEach(() => { invoke.mockReset(); });

  it('approves in one press, sending approve-release with the release, the stage, the decision and the comment', async () => {
    wireRelease(invoke);
    drawDock();

    const line = screen.getByRole('group', { name: 'release-approve: waiting on approver: you' });
    expect(within(line).getByRole('button', { name: 'Not needed this run…' })).toBeInTheDocument();
    expect(within(line).getByRole('button', { name: 'End this work…' })).toBeInTheDocument();
    await userEvent.type(await within(line).findByRole('textbox', { name: 'Comment (optional)' }), 'Checked on staging.');
    await userEvent.click(within(line).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(sentTo(invoke, 'PLUGIN_COMMAND')).toEqual([{
      id: 'daoris-azure-devops', command: 'approve-release',
      args: { origin: ORIGIN, release: '90', stage: 'Prod', decision: 'approve', comment: 'Checked on staging.' },
      answers: {}, place: 'portal', language: 'en',
    }]));
  });

  it('opens the stage’s page, with no press of its own, where the plugin hands the command to agents', async () => {
    wireRelease(invoke, { tool: true });
    drawDock();

    const line = screen.getByRole('group', { name: 'release-approve: waiting on approver: you' });
    await waitFor(() => expect([sentTo(invoke, 'PLUGINS').length, sentTo(invoke, 'GIT_BRANCHES').length]).toEqual([1, 1]));
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    expect(within(line).getByRole('button', { name: 'Open' })).toBeInTheDocument();
    expect(within(line).queryByRole('button', { name: 'Approve' })).toBeNull();
    expect(within(line).queryByRole('button', { name: 'Reject' })).toBeNull();
  });
});
