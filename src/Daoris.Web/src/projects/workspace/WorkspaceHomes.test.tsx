import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// SETUPUX1v (the setup-UX design §4, §5 row v): the census of the workspace Setup tab's controls before the tab goes. Wire and Unwire are
// rows of Repositories' workspace Details; the rules, the retirement of an undeclared machine and the line's defaults are rows of the
// workspace place's Sessions and Workflow; each is reached by the door that opened its section of the tab, planned by `placeOpening`, and
// pressed here over a mocked bridge as the application holds it.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { PlaceSessions, PlaceWorkflow } from '../../agents/setup/PlaceSessions';
import type { PlaceHold } from '../../agents/AgentsView';
import { doorOpening, opening, placeOpening, workspaceItem } from '../../app/opener';
import type { PlaceRow } from '../../data/wiring';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { chooseWorkspace, ProjectsView, repositoryMain } from '../../test/projectsView';
import { WORKSPACE } from '../../workflow/fixtures';
import type { WorkspaceSection } from '../tabs';

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1, workspace: 'aurora' },
  { repository: 'tools', adopted: false, registered: true, owns: [], accepts: [], packs: [], entries: 0, workspace: 'forge' },
];
const STATE = {
  ...DRIVER_STATE, drivable: ['engine'], workspaceLines: [], workspaceLandings: [], workspaceLanguages: [], workspaceReadAcross: [],
  workspaceCommits: [], workspaceReviews: [], workspaceOpinions: [],
  languageTable: [{ code: 'en', name: 'English' }],
};
const RULES = {
  path: 'x', defaults: [],
  scopes: [{ scope: 'workspace', name: 'aurora', allow: ['Bash(make:*)'], ask: [], deny: [] }],
};
const STANDING = { workspace: 'aurora', wired: true, ahead: 0, behind: [], conflicts: [], undeclared: ['m-gone'] };

const where = () => 'aurora';
const plan = (workspace: string, section: WorkspaceSection) => placeOpening(opening('projects', workspaceItem(workspace), { workspaceSection: section }), where);

beforeEach(() => {
  window.localStorage.clear();
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input);
    if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
    if (url.startsWith('/api/sync/machines')) return Response.json({ workspace: 'aurora', machine: 'm-gone', retired: true, undeclared: [], message: 'ok' });
    if (url.startsWith('/api/sync')) return Response.json(STANDING);
    return respond(url);
  }));
  invoke.mockImplementation(async (module: string, type: string) => {
    if (module === 'DAORIS.REMOTES') return WIRING;
    if (type === 'RULES' || type === 'RULE_ACTION') return RULES;
    if (type === 'ACROSS') return { repositories: [{ repository: 'engine', workspace: 'aurora', checkout: true, read: true, source: 'default', writesTo: [] }] };
    if (type === 'PLUGINS') return { folder: 'x', plugins: [] };
    if (type === 'HARNESSES') return { settingsPath: 'x', adapter: 'claude-code', harnesses: [] };
    if (type === 'ACCOUNTS') return { agents: [] };
    if (type === 'STARTS') return { adapter: 'claude-code', starts: [] };
    if (type === 'WORKFLOW_CURRENT') return WORKSPACE;
    if (type === 'LINES') return { lines: [], landings: [], languages: [] };
    return STATE;
  });
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  invoke.mockReset();
  window.localStorage.clear();
});

const called = (type: string, match?: (payload: unknown) => boolean) =>
  invoke.mock.calls.filter(([, name, options]) => name === type && (!match || match((options as { payload?: unknown } | undefined)?.payload)));

describe("the doors that opened a workspace's Setup sections", () => {
  it('lead each to its home, whoever pressed them', () => {
    // The status bar's remote, Settings' wiring and Ask Daoris's workspace-remote all say `workspaceSection: 'remote'`.
    for (const door of [
      opening('projects', null, { workspaceSection: 'remote' }, 'aurora'),
      doorOpening({ view: 'projects', workspaceTab: 'setup', workspaceSection: 'remote' }, 'aurora'),
    ]) {
      expect(placeOpening(door, where)).toMatchObject({ view: 'projects', workspaceTab: 'details', workspaceSection: 'remote' });
    }
    expect(plan('aurora', 'reach')).toMatchObject({ place: 'workspace:aurora', placeAt: { section: 'sessions', row: 'reach' } });
    expect(plan('aurora', 'defaults')).toMatchObject({ place: 'workspace:aurora', placeAt: { section: 'workflow', row: 'defaults' } });
    expect(plan('aurora', 'supervisor')).toMatchObject({ place: 'workspace:aurora', placeAt: { section: 'roles', row: 'supervisor' } });
  });
});

