import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The Permissions domain in SHELL mode. Moved from `shell.test.tsx` with MOD4: tests follow their code.

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

import { SettingsView } from '../SettingsView';
import { respond, show, WIRING } from '../test/shellHarness';

/**
 * What agents may do (PERM1, D72): the machine's `permissions.json` over the bridge, and every change
 * the screen's half of `daoris agent rules` (D50). Shaped as the wire carries it — the machine's scope
 * with no `name` and a clean file with no `problem`, because the bridge leaves a null out.
 */
describe('the rules card', () => {
  const RULES = {
    path: 'C:/somewhere/data/permissions.json',
    defaults: [
      { id: 'connector', list: 'allow', rules: ['mcp__daoris-knowledge__quest_respond'], why: 'The connector.', on: true },
      { id: 'no-push', list: 'deny', rules: ['Bash(git push:*)'], why: "A push stays the person's (D37).", on: true },
    ],
    scopes: [
      { scope: 'machine', allow: [], ask: [], deny: [] },
      { scope: 'repository', name: 'engine', allow: ['Bash(make:*)'], ask: [], deny: [] },
    ],
  };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'RULES' || type === 'RULE_ACTION' ? RULES : WIRING));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('shows the defaults and each scope from the machine\'s own file, asking no service', async () => {
    show(<SettingsView notify={() => {}} section="permissions" />);

    expect(await screen.findByText('C:/somewhere/data/permissions.json')).toBeTruthy();
    expect(screen.getByRole('listitem', { name: 'no-push' })).toBeTruthy();
    expect(within(screen.getByRole('list', { name: 'Repository engine' })).getByText('Bash(make:*)')).toBeTruthy();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULES', {});
  });

  it('a default switched and a rule removed land on the bridge as the actions a terminal has', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const push = await screen.findByRole('listitem', { name: 'no-push' });
    await userEvent.click(within(push).getByRole('checkbox'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'default', id: 'no-push', on: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('The default no-push is off on this machine.'));

    await userEvent.click(screen.getByRole('button', { name: 'remove Bash(make:*)' }));
    expect(invoke).toHaveBeenCalledWith(
      'DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'remove', rule: 'Bash(make:*)', scope: 'repository', name: 'engine' } });
  });

  /** PERM2 (D74): the person's answer to an agent's proposal lands as `daoris agent rules accept|decline` would. */
  it('a proposal accepted on the screen lands on the bridge by its id, and says what changed', async () => {
    const proposed = {
      ...RULES,
      proposals: [{
        id: 'p0000002', state: 'waiting', action: 'add', scope: 'machine', list: 'allow', rule: 'WebFetch',
        why: 'The docs it needs are on the web.', session: 'i9n8t7k6', proposed: '2026-09-24T11:00:00Z',
      }],
    };
    invoke.mockImplementation(async (_module: string, type: string) =>
      (type === 'RULES' || type === 'RULE_PROPOSAL' ? proposed : WIRING));
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const row = await screen.findByRole('listitem', { name: 'proposal #p0000002' });
    await userEvent.click(within(row).getByRole('button', { name: 'Accept' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000002', accept: true } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Accepted: allow WebFetch for every session on this machine. Sessions started from now on are handed it.'));
  });

  /** A shell older than the rules answers something else to a question it never heard: no card, no blank page. */
  it('an older shell gets no card', async () => {
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'RULES' ? undefined : WIRING));
    show(<SettingsView notify={() => {}} section="permissions" />);

    // The machine's domains appear once the shell has answered, and Permissions is the one open.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Permissions' })).toHaveAttribute('aria-current', 'page'));
    expect(screen.queryByText('The rules file')).toBeNull();
    expect(screen.queryByText('Across repositories')).toBeNull();
    // Both questions are answered, so neither card is loading: no skeleton left standing in for one.
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULES', {}));
    await waitFor(() => expect(screen.getByRole('main').querySelector('[aria-busy="true"]')).toBeNull());
  });

  /**
   * FRAME1g (D118 §3h; audit ST11): the domain drew nothing until its first answers, so a first open was a
   * blank page under its name. Each card holds its place with skeleton rows until its own answer is in.
   */
  it('holds each card\'s place with skeleton rows on its first load, never nothing', async () => {
    const answers: Partial<Record<'RULES' | 'ACROSS', (value: unknown) => void>> = {};
    invoke.mockImplementation((_module: string, type: string) => (type === 'RULES' || type === 'ACROSS'
      ? new Promise((resolve) => { answers[type] = resolve; })
      : Promise.resolve(WIRING)));
    show(<SettingsView notify={() => {}} section="permissions" />);

    // The machine's domains are offered once the shell answers, and the main area is drawn anew for this one.
    await screen.findByRole('heading', { level: 1, name: 'Permissions' });
    const main = screen.getByRole('main');
    await waitFor(() => expect(main.querySelectorAll('[aria-busy="true"]')).toHaveLength(2));

    answers.RULES?.(RULES);
    expect(await screen.findByText('C:/somewhere/data/permissions.json')).toBeTruthy();
    expect(main.querySelectorAll('[aria-busy="true"]')).toHaveLength(1);
    answers.ACROSS?.({ repositories: [] });
    await waitFor(() => expect(main.querySelector('[aria-busy="true"]')).toBeNull());
  });
});

