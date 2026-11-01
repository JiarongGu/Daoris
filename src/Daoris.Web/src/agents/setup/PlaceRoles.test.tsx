import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// AGENTSPLACE1g1 (the Agents-place design §3, Roles): a place's page lists one row per work for the place's chain, with the role editor's
// *Give … its own…* on a row taken from a parent and *Edit* and *Back to …'s* on a role placed here, all over SET_ROLE.

const { invoke, notifyReady, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
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

import type { StructurePlaceAsk } from '../../data/wiring';
import i18n from '../../locales/i18n';
import { DRIVER_STATE, respond, show, WIRING } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { useAgentsMode, useAgentsView } from '../AgentsView';
import { CATALOGUE, CHAIN, PLACES } from './setupFixtures';

function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {} });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const ROSTER = {
  settingsPath: 'x', adapter: 'claude-code',
  harnesses: [{
    harness: 'claude-code', product: 'Claude Code', maker: 'Anthropic', wire: 'pipe', present: true, version: 'claude 9.9.9', problem: null,
    machineDefault: null, pinned: null, managed: null, pinnable: true, ownLogin: 'in', workspaceDefaults: [], profiles: [],
  }],
};

const STANDING = {
  offered: false, on: false, presses: [], built: [], unwritten: false, used: 0, held: [], appointmentsOffered: true,
  supervisorOffered: false, stewardOffered: false, stewardActsOffered: [], stewardActs: [], stewardDesk: [], appointments: [], holders: [],
};

const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SET_ROLE').map(([, , options]) => (options as { payload: unknown }).payload);
const asked = () => invoke.mock.calls.filter(([, type]) => type === 'ROLE_STRUCTURE').map(([, , options]) => (options as { payload: { chain?: StructurePlaceAsk } }).payload.chain);
const rowOf = (work: string) => within(screen.getByText(work, { selector: 'span' }).closest('[data-setting-row]') as HTMLElement);

