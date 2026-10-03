import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import type { Session } from '../api';

// REVIEW4: the review while git reads, over a slow mocked bridge. Its frame and skeleton stand at once and the words
// say for how long after a moment; each refusal is worded with the person's next move; an ended session's second open
// is served from the cache while a live one's reads again behind the last answer; and leaving stops the wait without
// throwing away what the host answers after.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: () => {},
}));

import '../i18n';
import { keys } from '../queries';
import { DiffPane } from './DiffPane';

const ENDED: Session = {
  id: 's1a2b3c4',
  quest: 'abc123',
  repository: 'report-ui',
  adapter: 'claude-code',
  state: 'completed',
  kind: 'driven',
  created: '2026-10-03T08:00:00Z',
  updated: '2026-10-03T09:00:00Z',
  workspace: 'default',
  tree: 'C:/somewhere/.daoris/trees/default/report-ui/s-2394e5d9',
  baseCommit: '0fda18c2b7e4a9d1',
  evidence: 'commits landed:\nabc1234 Draw the header\ndef5678 Name the columns\n0123abc Test the header',
};

const LIVE: Session = { ...ENDED, state: 'working', evidence: undefined };

const DIFF = {
  session: 's1a2b3c4',
  base: '0fda18c2b7e4a9d1',
  truncated: null,
  files: [{ path: 'src/report/header.ts', status: 'modified', added: 4, removed: 1, patch: '@@ -1 +1,2 @@\n-a\n+b' }],
};

const refusal = (code: string) => Object.assign(new Error('the host’s English'), { code, parameters: { session: 's1a2b3c4' } });

/** Every read a test left open, settled after it: the review's one read per session outlives the pane, as git does. */
const open: ((error: unknown) => void)[] = [];

/** An answer the test hands over when it chooses: the host still reading git, as the installed window's took 55 s. */
function slow() {
  let answer: (value: unknown) => void = () => {};
  let refuse: (error: unknown) => void = () => {};
  const promise = new Promise((resolve, reject) => { answer = resolve; refuse = reject; });
  open.push(refuse);
  return { promise, answer, refuse };
}

const diffs = () => invoke.mock.calls.filter(([module, route]) => module === 'DAORIS.DRIVER' && route === 'SESSION_DIFF');

function answering(diff: () => unknown) {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return diff();
    if (type === 'HANDOFF_PLAN') return { session: 's1a2b3c4', branch: null };
    return {};
  });
}

const newClient = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });

function pane(client: QueryClient, record: Session = ENDED) {
  return (
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <DiffPane session={record.id} record={record} title="Fix the report header" hasTree />
      </Tooltip.Provider>
    </QueryClientProvider>
  );
}

