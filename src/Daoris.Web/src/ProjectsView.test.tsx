import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Repositories in SHELL mode — the bridge mocked as present, so the controls that only a desktop with a
// driver may render actually render, and land on the DAORIS.DRIVER and DAORIS.REGISTRY modules. Browser
// mode needs no twin suite: the absence of these controls there is asserted by their queries never
// firing (an unstubbed fetch throws). Moved from `shell.test.tsx` with MOD3: tests follow their code.
// Since FRAME1e the view is a list pane and a page (D118 §2): the repositories are the list, and what a
// card held is the chosen repository's page, so each test chooses the repository it reads.

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

import { DRIVER_STATE, REGISTRY, REPOSITORIES, respond, show } from './test/shellHarness';
import { chooseRepository, ProjectsView, repositoryList, repositoryMain, repositoryRow } from './test/projectsView';

const CHOSEN = 'daoris.list.projects.chosen';

afterEach(() => window.localStorage.clear());

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
    const page = await chooseRepository('engine');

    await userEvent.click(await within(page).findByLabelText('Drive on this machine'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'engine', drivable: true },
    });
  });

  /**
   * Seen on the installed window, 2026-09-24, on a machine whose registrations had all been retired:
   * Projects was its header over a blank page. The fixture always has a repository, so no story or
   * test had ever rendered this, and it is the first thing a new installation shows. Since FRAME1e the
   * list says it, and offers its ＋'s act where the eye already is.
   */
  it('a machine with no repository says so, and offers the one act that changes it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json([]) : respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    expect(await within(repositoryList()).findByText('No repository is registered yet')).toBeInTheDocument();
    expect(within(repositoryList()).getByText(/daoris import/).tagName).toBe('CODE');
    // The list's ＋ and its empty state's: the second is where the eye already is.
    expect(within(repositoryList()).getAllByRole('button', { name: 'Add repository' })).toHaveLength(2);
  });

  /**
   * D118 §2 (audit PR1): the repositories are a list, the adopted first and then *Registered, not adopted*,
   * each a row a person chooses; the cards that opened nothing but *Manage* are gone, and what a card said
   * is the chosen repository's page.
   */
  it('lists the adopted repositories, then the registered ones not adopted, each a row to choose', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) return Response.json([...REGISTRY, { ...REGISTRY[0], repository: 'lone', adopted: false, summary: undefined }]);
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    const adopted = await within(repositoryList()).findByRole('heading', { name: 'Adopted (1)' });
    const outside = within(repositoryList()).getByRole('heading', { name: 'Registered, not adopted (1)' });
    expect(adopted.compareDocumentPosition(outside)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    // A row says the repository and its one line: its summary, content shown as it is.
    expect(within(await repositoryRow('engine')).getByText('the engine')).toBeInTheDocument();
    // With nothing chosen, the main area says how to choose and offers the ＋.
    expect(within(repositoryMain()).getByText('Choose a repository')).toBeInTheDocument();
    expect(within(repositoryMain()).getByRole('button', { name: 'Add repository' })).toBeInTheDocument();
  });

  /** D118 §3f (audit PR9): the chosen repository is the list's memory, so a relaunch reopens it. */
  it('remembers the repository chosen, and reopens it', async () => {
    show(<ProjectsView notify={() => {}} />);
    const row = await repositoryRow('engine');
    await chooseRepository('engine');

    expect(window.localStorage.getItem(CHOSEN)).toBe('engine');
    expect(within(row).getAllByRole('button')[0]).toHaveAttribute('aria-current', 'true');

    cleanup();
    show(<ProjectsView notify={() => {}} />);
    expect(await screen.findByRole('heading', { level: 1, name: 'engine' })).toBeInTheDocument();
  });

  /** §3b: a repository the list chose and the registry no longer holds says so, rather than a blank page. */
  it('says a chosen repository has gone, once the registry has answered without it', async () => {
    window.localStorage.setItem(CHOSEN, 'retired-one');
    show(<ProjectsView notify={() => {}} />);

    expect(await within(repositoryMain()).findByText('This repository is no longer here')).toBeInTheDocument();
  });

  /** The page's header holds its acts (D118 §3b): *Manage* on a shell, and the door to its code map. */
  it("heads a repository's page with Manage and a door to its code map", async () => {
    const onOpenCode = vi.fn();
    show(<ProjectsView notify={() => {}} onOpenCode={onOpenCode} />);
    const page = await chooseRepository('engine');
    const head = within(page).getByRole('heading', { level: 1, name: 'engine' }).closest('header')!;

    expect(within(head).getByRole('button', { name: 'Manage' })).toBeInTheDocument();
    await userEvent.click(within(head).getByRole('button', { name: 'Open code map' }));
    expect(onOpenCode).toHaveBeenCalledWith('engine');
  });

  it('hold appears only once a repository is drivable — a hold on nothing is noise', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    await within(page).findByLabelText('Drive on this machine');
    expect(within(page).queryByLabelText('Hold')).not.toBeInTheDocument();
  });

  it('holding a drivable repository lands on DAORIS.DRIVER with its own payload key', async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, drivable: ['engine'] }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    await userEvent.click(await within(page).findByLabelText('Hold'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_HOLD', {
      payload: { repository: 'engine', held: true },
    });
  });

  /**
   * KNOWUSE1b (D135 §3): a repository's standing answer on its page, beside the driver's other choices for it, in the
   * person's words as this machine keeps them; edited and cleared over the same file the terminal edits, on DAORIS.DRIVER.
   */
  it("shows a repository's standing answer on its page, and edits and clears it on DAORIS.DRIVER", async () => {
    invoke.mockImplementation(async () => ({
      ...DRIVER_STATE, standing: [{ repository: 'Engine', says: 'dev writes allowed; prod only on a yes', at: '2026-10-03T09:00:00Z' }],
    }));
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    expect(await within(page).findByText('dev writes allowed; prod only on a yes')).toBeInTheDocument();
    await userEvent.click(within(page).getByRole('button', { name: 'Edit' }));
    const field = within(page).getByRole('textbox', { name: 'Standing answer' });
    await userEvent.clear(field);
    await userEvent.type(field, 'dev only');
    await userEvent.click(within(page).getByRole('button', { name: 'Save' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STANDING', { payload: { repository: 'engine', says: 'dev only' } });

    await userEvent.click(within(page).getByRole('button', { name: 'Clear' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_STANDING', { payload: { repository: 'engine' } });
  });

  /**
   * The rail's words for a repository's standing on this machine (RepositoryGroup), on its row and its page's
   * head: held outranks *drives here*, and a registration with no checkout here says so.
   */
  it("marks a repository's standing on this machine, on its row and its page", async () => {
    invoke.mockImplementation(async () => ({ ...DRIVER_STATE, drivable: ['engine'], holds: ['engine'] }));
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          { ...REGISTRY[0], root: 'C:/somewhere/engine' },
          { ...REGISTRY[0], repository: 'mirrored', summary: 'a teammate’s' },
        ]);
      }
      return respond(url);
    }));
    show(<ProjectsView notify={() => {}} />);

    const engine = await repositoryRow('engine');
    expect(await within(engine).findByText('held')).toBeInTheDocument();
    expect(within(engine).queryByText('drives here')).toBeNull();
    expect(within(engine).queryByText('not on this machine')).toBeNull();
    expect(within(await repositoryRow('mirrored')).getByText('not on this machine')).toBeInTheDocument();

    const page = await chooseRepository('engine');
    const head = within(page).getByRole('heading', { level: 1, name: 'engine' }).closest('header')!;
    expect(within(head).getByText('held')).toBeInTheDocument();
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
    shareKnowledge: false, uses: ['engine'],
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

    // The list's ＋ is the act (D118 §2): one kind, so the press is the act itself.
    await userEvent.click(await within(repositoryList()).findByRole('button', { name: 'Add repository' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));

    expect(invoke).toHaveBeenCalledWith('DAORIS.REGISTRY', 'PICK_FOLDER', { timeoutMs: Infinity });
    expect(await screen.findByText('D:/repos/borealis')).toBeInTheDocument();

    // UX5 U38: the move leads and *never mind* follows, as in every other drawer; this one was reversed.
    const register = screen.getByRole('button', { name: 'Register' });
    expect(register.compareDocumentPosition(screen.getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(register);

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toMatchObject({
      repository: 'borealis', root: 'D:/repos/borealis', adopted: true,
      // What its manifest says it uses travels with the rest of the declaration (MANAGE1, D91).
      domain: { summary: 'The aurora.', owns: ['the sky'], accepts: ['a quest'], uses: ['engine'] },
    });
    // Once registered, the list has it chosen, as an installed plugin is (D119 §3.1).
    await vi.waitFor(() => expect(window.localStorage.getItem(CHOSEN)).toBe('borealis'));
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

    await userEvent.click(await within(repositoryList()).findByRole('button', { name: 'Add repository' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose a folder…' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Register' }));

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
    await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
    expect(await within(drawer).findByText('D:/repos/borealis')).toBeInTheDocument();
    expect(within(drawer).getByLabelText('Workspace')).toHaveValue('borealis');

    // The move leads and *never mind* follows, as in every drawer (UX5 U38).
    const importIt = within(drawer).getByRole('button', { name: 'Import' });
    expect(importIt.compareDocumentPosition(within(drawer).getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(importIt);

    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry/import' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body))).toEqual({ folder: 'D:/repos/borealis', workspace: 'borealis' });
    expect(notify).toHaveBeenCalledWith('Registered 2 from D:/repos/borealis into workspace `borealis`: aurora, dusk.');
  });

  /** D118 §2: *Import a folder* is the list's ⋯, beside the ＋ that adds one. */
  it("opens the import from the list's ⋯", async () => {
    const user = userEvent.setup();
    show(<ProjectsView notify={() => {}} />);

    // A menu opens from the keyboard in jsdom, the path D41 §6 requires anyway.
    (await within(repositoryList()).findByRole('button', { name: 'More actions' })).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Import a folder…' }));

    expect(await screen.findByRole('dialog', { name: 'Import a folder as a workspace' })).toBeInTheDocument();
  });

  /** Emptied, the import names none — and an import that names none still moves nobody (D48 §2). */
  it('an import with its workspace emptied names none', async () => {
    show(<ProjectsView notify={() => {}} importRequested onImportOpened={() => {}} />);

    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Choose a folder…' }));
    await userEvent.clear(await within(drawer).findByLabelText('Workspace'));
    await userEvent.click(within(drawer).getByRole('button', { name: 'Import' }));

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
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');
    expect(within(drawer).getByText(/Nothing is deleted/)).toBeInTheDocument();

    // Two clicks, deliberately: the first is not the destructive one.
    await userEvent.click(within(drawer).getByRole('button', { name: 'Retire' }));
    // UX5 U38: the move, then *never mind*, as every other drawer puts them; this pair was reversed.
    const confirm = within(drawer).getByRole('button', { name: 'Retire repository' });
    expect(confirm.compareDocumentPosition(within(drawer).getByRole('button', { name: 'Never mind' })))
      .toBe(Node.DOCUMENT_POSITION_FOLLOWING);
    await userEvent.click(confirm);

    expect(vi.mocked(fetch)).toHaveBeenCalledWith('/api/registry/engine', { method: 'DELETE' });
    expect(notify).toHaveBeenCalledWith(expect.stringContaining('Nothing was deleted'));
    // Retired by the person's own press, so the page goes back to choosing, not to *gone*.
    expect(await within(repositoryMain()).findByText('Choose a repository')).toBeInTheDocument();
    expect(window.localStorage.getItem(CHOSEN)).toBeNull();
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
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');

    for (const field of ['Summary', 'Owns', 'Accepts']) {
      expect(within(drawer).getByLabelText(field)).not.toHaveClass('font-mono');
    }
  });

  /**
   * MANAGE1: the registry replaces a row's `uses` with the declaration's (D91), and silence there is
   * none, since `connect` and the driver's sync send `uses` only when there is one. Saving re-registered
   * with a domain that carried no `uses`, so the repository's dependencies left the registry until its
   * next `connect`, while the file just written still held them. The re-registration says what the file
   * says, from the shell's answer to the write.
   */
  it('saving a declaration re-registers with what the written file says it uses', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url === '/api/registry' && init?.method === 'POST') return Response.json({ repository: 'engine', workspace: 'default' });
      if (url.startsWith('/api/registry')) return Response.json([{ ...REGISTRY[0], root: 'C:/somewhere/engine' }]);
      return respond(url);
    }));
    invoke.mockImplementation(async (_module: string, type: string) =>
      type === 'WRITE_DECLARATION'
        ? {
          path: 'C:/somewhere/engine', name: 'engine', exists: true, adopted: true, git: true,
          summary: 'the engine', owns: ['the runtime'], accepts: [], packs: [], join: false, shareKnowledge: false,
          uses: ['tools'],
        }
        : DRIVER_STATE);
    const notify = vi.fn();
    show(<ProjectsView notify={notify} />);
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Write daoris.json' }));

    await vi.waitFor(() => expect(notify).toHaveBeenCalled());
    const posted = vi.mocked(fetch).mock.calls
      .find(([url, init]) => String(url) === '/api/registry' && init?.method === 'POST');
    expect(JSON.parse(String(posted![1]!.body)).domain).toEqual({
      summary: 'the engine', owns: ['the runtime'], accepts: [], uses: ['tools'],
    });
  });

  // The CONSOLE's tests moved to `work/WorkFrame.test.tsx` with the console itself (D55): one home
  // for the stream, and the quest's page keeps the record summary plus a door into it.

  /**
   * Provenance is served, not implied (D48 §6). A person looking at a repository's knowledge must be
   * able to see which commit it came from — freshness they have to assume is exactly what the rule
   * exists to replace.
   */
  it('projects name the commit a deployment was fed from', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    expect(await within(page).findByText(/c0ffee12/)).toBeTruthy();
    // Relative time beside it: "how stale is this" is the question being answered.
    expect(within(page).getByText(/c0ffee12 · /)).toBeTruthy();
  });

  /**
   * One sentence for one fact. On the deployed family, a repository present in the index with a
   * count of zero read "0 entries · 0 local · 0 canonical", one absent from it read "nothing
   * indexed yet", and one outside the family read "—" — three renderings of "the index holds
   * nothing of this", on one page. Since FRAME1e an adopted repository's count is on its page, and one
   * not adopted says its count on its row, where nothing else does.
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
    expect(within(await repositoryRow('lone')).getByText('1 entry indexed read-only')).toBeTruthy();
    expect(within(await repositoryRow('many')).getByText('1,234 entries indexed read-only')).toBeTruthy();
    expect(within(await repositoryRow('stranger')).getByText('nothing indexed yet')).toBeTruthy();

    expect(within(await chooseRepository('engine')).getByText('1 entry · 1 local · 0 canonical')).toBeTruthy();
    for (const name of ['blank', 'unseen']) {
      expect(within(await chooseRepository(name)).getByText('nothing indexed yet')).toBeTruthy();
    }
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
    const page = await chooseRepository('engine');

    const owns = within(page).getByText('Owns');
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
    const page = await chooseRepository('lone');

    const init = within(page).getByText('daoris init');
    expect(init.tagName).toBe('CODE');
    expect(init.parentElement).not.toHaveClass('font-mono');
    // Prose keeps its measure once the column follows the window (UX5 U59): uncapped, the sentence
    // ran about two hundred characters on one line.
    expect(init.parentElement).toHaveClass('max-w-prose');
  });

  /** Re-wiring is a row on this machine; it must not touch the repository's tracked file. */
  it('re-wiring edits one row and writes no file', async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('engine');

    await userEvent.click(within(page).getByRole('button', { name: 'Manage' }));
    const drawer = await screen.findByRole('dialog');
    await userEvent.clear(within(drawer).getByLabelText('Workspace'));
    await userEvent.type(within(drawer).getByLabelText('Workspace'), 'tools');
    await userEvent.click(within(drawer).getByRole('button', { name: 'Move to workspace' }));

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
 * screen offers what `daoris driver` already can: the same driving row an adopter's page carries
 * (D50). One with no root here has nowhere to start, and is offered nothing. On a machine whose
 * door is direct, the page says a quest there will sit — the planner's own answer, said before it.
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
    const page = await chooseRepository('newbie');

    await userEvent.click(await within(page).findByLabelText('Drive on this machine'));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_DRIVABLE', {
      payload: { repository: 'newbie', drivable: true },
    });
    // A protocol-door machine can carry it, so nothing is added beside the choice.
    expect(within(page).queryByText(DIRECT_NOTE)).toBeNull();
    // None of adoption's own acts, like managing its declaration, which writes into the repository.
    expect(within(page).queryByRole('button', { name: 'Manage' })).toBeNull();
  });

  it('says a quest there will sit, on a machine whose door is direct', async () => {
    machine('claude-code');
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('newbie');

    expect(await within(page).findByText(DIRECT_NOTE)).toBeInTheDocument();
    expect(within(page).getByLabelText('Drive on this machine')).toBeInTheDocument();
  });

  /**
   * 🔴 UX5 U36: the group opened with an eight-line paragraph above its one row, read once and
   * scrolled past every visit after, the essay a settings page was before its rows (§4, `SettingRow`).
   * It leads with one line now, and the reasoning is on the info glyph, where a row keeps its why.
   */
  it('leads with one line, and keeps its reasoning on the info glyph', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('newbie');

    expect(within(page).getByText(/Registered here without a manifest/)).toBeInTheDocument();
    expect(within(page).queryByText(/^Adoption is a repository's own act/)).toBeNull();
    expect(within(page).getByRole('note', { name: /Adoption is a repository's own act/ })).toBeInTheDocument();
  });

  it('offers nothing to drive for one with no root here — there is nowhere to start it', async () => {
    machine('claude-code-acp');
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseRepository('elsewhere');

    expect(await within(page).findByText('Adoption steps')).toBeInTheDocument();
    expect(within(page).queryByLabelText('Drive on this machine')).toBeNull();
  });
});
