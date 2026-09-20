import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The views in SHELL mode — the bridge mocked as present, so the controls that only a desktop with a
// driver may render actually render, and land on the DAORIS.DRIVER module. Browser mode needs no
// twin suite: every other test in this project runs with no transport, and the absence of these
// controls there is asserted by their queries never firing (an unstubbed fetch throws).

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

import { ProjectsView } from './ProjectsView';
import { QuestsView } from './QuestsView';
import { SettingsView } from './SettingsView';
import { ShellSignals } from './ShellSignals';
import { keys } from './queries';

const DRIVER_STATE = { drivable: [], holds: [], running: ['s1a2b3c4'] };

/** What DAORIS.REMOTES answers: the wiring, with the key already reduced to its audit prefix. */
const WIRING = {
  // Neutral by convention, like the driver's fixtures: a tracked file carries no machine path, not
  // even a plausible-looking one a scanner would have to be told to forgive.
  path: 'C:/somewhere/.daoris/remotes.json',
  fromEnvironment: false,
  remotes: [{ workspace: 'aurora', url: 'https://aurora.example.com', key: 'dk_abcd1234…' }],
};

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1 },
];
const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];
const SESSIONS = [{
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
}];

/** What a SHARED deployment answers: counts, plus the commit its copy came from (D48 §6). */
const REPOSITORIES = [{
  name: 'engine', total: 1, local: 1, canonical: 0, workspace: 'default',
  fed: {
    commit: 'c0ffee1234567890', shortCommit: 'c0ffee12',
    committedAt: new Date(Date.now() - 3 * 3600_000).toISOString(),
    branch: 'main', origin: 'person@machine-a',
  },
}];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  throw new Error(`unstubbed request: ${url}`);
}

function show(node: React.ReactElement, client = new QueryClient({ defaultOptions: { queries: { retry: false } } })) {
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>{node}</Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the shell-attached platform', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    notifyReady.mockClear();
    eventHandlers.clear();
  });

  it('projects grow the per-machine driver controls, landing on DAORIS.DRIVER', async () => {
    show(<ProjectsView notify={() => {}} />);

    const drive = await screen.findByLabelText('drive on this machine');
    await userEvent.click(drive);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'engine', drivable: true },
    });
  });

  it('hold appears only once a repository is drivable — a hold on nothing is noise', async () => {
    show(<ProjectsView notify={() => {}} />);

    await screen.findByLabelText('drive on this machine');
    expect(screen.queryByLabelText('hold')).not.toBeInTheDocument();
  });

  it('holding a drivable repository lands on DAORIS.DRIVER with its own payload key', async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, drivable: ['engine'] }));
    show(<ProjectsView notify={() => {}} />);

    await userEvent.click(await screen.findByLabelText('hold'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HOLD', {
      payload: { repository: 'engine', held: true },
    });
  });

  it('a running session offers stop, and stop names the session', async () => {
    show(<QuestsView notify={() => {}} />);

    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'stop session' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STOP_SESSION', {
      payload: { id: 's1a2b3c4' },
    });
  });
});

/**
 * Managing the machine's repositories (D48 §7). Three properties are worth a test rather than a
 * comment, because each fails silently: the machine path comes from the SHELL and never from the page,
 * retiring says what it does not do, and the two kinds of update stay visibly apart.
 */
