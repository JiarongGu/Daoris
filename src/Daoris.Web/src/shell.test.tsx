import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, renderHook, screen, waitFor, within } from '@testing-library/react';
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

import { OverviewView } from './OverviewView';
import { ProjectsView } from './ProjectsView';
import { QuestsView } from './QuestsView';
import { SettingsView } from './SettingsView';
import { ShellSignals } from './ShellSignals';
import { keys } from './queries';
import { useSyncNow } from './shell';

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

/** What the service says answers search (D24) — the same answer every browser is given. */
const STATUS = {
  semantic: false, tier: 'lexical only',
  note: 'Set DAORIS_EMBED_MODEL to enable semantic recall — it is what finds two repositories that reached the same conclusion in different words.',
};

function respond(url: string): Response {
  if (url.startsWith('/api/status')) return Response.json(STATUS);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  throw new Error(`unstubbed request: ${url}`);
}

/**
 * What went over the SERVICE while a shell-only surface rendered. The registry and the status are
 * the two things these surfaces may read from it — which circles this machine has, for choosing an
 * account per workspace, and which tier answers search, for Daoris's own AI (AGT6) — and both are
 * public, non-sensitive answers every browser is given. Everything else must be absent.
 */
const serviceCalls = () =>
  vi.mocked(fetch).mock.calls.map((call) => String(call[0]))
    .filter((url) => !url.startsWith('/api/registry') && !url.startsWith('/api/status'));

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
      repository: 'borealis', root: 'D:/repos/borealis', adopted: true,
    });
  });

  /**
   * Registered is addressable; adopted is disciplined (D70) — so a folder with no manifest is added as
   * what it is. The register door used to store every row adopted, which would have driven one over
   * the pipe door with no connector and no sentence saying why.
   */
  it('adding a folder that has not adopted registers it as not adopted, and declares nothing', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'PICK_FOLDER'
        ? { ...PICKED, adopted: false, summary: undefined, owns: [], accepts: [] }
        : DRIVER_STATE);
    show(<ProjectsView notify={() => {}} />);

    await userEvent.click(await screen.findByRole('button', { name: 'add repository' }));
    await userEvent.click(await screen.findByRole('button', { name: 'choose a folder…' }));
    await userEvent.click(await screen.findByRole('button', { name: 'register it' }));

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    const body = JSON.parse(String(posted![1]!.body));
    expect(body).toMatchObject({ repository: 'borealis', root: 'D:/repos/borealis', adopted: false });
    expect(body.domain).toBeUndefined();
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

  // The CONSOLE's tests moved to `work/WorkFrame.test.tsx` with the console itself (D55): one home
  // for the stream, and the quest drawer keeps the record summary plus a door into it.

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

  /**
   * One sentence for one fact. On the deployed family, a repository present in the index with a
   * count of zero read "0 entries · 0 local · 0 canonical", one absent from it read "nothing
   * indexed yet", and one outside the family read "—" — three renderings of "the index holds
   * nothing of this", on one page.
   */
  it('says "nothing indexed yet" the same way whether a count is zero, missing, or outside', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          ...REGISTRY,
          { ...REGISTRY[0], repository: 'blank', summary: 'indexed, holding nothing' },
          { ...REGISTRY[0], repository: 'unseen', summary: 'never indexed' },
          { ...REGISTRY[0], repository: 'stranger', adopted: false },
          { ...REGISTRY[0], repository: 'lone', adopted: false },
          { ...REGISTRY[0], repository: 'many', adopted: false },
        ]);
      }
      if (url.startsWith('/api/repositories')) {
        return Response.json([
          ...REPOSITORIES,
          { name: 'blank', total: 0, local: 0, canonical: 0, workspace: 'default' },
          { name: 'lone', total: 1, local: 1, canonical: 0, workspace: 'default' },
          { name: 'many', total: 1234, local: 1234, canonical: 0, workspace: 'default' },
        ]);
      }
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    // A count of one is singular — "1 entries" was on the deployed page too — and a count in the
    // thousands keeps its separator, so the plural form and the formatting are two parameters.
    expect(await screen.findByText('1 entry · 1 local · 0 canonical')).toBeTruthy();
    expect(screen.getByText('1 entry indexed read-only')).toBeTruthy();
    expect(screen.getByText('1,234 entries indexed read-only')).toBeTruthy();
    expect(screen.getAllByText('nothing indexed yet')).toHaveLength(3);
    expect(screen.queryByText(/^0 entries/)).not.toBeInTheDocument();
    expect(screen.queryByText('—')).not.toBeInTheDocument();
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