describe('the review while git reads (REVIEW4)', () => {
  afterEach(async () => {
    vi.useRealTimers();
    // A read answered already ignores this; one still open ends, so the next test's review asks afresh.
    open.splice(0).forEach((refuse) => refuse(refusal('TIMEOUT')));
    await act(async () => {});
    invoke.mockReset();
  });

  /**
   * The installed window's case: a session of seventy files, whose diff took most of a minute. The frame stands at once
   * with what the record holds, the files and the patch as a skeleton beneath words saying what is read, and after a
   * moment for how long. Nothing acts on a tree whose state is not known yet.
   */
  it('stands the frame and the skeleton at once, and says for how long after a moment', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const host = slow();
    answering(() => host.promise);
    const { container } = render(pane(newClient()));

    expect(screen.getByText('Fix the report header')).toBeTruthy();
    expect(screen.getByText('report-ui')).toBeTruthy();
    expect(screen.getByText('daoris/s-2394e5d9')).toBeTruthy();
    expect(screen.getByText('since 0fda18c2')).toBeTruthy();
    expect(screen.getByText('3 commits')).toBeTruthy();
    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui…');
    expect(container.querySelector('[data-skeleton="review"]')).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Discard tree' })).toBeNull();

    // The page waits as long as git may take, where the bridge's own 30 seconds gave up first.
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DIFF', {
      payload: { id: 's1a2b3c4' }, timeoutMs: 180_000,
    }));

    act(() => vi.advanceTimersByTime(2_000));
    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui… 2s');
    act(() => vi.advanceTimersByTime(53_000));
    expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui… 55s');

    await act(async () => host.answer(DIFF));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();
    expect(screen.queryByRole('status')).toBeNull();
    expect(container.querySelector('[data-skeleton="review"]')).toBeNull();
    // The frame stays, with the range the answer measured from.
    expect(screen.getByText('since 0fda18c2')).toBeTruthy();
  });

  /** Each code the route refuses with, and the bridge's own: what happened, and what the person can do next. */
  it.each([
    ['SESSION_NOT_REVIEWABLE', 'Its work is on another machine', /Review it on the machine that ran it/],
    ['SESSION_TREE_GONE', 'Its tree is gone', /A landing that made a branch is read from that branch/],
    ['SESSION_NO_BASE', 'No range to read', /Its timeline still lists the commits it reported/],
    ['SESSION_RANGE_UNREADABLE', 'Git could not read its changes', /if its tree was moved or discarded/],
    ['DRIVER_NOT_READY', 'The driver is still starting', /reads again by itself once the driver answers/],
    ['TIMEOUT', 'The changes took too long to read', /waited 3 minutes for git/],
  ])('says %s as what happened and what can be done, under the frame', async (code, headline, next) => {
    answering(() => { throw refusal(code); });
    render(pane(newClient()));

    expect(await screen.findByText(headline)).toBeTruthy();
    expect(screen.getByText(next)).toBeTruthy();
    expect(screen.queryByText(/the host’s English/)).toBeNull();
    // The frame is still what the record says.
    expect(screen.getByText('Fix the report header')).toBeTruthy();
  });

  /** A read that can answer otherwise is offered again, and while it runs the review is reading, not failed. */
  it('reads again on the press, saying it reads while it does', async () => {
    const host = slow();
    let first = true;
    answering(() => {
      if (first) {
        first = false;
        throw refusal('TIMEOUT');
      }
      return host.promise;
    });
    render(pane(newClient()));

    await userEvent.click(await screen.findByRole('button', { name: 'Read again' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Reading the changes in report-ui…');
    expect(screen.queryByText('The changes took too long to read')).toBeNull();

    await act(async () => host.answer(DIFF));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();
    expect(diffs()).toHaveLength(2);
  });

  /** An ended session's committed range does not move: its review's second open is the first's answer, at once. */
  it('serves a second open of an ended session from the cache', async () => {
    answering(() => DIFF);
    const client = newClient();
    const first = render(pane(client));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();
    first.unmount();

    render(pane(client));
    expect(screen.getByText('src/report/header.ts')).toBeTruthy();
    expect(screen.queryByRole('status')).toBeNull();
    expect(diffs()).toHaveLength(1);
  });

  /**
   * A live session is still committing: its second open reads again once the moment every view is fresh for has
   * passed, holding the last answer dimmed until the new one.
   */
  it('reads a live session again on its second open, holding the last answer dimmed meanwhile', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    answering(() => DIFF);
    const client = newClient();
    const first = render(pane(client, LIVE));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();
    first.unmount();

    // A glance away and straight back is the same answer: no second git run.
    const glance = render(pane(client, LIVE));
    expect(screen.getByText('src/report/header.ts')).toBeTruthy();
    glance.unmount();
    expect(diffs()).toHaveLength(1);

    vi.setSystemTime(Date.now() + 16_000);
    const host = slow();
    answering(() => host.promise);
    const { container } = render(pane(client, LIVE));
    expect(screen.getByText('src/report/header.ts')).toBeTruthy();
    expect(await screen.findByRole('status')).toHaveTextContent('reading again…');
    expect(container.querySelector('[aria-busy="true"]')?.className).toMatch(/opacity-60/);

    await act(async () => host.answer({ ...DIFF, files: [...DIFF.files, { path: 'src/report/columns.ts', status: 'added', added: 9, removed: 0, patch: null }] }));
    expect(await screen.findByText('src/report/columns.ts')).toBeTruthy();
    expect(screen.queryByRole('status')).toBeNull();
    expect(container.querySelector('[aria-busy="true"]')).toBeNull();
    expect(diffs()).toHaveLength(2);
  });

  /** A session that ends while its review is open is read again, since its range is final only now. */
  it('reads again when the record moves past the answer it holds', async () => {
    answering(() => DIFF);
    const client = newClient();
    const { rerender } = render(pane(client, LIVE));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();

    rerender(pane(client, { ...ENDED, updated: new Date(Date.now() + 60_000).toISOString() }));
    await waitFor(() => expect(diffs()).toHaveLength(2));
  });

  /**
   * Leaving stops the page's wait: the query is cancelled and no clock runs for a review nobody looks at. The bridge
   * cannot stop git, so what the host answers after is kept — a return is served from it rather than asking again.
   */
  it('stops waiting when the person leaves, and keeps what the host answers after', async () => {
    const host = slow();
    answering(() => host.promise);
    const client = newClient();
    const first = render(pane(client));
    await waitFor(() => expect(diffs()).toHaveLength(1));

    first.unmount();
    expect(client.getQueryState(keys.diff('s1a2b3c4'))?.fetchStatus).toBe('idle');

    await act(async () => host.answer(DIFF));
    render(pane(client));
    expect(screen.getByText('src/report/header.ts')).toBeTruthy();
    expect(diffs()).toHaveLength(1);
  });

  /**
   * A landing or a discard invalidates the review: the read again is its own, not the one begun before the act, and
   * the earlier read's answer, arriving last, is never laid over the newer one.
   */
  it('asks afresh after an act that changed the tree, and never lays the earlier read over the newer', async () => {
    const before = slow();
    const after = slow();
    let calls = 0;
    answering(() => {
      calls += 1;
      return calls === 1 ? DIFF : calls === 2 ? before.promise : after.promise;
    });
    const client = newClient();
    const { rerender } = render(pane(client, LIVE));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();

    // The record moves, so a read again is on its way when the act lands.
    rerender(pane(client, { ...LIVE, updated: new Date(Date.now() + 60_000).toISOString() }));
    await waitFor(() => expect(diffs()).toHaveLength(2));

    act(() => void client.invalidateQueries({ queryKey: keys.diff('s1a2b3c4') }));
    await waitFor(() => expect(diffs()).toHaveLength(3));

    const landed = { ...DIFF, files: [{ path: 'src/report/columns.ts', status: 'added', added: 9, removed: 0, patch: null }] };
    await act(async () => after.answer(landed));
    expect(await screen.findByText('src/report/columns.ts')).toBeTruthy();

    await act(async () => {
      before.answer(DIFF);
      // Past the promise chain and the cache's own notification, so a late write would be on screen by now.
      await new Promise((resolve) => setTimeout(resolve, 20));
    });
    expect((client.getQueryData(keys.diff('s1a2b3c4')) as typeof DIFF).files.map((file) => file.path)).toEqual(['src/report/columns.ts']);
    expect(screen.getByText('src/report/columns.ts')).toBeTruthy();
    expect(screen.queryByText('src/report/header.ts')).toBeNull();
  });

  /** A return while git is still reading waits on that one read, and counts from when it began. */
  it('waits on the same read after a return, counting from when it began', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const host = slow();
    answering(() => host.promise);
    const client = newClient();
    const first = render(pane(client));
    await waitFor(() => expect(diffs()).toHaveLength(1));
    act(() => vi.advanceTimersByTime(5_000));
    first.unmount();

    act(() => vi.advanceTimersByTime(7_000));
    render(pane(client));
    await waitFor(() => expect(screen.getByRole('status').textContent).toBe('Reading the changes in report-ui… 12s'));
    expect(diffs()).toHaveLength(1);

    await act(async () => host.answer(DIFF));
    expect(await screen.findByText('src/report/header.ts')).toBeTruthy();
    expect(diffs()).toHaveLength(1);
  });
});
