import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';

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

import { OverviewView } from './OverviewView';
import { ShellSignals } from './ShellSignals';
import { keys } from './queries';
import { respond, show } from './test/shellHarness';

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
    for (const key of [keys.allSessions, keys.allQuests, keys.allAsks, keys.driver, keys.allRepositories, keys.allSync, keys.rules, keys.allRegistry]) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: key });
    }
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
    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSessions });
  });

  it('an ending is news rather than a demand, and a session that said nothing still says something', () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.SESSION_ATTENTION')!({
      kind: 'Ended', session: 's1', repository: 'tools', headline: 'tools — a session failed',
    });

    expect(notify).toHaveBeenCalledWith('tools — a session failed', 'ok');
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

    expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSessions });
    expect(notify).not.toHaveBeenCalled();
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