describe('A place page, Roles', () => {
  let refuse = false;
  let chain: (ask: StructurePlaceAsk) => ReturnType<typeof CHAIN>;

  beforeEach(() => {
    refuse = false;
    chain = CHAIN;
    window.localStorage.clear();
    window.localStorage.setItem('daoris.agents.setup', 'on');
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: { chain?: StructurePlaceAsk } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'SET_ROLE' && refuse) throw new Error('`build` is held by another change: read it again.');
      if (type === 'STATE' || type === 'SET_ROLE') return { ...DRIVER_STATE, standingConfirmation: STANDING };
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'PLACES') return PLACES({});
      if (type === 'ROLES') return CATALOGUE;
      if (type === 'ACTORS') return { actors: [] };
      if (type === 'ROLE_STRUCTURE') return chain(options?.payload?.chain ?? {});
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  const openEngine = async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /engine/ }));
    await screen.findByRole('heading', { name: 'Roles' });
    return await screen.findByText('Engine build · in repository engine');
  };

  it("reads the place's chain alone and lists a row per work: its role, where it is placed, its first choice and that choice's reading", async () => {
    await openEngine();
    expect(asked().at(-1)).toEqual({ workspace: 'aurora', repository: 'engine' });
    const build = rowOf('building');
    expect(build.getByText(/claude-code · sonnet · high · cannot run: signed out/)).toBeTruthy();
    const design = rowOf('design and decisions');
    expect(design.getByText('Build · on this machine')).toBeTruthy();
    expect(design.getByText(/claude-code · opus · high · can run/)).toBeTruthy();
  });

  it('offers Give its own on a row taken from a parent, and Edit and Back on a role placed here', async () => {
    await openEngine();
    const build = rowOf('building');
    expect(build.getByRole('button', { name: 'Edit, the role taking building' })).toBeTruthy();
    expect(build.getByRole('button', { name: "Back to aurora's, for building" })).toBeTruthy();
    expect(build.queryByRole('button', { name: /Give engine its own/ })).toBeNull();
    const design = rowOf('design and decisions');
    expect(design.getByRole('button', { name: 'Give engine its own… for design and decisions' })).toBeTruthy();
    expect(design.queryByRole('button', { name: /^Edit/ })).toBeNull();
    expect(design.queryByRole('button', { name: /Back to/ })).toBeNull();
  });

  it("gives the place its own role through the role editor: the place filled, the id made, the inherited choices carried", async () => {
    await openEngine();
    await userEvent.click(rowOf('design and decisions').getByRole('button', { name: 'Give engine its own… for design and decisions' }));
    const editor = await screen.findByRole('form', { name: 'A new role' });
    expect(within(editor).getByText(/Placed in repository engine; the place is fixed and the id is made for you\./)).toBeTruthy();
    await userEvent.click(within(editor).getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(sent()).toEqual([{
      change: 'set', id: 'build-engine', revision: null,
      role: { name: { en: 'Build · engine', zh: '开发 · engine' }, takes: ['design'], scope: { kind: 'repository', id: 'engine' } },
      choices: [{ agent: 'claude-code', model: 'opus', effort: 'high' }],
    }]));
    await waitFor(() => expect(screen.queryByRole('form', { name: 'A new role' })).toBeNull());
  });

  it("edits a role placed here in the same editor, and says a refusal under it", async () => {
    await openEngine();
    refuse = true;
    await userEvent.click(rowOf('building').getByRole('button', { name: 'Edit, the role taking building' }));
    const editor = await screen.findByRole('form', { name: 'Engine build' });
    await userEvent.click(within(editor).getByRole('button', { name: 'Save' }));
    expect(await within(editor).findByText(/held by another change/)).toBeTruthy();
    expect(sent()).toEqual([expect.objectContaining({ change: 'set', id: 'engine-build', revision: 3 })]);
  });

  it("asks before it goes Back to the parent's, naming the role the work falls back to, and removes only that role", async () => {
    await openEngine();
    await userEvent.click(rowOf('building').getByRole('button', { name: "Back to aurora's, for building" }));
    const ask = await screen.findByRole('group', { name: "Put building back to aurora's" });
    expect(within(ask).getByText(/Engine build goes, and building falls back to Build, placed on this machine\./)).toBeTruthy();
    expect(sent()).toEqual([]);
    await userEvent.click(within(ask).getByRole('button', { name: "Use aurora's" }));
    await waitFor(() => expect(sent()).toEqual([{ change: 'remove', id: 'engine-build', revision: 3 }]));
  });

  it("puts the ask down on Never mind and changes nothing", async () => {
    await openEngine();
    await userEvent.click(rowOf('building').getByRole('button', { name: "Back to aurora's, for building" }));
    await userEvent.click(await screen.findByRole('button', { name: 'Never mind' }));
    await waitFor(() => expect(screen.queryByRole('group', { name: "Put building back to aurora's" })).toBeNull());
    expect(sent()).toEqual([]);
  });

  it("names the machine as a workspace role's parent", async () => {
    chain = (ask) => {
      const answer = CHAIN(ask);
      const works = answer.places[0]!.works.map((each) => (each.work === 'chat'
        ? { ...each, role: { id: 'aurora-chat', name: { en: 'Aurora chat', zh: '极光对话' }, scope: { kind: 'workspace', id: 'aurora' }, revision: 2 } }
        : each));
      return { ...answer, places: [{ ...answer.places[0]!, works }, ...answer.places.slice(1)] };
    };
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /aurora\s*2 driven/ }));
    await screen.findByText('Aurora chat · in workspace aurora');
    expect(asked().at(-1)).toEqual({ workspace: 'aurora' });
    const chat = rowOf('chat');
    expect(chat.getByRole('button', { name: "Back to This machine's, for chat" })).toBeTruthy();
    expect(rowOf('building').getByRole('button', { name: 'Give aurora its own… for building' })).toBeTruthy();
  });

  it("gives this machine's roles an Edit and nothing to go back to", async () => {
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /This machine/ }));
    expect((await screen.findAllByText('Build · on this machine', { selector: 'span' })).length).toBe(2);
    expect(asked().at(-1)).toEqual({});
    expect(screen.queryByRole('button', { name: /Back to/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /its own/ })).toBeNull();
    expect(rowOf('building').getByRole('button', { name: 'Edit, the role taking building' })).toBeTruthy();
  });

  it('holds the editing buttons where this home does not keep its roles', async () => {
    invoke.mockImplementation(async (module: string, type: string, options?: { payload?: { chain?: StructurePlaceAsk } }) => {
      if (module !== 'DAORIS.DRIVER') return WIRING;
      if (type === 'STATE') return { ...DRIVER_STATE, standingConfirmation: STANDING };
      if (type === 'HARNESSES') return ROSTER;
      if (type === 'PLACES') return PLACES({});
      if (type === 'ROLES') return { ...CATALOGUE, kept: false };
      if (type === 'ACTORS') return { actors: [] };
      if (type === 'ROLE_STRUCTURE') return CHAIN(options?.payload?.chain ?? {});
      return undefined;
    });
    await openEngine();
    expect(await screen.findByText(/driver\.json is the file's own|not this home's|the file's own/i)).toBeTruthy();
    expect(rowOf('design and decisions').getByRole('button', { name: 'Give engine its own… for design and decisions' })).toBeDisabled();
    expect(rowOf('building').getByRole('button', { name: "Back to aurora's, for building" })).toBeDisabled();
  });

  it('says a dark appointed role as one quiet line', async () => {
    await openEngine();
    const line = await screen.findByText('Supervisor · Not offered in this build.');
    expect(line).toHaveAttribute('data-appointed-dark');
    expect(screen.queryByRole('heading', { name: 'This Daoris' })).toBeNull();
  });

  it('is in Chinese: the section, the rows and the acts', async () => {
    i18n.changeLanguage('zh');
    show(<Place />);
    await userEvent.click(await screen.findByRole('button', { name: /engine/ }));
    expect(await screen.findByRole('heading', { name: '角色' })).toBeTruthy();
    const design = rowOf('设计与决策');
    expect(design.getByRole('button', { name: '为engine设置专属角色…，设计与决策' })).toBeTruthy();
    expect(rowOf('开发').getByRole('button', { name: '回到aurora的角色，开发' })).toBeTruthy();
  });
});