describe('the shell-attached registry management', () => {
  const PICKED = {
    path: 'D:/repos/borealis', name: 'borealis', exists: true, adopted: true, git: true,
    summary: 'The aurora.', owns: ['the sky'], accepts: ['a quest'], packs: [], join: false,
    shareKnowledge: false,
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'DELETE') {
        return Response.json({
          repository: 'engine', retired: true,
          message: 'Nothing was deleted: its files, its history and its doctrine are its own.',
        });
      }
      if (url === '/api/registry' && init?.method === 'POST') {
        return Response.json({ repository: 'borealis', workspace: 'aurora' });
      }
      if (url.includes('/workspace')) return Response.json({ repository: 'engine', workspace: 'tools' });
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'PICK_FOLDER' ? PICKED : DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('adding a repository takes its path from the shell, never from the page', async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);

    await userEvent.click(await screen.findByRole('button', { name: 'add repository' }));
    await userEvent.click(await screen.findByRole('button', { name: 'choose a folder…' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REGISTRY', 'PICK_FOLDER', {});
    expect(await screen.findByText('D:/repos/borealis')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'register it' }));

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toMatchObject({
      repository: 'borealis', root: 'D:/repos/borealis',
    });
  });

  /**
   * The one thing a person must be able to trust about a remove button. The service composes the
   * sentence; the panel says it before the click, and the answer repeats it after.
   */
  it('retiring says what it does not do, before and after', async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);

    await userEvent.click(await screen.findByRole('button', { name: 'manage' }));
    const drawer = await screen.findByRole('dialog');
    expect(within(drawer).getByText(/Nothing is deleted/)).toBeInTheDocument();

    // Two clicks, deliberately: the first is not the destructive one.
    await userEvent.click(within(drawer).getByRole('button', { name: 'retire' }));
    await userEvent.click(within(drawer).getByRole('button', { name: 'yes, retire it' }));

    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registry/engine', { method: 'DELETE' });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing was deleted'));
  });

  /**
   * The console (D49 §2): the transcript capture, streaming. It reaches the page over the shell's
   * bridge and has no HTTP route at all, because output is transcript-class material and never leaves
   * the machine that produced it (D47 §4).
   */
  it('a session drawer asks the driver for the console and renders it verbatim', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? {
          session: 's1a2b3c4',
          lines: [{ sequence: 1, text: '$ npm test' }, { sequence: 2, text: 'ok 1337 passing' }],
          sequence: 2, live: true, dropped: 0,
        }
      : DRIVER_STATE));

    show(<QuestsView notify={() => {}} />);
    await userEvent.click(await screen.findByText('Expose a streaming budget'));

    expect(await screen.findByText(/ok 1337 passing/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TAIL_SESSION', {
      payload: { id: 's1a2b3c4' },
    });
  });

  /** Live lines arrive as events and join the backlog as one stream, keyed by the driver's sequence. */
  it('live output joins the backlog without repeating what was already shown', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'first' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show(<QuestsView notify={() => {}} />);
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    await screen.findByText(/first/);

    // A batch that repeats a line the page already holds, and adds one it does not.
    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 's1a2b3c4',
        lines: [{ sequence: 1, text: 'first' }, { sequence: 2, text: 'second' }],
      });
    });

    const well = await screen.findByText(/second/);
    expect(well.textContent).toBe('first\nsecond');
  });

  /**
   * The console is the part of the drawer that may be missing — a shell older than this surface
   * answers something else entirely. It degrades to absent; the RECORD is what the drawer is for.
   */
  it('an answer that is not a console leaves the record standing', async () => {
    invoke.mockImplementation(async () => DRIVER_STATE); // no `lines` anywhere in it

    show(<QuestsView notify={() => {}} />);
    await userEvent.click(await screen.findByText('Expose a streaming budget'));

    expect(await screen.findByText(/s1a2b3c4 · claude-code/)).toBeTruthy();
    expect(screen.queryByText('console')).toBeNull();
  });

  /** Another session's output is not this drawer's — the event carries whose it is. */
  it('a batch for a different session is ignored', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'TAIL_SESSION'
      ? { session: 's1a2b3c4', lines: [{ sequence: 1, text: 'mine' }], sequence: 1, live: true, dropped: 0 }
      : DRIVER_STATE));

    show(<QuestsView notify={() => {}} />);
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    await screen.findByText(/mine/);

    await act(async () => {
      eventHandlers.get('DAORIS.SESSION_OUTPUT')!({
        session: 'somebody-else', lines: [{ sequence: 1, text: 'theirs' }],
      });
    });

    expect(screen.queryByText(/theirs/)).toBeNull();
  });

  /**
   * Provenance is served, not implied (D48 §6). A person looking at a repository's knowledge must be
   * able to see which commit it came from — freshness they have to assume is exactly what the rule
   * exists to replace.
   */
  it('projects name the commit a deployment was fed from', async () => {
    show(<ProjectsView notify={() => {}} />);

    expect(await screen.findByText(/c0ffee12/)).toBeTruthy();
    // Relative time beside it: "how stale is this" is the question being answered.
    expect(screen.getByText(/c0ffee12 · /)).toBeTruthy();
  });

  /** Re-wiring is a row on this machine; it must not touch the repository's tracked file. */
  it('re-wiring edits one row and writes no file', async () => {
    show(<ProjectsView notify={() => {}} />);

    await userEvent.click(await screen.findByRole('button', { name: 'manage' }));
    const drawer = await screen.findByRole('dialog');
    await userEvent.clear(within(drawer).getByLabelText('workspace'));
    await userEvent.type(within(drawer).getByLabelText('workspace'), 'tools');
    await userEvent.click(within(drawer).getByRole('button', { name: 're-wire' }));

    expect(vi.mocked(fetch)).toHaveBeenCalledWith(
      '/api/registry/engine/workspace',
      expect.objectContaining({ method: 'POST' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.REGISTRY', 'WRITE_DECLARATION', expect.anything());
  });
});

/**
 * Conversations (D49 §3): a session a person entered rather than the driver planned. Shell-only,
 * because a chat is a process on this machine and processes never leave the driver (D46 §7) — and
 * Daoris pipes text without ever being the conversation: no model is named anywhere in this surface.
 */
describe('chat sessions', () => {
  const CHAT = {
    id: 'c0ffee11', quest: null, repository: 'engine', adapter: 'stub', state: 'working',
    kind: 'chat', created: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:05:00Z',
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('a repository with no live session offers to start one, and opens what comes back', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'START_CHAT') return { sessionId: 'c0ffee11', message: 'Chat `c0ffee11` opened in `engine`, via stub.' };
      if (type === 'TAIL_SESSION') return { session: 'c0ffee11', lines: [], sequence: 0, live: true, dropped: 0 };
      return DRIVER_STATE;
    });

    show(<ProjectsView notify={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: 'chat' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine' },
    });
  });

  /**
   * A refusal is the ledger's own sentence, verbatim — and the drawer does not open on one: a
   * conversation that was refused has no session to show.
   *
   * The fixture holds a live DRIVEN session in `engine`, which is the case worth pinning: a driven
   * session holds the tree but has no channel to speak into, so the button offers to start a chat
   * (and the ledger refuses, naming what holds it) rather than opening an input box nothing is
   * listening to.
   */
  it('a refused chat surfaces the service sentence and opens nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'START_CHAT'
      ? { sessionId: null, message: '`engine` already has an active session — `s1a2b3c4` (working, quest `#abc123`).' }
      : DRIVER_STATE));
    const notify = vi.fn();

    show(<ProjectsView notify={notify} />);
    await userEvent.click(await screen.findByRole('button', { name: 'chat' }));

    expect(notify).toHaveBeenCalledWith(expect.stringContaining('already has an active session'), 'error');
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('typing sends one message over the bridge and clears the box', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      // A live chat in the repository: the button opens it rather than starting a second.
      if (url.startsWith('/api/sessions')) return Response.json([CHAT]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') return { sent: true };
      if (type === 'TAIL_SESSION') return { session: 'c0ffee11', lines: [], sequence: 0, live: true, dropped: 0 };
      return DRIVER_STATE;
    });

    show(<ProjectsView notify={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: 'open session' }));

    const box = await screen.findByLabelText('message');
    await userEvent.type(box, 'what is this repository for?');
    await userEvent.click(screen.getByRole('button', { name: 'send' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: { id: 'c0ffee11', text: 'what is this repository for?' },
    });
    expect((box as HTMLTextAreaElement).value).toBe('');
  });

  /**
   * Finishing and stopping are different verbs and mean different things: end-of-input lets the
   * harness wind up, a stop cuts it off and the record says the person did.
   */
  it('finishing and stopping are offered separately', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/sessions')) return Response.json([CHAT]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'END_CHAT') return { ended: true };
      if (type === 'TAIL_SESSION') return { session: 'c0ffee11', lines: [], sequence: 0, live: true, dropped: 0 };
      return DRIVER_STATE;
    });

    show(<ProjectsView notify={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: 'open session' }));
    await userEvent.click(await screen.findByRole('button', { name: 'finish' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'END_CHAT', { payload: { id: 'c0ffee11' } });
    expect(screen.getByRole('button', { name: 'stop it' })).toBeTruthy();
  });
});

