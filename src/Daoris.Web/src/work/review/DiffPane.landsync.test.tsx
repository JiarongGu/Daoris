import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The review's foot says what Accept leaves out because the line holds it and what it replays; where replaying conflicts, Accept's
// place holds the words its session is asked to rebase with, and one press says them through the say door.

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear: what the mocked shell publishes after a press reaches it, as the kit's pump would.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published }),
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import i18n from '../../locales/i18n';
import { useStateHub } from '../../bridge/shell';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'notes.txt', status: 'added', added: 1, removed: 0, patch: '@@ -0,0 +1 @@\n+notes' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-gap', source: 'workspace', accept: { run: 'q1', node: 'review' } };
const LEFT = [{ commit: '1a2b3c4d', subject: 'the fix', why: 'patch', on: '9f8e7d6c' }];
const LANDS = [{ commit: '2b3c4d5e', subject: 'more of the work' }, { commit: '3c4d5e6f', subject: 'the notes' }];
const WORDS = 'Rebase your work onto `main`: the line moved since your work began and already holds some of it.';
const CONFLICT = { says: 'CONFLICT (content): Merge conflict in CHANGELOG.md.', words: WORDS };

function answer(landing: () => unknown) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;    if (type === 'LANDING') return landing();
    if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume' };
    return {};
  });
}

/** The window's state hub, as `ShellSignals` mounts it: a refused landing's plan is asked again here. */
function Hub() {
  useStateHub();
  return null;
}

function pane() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <Hub />
        <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const asked = (type: string) => invoke.mock.calls.filter(([, called]) => called === type);

describe('what Accept leaves out because the line holds it', () => {
  afterEach(() => { invoke.mockReset(); });

  it('names each commit it leaves out and why, and what it replays, before the press', async () => {
    answer(() => ({ ...PLAN, leftOut: LEFT, lands: LANDS }));
    pane();

    expect(await screen.findByText('Accepting leaves out 1 commit the line already holds:')).toBeInTheDocument();
    expect(screen.getByText('1a2b3c4d')).toBeInTheDocument();
    expect(screen.getByText(/on the line as/)).toHaveTextContent('on the line as 9f8e7d6c, the same change');
    expect(screen.getByText('It replays the 2 other commits onto the line.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument();
  });

  it('says nothing more where the line holds none of its commits and has not moved', async () => {
    answer(() => PLAN);
    pane();

    expect(await screen.findByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.queryByText(/leaves out/)).toBeNull();
    expect(screen.queryByText(/replays/)).toBeNull();
  });

  // The line moved since the work began and holds none of it, so Accept replays it onto the line first.
  it('says Accept replays the work onto the line first where the line only moved', async () => {
    answer(() => ({ ...PLAN, leftOut: [], lands: LANDS }));
    pane();

    expect(await screen.findByText('The line moved since this work began: Accepting replays its 2 commits onto the line first.')).toBeInTheDocument();
    expect(screen.queryByText(/leaves out/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Accept' })).toBeInTheDocument();
  });

  it('says it in Chinese too', async () => {
    answer(() => ({ ...PLAN, leftOut: [], lands: [LANDS[0]] }));
    await i18n.changeLanguage('zh');
    try {
      pane();
      expect(await screen.findByText('这份工作开始后主线已前进：采纳会先把它的 1 个提交重放到主线上。')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

describe('a replay onto the line that conflicts', () => {
  afterEach(() => { invoke.mockReset(); });

  it('stands in Accept’s place with the words, and its press says them to the session', async () => {
    answer(() => ({ ...PLAN, leftOut: LEFT, lands: LANDS, conflict: CONFLICT }));
    pane();

    expect(await screen.findByText(/replaying this work onto the line conflicts/)).toHaveTextContent('Merge conflict in CHANGELOG.md');
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    expect(screen.getByText('These words go to its session:')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Ask it to rebase' }));

    expect(await screen.findByText('Said to its session. Accept is offered again once its tree changes.')).toBeInTheDocument();
    expect(asked('SESSION_INPUT').map(([, , options]) => options.payload)).toEqual([{ id: 's1a2b3c4', text: WORDS }]);
    expect(asked('WORKFLOW_NODE')).toHaveLength(0);
  });
});

// Accept itself carries a session on where its tree holds uncommitted work or its landing conflicts, in
// Daoris's own words, with no card, and the work lands once its tree is clean; Review shows it as carried on, never as finished work.
describe('an Accept that carries its session on', () => {
  afterEach(() => { invoke.mockReset(); });

  it('says what Accept does with uncommitted work before the press, and no longer says it refuses', async () => {
    answer(() => ({ ...PLAN, uncommitted: 2 }));
    pane();

    expect(await screen.findByText('Accept carries its session on to commit the 2 uncommitted files, or to restore them and say so, and lands the work once its tree is clean.'))
      .toBeInTheDocument();
    expect(screen.queryByText(/will refuse/)).toBeNull();
  });

  it('says it in Chinese too', async () => {
    answer(() => ({ ...PLAN, carried: { carries: 2, awaiting: true } }));
    await i18n.changeLanguage('zh');
    try {
      pane();
      expect(await screen.findByText('已采纳：它的会话已被继续，去把它的工作树处理干净，工作树干净后即会落地它的工作。它还不是已完成的工作。')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
