import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → This Daoris → *Roles* (STEWARDROLE1b, D209; COMPANYVIEW1e): where the build offers the steward, D201's standing
// confirmation's row is renamed *Roles* and holds the steward's line: where it stands in one sentence, and a door to Agents → Roles →
// Appointed roles, where it is appointed (the ask and its presses are that section's: `agents/roles/AppointedRoles.test.tsx`).

const { invoke, notifyReady } = vi.hoisted(() => ({ invoke: vi.fn(), notifyReady: vi.fn(() => Promise.resolve()) }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: true, bridge: { notifyReady } }),
  useShenoraEvent: () => undefined,
}));

import { SettingsView } from './SettingsView';
import i18n from '../locales/i18n';
import { moment } from '../locales/format';
import { DRIVER_STATE, respond, show, WIRING } from '../test/shellHarness';
import { SPACE, WIRED } from './wiring/wiringFixtures';

const NO_UPDATE = { state: 'none', staged: null, mode: null, driven: 0, turns: 0, problem: null, outcome: null, last: null };
const OFFERED = ['drive', 'undrive', 'trees', 'hold', 'resume', 'stop', 'finish', 'retry'];
const SINCE = '2026-10-25T09:30:00Z';

/** The shell, its driver's state carrying `standing` as `standingConfirmation`. */
const shell = (standing: Record<string, unknown>) =>
  async (module: string, type: string) => {
    if (module === 'DAORIS.UPDATE') return NO_UPDATE;
    if (module !== 'DAORIS.DRIVER') return WIRING;
    if (type === 'SYSTEM_WIRING') return WIRED;
    if (type === 'SYSTEM_SPACE') return SPACE;
    return { ...DRIVER_STATE, standingConfirmation: standing };
  };

/** D201's grant not offered, as this design keeps it; the steward offered, as the page tests set it. */
const OFF = {
  offered: false, on: false, presses: [], built: ['stop', 'finish', 'hold', 'resume'], unwritten: false, used: 0, held: [],
  appointmentsOffered: true, supervisorOffered: false, stewardOffered: true, stewardActsOffered: OFFERED, stewardActs: [], stewardDesk: [],
  appointments: [], holders: [],
};
const STEWARD = { role: 'steward', scope: 'machine', holder: 'terminal', since: SINCE, dormant: false, answers: 0, holderId: 'h1', acts: OFFERED, setup: false };
const APPOINTED = {
  ...OFF,
  appointments: [STEWARD],
  stewardActs: [
    { id: 'a2', act: 'hold', repository: 'engine', at: '2026-10-25T10:02:00Z', standing: true, waiting: false },
    { id: 'a1', act: 'drive', repository: 'engine', at: '2026-10-25T10:01:00Z', standing: true, waiting: false },
  ],
};

const terminal = async () => within(await screen.findByRole('region', { name: /^(Terminal|终端)$/ }));
const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SET_APPOINTMENT');

describe('This Daoris → Terminal → Roles', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it("keeps D201's row as it was where the build does not offer the steward", async () => {
    invoke.mockImplementation(shell({ ...OFF, stewardOffered: false }));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(await card.findByText('Standing confirmation')).toBeTruthy();
    expect(card.queryByText('Roles')).toBeNull();
    expect(card.queryByText('Steward')).toBeNull();
  });

  it("renames the row Roles, holding the steward's one line and a door, and no Appoint", async () => {
    invoke.mockImplementation(shell(OFF));
    const onGo = vi.fn();
    show(<SettingsView notify={() => {}} section="wiring" onGo={onGo} />);

    const card = await terminal();
    expect(await card.findByText('Roles')).toBeTruthy();
    expect(card.queryByText('Standing confirmation')).toBeNull();
    // D201's own grant stays not offered, and is not said as a missing feature under the roles.
    expect(card.queryByText(/^Not offered yet/)).toBeNull();
    const steward = within(card.getByRole('group', { name: 'Steward' }));
    expect(steward.getByText('Not appointed: you make each of these acts yourself.')).toBeTruthy();
    expect(steward.queryByRole('button', { name: /^Appoint/ })).toBeNull();
    expect(steward.queryByRole('button', { name: 'Turn off' })).toBeNull();

    await userEvent.click(steward.getByRole('button', { name: 'Open Appointed roles' }));
    expect(onGo).toHaveBeenCalledWith({ view: 'agents', roleSection: 'appointed' });
    expect(sent()).toEqual([]);
  });

  it('says an appointment since when and its acts this run', async () => {
    invoke.mockImplementation(shell(APPOINTED));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    expect(within(await card.findByRole('group', { name: 'Steward' })).getByText(`Appointed since ${moment(SINCE)} · 2 acts this run`)).toBeTruthy();
  });

  it('says one found as Daoris started dormant', async () => {
    invoke.mockImplementation(shell({ ...OFF, appointments: [{ ...STEWARD, dormant: true, holderId: null }] }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    expect(within(await card.findByRole('group', { name: 'Steward' })).getByText('Found as Daoris started: dormant until you press Appoint again.')).toBeTruthy();
  });

  it("says the line and its door in Chinese, in the glossary's words", async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(APPOINTED));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    const steward = within(await card.findByRole('group', { name: '管家' }));
    expect(steward.getByText(`自 ${moment(SINCE, 'zh')} 起任命 · 本次运行 2 项操作`)).toBeTruthy();
    expect(steward.getByRole('button', { name: '打开“任命的角色”' })).toBeTruthy();
  });
});
