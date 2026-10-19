import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// LANDSYNC1 (D87's 2026-10-15 amendment points 4 and 5): the review's foot says what Accept leaves out because the line holds it and
// what it replays, before the press; where replaying conflicts, the plan's trial or the press's refusal, Accept's place holds the words
// its session is asked to rebase with, and one press says them through D137's say door.

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear (STATEHUB1f, D169): what the mocked shell publishes after a press reaches it, as the kit's pump would.
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

import '../../locales/i18n';
import { useStateHub } from '../../bridge/shell';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'notes.txt', status: 'added', added: 1, removed: 0, patch: '@@ -0,0 +1 @@\n+notes' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-gap', source: 'workspace' };
const LEFT = [{ commit: '1a2b3c4d', subject: 'the fix', why: 'patch', on: '9f8e7d6c' }];
const LANDS = [{ commit: '2b3c4d5e', subject: 'more of the work' }, { commit: '3c4d5e6f', subject: 'the notes' }];
const WORDS = 'Rebase your work onto `main`: the line moved since your work began and already holds some of it.';
const CONFLICT = { says: 'CONFLICT (content): Merge conflict in CHANGELOG.md.', words: WORDS };

function answer(landing: () => unknown, pressed?: unknown) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;
    if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
    if (type === 'LANDING') return landing();
    if (type === 'LAND_SESSION_TREE') return pressed;
    if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume' };
    return {};
  });
}

/** The window's state hub, as `ShellSignals` mounts it: a refused landing's plan is asked again here (STATEHUB1f, D169). */
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

  it('says nothing more where the line holds none of its commits', async () => {
    answer(() => PLAN);
    pane();

    expect(await screen.findByRole('button', { name: 'Accept' })).toBeInTheDocument();
    expect(screen.queryByText(/leaves out/)).toBeNull();
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
    expect(asked('LAND_SESSION_TREE')).toHaveLength(0);
  });

  it('follows a press refused for a conflict: the refusal said, the plan read again, the words in Accept’s place', async () => {
    let plans = 0;
    answer(() => (++plans === 1 ? { ...PLAN, leftOut: LEFT, lands: LANDS } : { ...PLAN, leftOut: LEFT, lands: LANDS, conflict: CONFLICT }), {
      session: 's1a2b3c4', done: false, message: 'Nothing was landed: replaying its own commits onto `main` conflicts.', conflict: CONFLICT,
    });
    pane();

    await userEvent.click(await screen.findByRole('button', { name: 'Accept' }));

    expect(await screen.findByText('Nothing was landed: replaying its own commits onto `main` conflicts.', { exact: false })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Ask it to rebase' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
    await waitFor(() => expect(asked('LANDING').length).toBeGreaterThanOrEqual(2));
  });
});
