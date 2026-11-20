import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

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
import { ORIGIN, sentTo, wireRelease } from '../../test/doubles/releaseShell';
import { show } from '../../test/shellHarness';
import { APPROVAL_PAGE, APPROVAL_WORDS, GRAPH_RUN_APPROVAL } from '../../workflow/runFixtures';
import type { StepWaitRow } from '../attention/attentionPushed';
import { stateLineOf, stateLineSaid } from '../head/stateLine';
import { releaseDoorsOf } from '../head/useRunWaitDoors';
import { dockOf } from '../waits/dock';
import type { ParkQuestion } from '../waits/parkQuestion';
import { SessionDock } from './SessionDock';

// The dock's presses that reach past the page: a sign-in's site and its answer, a release's approval through its plugin, and the
// rebase words a conflicting line asks its session to say.

afterEach(async () => { await i18n.changeLanguage('en'); });

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'q7', repository: 'engine', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-11-18T08:00:00Z', updated: '2026-11-18T09:30:00Z', note: 'Which table holds the totals?', ...over,
});

const SIGN_IN: ParkQuestion = {
  id: 'q-sign', question: 'The production identity (ops) expired: sign in so I can restore the settings.', choices: [], recommend: null,
  why: null, by: null, owner: 'sign-in', site: 'https://portal.example.com/settings', at: '2026-11-19T09:00:00Z',
};

describe('SessionDock, at a park waiting on the person’s sign-in', () => {
  const parked = session({ note: 'It waits on your sign-in at the portal.' });

  it('reads Waiting on you · Sign in, and carries Open the sign-in… with the site and Signed in, which answers the park', async () => {
    const opened: string[] = [];
    const onAnswerSession = vi.fn(() => Promise.resolve(null));
    render(
      <LinkOpener.Provider value={(address) => opened.push(address)}>
        <SessionDock session={parked} question={SIGN_IN} onAnswerSession={onAnswerSession} onResolve={vi.fn()} />
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
});

describe('SessionDock, at a conversation waiting on the person’s sign-in', () => {
  const chat = session({ kind: 'chat', quest: null, state: 'working', note: undefined });

  it('carries Open the sign-in… with the site and Signed in, which sends those words, and no finish or decline', async () => {
    const opened: string[] = [];
    const onAnswerSession = vi.fn(() => Promise.resolve(null));
    render(
      <LinkOpener.Provider value={(address) => opened.push(address)}>
        <SessionDock session={chat} question={SIGN_IN} onAnswerSession={onAnswerSession} onResolve={vi.fn()} answerBelow />
      </LinkOpener.Provider>,
    );

    const docked = dockOf({ session: chat, question: SIGN_IN, onAnswerSession });
    const said = stateLineSaid(i18n.t, stateLineOf({ session: chat, grouping: null, docked }));
    expect(`${said.word} · ${said.rest.split(' · ').at(-1)}`).toBe('Waiting on you · Sign in');
    expect(screen.getByText(SIGN_IN.question)).toBeInTheDocument();
    const line = screen.getByRole('group', { name: 'Sign in at portal.example.com' });
    await userEvent.click(within(line).getByRole('link', { name: 'Open the sign-in…' }));
    expect(opened).toEqual(['https://portal.example.com/settings']);
    expect(screen.queryByRole('button', { name: /^Finish/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Decline/ })).toBeNull();
    await userEvent.click(within(line).getByRole('button', { name: 'Signed in' }));
    expect(onAnswerSession).toHaveBeenCalledWith('Signed in');
  });

  it('says the sentence a refusal of the answer comes in, beside the press', async () => {
    const onAnswerSession = vi.fn(() => Promise.resolve('It is not taking words just now.'));
    render(<SessionDock session={chat} question={SIGN_IN} onAnswerSession={onAnswerSession} />);

    await userEvent.click(screen.getByRole('button', { name: 'Signed in' }));
    expect(await screen.findByText('It is not taking words just now.')).toBeInTheDocument();
  });

  it('draws nothing for a question that names no site, which is the conversation’s own reply', () => {
    const { container } = render(<SessionDock session={chat} question={{ ...SIGN_IN, site: null }} onAnswerSession={vi.fn()} />);
    expect(container).toBeEmptyDOMElement();
  });
});

describe('SessionDock, at a release step waiting on its approver', () => {
  beforeEach(() => { invoke.mockReset(); });

  const WAIT = { run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', since: '2026-11-18T10:18:40Z' };
  const ROW: StepWaitRow = {
    run: 'q7', node: 'release-approve', why: 'actor', role: 'approver', holder: 'person', presses: ['skip', 'stop'], words: APPROVAL_WORDS, url: APPROVAL_PAGE,
  };
  const drawDock = () => show(
    <SessionDock
      session={session({ state: 'working', note: undefined })}
      runWaits={{ waits: [WAIT], steps: [ROW], onWord: vi.fn(), release: releaseDoorsOf([GRAPH_RUN_APPROVAL]) }}
    />,
  );

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

  it('keeps the person’s press where they hold the approver, though the plugin hands the command to an agent holding it as its tool', async () => {
    wireRelease(invoke, { tool: true });
    drawDock();

    const line = screen.getByRole('group', { name: 'release-approve: waiting on approver: you' });
    expect(await within(line).findByRole('button', { name: 'Approve' })).toBeInTheDocument();
    expect(within(line).getByRole('button', { name: 'Reject' })).toBeInTheDocument();
  });
});

describe('SessionDock, the rebase words where the line conflicts', () => {
  beforeEach(() => { invoke.mockReset(); });

  const LANDS = { branch: 'daoris/s-1a2b3c4', tree: 's-1a2b3c4', commits: 3, uncommitted: 0 };
  const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/delta' };
  const CONFLICT = { says: 'CONFLICT (content): Merge conflict in CHANGELOG.md.', words: 'Rebase your work onto `main`.' };
  const draw = (landing: unknown) => render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessionDock
        session={session({ quest: 'q1', repository: 'reports', state: 'completed', tree: 'C:/somewhere/reports', note: undefined })}
        lands={LANDS} onReviewRun={vi.fn()} onSayRebase={vi.fn(async () => true)} landing={landing as never}
      />
    </QueryClientProvider>,
  );

  it('offers the rebase words in the review door’s place where replaying conflicts', () => {
    draw({ ...PLAN, conflict: CONFLICT });
    expect(screen.queryByRole('button', { name: 'Open outputs' })).toBeNull();
    expect(screen.getByText(/replaying this work onto the line conflicts/)).toHaveTextContent('Merge conflict in CHANGELOG.md');
    expect(screen.getByRole('button', { name: 'Ask it to rebase' })).toBeInTheDocument();
  });

  it('offers the door and no rebase words where nothing conflicts', () => {
    draw(PLAN);
    expect(screen.getByRole('button', { name: 'Open outputs' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Ask it to rebase' })).toBeNull();
  });

  it('says the rebase words in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw({ ...PLAN, conflict: CONFLICT });
    expect(screen.getByRole('button', { name: '请它变基' })).toBeInTheDocument();
  });
});