// CONVERSATIONS moved to `work/WorkFrame.test.tsx` with the surface that starts and holds them
// (design §3): starting a session belongs where its result appears, not in the registry's view.

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
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * Where this machine's Daoris lives (D63), and what the start did about it. The notice rides the
   * STATE rather than only the one-time event, because the page subscribes after the host answers
   * and a toast raised before that reached nobody — measured on the first migrated start.
   */
  it('shows where this machine\'s Daoris lives, and what establishing it did', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER'
      ? {
        ...DRIVER_STATE,
        home: 'D:/somewhere/Daoris/data',
        homeNotice: 'Daoris home: D:/somewhere/Daoris/data — moved in from D:/somewhere/.daoris: driver.json.',
        hostNotice: 'the host at http://localhost:5177 was already running and serves a different page than this install carries.',
      }
      : WIRING));
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('D:/somewhere/Daoris/data')).toBeTruthy();
    expect(screen.getByText(/moved in from/)).toBeTruthy();
    // The adopted host's page is a standing fact and gets a standing line, not only a toast.
    expect(screen.getByText(/serves a different page/)).toBeTruthy();
    // Every path on the page is under it — the wiring file included. Awaited: the machine's half
    // mounts once the driver has answered (D66: Settings asks whether a shell is here first), so its
    // wiring query starts a beat after the home is already on the page.
    expect(await screen.findByText('C:/somewhere/.daoris/remotes.json')).toBeTruthy();
  });

  it('says nothing about the home on a shell that has never heard of one', async () => {
    invoke.mockImplementation(async (module: string) => (module === 'DAORIS.DRIVER' ? DRIVER_STATE : WIRING));
    show(<SettingsView notify={() => {}} />);
    await screen.findByText('aurora');

    // The header still names the home in a sentence; what is absent is the path a newer shell answers.
    expect(screen.queryByText(/somewhere\/Daoris\/data/)).toBeNull();
    expect(screen.queryByText(/moved in from/)).toBeNull();
  });

  /**
   * A setting is a row (2026-09-23): the control sits beside its label, the terminal's door is the
   * one-line hint, and the paragraph that motivated the setting is on the glyph rather than on the
   * page — so the first control is where the eye lands rather than a screen below the title.
   */
  it('lays each setting out as a row, with its why one hover away rather than on the page', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true, strikes: 3 } : WIRING));
    show(<SettingsView notify={() => {}} />);
    await screen.findByRole('checkbox', { checked: true });

    // The number dial is reachable by its label, and the why is a note, not a paragraph.
    expect(screen.getByLabelText('Park a quest after this many failed sessions')).toBeTruthy();
    expect(screen.getByRole('note', { name: /Nobody should have to watch a driver/ })).toBeTruthy();
    expect(screen.queryByText(/Nobody should have to watch a driver/)).toBeNull();
    // The wiring form is one press away, not open on every visit.
    expect(screen.queryByLabelText('deployment')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Wire a workspace' }));
    expect(screen.getByLabelText('deployment')).toBeTruthy();
  });

  it('wiring a workspace edits the map and clears the key out of the form', async () => {
    show(<SettingsView notify={() => {}} />);
    await screen.findByText('aurora');

    await userEvent.click(screen.getByRole('button', { name: 'Wire a workspace' }));
    await userEvent.type(screen.getByLabelText('workspace'), 'tools');
    await userEvent.type(screen.getByLabelText('deployment'), 'https://tools.example.com');
    await userEvent.type(screen.getByLabelText('key'), 'dk_toolskey0000');
    await userEvent.click(screen.getByRole('button', { name: 'wire it' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'SET', {
      payload: { workspace: 'tools', url: 'https://tools.example.com', key: 'dk_toolskey0000' },
    });
    // The form closes on success, and the key does not linger in it once it has landed in the file:
    // opened again, it is empty.
    expect(screen.queryByLabelText('key')).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Wire a workspace' }));
    expect((screen.getByLabelText('key') as HTMLInputElement).value).toBe('');
  });

  /** A key goes in and never comes out: what is rendered is the prefix the module chose to answer. */
  it('renders only the audit prefix a key was reduced to', async () => {
    const { container } = show(<SettingsView notify={() => {}} />);
    await screen.findByText('aurora');

    expect(screen.getByText('dk_abcd1234…')).toBeTruthy();
    expect(container.textContent).not.toContain('dk_abcd1234wxyz');
  });

  /**
   * Notifications (SURF5b): the desktop's door onto the same `driver.json` field
   * `daoris driver notify on|off` edits. It is ON until somebody says otherwise — a driver nobody
   * has to watch is the point of one — and the surface names the other door so a person on a
   * machine they reach over ssh does not go looking for a second setting.
   */
  it('the notification switch reads the machine and writes the same file a terminal does', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: true } : WIRING));

    show(<SettingsView notify={() => {}} />);

    // Found by its loaded STATE rather than by its label, because the switch renders before the
    // machine has answered and its default is on — a bare label query would pass either way.
    const check = await screen.findByRole('checkbox', { checked: true });
    expect(screen.getByText(/daoris driver notify on\|off/)).toBeTruthy();

    await userEvent.click(check);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_NOTIFY', { payload: { notify: false } });
  });

  it('a machine that has turned notifications off says so', async () => {
    invoke.mockImplementation(async (module: string) => (
      module === 'DAORIS.DRIVER' ? { ...DRIVER_STATE, notify: false } : WIRING));

    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByRole('checkbox', { checked: false })).toBeTruthy();
  });

  it('unwiring says what it did not do, and touches no deployment', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);
    await screen.findByText('aurora');

    await userEvent.click(screen.getByRole('button', { name: 'unwire' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'REMOVE', { payload: { workspace: 'aurora' } });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing at the deployment changed'));
    expect(serviceCalls()).toEqual([]);
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
 * This machine's plugins (D64): one row per folder under the home's `plugins/`, the switch a row in
 * the same file `daoris plugin enable|disable` edits, and Remove naming what the plugin kept. Every
 * call lands on the bridge — a plugin's folder is a machine path — and none on the service.
 */
describe('the plugins card', () => {
  const PLUGINS = {
    folder: 'C:/somewhere/data/plugins',
    plugins: [
      {
        id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
        enabled: true, problem: null, harnesses: ['acme-agent'], points: ['quest/consider'],
        running: true, folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
      },
      {
        id: 'future', name: 'future', version: '', description: '',
        enabled: true, problem: 'needs plugin API 99, and this build speaks 1 — update Daoris, or use a plugin written for 1.',
        harnesses: [], points: [], running: false,
        folder: 'C:/somewhere/data/plugins/future', data: 'C:/somewhere/data/plugins/.data/future',
      },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'PLUGINS' ? PLUGINS : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('lists each plugin with what it declares, what it speaks on, and the driver\'s sentence for a refused one', async () => {
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('Acme gate')).toBeTruthy();
    expect(screen.getByText(/declares acme-agent; speaks on quest\/consider/)).toBeTruthy();
    expect(screen.getByText('running')).toBeTruthy();
    // The refused plugin is listed WITH the driver's own sentence, verbatim.
    expect(screen.getByText(/needs plugin API 99/)).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGINS', {});
    expect(serviceCalls()).toEqual([]);
  });

  it('the switch and Remove land on the bridge as the actions a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);
    const row = (await screen.findByText('Acme gate')).closest('div')!.parentElement!.parentElement!;

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Turn off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_ACTION', { payload: { id: 'acme.gate', action: 'disable' } });

    await userEvent.click(within(row as HTMLElement).getByRole('button', { name: 'Remove' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'PLUGIN_ACTION', { payload: { id: 'acme.gate', action: 'remove' } });
  });

  it('a machine with no plugins says where one would go', async () => {
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'PLUGINS' ? { folder: 'C:/somewhere/data/plugins', plugins: [] } : WIRING));
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText(/daoris plugin add/)).toBeTruthy();
    expect(screen.getByText('C:/somewhere/data/plugins')).toBeTruthy();
  });
});

/**
 * Daoris's own AI (AGT6): the jobs it may use a model for, the tier answering each, and how to change
 * it. The search tier is the service's answer, over HTTP like every browser's; the intake is this
 * machine's `driver.json`, and its control is the screen's half of `daoris driver intake` (D50).
 */
describe("Daoris's own AI on Settings", () => {
  const ROSTER = {
    settingsPath: 'C:/somewhere/.daoris/harnesses.json',
    adapter: 'claude-code',
    harnesses: [
      {
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe',
        present: true, version: 'claude 9.9.9', problem: null, machineDefault: 'personal',
        pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [],
        profiles: [{ name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' }],
      },
      {
        harness: 'claude-code-acp', product: 'Claude Code', maker: 'Anthropic', wire: 'acp', accountOf: 'claude-code',
        present: true, version: '0.9.1', problem: null, machineDefault: 'personal',
        pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
      },
      {
        harness: 'codex', product: 'Codex', maker: 'OpenAI', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null,
        pinned: null, managed: null, pinnable: false, profiles: [],
      },
    ],
  };
  const start = (job: 'work' | 'intake', adapter: string) => ({
    job, workspace: 'default', adapter, owner: 'claude-code', product: 'Claude Code',
    profile: 'personal', profileFrom: 'machine', version: '0.9.1', versionFrom: 'unset',
    commanded: false, refusal: null,
  });

  let intakeAdapter: string | null = null;
  beforeEach(() => {
    intakeAdapter = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, request?: { payload?: { adapter?: string | null } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_INTAKE') intakeAdapter = request?.payload?.adapter ?? null;
      // As the wire carries it: off is "", never null — the bridge leaves a null out, and a missing
      // field is how the page knows a shell older than the intake (AGT6, seen on the window).
      if (type === 'STATE' || type === 'SET_INTAKE') return { ...DRIVER_STATE, intakeAdapter: intakeAdapter ?? '' };
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') {
        return {
          adapter: 'claude-code',
          starts: [start('work', 'claude-code'), ...(intakeAdapter ? [start('intake', intakeAdapter)] : [])],
        };
      }
      return undefined;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it("states the service's own tier, verbatim, and how the model is chosen", async () => {
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    expect(screen.getByText(STATUS.note)).toBeTruthy();
    expect(screen.getByText(/DAORIS_EMBED_MODEL \(a model's name\)/)).toBeTruthy();
  });

  /**
   * 🔴 Seen on the window (AGT6): with the intake off, the card offered no intake control at all —
   * the bridge left the null out, and the page read the missing field as a shell older than the
   * intake. Off is "" on the wire now, and reads as Off.
   */
  it('offers the intake control while it is off, reading as Off', async () => {
    show(<SettingsView notify={() => {}} />);

    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    expect(trigger).toHaveTextContent('Off — declarations only');
  });

  /** A shell that answers no field has never heard of the intake: no control, not a disabled one. */
  it('offers no intake control on a shell older than the intake', async () => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return DRIVER_STATE;
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'STARTS') return { adapter: 'claude-code', starts: [start('work', 'claude-code')] };
      return undefined;
    });
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('lexical only')).toBeTruthy();
    await screen.findByRole('heading', { name: 'This machine' });
    expect(screen.queryByRole('combobox', { name: 'the intake agent' })).toBeNull();
  });

  /**
   * D50: the screen's half of `daoris driver intake <agent>|off`. It offers the ways in this machine
   * HAS — an agent that is not installed is not a choice — and lands on the same file.
   */
  it('names an agent this machine has for the intake, over SET_INTAKE, and says what that does', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);

    const user = userEvent.setup();
    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    trigger.focus();
    await user.keyboard('{Enter}');
    expect(screen.queryByRole('option', { name: /codex/i })).toBeNull();
    await user.click(await screen.findByRole('option', { name: /claude-code-acp/ }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_INTAKE', { payload: { adapter: 'claude-code-acp' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('intake session on claude-code-acp')));
  });

  /**
   * As whom: the driver's own answer for the intake, per circle — the same rows *What a start runs
   * on* draws, so the two cards cannot disagree about one account.
   */
  it('says which account an intake runs as, from the driver\'s answer, in both places it is drawn', async () => {
    intakeAdapter = 'claude-code-acp';
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText(/opens a session on claude-code-acp/)).toBeTruthy();
    // Awaited as a pair: the machine's half mounts after the driver answers, a beat behind this card.
    await waitFor(() => expect(screen.getAllByRole('listitem', { name: 'an intake in default' })).toHaveLength(2));
    for (const row of screen.getAllByRole('listitem', { name: 'an intake in default' })) {
      expect(within(row).getByText('personal')).toBeTruthy();
    }
  });

  it('turns the intake off as the terminal does — no agent named', async () => {
    intakeAdapter = 'claude-code-acp';
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);

    const user = userEvent.setup();
    const trigger = await screen.findByRole('combobox', { name: 'the intake agent' });
    await waitFor(() => expect(trigger).toHaveTextContent('claude-code-acp'));
    trigger.focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: 'Off — declarations only' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_INTAKE', { payload: { adapter: null } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringContaining('declarations only')));
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
        harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic',
        present: true, version: 'claude 9.9.9', problem: null,
        machineDefault: 'personal', pinned: null, managed: null, pinnable: true,
        ownLogin: 'in',
        workspaceDefaults: [{ workspace: 'orbit', profile: 'work' }],
        profiles: [
          { name: 'personal', home: 'C:/somewhere/.daoris/harnesses/claude-code/personal', login: 'in' },
          { name: 'work', home: 'C:/somewhere/.daoris/harnesses/claude-code/work', login: 'out' },
        ],
      },
      {
        harness: 'codex', present: false, version: null,
        problem: '`codex` is not on this machine\'s PATH', machineDefault: null,
        pinned: null, managed: null, pinnable: false, profiles: [],
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
    // AGT1: a tool is named as a person knows it, with whose it is; one that says neither keeps its id.
    expect(screen.getByText('Claude Code')).toBeTruthy();
    expect(screen.getByText('Anthropic')).toBeTruthy();
    expect(screen.getAllByText('codex').length).toBeGreaterThan(0);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESSES', {});
    expect(serviceCalls()).toEqual([]);
  });

  /**
   * MAP1b: what a start in each workspace would run on, beside the agents. The circles are the
   * registry's, the answer is the driver's, and the account is named the way the roster names it.
   */
  it('says what a start in each workspace would run on, asking the driver for the registry\'s circles', async () => {
    const STARTS = {
      adapter: 'claude-code',
      starts: [{
        job: 'work', workspace: 'default', adapter: 'claude-code', owner: 'claude-code', product: 'Claude Code',
        profile: 'personal', profileFrom: 'machine', version: 'claude 9.9.9', versionFrom: 'unset',
        commanded: false, refusal: null,
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'HARNESSES' ? ROSTER : type === 'STARTS' ? STARTS : WIRING));
    show(<SettingsView notify={() => {}} />);

    const row = await screen.findByRole('listitem', { name: 'a start in default' });
    expect(within(row).getByText('personal')).toBeTruthy();
    expect(within(row).getByText("this machine's default")).toBeTruthy();
    expect(within(row).getByText('from PATH')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STARTS', { payload: { workspaces: ['default'] } });
  });

  /** An absent harness names what it is and offers the action, rather than leaving a blank row. */
  it('an absent tool says so and offers its own installer', async () => {
    show(<SettingsView notify={() => {}} />);

    // 🔴 Twice over, and on purpose: the TOOL says whether a person has it, and the way in says
    // whether that particular door is installed. They were one line when a door was a tool.
    expect(await screen.findAllByText('not installed')).not.toHaveLength(0);
    expect(screen.getByText(/is not on this machine's PATH/)).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'install' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'codex', action: 'install' },
    });
  });

  /**
   * 🔴 A login outlives its request (2026-09-23): the host answers `started`, the sign-in sits on the
   * row while the person is in the browser, and the end arrives as news — the row then says what
   * the person can do with the account, and the panel goes.
   */
  it('a login that has started stays on its row until its end arrives as news', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);

    const logins = await screen.findAllByRole('button', { name: /^Log in/ });
    await userEvent.click(logins[1]!);

    expect(await screen.findByText('Signing in to work')).toBeTruthy();
    expect(notify).not.toHaveBeenCalled();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login', profile: 'work', exitCode: 0, problem: null,
      });
    });

    expect(notify).toHaveBeenCalledWith('work is signed in — sessions can run as it.');
    await waitFor(() => expect(screen.queryByText('Signing in to work')).toBeNull());
  });

  it('each profile shows its login state, and logging in names the profile', async () => {
    show(<SettingsView notify={() => {}} />);

    // The tool's own home and `personal` are both logged in; `work` is the one that is not.
    expect(await screen.findAllByText('logged in')).toHaveLength(2);
    expect(screen.getByText('not logged in')).toBeTruthy();

    // Two profiles, two buttons — the second one is `work`, which is the logged-out one.
    const logins = screen.getAllByRole('button', { name: /^Log in/ });
    await userEvent.click(logins[1]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login', profile: 'work' },
    });
  });

  /**
   * 🔴 The account a person actually has — the tool's own configuration home — led nowhere: a
   * machine with no named profile read "No accounts" while its owner was logged in. It leads the
   * list now, with the login state the tool reports for its own home, and it is what sessions use
   * until a default is named. Daoris never logs into it: that is the tool's own business, and the
   * row says so instead of offering a button.
   */
  it('the tool’s own home leads the accounts, with its login state, and takes no login from here', async () => {
    show(<SettingsView notify={() => {}} />);

    // Two tools, two own rows; claude-code's is first, in the roster's order.
    const own = (await screen.findAllByText("this machine's own"))[0]!.closest('li')!;
    expect(within(own).getByText('logged in')).toBeTruthy();
    expect(within(own).queryByRole('button', { name: /^Log in/ })).toBeNull();
    // `personal` is the machine's default, so the own home is not what sessions use — yet.
    expect(within(own).queryByText('sessions use this')).toBeNull();
    // Choosing it again names no profile: the default is CLEARED, not pointed somewhere.
    await userEvent.click(within(own).getByRole('button', { name: 'Make default' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default' },
    });
  });

  it('the tool’s own home is what sessions use when nothing is named', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], machineDefault: null, profiles: [] }] }
      : WIRING));
    show(<SettingsView notify={() => {}} />);

    const own = (await screen.findAllByText("this machine's own"))[0]!.closest('li')!;
    expect(within(own).getByText('sessions use this')).toBeTruthy();
    expect(screen.queryByText(/^No accounts/)).toBeNull();
  });

  /**
   * 🔴 An account is made by SIGNING IN (D66 §3; the owner: *"we probably should just allow to login
   * and create account based on login"*). One press, no name typed first; the sign-in shows where
   * the new account will be, and the end names who signed in.
   */
  it('an account is made by signing in, and the end names who signed in', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);

    // No name box anywhere: the account is named by who signs in.
    await screen.findByText('claude 9.9.9');
    expect(screen.queryByPlaceholderText(/a name/)).toBeNull();

    await userEvent.click(screen.getAllByRole('button', { name: 'Sign in to another account' })[0]!);
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'login-new' },
    });
    expect(await screen.findByText('Signing in to another Claude Code account')).toBeTruthy();

    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: 'someone@example.invalid', kept: true,
      });
    });

    expect(notify).toHaveBeenCalledWith('Signed in as someone@example.invalid — sessions can run as it.');
    await waitFor(() => expect(screen.queryByText('Signing in to another Claude Code account')).toBeNull());
  });

  /** A sign-in that did not finish keeps nothing, and says so rather than going quiet. */
  it('a sign-in that kept nothing says so', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'login-new', started: true };
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    const notify = vi.fn();
    show(<SettingsView notify={notify} />);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Sign in to another account' }))[0]!);
    await act(async () => {
      eventHandlers.get('DAORIS.HARNESS_ENDED')!({
        harness: 'claude-code', action: 'login-new', profile: 'account-1', exitCode: 0, problem: null,
        account: null, kept: false,
      });
    });

    expect(notify).toHaveBeenCalledWith('Nobody was signed in, so nothing was kept.', 'error');
  });

  /** A person knows an account by who is signed in there, not by `account-2`. */
  it('lists each account by who is signed in, where the tool says', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          ownAccount: 'owner@example.invalid',
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', account: 'someone@example.invalid' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} />);

    const row = (await screen.findByText('someone@example.invalid')).closest('li')!;
    // The directory's name is still there, inside its path, for a terminal.
    expect(within(row).getByText(/account-1$/)).toBeTruthy();
    expect(screen.getByText('owner@example.invalid')).toBeTruthy();
  });

  /**
   * 🔴 Remove REMOVES (D66 §3) — Forget had kept a signed-in account on disk and on the list. It
   * deletes the sign-in, so the first press only asks, and says what the second will do.
   */
  it('Remove asks once, then deletes the account', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') return { harness: 'claude-code', action: 'profile-remove', exitCode: 0 };
      // The one line the host streams for it — which a console would render, were one shown.
      if (type === 'TAIL_SESSION') {
        return {
          session: 'claude-code:profile-remove', sequence: 1, live: false, dropped: 0,
          lines: [{ sequence: 1, text: 'removed … — the account and its sign-in are gone from this machine.' }],
        };
      }
      return type === 'HARNESSES' ? ROSTER : WIRING;
    });
    show(<SettingsView notify={() => {}} />);

    const work = (await screen.findByText('work')).closest('li')!;
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
    expect(within(work).getByText(/deletes the account from this machine, sign-in included/)).toBeTruthy();

    await userEvent.click(within(work).getByRole('button', { name: 'Remove it' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-remove', profile: 'work' },
    });
    // An account edit is not a door's work: nothing streams under "Ways in" for it (seen on the
    // window, where the removal's one line sat under the direct door as a live console).
    await new Promise((settle) => setTimeout(settle, 50));
    expect(screen.queryByText(/its sign-in are gone/)).toBeNull();
  });

  it('Remove can be taken back before it deletes anything', async () => {
    show(<SettingsView notify={() => {}} />);

    const work = (await screen.findByText('work')).closest('li')!;
    await userEvent.click(within(work).getByRole('button', { name: 'Remove' }));
    await userEvent.click(within(work).getByRole('button', { name: 'never mind' }));

    expect(within(work).queryByRole('button', { name: 'Remove it' })).toBeNull();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', expect.anything());
  });

  /** A tool with no sign-in of its own offers neither sign-in button — the pin's rule. */
  it('a tool that cannot sign in offers no sign-in at all', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], signsIn: false }] }
      : WIRING));
    show(<SettingsView notify={() => {}} />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByRole('button', { name: 'Sign in to another account' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^Log in/ })).toBeNull();
  });

  /** After "Add", the next step and what it does were nowhere: the logged-out row says both. */
  it('a logged-out account says what Log in will do', async () => {
    show(<SettingsView notify={() => {}} />);

    const work = (await screen.findByText('work')).closest('li')!;
    expect(within(work).getByText(/runs the tool's own sign-in/)).toBeTruthy();
    const personal = screen.getByText('personal').closest('li')!;
    expect(within(personal).queryByText(/runs the tool's own sign-in/)).toBeNull();
  });

  /**
   * 🔴 A work account for the work circle (D49 §4) could be set from a terminal and not from here.
   * The row says which circles use it, and a circle is chosen from the ones this machine has.
   */
  it('an account can be made one workspace’s default, and the row says which circles use it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          { ...REGISTRY[0], workspace: 'orbit' },
          { ...REGISTRY[0], repository: 'game', workspace: 'lab' },
        ]);
      }
      return respond(url);
    }));
    show(<SettingsView notify={() => {}} />);

    const work = (await screen.findByText('work')).closest('li')!;
    expect(within(work).getByText('sessions in orbit use this')).toBeTruthy();

    // Opened from the keyboard: in this suite a pointer click leaves the Radix trigger closed, where
    // the App suite's does not, and Enter is a door the person has too.
    const trigger = await screen.findByRole('combobox', { name: 'use personal for a workspace' });
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('option', { name: 'lab' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'profile-default', profile: 'personal', workspace: 'lab' },
    });
  });

  /**
   * A sign-in is the tool's to keep (D49 §4): there is no field for a token or a password, and the
   * surface says where a sign-in lives. An API key is the one exception (D67 §1), and it has its own
   * test below: one field, behind a press, on an agent that takes a key.
   */
  it('offers nowhere to put a sign-in, and says where one lives instead', async () => {
    const { container } = show(<SettingsView notify={() => {}} />);
    await screen.findByText('claude 9.9.9');

    const card = screen.getByText('Agent tools').closest('section, div')!;
    expect(within(card as HTMLElement).queryByLabelText(/token|password|credential|API key/i)).toBeNull();
    expect(container.textContent).toContain('the tool stores itself');
  });

  /**
   * 🔴 An account that is an API key (AGT3, D67 §1: *"daoris can keep the key"*). One password field,
   * behind a press, only on an agent that takes a key; saving sends the key once and the page is
   * told back only its last four characters.
   */
  it('takes an API key once, behind a press, and never shows it back', async () => {
    const key = 'sk-ant-api03-page-test-wxyz';
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESS_ACTION') {
        return { harness: 'claude-code', action: 'key-add', exitCode: 0, profile: 'account-1', key: '…wxyz' };
      }
      return type === 'HARNESSES'
        ? { ...ROSTER, harnesses: [{ ...ROSTER.harnesses[0], takesKey: true }, ROSTER.harnesses[1]] }
        : WIRING;
    });
    const notify = vi.fn();
    const { container } = show(<SettingsView notify={notify} />);

    // Only the agent that takes a key offers it; codex, in this roster, does not.
    await screen.findByText('claude 9.9.9');
    expect(screen.getAllByRole('button', { name: 'Add an API key' })).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: 'Add an API key' }));
    const field = screen.getByLabelText('API key for Claude Code');
    expect(field.getAttribute('type')).toBe('password');

    fireEvent.change(field, { target: { value: key } });
    await userEvent.click(screen.getByRole('button', { name: 'Save the key' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'key-add', key },
    });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Added account-1, the API key …wxyz.'));
    // The field is gone, and the key with it.
    expect(screen.queryByLabelText('API key for Claude Code')).toBeNull();
    expect(container.innerHTML).not.toContain(key);
  });

  /** A key account reads as its handle and offers no sign-in: it is signed in by its key. */
  it('lists a key account by its handle, with no sign-in to offer', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          takesKey: true,
          profiles: [
            { name: 'account-1', home: 'C:/somewhere/.daoris/harnesses/claude-code/account-1', login: 'in', key: '…wxyz' },
          ],
        }],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} />);

    const row = (await screen.findByText('API key …wxyz')).closest('li')!;
    expect(within(row).queryByRole('button', { name: /^Log in/ })).toBeNull();
    // 🔴 Never "logged in": the tool says so for any key, a wrong one included (measured, AGT3).
    expect(within(row).getByText('unchecked')).toBeTruthy();
    expect(within(row).queryByText('logged in')).toBeNull();
    expect(within(row).getByRole('button', { name: 'Remove' })).toBeTruthy();
  });

  /** A door a plugin declared says so beside its name (D64), and the build's own say nothing. */
  it('a declared door names the plugin it came from', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [
          ...ROSTER.harnesses,
          {
            harness: 'acme-agent', present: true, version: '(not asked)', problem: null, wire: 'acp',
            machineDefault: null, pinned: null, managed: null, pinnable: false, profiles: [], plugin: 'acme.gate',
          },
        ],
      }
      : WIRING));
    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('declared by plugin acme.gate')).toBeTruthy();
    expect(screen.getAllByText(/declared by plugin/)).toHaveLength(1);
  });

  /** A shell older than this surface answers something else; the rest of the page must stand. */
  it('an answer that is not a roster leaves the wiring card standing', async () => {
    invoke.mockImplementation(async () => WIRING); // no `harnesses` anywhere in it

    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('aurora')).toBeTruthy();
    expect(screen.queryByText('Agent tools')).toBeNull();
  });

  /**
   * What each account has carried (TOOL3/D57 §4). Absent entirely on a machine that has measured
   * nothing — a row of zeroes would claim sessions used nothing, when what is true is that nothing
   * was measured.
   */
  it('says nothing about usage on a machine that has measured none', async () => {
    show(<SettingsView notify={() => {}} />);

    await screen.findByText('claude 9.9.9');
    expect(screen.queryByText('What each account has carried')).toBeNull();
  });

  it('totals what each account carried, and names the one with no profile', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'USAGE') {
        return {
          sessions: [],
          accounts: [
            { harness: 'claude-code', profile: 'work', sessions: 2, used: 60000 },
            { harness: 'claude-code', profile: null, sessions: 1, used: 9000 },
          ],
        };
      }
      return WIRING;
    });

    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('What each account has carried')).toBeTruthy();
    // The unit is named beside the figure: a bare number in a column says nothing, and "context"
    // is the honest word — calling them tokens would be a claim Daoris cannot make.
    expect(screen.getByText('60,000 context')).toBeTruthy();
    // A session on the harness's own configuration home is still somebody's usage.
    expect(screen.getByText('its own home')).toBeTruthy();
    // 🔴 No price is claimed anywhere — Daoris does not know what a token costs (D24).
    expect(screen.queryByText(/[$£€]/)).toBeNull();
  });

  /**
   * The managed toolchain (TOOL2/D57), desktop half. **Absent means PATH**, and the surface says so
   * rather than leaving a blank — "Daoris manages this" and "the machine happens to have one" are
   * different facts about the same working session.
   */
  it('says a harness runs from PATH until something is pinned', async () => {
    show(<SettingsView notify={() => {}} />);

    // Said on the door itself, where the pin control is — the roster's body no longer restates it.
    expect(await screen.findAllByText(/from PATH/)).not.toHaveLength(0);
    expect(screen.queryByText(/Daoris runs/)).toBeNull();
  });

  it('pinning installs that version and pins to it, in one action', async () => {
    show(<SettingsView notify={() => {}} />);

    // The form is behind a press now: five always-open version boxes were the widest thing on
    // the surface and almost nobody types in one.
    await userEvent.click((await screen.findAllByRole('button', { name: 'Pin a version' }))[0]!);
    const version = await screen.findByLabelText('version of claude-code to pin');
    await userEvent.type(version, '1.2.3');
    await userEvent.click(screen.getAllByRole('button', { name: 'pin it' })[0]!);

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'HARNESS_ACTION', {
      payload: { harness: 'claude-code', action: 'pin', profile: undefined, version: '1.2.3' },
    });
  });

  it('offers nothing to press until a version is typed', async () => {
    show(<SettingsView notify={() => {}} />);

    await userEvent.click((await screen.findAllByRole('button', { name: 'Pin a version' }))[0]!);
    const [pin] = await screen.findAllByRole('button', { name: 'pin it' });
    expect(pin).toBeDisabled();
  });

  /**
   * 🔴 A harness that declares no package has no version for Daoris to fetch, so the control is
   * **absent** rather than present and refusing — half a control is worse than none. Found by
   * looking at the real window, where the stub adapter was offering a button whose only possible
   * outcome was a refusal.
   */
  it('offers no pin at all for a harness that cannot be pinned', async () => {
    show(<SettingsView notify={() => {}} />);

    await screen.findByText('claude 9.9.9');
    // Only the pinnable tool offers the disclosure at all, though the roster carries two.
    expect(screen.getAllByRole('button', { name: 'Pin a version' })).toHaveLength(1);
  });

  /**
   * 🔴 A pin naming a version nobody installed **refuses every spawn**, so the surface must say that
   * rather than show the pin as though it were in force. The two fields are answered separately by
   * the host precisely so this state is renderable.
   */
  it('a pin with nothing installed at it reads as missing, not as in force', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{ ...ROSTER.harnesses[0], pinned: '9.9.9', managed: null }],
      }
      : WIRING));

    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText(/pinned 9\.9\.9 — not installed/)).toBeTruthy();
    // And the way back is offered, because a refusing pin is exactly when somebody wants it.
    expect(screen.getByRole('button', { name: 'use PATH again' })).toBeTruthy();
  });

  it('a pin that is in force names the binary sessions actually run', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'HARNESSES'
      ? {
        ...ROSTER,
        harnesses: [{
          ...ROSTER.harnesses[0],
          pinned: '1.2.3',
          managed: 'C:/somewhere/.daoris/toolchain/claude-code/1.2.3/node_modules/.bin/claude',
        }],
      }
      : WIRING));

    show(<SettingsView notify={() => {}} />);

    expect(await screen.findByText('Daoris runs 1.2.3')).toBeTruthy();
    expect(screen.getByText(/toolchain[\\/]claude-code[\\/]1\.2\.3/)).toBeTruthy();
  });

  // The per-session PICKER moved with the start form it belongs to (`work/WorkFrame.test.tsx`).
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
    // The index too: a schema rebuild is observable, and a summary cached mid-feed stayed "555 · 7"
    // on an index of 1,050 · 17 for as long as the window was open (deployed app, 2026-09-23).
    // And where each circle stands with its remote (SYNC6b): every tick runs a pass. And the asks: a
    // tick takes them (INT4b) and the band reads them (INT4d) — seen on the window, an ask made by the
    // other door was missing from *What needs you*, and its parked intake read as a bare session.
    for (const key of [keys.allSessions, keys.allQuests, keys.allAsks, keys.driver, keys.allRepositories, keys.allSync]) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: key });
    }
  });

  /**
   * *Sync now* (SYNC6b) is the tick's own pass for one circle, run by the shell's driver loop — so it
   * lands on DAORIS.DRIVER naming the circle, and afterwards where it stands is asked again.
   */
  it('Sync now lands on DAORIS.DRIVER naming the circle, and the standing refetches', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    invoke.mockImplementation(async () => ({ workspace: 'aurora', problem: null, notes: [] }));
    const { result } = renderHook(() => useSyncNow(), {
      wrapper: ({ children }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>,
    });

    const report = await result.current.mutateAsync('aurora');

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SYNC_NOW', { payload: { workspace: 'aurora' } });
    expect(report.notes).toEqual([]);
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: keys.allSync }));
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
        <OverviewView onNavigate={() => {}} notify={() => {}} />
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

