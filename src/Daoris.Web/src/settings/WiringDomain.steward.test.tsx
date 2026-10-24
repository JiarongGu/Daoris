import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Settings → This Daoris → *Roles* (STEWARDROLE1b, D209; the steward design §5.3, §2.3): where the build offers the steward, D201's
// standing confirmation's row is renamed *Roles* and holds the steward's row: not appointed, appointed since when, or dormant; *Appoint…*
// or *Appoint again…*, and **Turn off**. *Appoint…* opens the inline ask: one line per act the build offers, the six it never does,
// its bounds, its holder and what that does not close, its record, its end at a restart, the set-up tick always shown off, and the
// choice of terminal while a holder lives. *Appoint* sends `SET_APPOINTMENT` for the machine; *Turn off* sends `on: false`.

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
const shell = (standing: Record<string, unknown>, answer: unknown = { done: true }) =>
  async (module: string, type: string) => {
    if (module === 'DAORIS.UPDATE') return NO_UPDATE;
    if (module !== 'DAORIS.DRIVER') return WIRING;
    if (type === 'SYSTEM_WIRING') return WIRED;
    if (type === 'SYSTEM_SPACE') return SPACE;
    if (type === 'SET_APPOINTMENT') return answer;
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
const sent = () => invoke.mock.calls.filter(([, type]) => type === 'SET_APPOINTMENT').map(([, , request]) => request?.payload);
const ASK = 'appoint the steward';

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

  it("renames the row Roles, holding the steward's: not appointed, with Appoint…", async () => {
    invoke.mockImplementation(shell(OFF));
    show(<SettingsView notify={() => {}} section="wiring" />);

    const card = await terminal();
    expect(await card.findByText('Roles')).toBeTruthy();
    expect(card.queryByText('Standing confirmation')).toBeNull();
    // D201's own grant stays not offered, and is not said as a missing feature under the roles.
    expect(card.queryByText(/^Not offered yet/)).toBeNull();
    const steward = within(card.getByRole('group', { name: 'Steward' }));
    expect(steward.getByText('Not appointed: you make each of these acts yourself.')).toBeTruthy();
    expect(steward.getByRole('button', { name: 'Appoint…' })).toBeTruthy();
    expect(steward.queryByRole('button', { name: 'Turn off' })).toBeNull();
  });

  it('opens the ask with a line per offered act, the six it never does, its bounds and holder, the set-up tick off; Appoint sends it', async () => {
    invoke.mockImplementation(shell(OFF));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Appoint…' }));
    expect(sent()).toEqual([]);

    const ask = within(card.getByRole('group', { name: ASK }));
    expect(ask.getByText('The steward, for every repository registered on this machine, until Daoris restarts.')).toBeTruthy();
    for (const line of [
      'Drive a repository, so Daoris starts its quests within the cap you set.',
      'Stop driving a repository.',
      "Give a repository's sessions trees of their own; never take them away.",
      'Hold a repository.',
      'Resume a repository it held itself.',
      "Stop a quest's session; its Try again or yours carries it on.",
      "Finish a quest's session that waits with nothing said to it, without accepting or closing its quest.",
      'Try a quest again whose session it stopped itself.',
    ]) expect(ask.getByText(line)).toBeTruthy();
    for (const line of [
      'A production or live go-ahead.',
      'A sign-in or an account.',
      'Widening any access.',
      'A push or a publication, or accepting what is delivered: closing a quest as done stays yours.',
      "Spending beyond development's own runs.",
      'Deleting your data.',
    ]) expect(ask.getByText(line)).toBeTruthy();
    expect(ask.getByText(/drives or gives trees only to a repository whose landing does not push, and never stops or finishes a session where that would publish/)).toBeTruthy();
    expect(ask.getByText(/drives only a repository whose sessions open trees of their own, so Daoris's sessions never work in your checkout by its act/)).toBeTruthy();
    expect(ask.getByText(/A repository or quest you act on after Daoris starts stays yours until it restarts/)).toBeTruthy();
    expect(ask.getByText(/includes one that any program of your account registers/)).toBeTruthy();
    expect(ask.getByText(/Daoris's own pinned Claude Code, started by Daoris on its own configuration/)).toBeTruthy();
    expect(ask.getByText(/starts signed out: sign in once in its tab/)).toBeTruthy();
    expect(ask.getByText(/a program of your account that rewrites that Claude Code, or reaches into its session, can act as the steward/)).toBeTruthy();
    expect(ask.getByText(/recorded as the steward's, never as yours, and said in Notifications/)).toBeTruthy();
    expect(ask.getByText(/lasts until Daoris restarts/)).toBeTruthy();
    // No holder lives: no choice of terminal.
    expect(ask.queryByRole('radio')).toBeNull();
    const tick = ask.getByRole('checkbox', { name: "And set repositories up: adds nine exact doctrine commands to a repository's allowed rules" });
    expect(tick.getAttribute('aria-checked')).toBe('false');

    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: true, setup: false }]));
  });

  it('names only the acts the build offers, and sends the set-up tick when the owner ticks it', async () => {
    invoke.mockImplementation(shell({ ...OFF, stewardActsOffered: ['drive', 'hold'] }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Appoint…' }));
    const ask = within(card.getByRole('group', { name: ASK }));
    expect(ask.getByText('Hold a repository.')).toBeTruthy();
    expect(ask.queryByText('Stop driving a repository.')).toBeNull();
    expect(ask.queryByText(/^Finish a quest's session/)).toBeNull();

    await userEvent.click(ask.getByRole('checkbox', { name: /^And set repositories up/ }));
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: true, setup: true }]));
  });

  it('says an appointment since when and its acts this run, and Turn off sends it off and asks nothing', async () => {
    invoke.mockImplementation(shell(APPOINTED));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    const steward = within(await card.findByRole('group', { name: 'Steward' }));
    expect(steward.getByText(`Appointed since ${moment(SINCE)} · 2 acts this run`)).toBeTruthy();
    expect(steward.queryByRole('button', { name: /^Appoint/ })).toBeNull();
    await userEvent.click(steward.getByRole('button', { name: 'Turn off' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: false }]));
    expect(card.queryByRole('group', { name: ASK })).toBeNull();
  });

  it("says one found as Daoris started dormant; Appoint again opens the ordinary ask, never the record's acts nor its tick", async () => {
    invoke.mockImplementation(shell({
      ...OFF, appointments: [{ ...STEWARD, dormant: true, holderId: null, acts: ['drive'], setup: true }],
    }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    const steward = within(await card.findByRole('group', { name: 'Steward' }));
    expect(steward.getByText('Found as Daoris started: dormant until you press Appoint again.')).toBeTruthy();
    expect(steward.getByRole('button', { name: 'Turn off' })).toBeTruthy();
    await userEvent.click(steward.getByRole('button', { name: 'Appoint again…' }));
    expect(sent()).toEqual([]);
    const ask = within(card.getByRole('group', { name: ASK }));
    expect(ask.getByText('Stop driving a repository.')).toBeTruthy();
    expect(ask.getByText('Try a quest again whose session it stopped itself.')).toBeTruthy();
    expect(ask.getByRole('checkbox', { name: /^And set repositories up/ }).getAttribute('aria-checked')).toBe('false');
  });

  it('offers adding the steward to the living terminal or one of its own, neither preselected, and sends the choice', async () => {
    invoke.mockImplementation(shell({ ...OFF, holders: [{ id: 'h1', since: SINCE, roles: ['supervisor'] }] }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Appoint…' }));
    const ask = within(card.getByRole('group', { name: ASK }));

    const add = ask.getByRole('radio', { name: `Add to the terminal Daoris started at ${moment(SINCE)}` });
    const own = ask.getByRole('radio', { name: 'A terminal of its own' });
    expect(add.getAttribute('aria-checked') ?? String((add as HTMLInputElement).checked)).toBe('false');
    expect(own.getAttribute('aria-checked') ?? String((own as HTMLInputElement).checked)).toBe('false');
    expect((ask.getByRole('button', { name: 'Appoint' }) as HTMLButtonElement).disabled).toBe(true);

    await userEvent.click(add);
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    await vi.waitFor(() => expect(sent()).toEqual([{ role: 'steward', scope: 'machine', holder: 'terminal', on: true, share: 'h1', setup: false }]));
  });

  it("says an Appoint refused inside the ask, in the catalogue's words", async () => {
    invoke.mockImplementation(shell(OFF, { done: false, why: 'not-offered', message: 'Not yet.' }));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    await userEvent.click(await card.findByRole('button', { name: 'Appoint…' }));
    const ask = within(card.getByRole('group', { name: ASK }));
    await userEvent.click(ask.getByRole('button', { name: 'Appoint' }));
    expect((await ask.findByRole('alert')).textContent).toBe("This Daoris does not offer the steward's appointment yet.");
  });

  it("says the row and its ask in Chinese, in the glossary's words", async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(OFF));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();

    expect(await card.findByText('角色')).toBeTruthy();
    const steward = within(card.getByRole('group', { name: '管家' }));
    expect(steward.getByText('未任命：这些操作都由你自己来做。')).toBeTruthy();
    await userEvent.click(steward.getByRole('button', { name: '任命…' }));
    const ask = card.getByRole('group', { name: '任命管家' });
    const text = ask.textContent ?? '';
    expect(text).toContain('管家，负责本机注册的每个仓库，直到 Daoris 重启。');
    expect(text).toContain('停止一个委托的会话；管家或你的「重试」会让它继续。');
    expect(text).toContain('推送或发布，或采纳交付的结果：把委托标为完成仍由你来做。');
    expect(text).not.toMatch(/这台机器|渠道|总管|任务|请求|\{\{|undefined/);
    expect(within(ask).getByRole('checkbox', { name: /^并配置仓库/ }).getAttribute('aria-checked')).toBe('false');
    expect(within(ask).getByRole('button', { name: '任命' })).toBeTruthy();
  });

  it('says an appointment in Chinese', async () => {
    await i18n.changeLanguage('zh');
    invoke.mockImplementation(shell(APPOINTED));
    show(<SettingsView notify={() => {}} section="wiring" />);
    const card = await terminal();
    const steward = within(await card.findByRole('group', { name: '管家' }));
    expect(steward.getByText(`自 ${moment(SINCE, 'zh')} 起任命 · 本次运行 2 项操作`)).toBeTruthy();
    expect(steward.getByRole('button', { name: '停用' })).toBeTruthy();
  });
});