/**
 * The machine's wiring (D48 §5, D50): which deployment serves each workspace here. Shell-only and
 * more strictly than the rest — the service has no route onto this at all, so every call must land on
 * the bridge and none on the API.
 */
describe('the machine settings surface', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => WIRING);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('reads the wiring over the bridge and never over the service', async () => {
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('aurora')).toBeTruthy();
    expect(screen.getByText('https://aurora.example.com')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'STATE', {});
    // Not a single call toward the service: a keyed remote's browser must never learn, or change,
    // where a machine syncs.
    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  it('wiring a workspace edits the map and clears the key out of the form', async () => {
    show(<SettingsView notify={() => {}} />);
    await screen.findByText('aurora');

    await userEvent.type(screen.getByLabelText('workspace'), 'tools');
    await userEvent.type(screen.getByLabelText('deployment'), 'https://tools.example.com');
    await userEvent.type(screen.getByLabelText('key'), 'dk_toolskey0000');
    await userEvent.click(screen.getByRole('button', { name: 'wire it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'SET', {
      payload: { workspace: 'tools', url: 'https://tools.example.com', key: 'dk_toolskey0000' },
    });
    // The key does not linger in the form once it has landed in the file.
    expect((screen.getByLabelText('key') as HTMLInputElement).value).toBe('');
  });

  /** A key goes in and never comes out: what is rendered is the prefix the module chose to answer. */
  it('renders only the audit prefix a key was reduced to', async () => {
    const { container } = show(<SettingsView notify={() => {}} />);
    await screen.findByText('aurora');

    expect(screen.getByText('dk_abcd1234…')).toBeTruthy();
    expect(container.textContent).not.toContain('dk_abcd1234wxyz');
  });

  it('unwiring says what it did not do, and touches no deployment', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);
    await screen.findByText('aurora');

    await userEvent.click(screen.getByRole('button', { name: 'unwire' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'REMOVE', { payload: { workspace: 'aurora' } });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing at the deployment changed'));
    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  /**
   * With the environment pair set no loader reads the file, so the surface must say which source
   * decided — otherwise it reports its own last edit as though it were the machine's wiring.
   */
  it('says when the environment, not the file, is the answer', async () => {
    invoke.mockImplementation(async () => ({ ...WIRING, fromEnvironment: true }));
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText(/environment names this machine's remote/i)).toBeTruthy();
  });
});

/**
 * The toolchain roster (D49 §4, D50): which harnesses this machine has, and which accounts they hold.
 *
 * Shell-only for the sharpest reason yet — a profile HOME is a filesystem path (D47 §4) — so, like
 * the wiring above, every call must land on the bridge and none on the API.
 *
 * The property that matters most here is a negative one: **no credential is anywhere on this
 * surface.** There is nowhere to type one, nothing that reads one, and logging in spawns the
 * harness's own flow. The last test in this block is what fails if that ever stops being true.
 */
describe('the harness roster', () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', present: true, version: 'claude 9.9.9', problem: null,
        machineDefault: 'personal',
        profiles: [
          { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
          { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
        ],
      },
      {
        harness: 'codex', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null, profiles: [],
      },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('reads the roster over the bridge and never over the service', async () => {
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('claude 9.9.9')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {});
    expect(vi.mocked(fetch)).not.toHaveBeenCalled();
  });

  /** An absent harness names what it is and offers the action, rather than leaving a blank row. */
  it('an absent harness says so and offers its own installer', async () => {
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('not installed')).toBeTruthy();
    expect(screen.getByText(/is not on this machine's PATH/)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'install' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex', action: 'install' },
    });
  });

  it('each profile shows its login state, and logging in names the profile', async () => {
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('logged in')).toBeTruthy();
    expect(screen.getByText('not logged in')).toBeTruthy();

    // Two profiles, two buttons — the second one is `work`, which is the logged-out one.
    const logins = screen.getAllByRole('button', { name: 'log in' });
    await userEvent.click(logins[1]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'work' },
    });
  });

  /**
   * Daoris manages directories and names, never secrets. There is no field for a token, no call
   * carrying one, and the surface says where the credential actually lives.
   */
  it('offers nowhere to put a credential, and says where one lives instead', async () => {
    const { container } = show(<SettingsView notify={() => {}} />);
    await screen.findByText('claude 9.9.9');

    const card = screen.getByText('Harnesses').closest('section, div')!;
    expect(within(card as HTMLElement).queryByLabelText(/token|password|credential/i)).toBeNull();
    expect(container.textContent).toContain('the harness stores itself');
  });

  /** A shell older than this surface answers something else; the rest of the page must stand. */
  it('an answer that is not a roster leaves the wiring card standing', async () => {
    invoke.mockImplementation(async () => WIRING); // no `harnesses` anywhere in it

    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('aurora')).toBeTruthy();
    expect(screen.queryByText('Harnesses')).toBeNull();
  });

  /**
   * The per-session picker (D49 §4). Empty is not "no profile": it means whatever this machine
   * already decided — the workspace's default, then the machine's — which is what the label says and
   * what the payload omitting the field means.
   */
  it('projects offer a profile for the next conversation, and omit it when unchosen', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'START_CHAT') return { sessionId: null, message: 'busy' };
      return DRIVER_STATE;
    });

    show(<ProjectsView notify={() => {}} />);
    await screen.findByLabelText(/the next conversation runs as/);

    await userEvent.click(screen.getByRole('button', { name: 'chat' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine' },
    });

    await userEvent.selectOptions(screen.getByLabelText(/the next conversation runs as/), 'work');
    await userEvent.click(screen.getByRole('button', { name: 'chat' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'START_CHAT', {
      payload: { repository: 'engine', profile: 'work' },
    });
  });
});

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
    for (const key of [keys.allSessions, keys.allQuests, keys.driver]) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: key });
    }
  });

  it("a driver error arrives as an error toast, the driver's own sentence verbatim", () => {
    const notify = vi.fn();
    show(<ShellSignals notify={notify} />);

    eventHandlers.get('DAORIS.DRIVER_ERROR')!({ message: 'the loop hit a wall' });

    expect(notify).toHaveBeenCalledWith('the loop hit a wall', 'error');
  });
});