describe("a workspace's Details, Remote", () => {
  it('unwires a wired workspace from the row the remote door opens, with the focus on it', async () => {
    const door = plan('aurora', 'remote');
    show(<ProjectsView notify={() => {}} door={workspaceItem('aurora')} tab={door.workspaceTab} section={door.workspaceSection} />);

    const region = await screen.findByRole('region', { name: 'Remote' });
    expect(await within(region).findByText('https://aurora.example.com')).toBeInTheDocument();
    await waitFor(() => expect(region.querySelector('[data-workspace-row="remote"]')).toHaveFocus());
    await userEvent.click(within(region).getByRole('button', { name: 'Unwire' }));
    expect(called('REMOVE')).toHaveLength(1);
    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'REMOVE', { payload: { workspace: 'aurora' } });
  });

  it("wires an unwired workspace from the header's press, which opens the form in the row and leaves the tab on Details", async () => {
    show(<ProjectsView notify={() => {}} />);
    const page = await chooseWorkspace('forge');
    await userEvent.click(within(page).getByRole('tab', { name: 'Details' }));

    // The header's press, the first of two: the row's own opens the same form.
    await userEvent.click((await within(page).findAllByRole('button', { name: 'Wire to a remote…' }))[0]!);
    const region = within(repositoryMain()).getByRole('region', { name: 'Remote' });
    expect(within(repositoryMain()).getByRole('tab', { name: 'Details' })).toHaveAttribute('aria-selected', 'true');
    await userEvent.type(within(region).getByLabelText('Deployment'), 'https://forge.example.com');
    await userEvent.type(within(region).getByLabelText('Key'), 'dk_forgekey0000');
    await userEvent.click(within(region).getByRole('button', { name: 'Wire' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.REMOTES', 'SET', { payload: { workspace: 'forge', url: 'https://forge.example.com', key: 'dk_forgekey0000' } });
  });

});

const workspacePlace = (asked: string | null): PlaceHold => ({
  kind: 'workspace', row: { workspace: 'aurora' } as PlaceRow, asked, view: { route: 'default', step: null },
  onView: () => {}, onOpenSection: () => {}, onGo: () => {},
});

describe("the workspace place's Sessions, Reach", () => {
  it('retires an undeclared machine and removes a rule, from the row the reach door opens', async () => {
    const row = plan('aurora', 'reach').placeAt!.row!;
    show(<PlaceSessions place={workspacePlace(row)} notify={() => {}} />);

    const reach = await screen.findByRole('button', { name: 'Reach' });
    expect(reach).toHaveAttribute('aria-expanded', 'true');
    expect(reach).toHaveAttribute('data-place-row', 'reach');
    const machine = (await screen.findByText('m-gone')).closest('li')!;
    await userEvent.click(within(machine).getByRole('button', { name: 'Retire' }));
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes('/api/sync/machines/m-gone/retire'))).toBe(true));

    await userEvent.click(screen.getByRole('button', { name: 'remove Bash(make:*)' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', { payload: { action: 'remove', rule: 'Bash(make:*)', scope: 'workspace', name: 'aurora' } });
  });

  it.each(['allow', 'ask', 'deny'] as const)('adds a rule to the %s list', async (list) => {
    show(<PlaceSessions place={workspacePlace('reach')} notify={() => {}} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: 'Add a rule' }));
    screen.getByRole('combobox', { name: 'the list' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('option', { name: list }));
    await user.type(screen.getByRole('textbox', { name: 'the rule' }), `Bash(${list}:*){Enter}`);
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_ACTION', {
      payload: { action: 'add', list, rule: `Bash(${list}:*)`, scope: 'workspace', name: 'aurora' },
    }));
  });
});

describe("the workspace place's Workflow, the line's defaults", () => {
  it("opens the defaults from the row the defaults door names, with the line's rows", async () => {
    const row = plan('aurora', 'defaults').placeAt!.row!;
    show(<PlaceWorkflow place={workspacePlace(row)} notify={() => {}} />);

    const defaults = await screen.findByRole('button', { name: 'Defaults' });
    expect(defaults).toHaveAttribute('aria-expanded', 'true');
    expect(defaults).toHaveAttribute('data-place-row', 'defaults');
    for (const label of ['Line', 'Commits', 'Review before landing', 'Second opinion before landing']) expect(screen.getAllByText(label, { selector: 'span' }).length).toBeGreaterThan(0);
    // The session rows are Sessions', not Workflow's.
    expect(screen.queryByText('Session language')).toBeNull();
  });
});
