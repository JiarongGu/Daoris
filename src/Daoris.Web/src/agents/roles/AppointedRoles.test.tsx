import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// COMPANYVIEW1e (the company-view design §3, §3.2, §5): the Roles mode's Appointed roles section. The steward for This Daoris, the
// supervisor per workspace and per repository wherever Setup drew it, each row the molecule it was (`SupervisorRow`, `WiringSteward`)
// over `SET_APPOINTMENT`. Dark while the build does not offer a role: it draws what its homes drew, and appoints nothing.

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../../locales/i18n';
import { moment } from '../../locales/format';
import { DRIVER_STATE, respond, show } from '../../test/shellHarness';
import { useListPanes } from '../../work/frame/listPanes';
import { ViewFrame } from '../../work/frame/ViewFrame';
import { useAgentsMode, useAgentsView } from '../AgentsView';

const SINCE = '2026-10-25T09:30:00Z';
const OFFERED = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'];

/** The place as the application holds it on a desktop, opened on Roles → Appointed roles. */
function Place() {
  const lists = useListPanes();
  const modes = useAgentsMode(lists);
  const [chosen, setChosen] = useState<string | null>(null);
  const [over, setOver] = useState(false);
  const layout = useAgentsView({ active: true, modes, chosen, onChoose: setChosen, notify: () => {} });
  return <ViewFrame layout={layout} lists={lists} over={over} onOver={setOver} />;
}

const BASE = {
  offered: false, on: false, presses: [], built: ['stop', 'finish', 'hold', 'resume'], unwritten: false, used: 0, held: [],
  appointmentsOffered: true, supervisorOffered: true, stewardOffered: true, stewardActsOffered: OFFERED, stewardActs: [], stewardDesk: [],
  appointments: [], holders: [],
};
const STEWARD = { role: 'steward', scope: 'machine', holder: 'terminal', since: SINCE, dormant: false, answers: 0, holderId: 'h1', acts: OFFERED, setup: false };
const OF_REPOSITORY = {
  role: 'supervisor', scope: 'repository:engine', repository: 'engine', workspace: '', holder: 'terminal', since: SINCE, dormant: false,
  answers: 3, holderId: 'h1', acts: [], setup: false, window: 12,
};
const OF_WORKSPACE = {
  role: 'supervisor', scope: 'workspace:default', repository: '', workspace: 'default', holder: 'terminal', since: SINCE, dormant: false,
  answers: 1, holderId: 'h1', acts: [], setup: false, window: 7,
};

let standing: Record<string, unknown> = BASE;
let drivable = ['engine'];
let answer: unknown = { done: true };

const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SET_APPOINTMENT').map(([, , request]) => request?.payload);
const row = (name: string) => within(screen.getByRole('group', { name }));

async function opened(steward = 'Steward · This Daoris') {
  window.localStorage.setItem('daoris.list.agents.mode', 'setup');
  window.localStorage.setItem('daoris.list.roles.chosen', 'appointed');
  show(<Place />);
  await screen.findByRole('group', { name: steward });
}