/**
 * READ1 (D107): reading across, the screen's half of `daoris driver across --workspace` (D50) — each change lands on the
 * bridge as the verb a terminal has, and what the driver answered is what the card shows. Since UX6f (D150 §3.1) a
 * repository's own reading and what its sessions also write into are on its page, under Setup, which the card's doors open.
 */
describe('the reading across card', () => {
  const ACROSS = {
    repositories: [
      { repository: 'engine', workspace: 'default', checkout: true, read: true, source: 'default', writesTo: [] },
      { repository: 'plugins', workspace: 'default', checkout: true, read: true, source: 'default', writesTo: ['engine'] },
    ],
  };
  const STATE = { drivable: [], holds: [], running: [], workspaceReadAcross: [] };

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'ACROSS') return ACROSS;
      if (type === 'STATE' || type === 'SET_READ_ACROSS' || type === 'SET_WRITE_ACROSS') return STATE;
      return WIRING;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('switches a workspace\'s reading off, as `daoris driver across --workspace <name> read off` does, and says what it means', async () => {
    const notify = vi.fn();
    show(<SettingsView notify={notify} section="permissions" />);

    const workspace = await screen.findByRole('radiogroup', { name: 'Reading across in default' });
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ACROSS', {});
    await userEvent.click(within(workspace).getByRole('radio', { name: 'Off' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SET_READ_ACROSS', { payload: { workspace: 'default', read: false } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'default: its checkout is read by no agent outside it. A session already running keeps what it began with.'));
  });

  // UX6f: a repository's relationship is taken back on its Setup now; the card's door opens it there.
  it("opens the Setup of a repository that declared a relationship, by the application's one opener", async () => {
    const onGo = vi.fn();
    show(<SettingsView notify={() => {}} section="permissions" onGo={onGo} />);

    expect(await screen.findByText('Set for a repository of its own, on its page under Setup:')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Stop plugins writing into engine' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: "Open plugins's setup" }));
    expect(onGo).toHaveBeenCalledWith({ view: 'projects', item: 'plugins', tab: 'setup' });
  });

  /** HELP10: Ask Daoris's go to *Reading and writing across* opens the domain at this card, once it is drawn. */
  it('is where a go to Permissions → Across repositories opens', async () => {
    const scrolled = vi.fn();
    const original = Element.prototype.scrollIntoView;
    Element.prototype.scrollIntoView = function scroll(this: Element) { scrolled(this.id); };
    const anchored = vi.fn();
    try {
      show(<SettingsView notify={() => {}} section="permissions" anchor="across" onAnchored={anchored} />);

      await screen.findByRole('radiogroup', { name: 'Reading across in default' });
      await waitFor(() => expect(scrolled).toHaveBeenCalledWith('settings-across'));
      expect(anchored).toHaveBeenCalled();
    } finally {
      Element.prototype.scrollIntoView = original;
    }
  });
});
