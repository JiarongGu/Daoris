import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// The shell's push channel: ticks, attention and endings become toasts and refetches. Moved from
// `shell.test.tsx` with MOD3: tests follow their code.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import { OverviewView } from '../overview/OverviewView';
import { ShellSignals } from './ShellSignals';
import { keys } from '../data/queries';
import { useLanding } from '../bridge/sessions/trees';
import { respond, show } from '../test/shellHarness';

describe('the shell push channel (ShellSignals)', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  it('announces readiness once — the kit buffers host events until this handshake', () => {
    show(<ShellSignals notify={() => {}} />);
    expect(notifyReady).toHaveBeenCalledTimes(1);
  });

  it('a tick becomes toasts, and everything a tick can change refetches', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />, client);

    eventHandlers.get('DAORIS.DRIVER_TICK')!({ events: ['engine  spawned s1a2b3c4', 'sync  fed 2'] });

    expect(notify).toHaveBeenCalledWith('engine  spawned s1a2b3c4');
    expect(notify).toHaveBeenCalledWith('sync  fed 2');
    // The index too: a schema rebuild is observable, and a summary cached mid-feed stayed "555 · 7"
    // on an index of 1,050 · 17 for as long as the window was open (deployed app, 2026-09-23).
    // And where each circle stands with its remote (SYNC6b): every tick runs a pass. And the asks: a
    // tick takes them (INT4b) and the band reads them (INT4d) — seen on the window, an ask made by the
    // other door was missing from *What needs you*, and its parked intake read as a bare session.
    // And the rules: a tick settles what agents proposed about them (PERM2), and the band reads that.
    // And the registry (FG4): a folder imported from a terminal left Overview saying *no workspace
    // yet* until a reload, because nothing told the page the registry had moved.
    // What is read under the sessions', the quests' and the asks' keys, passing over the records where the shell pushes them (PUSH1d2,
    // PUSH1d3).
    for (const key of [keys.allSessions, keys.allQuests, keys.allAsks]) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: key, predicate: expect.any(Function) });
    }
    for (const key of [keys.allRepositories, keys.rules, keys.allRegistry]) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: key });
    }
    // Where each circle stands, passing over the standings where the shell pushes them (PUSH1a2): each pass pushes what it moved.
    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSync, predicate: expect.any(Function) });
    // What is read under the driver's key, passing over its state where the shell pushes it (PUSH1a, `pushedState.test.tsx`).
    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.driver, predicate: expect.any(Function) });
  });

  /**
   * LOOK2a: the driver saying its service is up is heard wherever the ticks are, and the driver's answers are asked
   * again then, since the first tick can be long in coming.
   */
  it('the driver coming up asks its answers again', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    show(<ShellSignals notify={() => {}} />, client);

    eventHandlers.get('DAORIS.DRIVER_READY')!({ ready: true });

    // Its state itself is passed over where the shell pushes it, whose push says it is up (PUSH1a).
    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.driver, predicate: expect.any(Function) });
  });

  /**
   * 🔴 UX5 U30: a tick that could not reach the service toasted the .NET socket's own words, *No
   * connection could be made because the target machine actively refused it. (127.0.0.1:5188)*. The
   * loop now says which failure it was, and the page words a known one from its catalogue, in the
   * reader's language; an unknown one keeps the driver's words.
   */
  it("words a failed tick from the catalogue when the driver names the failure", () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.DRIVER_ERROR')!({ code: 'SERVICE_UNREACHABLE', message: 'the service is not answering.' });
    expect(notify).toHaveBeenLastCalledWith(expect.stringMatching(/not answering\. What is on screen/), 'error');

    eventHandlers.get('DAORIS.DRIVER_ERROR')!({ message: 'the config could not be read.' });
    expect(notify).toHaveBeenLastCalledWith('the config could not be read.', 'error');
  });

  /**
   * Why a quest is sitting — the driver has said it every tick since D46, the shell forwarded it,
   * and the page dropped it: the Overview asked "is anything sitting" and never said why, while
   * the only surface that did was a toast (deployed application, 2026-09-23).
   */
  it('the Overview says why a quest is sitting, in the driver’s own words from its last tick', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    show(
      <>
        <ShellSignals notify={() => {}} />
        <OverviewView onNavigate={() => {}} onOpenQuest={() => {}} notify={() => {}} />
      </>,
      client,
    );
    await screen.findByText('Expose a streaming budget');
    expect(screen.queryByText(/sitting —/)).not.toBeInTheDocument();

    eventHandlers.get('DAORIS.DRIVER_TICK')!({
      events: [],
      considered: [{ quest: 'abc123', repository: 'engine', verdict: 'NotDrivable', reason: 'engine is not drivable on this machine' }],
    });
    expect(await screen.findByText('sitting — engine is not drivable on this machine')).toBeTruthy();

    // A later tick that would START it is not sitting, and the line goes.
    eventHandlers.get('DAORIS.DRIVER_TICK')!({
      events: ['engine  spawned s1a2b3c4'],
      considered: [{ quest: 'abc123', repository: 'engine', verdict: 'Start', reason: 'starting' }],
    });
    await waitFor(() => expect(screen.queryByText(/sitting —/)).not.toBeInTheDocument());
  });

  /**
   * UX6d (D150 §6.2): the starts the look held on cooling accounts, an ask's intake among them, land where *What needs you*
   * reads them, replaced whole by each tick, and a tick that says none, or a shell older than them, leaves none.
   */
  it('keeps the starts a tick held on its accounts, replaced whole by the next', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    show(<ShellSignals notify={() => {}} />, client);
    const wait = {
      agent: 'claude-code', account: 'account-2', workspace: 'work', until: '2026-10-06T04:42:00Z', stated: true,
      quests: [], asks: ['a1', 'a2'], signedOut: ['account-1'],
    };

    eventHandlers.get('DAORIS.DRIVER_TICK')!({ events: [], waits: [wait] });
    expect(client.getQueryData(keys.waits)).toEqual([wait]);

    eventHandlers.get('DAORIS.DRIVER_TICK')!({ events: [] });
    expect(client.getQueryData(keys.waits)).toEqual([]);
  });

  it("a driver error arrives as an error toast, the driver's own sentence verbatim", () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.DRIVER_ERROR')!({ message: 'the loop hit a wall' });

    expect(notify).toHaveBeenCalledWith('the loop hit a wall', 'error');
  });

  /**
   * The in-window half of the notification (SURF5b). The shell raises an OS balloon only while
   * nobody is looking at the window, so these two never both fire — this is the one for when
   * somebody is, and it carries the driver's own sentence exactly as the tick's lines do.
   */
  it('a session that needs somebody becomes a toast, and the sessions refetch', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />, client);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'Parked',
      session: 's1a2b3c4',
      repository: 'engine',
      headline: 'engine — a session needs you',
      detail: 'Two ways forward; I recommend capping.',
    });

    // A park wears the status tone, because it is the one that is WAITING on somebody.
    expect(notify).toHaveBeenCalledWith(
      'engine — a session needs you: Two ways forward; I recommend capping.', 'error');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSessions, predicate: expect.any(Function) });
  });

  /**
   * SESSUX1i (D126 §4.7): a quest parked on its failed sessions waits on the person's Try again, so it wears the park's
   * tone. Its sentence is the driver's, verbatim, as every notice's is.
   */
  it("a quest's park is a demand, in the driver's own words", () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'QuestParked', session: 's3a2b3c4', quest: '7a82cc', repository: 'engine',
      headline: 'engine — `#7a82cc` parked after 3 failed sessions', detail: "You've hit your limit.",
    });

    expect(notify).toHaveBeenCalledWith("engine — `#7a82cc` parked after 3 failed sessions: You've hit your limit.", 'error');
  });

  /**
   * CONFIRMSEEN1: a terminal's press waiting for the window is its card's and its row's; the shell's balloon is for when no window is
   * in front, so the page says it in no toast over the card.
   */
  it("a terminal's press waiting for the window is no toast: its card and its row say it", () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'Confirming', session: null, headline: "A terminal's press waits for you in Daoris's window",
      detail: 'SESSION_ARCHIVE, sent by daoris-driver (4242). Confirm or refuse it there.',
    });

    expect(notify).not.toHaveBeenCalled();
  });

  it('an ending is news rather than a demand, and a session that said nothing still says something', () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'Ended', session: 's1', repository: 'tools', headline: 'tools — a session failed',
    });

    expect(notify).toHaveBeenCalledWith('tools — a session failed', 'ok');
  });

  /**
   * NOTIFY1a: a conversation that answered the person is news they wait on, never a demand: the driver's own sentence, in
   * the ending's tone, and the sessions asked again so its row shows the turn's end.
   */
  it('a conversation that answered is news in the driver\'s own words', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />, client);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'Answered', session: 'c1a2b3c4', repository: 'engine', headline: 'engine — a conversation answered you',
    });

    expect(notify).toHaveBeenCalledWith('engine — a conversation answered you', 'ok');
    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSessions, predicate: expect.any(Function) });
  });

  /**
   * NOTIFY1c: the conversation the person is reading in a window in front already shows its answer, so its toast says nothing
   * new; every other session's answer, every other kind and a hidden window keep theirs.
   */
  it('skips the answered toast for the conversation attended in a window in front, and only that', () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} attended="c1a2b3c4" />);
    const answered = (session: string) => eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'Answered', session, repository: 'engine', headline: 'engine — a conversation answered you',
    });

    answered('c1a2b3c4');
    expect(notify).not.toHaveBeenCalled();

    answered('c9z9z9z9');
    expect(notify).toHaveBeenCalledWith('engine — a conversation answered you', 'ok');

    // A park of the attended session still demands them: only an answer is news the page already shows.
    notify.mockClear();
    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({ kind: 'Parked', session: 'c1a2b3c4', headline: 'engine — a session needs you' });
    expect(notify).toHaveBeenCalledWith('engine — a session needs you', 'error');
  });

  it('keeps the answered toast while the window is hidden, though its conversation is the attended one', () => {
    const notify = vi.fn();
    const visibility = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
    try {
      show(<ShellSignals notify={notify} attended="c1a2b3c4" />);
      eventHandlers.get('DAORIS.SESSION_ATTENTION')!({ kind: 'Answered', session: 'c1a2b3c4', headline: 'engine — a conversation answered you' });
      expect(notify).toHaveBeenCalledWith('engine — a conversation answered you', 'ok');
    } finally {
      visibility.mockRestore();
    }
  });

  /** An older shell, or a reworded payload: the console must tolerate any shape (SES1's rule). */
  it('an attention event with nothing to say is not a blank toast', () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({ kind: 'Parked' });
    eventHandlers.get('DAORIS.SESSION_ATTENTION')!(undefined);

    expect(notify).not.toHaveBeenCalled();
  });

  /**
   * REV3: the driver said a conversation ended the moment its record moved, and no page code
   * listened — so a chat whose harness exited read as working until the next tick, and a message
   * typed into it in that window went nowhere.
   */
  it('a conversation\'s end refetches the sessions at once, without a toast', () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />, client);

    eventHandlers.get('DAORIS.SESSION_ENDED')!({ session: 'c0ffee11', state: 'completed' });

    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSessions, predicate: expect.any(Function) });
    expect(notify).not.toHaveBeenCalled();
  });

  /**
   * STATEHUB1 (D169): the 采纳 a tab switch read stale. A landing pressed in another window, or anywhere but this view, reaches
   * this window as the driver's event alone, with no press of this client's; the plan is asked again where it is shown, and a
   * view mounted again after it (a tab switched back) reads the new plan rather than the one its cache held as fresh.
   */
  it('a landing done in another window reads anew here, shown or mounted again, from the event alone', async () => {
    // As the application's client keeps an answer fresh (main.tsx): a remount alone would not ask again.
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 15_000 } } });
    let target = 'main';
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'LANDING' ? { session: 's1a2b3c4', form: 'merge', target } : {});
    function Plan() {
      const plan = useLanding('s1a2b3c4');
      return <p>{plan.data ? `lands in ${plan.data.target}` : 'reading'}</p>;
    }
    function Window({ showing }: { showing: boolean }) {
      return <><ShellSignals notify={() => {}} />{showing && <Plan />}</>;
    }

    const view = show(<Window showing />, client);
    expect(await screen.findByText('lands in main')).toBeInTheDocument();

    // Another window accepted it: only the driver's event reaches this one.
    target = 'release';
    eventHandlers.get('DAORIS.STATE_CHANGED')!({ scopes: ['sessions', 'landing', 'diff'], sessions: ['s1a2b3c4'] });
    expect(await screen.findByText('lands in release')).toBeInTheDocument();

    // The tab switched away, the line moved again by a press elsewhere, and the tab switched back.
    view.rerender(<QueryClientProvider client={client}><Window showing={false} /></QueryClientProvider>);
    target = 'next';
    eventHandlers.get('DAORIS.STATE_CHANGED')!({ scopes: ['landing'] });
    view.rerender(<QueryClientProvider client={client}><Window showing /></QueryClientProvider>);
    expect(await screen.findByText('lands in next')).toBeInTheDocument();

    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_SESSION_TREE', expect.anything());
  });

  /** A notification is a door (design §4): clicking it names the session to attend. */
  it('the shell can ask the page to attend a session', () => {
    const onAttend = vi.fn();
    show(<ShellSignals notify={() => {}} onAttend={onAttend} />);

    eventHandlers.get('DAORIS.ATTEND_SESSION')!({ session: 's1a2b3c4' });
    expect(onAttend).toHaveBeenCalledWith('s1a2b3c4');

    // Nothing named is nothing to open, not a door onto whatever was last selected.
    eventHandlers.get('DAORIS.ATTEND_SESSION')!({});
    expect(onAttend).toHaveBeenCalledTimes(1);
  });
});