describe('Roles → Appointed roles', () => {
  beforeEach(() => {
    standing = BASE;
    drivable = ['engine'];
    answer = { done: true };
    window.localStorage.clear();
    i18n.changeLanguage('en');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (module: string, type: string) => {
      if (module !== 'DAORIS.DRIVER') return undefined;
      if (type === 'SET_APPOINTMENT') return answer;
      if (type === 'STATE') return { ...DRIVER_STATE, drivable, standingConfirmation: standing };
      if (type === 'HARNESSES') return { settingsPath: '', adapter: 'claude-code', harnesses: [] };
      return undefined;
    });
  });
  afterEach(() => {
    cleanup();
    window.localStorage.clear();
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  it('draws the steward for This Daoris, the supervisor of each workspace and of each repository Setup draws', async () => {
    await opened();
    expect(row('Steward · This Daoris').getByText('Not appointed: you make each of these acts yourself.')).toBeTruthy();
    expect(row('Steward · This Daoris').getByRole('button', { name: 'Appoint…' })).toBeTruthy();
    expect(row('Supervisor · workspace default').getByText('Not appointed: every question here waits for you.')).toBeTruthy();
    expect(row('Supervisor · workspace default').getByRole('button', { name: 'Appoint…' })).toBeTruthy();
    expect(row('Supervisor · engine').getByText('Not appointed: every question here waits for you.')).toBeTruthy();
  });

  it('leaves out a repository Setup does not draw: not driven and nothing appointed', async () => {
    drivable = [];
    await opened();
    expect(screen.queryByRole('group', { name: 'Supervisor · engine' })).toBeNull();
    standing = { ...BASE, appointments: [OF_REPOSITORY] };
    cleanup();
    await opened();
    expect(screen.getByRole('group', { name: 'Supervisor · engine' })).toBeTruthy();
  });

  it('says an appointment: since when, its answers, the overrule window, and the steward\'s acts this run', async () => {
    standing = {
      ...BASE, appointments: [STEWARD, OF_REPOSITORY, OF_WORKSPACE],
      stewardActs: [{ id: 'a1', act: 'drive', repository: 'engine', at: '2026-10-25T10:01:00Z', standing: true, waiting: false }],
    };
    await opened();
    expect(row('Steward · This Daoris').getByText(`Appointed since ${moment(SINCE)} · 1 act this run`)).toBeTruthy();
    expect(row('Supervisor · engine').getByText(`Appointed since ${moment(SINCE)}, held by the terminal Daoris started at your appointment · 3 answers this run`)).toBeTruthy();
    expect(row('Supervisor · engine').getByText('Its answers are held 12 minutes for your overrule.')).toBeTruthy();
    expect(row('Supervisor · workspace default').getByText('Its answers are held 7 minutes for your overrule.')).toBeTruthy();
  });

  it('says a repository with none of its own is covered by its workspace\'s', async () => {
    standing = { ...BASE, appointments: [OF_WORKSPACE] };
    await opened();
    expect(row('Supervisor · engine').getByText(/^Covered by the supervisor appointed for default, its answers held 7 minutes/)).toBeTruthy();
  });

  it('says one found as Daoris started dormant, and offers Appoint again', async () => {
    standing = { ...BASE, appointments: [{ ...STEWARD, dormant: true, holderId: null }, { ...OF_WORKSPACE, dormant: true, holderId: null }] };
    await opened();
    expect(row('Steward · This Daoris').getByText('Found as Daoris started: dormant until you press Appoint again.')).toBeTruthy();
    expect(row('Steward · This Daoris').getByRole('button', { name: 'Appoint again…' })).toBeTruthy();
    expect(row('Supervisor · workspace default').getByText('Found as Daoris started: dormant until you press Appoint again.')).toBeTruthy();
    expect(row('Supervisor · workspace default').getByRole('button', { name: 'Appoint again…' })).toBeTruthy();
  });

  it('draws both roles dark while the build does not offer them: said so, and nothing to press', async () => {
    standing = { ...BASE, supervisorOffered: false, stewardOffered: false };
    await opened();
    expect(row('Steward · This Daoris').getByText('Not offered in this build.')).toBeTruthy();
    expect(row('Steward · This Daoris').queryByRole('button')).toBeNull();
    expect(row('Supervisor · engine').getByText(/^Not offered yet/)).toBeTruthy();
    expect(row('Supervisor · engine').queryByRole('button')).toBeNull();
    expect(row('Supervisor · workspace default').queryByRole('button')).toBeNull();
    expect(sent()).toEqual([]);
  });

  it('sends the supervisor\'s Appoint from the owner\'s press: the scope, the window, nothing before the press', async () => {
    await opened();
    await userEvent.click(row('Supervisor · engine').getByRole('button', { name: 'Appoint…' }));
    expect(sent()).toEqual([]);
    await userEvent.click(within(screen.getByRole('group', { name: 'appoint the supervisor for engine' })).getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'supervisor', scope: 'repository:engine', holder: 'terminal', on: true, window: 5 }]));
  });

  it('sends the workspace supervisor\'s Appoint and Turn off', async () => {
    await opened();
    await userEvent.click(row('Supervisor · workspace default').getByRole('button', { name: 'Appoint…' }));
    await userEvent.click(within(screen.getByRole('group', { name: 'appoint the supervisor for default' })).getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'supervisor', scope: 'workspace:default', holder: 'terminal', on: true, window: 5 }]));
  });

  it('sends Turn off at once and asks nothing, a refusal said under the row', async () => {
    standing = { ...BASE, appointments: [OF_REPOSITORY] };
    answer = { done: false, why: 'unwritten', message: 'Not written.' };
    await opened();
    await userEvent.click(row('Supervisor · engine').getByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'supervisor', scope: 'repository:engine', holder: 'terminal', on: false }]));
    expect(await row('Supervisor · engine').findByRole('alert')).toBeTruthy();
    expect(screen.queryByRole('group', { name: 'appoint the supervisor for engine' })).toBeNull();
  });

  it('sends the steward\'s Appoint with its set-up tick off, and Turn off', async () => {
    await opened();
    await userEvent.click(row('Steward · This Daoris').getByRole('button', { name: 'Appoint…' }));
    const ask = within(screen.getByRole('group', { name: 'appoint the steward' }));
    expect(ask.getByRole('checkbox', { name: /^And set repositories up/ }).getAttribute('aria-checked')).toBe('false');
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: true, setup: false }]));
  });

  it('sends the steward\'s Turn off', async () => {
    standing = { ...BASE, appointments: [STEWARD] };
    await opened();
    await userEvent.click(row('Steward · This Daoris').getByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: false }]));
  });

  it('says a refused Appoint inside its ask, in the catalogue\'s words', async () => {
    answer = { done: false, why: 'not-offered', message: 'Not yet.' };
    await opened();
    await userEvent.click(row('Steward · This Daoris').getByRole('button', { name: 'Appoint…' }));
    const ask = within(screen.getByRole('group', { name: 'appoint the steward' }));
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    expect((await ask.findByRole('alert')).textContent).toBe("This Daoris does not offer the steward's appointment yet.");
  });

  it('opens the steward\'s ask with a line per offered act, the six it never does, its bounds and holder', async () => {
    standing = { ...BASE, stewardActsOffered: ['drive', 'hold'] };
    await opened();
    await userEvent.click(row('Steward · This Daoris').getByRole('button', { name: 'Appoint…' }));
    const ask = within(screen.getByRole('group', { name: 'appoint the steward' }));
    expect(ask.getByText('The steward, for every repository registered on this machine, until Daoris restarts.')).toBeTruthy();
    expect(ask.getByText('Hold a repository.')).toBeTruthy();
    expect(ask.queryByText('Stop driving a repository.')).toBeNull();
    for (const line of ['A production or live go-ahead.', 'A sign-in or an account.', 'Widening any access.', 'Deleting your data.']) {
      expect(ask.getByText(line)).toBeTruthy();
    }
    expect(ask.getByText(/lasts until Daoris restarts/)).toBeTruthy();
    expect(ask.queryByRole('radio')).toBeNull();
    await userEvent.click(ask.getByRole('checkbox', { name: /^And set repositories up/ }));
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: true, setup: true }]));
  });

  it('offers adding a role to the living terminal or one of its own, neither preselected, and sends the choice', async () => {
    standing = { ...BASE, holders: [{ id: 'h1', since: SINCE, roles: ['supervisor'] }] };
    await opened();
    await userEvent.click(row('Steward · This Daoris').getByRole('button', { name: 'Appoint…' }));
    const ask = within(screen.getByRole('group', { name: 'appoint the steward' }));
    const add = ask.getByRole('radio', { name: `Add to the terminal Daoris started at ${moment(SINCE)}` });
    expect((ask.getByRole('button', { name: 'Appoint' }) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(add);
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: true, share: 'h1', setup: false }]));
  });

  it('opens the ordinary ask on Appoint again, never the record\'s acts nor its tick', async () => {
    standing = { ...BASE, appointments: [{ ...STEWARD, dormant: true, holderId: null, acts: ['drive'], setup: true }] };
    await opened();
    await userEvent.click(row('Steward · This Daoris').getByRole('button', { name: 'Appoint again…' }));
    const ask = within(screen.getByRole('group', { name: 'appoint the steward' }));
    expect(ask.getByText('Try a quest again whose session it stopped itself.')).toBeTruthy();
    expect(ask.getByRole('checkbox', { name: /^And set repositories up/ }).getAttribute('aria-checked')).toBe('false');
  });

  it('says the unreadable appointments instead of drawing none', async () => {
    standing = null as unknown as Record<string, unknown>;
    window.localStorage.setItem('daoris.list.agents.mode', 'setup');
    window.localStorage.setItem('daoris.list.roles.chosen', 'appointed');
    show(<Place />);
    expect(await screen.findByText('The appointments could not be read here.')).toBeTruthy();
  });

  it('says its rows in Chinese', async () => {
    await i18n.changeLanguage('zh');
    standing = { ...BASE, appointments: [STEWARD] };
    await opened('管家 · 本机');
    expect(row('管家 · 本机').getByRole('button', { name: '停用' })).toBeTruthy();
    expect(row('主管 · 工作区 default').getByRole('button', { name: '任命…' })).toBeTruthy();
  });
});
