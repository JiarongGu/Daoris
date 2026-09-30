import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Projects in SHELL mode — the bridge mocked as present, so the controls that only a desktop with a
// driver may render actually render, and land on the DAORIS.DRIVER and DAORIS.REGISTRY modules. Browser
// mode needs no twin suite: the absence of these controls there is asserted by their queries never
// firing (an unstubbed fetch throws). Moved from `shell.test.tsx` with MOD3: tests follow their code.

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
import { DRIVER_STATE, REGISTRY, REPOSITORIES, respond, show } from './test/shellHarness';

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

  /**
   * Seen on the installed window, 2026-09-24, on a machine whose registrations had all been retired:
   * Projects was its header over a blank page. The fixture always has a repository, so no story or
   * test had ever rendered this, and it is the first thing a new installation shows.
   */
  it('a machine with no repository says so, and offers the one act that changes it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json([]) : respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    expect(await screen.findByText('No repository is registered yet')).toBeInTheDocument();
    expect(screen.getByText(/daoris import/).tagName).toBe('CODE');
    // The page header's and the empty state's: the second is where the eye already is.
    expect(screen.getAllByRole('button', { name: 'add repository' })).toHaveLength(2);
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
      if (url === '/api/registry/import' && init?.method === 'POST') {
        return Response.json({
          folder: 'D:/repos/borealis', imported: 2, repositories: ['aurora', 'dusk'], workspace: 'borealis',
          message: 'Registered 2 from D:/repos/borealis into workspace `borealis`: aurora, dusk.',
        });
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

    // UX5 U38: the move leads and *never mind* follows, as in every other drawer; this one was reversed.
    const register = screen.getByRole('button', { name: 'register it' });
    expect(register.compareDocumentPosition(screen.getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(register);

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
   * Setting a folder up as a workspace (D77, FG4): `daoris import <folder> --workspace <name>`'s
   * screen twin. The Workspace menu's import used to state no workspace at all, so a folder set up from
   * the window landed in `default`. The folder's name is offered, since that is what setting a folder up
   * as a workspace means; the service's sentence comes back verbatim.
   */
  it("importing a folder names the workspace its repositories land in, offering the folder's name", async () => {
    const notify = vi.fn();
    show(<ProjectsView notify={notify} importRequested onImportOpened={() => {}} />);

    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'choose a folder…' }));
    expect(await within(drawer).findByText('D:/repos/borealis')).toBeInTheDocument();
    expect(within(drawer).getByLabelText('workspace')).toHaveValue('borealis');

    // The move leads and *never mind* follows, as in every drawer (UX5 U38).
    const importIt = within(drawer).getByRole('button', { name: 'import them' });
    expect(importIt.compareDocumentPosition(within(drawer).getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(importIt);

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry/import' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toEqual({ folder: 'D:/repos/borealis', workspace: 'borealis' });
    expect(notify).toHaveBeenCalledWith('Registered 2 from D:/repos/borealis into workspace `borealis`: aurora, dusk.');
  });

  /** Emptied, the import names none — and an import that names none still moves nobody (D48 §2). */
  it('an import with its workspace emptied names none', async () => {
    show(<ProjectsView notify={() => {}} importRequested onImportOpened={() => {}} />);

    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'choose a folder…' }));
    await userEvent.clear(await within(drawer).findByLabelText('workspace'));
    await userEvent.click(within(drawer).getByRole('button', { name: 'import them' }));

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry/import' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toEqual({ folder: 'D:/repos/borealis' });
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
    // UX5 U38: the move, then *never mind*, as every other drawer puts them; this pair was reversed.
    const confirm = within(drawer).getByRole('button', { name: 'yes, retire it' });
    expect(confirm.compareDocumentPosition(within(drawer).getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(confirm);

    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registry/engine', { method: 'DELETE' });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing was deleted'));
  });

  /**
   * UX5 U37: *owns* and *accepts* were edited in monospace, beside a summary in the body face, and
   * they become chips in the body face. They are phrases, not code, so they are written as phrases.
   */
  it('edits a declaration in the face it is read in', async () => {
    // The declaration is edited only where its repository has a root on this machine.
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([{ ...REGISTRY[0], root: 'C:/somewhere/engine' }]);
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    await userEvent.click(await screen.findByRole('button', { name: 'manage' }));
    const drawer = await screen.findByRole('dialog');

    for (const field of ['summary', 'owns', 'accepts']) {
      expect(within(drawer).getByLabelText(field)).not.toHaveClass('font-mono');
    }
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

  /**
   * 🔴 Seen on the window (POLISH4): a chip that wrapped fell back under its row's label, because
   * the label and the chips were one flowing line. The labels are a column of their own now.
   */
  it("keeps a project row's label apart from its chips, so a wrapped chip lines up with the first", async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([{ ...REGISTRY[0], owns: ['the runtime', 'the public API'], accepts: ['a failing case'] }]);
      }
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    const owns = await screen.findByText('owns');
    expect(owns.tagName).toBe('DT');
    expect(owns.nextElementSibling?.tagName).toBe('DD');
    expect(within(owns.nextElementSibling as HTMLElement).getByText('the public API')).toBeTruthy();
  });

  /** The join steps were all monospace, prose included; the commands are code in a sentence. */
  it('sets the join steps as a sentence with its commands as code', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([...REGISTRY, { ...REGISTRY[0], repository: 'lone', adopted: false }]);
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    const init = await screen.findByText('daoris init');
    expect(init.tagName).toBe('CODE');
    expect(init.parentElement).not.toHaveClass('font-mono');
    // Prose keeps its measure once the column follows the window (UX5 U59): uncapped, the sentence
    // ran about two hundred characters on one line.
    expect(init.parentElement).toHaveClass('max-w-prose');
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

  /**
   * 🔴 UX5 U36: the group opened with an eight-line paragraph above its one row, read once and
   * scrolled past every visit after, the essay a settings page was before its rows (§4, `SettingRow`).
   * It leads with one line now, and the reasoning is on the info glyph, where a row keeps its why.
   */
  it('leads with one line, and keeps its reasoning on the info glyph', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);
    await screen.findByRole('listitem', { name: 'newbie' });

    expect(screen.getByText(/Registered here without a manifest/)).toBeInTheDocument();
    expect(screen.queryByText(/^Adoption is a repository's own act/)).toBeNull();
    expect(screen.getByRole('note', { name: /Adoption is a repository's own act/ })).toBeInTheDocument();
  });

  it('offers nothing to drive for one with no root here — there is nowhere to start it', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);

    const row = await screen.findByRole('listitem', { name: 'elsewhere' });
    await screen.findAllByLabelText('drive on this machine');
    expect(within(row).queryByLabelText('drive on this machine')).toBeNull();
  });
});