/**
 * INT3c: an unadopted repository with a root here is drivable over the protocol door (D70), so the
 * screen offers what `daoris driver` already can: the same driving row an adopter's card carries
 * (D50). A row with no root here has nowhere to start, and is offered nothing. On a machine whose
 * door is direct, the row says a quest there will sit — the planner's own answer, said before it.
 */
describe('an unadopted repository on this machine (INT3c)', () => {
  const WITH_OUTSIDERS = [
    ...REGISTRY,
    { repository: 'newbie', adopted: false, addressable: true, registered: true, owns: [], accepts: [], packs: [], entries: 0 },
    { repository: 'elsewhere', adopted: false, addressable: false, registered: true, owns: [], accepts: [], packs: [], entries: 0 },
  ];
  const roster = (adapter: string) => ({
    settingsPath: 'C:/somewhere/data/harnesses.json',
    adapter,
    harnesses: [
      { harness: 'claude-code', present: true, wire: 'pipe', version: '2.1.281', problem: null, machineDefault: null, pinned: null, managed: null, pinnable: true, profiles: [] },
      { harness: 'claude-code-acp', present: true, wire: 'acp', accountOf: 'claude-code', version: '0.79.0', problem: null, machineDefault: null, pinned: null, managed: null, pinnable: true, profiles: [] },
    ],
  });
  const machine = (adapter: string) => {
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return undefined;
      if (type === 'HARNESSES') return roster(adapter);
      return { ...DRIVER_STATE, adapter };
    });
  };
  const DIRECT_NOTE = 'This machine drives on a direct agent, so a quest here sits, saying why, until it drives on a protocol one.';

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json(WITH_OUTSIDERS) : respond(url);
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('offers the driving row to one with a root here, landing on DAORIS.DRIVER', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);

    const row = await screen.findByRole('listitem', { name: 'newbie' });
    await userEvent.click(await within(row).findByLabelText('drive on this machine'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'newbie', drivable: true },
    });
    // A protocol-door machine can carry it, so nothing is added beside the choice.
    expect(within(row).queryByText(DIRECT_NOTE)).toBeNull();
  });

  it('says a quest there will sit, on a machine whose door is direct', async () => {
    machine('claude-code');
    show(<ProjectsView notify={() => {}} />);

    const row = await screen.findByRole('listitem', { name: 'newbie' });
    expect(await within(row).findByText(DIRECT_NOTE)).toBeInTheDocument();
    expect(within(row).getByLabelText('drive on this machine')).toBeInTheDocument();
  });

  it('offers nothing to drive for one with no root here — there is nowhere to start it', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);

    const row = await screen.findByRole('listitem', { name: 'elsewhere' });
    await screen.findAllByLabelText('drive on this machine');
    expect(within(row).queryByLabelText('drive on this machine')).toBeNull();
  });
});
