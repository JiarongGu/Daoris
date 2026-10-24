import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactNode, useRef } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Drawer } from '../design/ui';
import i18n from '../locales/i18n';
import { watchScreen } from '../test/onScreen';
import {
  AdoptionNotice, type Confirmation, type ConfirmationAnswer, Confirmations, planDoor, type PlanDoor, PressArea, type PressNames,
  regionShown, routeKey,
} from './Confirmations';

// The window's confirmation of a terminal's press (PERSONDOOR1f, RPC1c; D156 point 4, the person-door design §4.2), as NOTIFY2b
// (D196 point 4, the notification design §3) draws it in *Notifications*: in plain words that keep what tells one session and one
// file from another, every value in the card's view, the route's name under *Details*; its decision region bounded, and *shown*
// once that region is in view in a window in front of the person. A terminal's press waits until the person answers
// (CONFIRMWAIT1a-i): its card says so and whether a terminal still waits on it; its Confirm is judged before it runs.

const SAY: Confirmation = {
  source: 'pipe', id: 'c1', module: 'DAORIS.DRIVER', route: 'SESSION_INPUT',
  fields: [
    { name: '/id', type: 'string', value: '3f2a91c4' }, { name: '/text', type: 'string', value: 'Run the database tests before you land.' },
    { name: '/level', type: 'string', value: 'urgent' }, { name: '/said', type: 'boolean', value: 'true' },
    { name: '/paths/0', type: 'string', value: 'src/feed/export.ts' }, { name: '/paths/1', type: 'string', value: 'test/feed/export.ts' },
    { name: '/key', type: 'string', value: 'k-1' },
  ],
  named: [
    { role: 'session', name: '/id', value: '3f2a91c4' }, { role: 'words', name: '/text', value: 'Run the database tests before you land.' },
    { role: 'level', name: '/level', value: 'urgent' }, { role: 'files', name: '/paths/0', value: 'src/feed/export.ts' },
    { role: 'files', name: '/paths/1', value: 'test/feed/export.ts' }, { role: 'form', name: '/said', value: 'true' },
  ],
  asker: { process: 'bash', pid: 4120 }, asked: new Date(Date.now() - 60_000).toISOString(), expires: null, waitedOn: true,
};
const STOP: Confirmation = {
  source: 'pipe', id: 'c2', module: 'DAORIS.DRIVER', route: 'STOP_SESSION',
  fields: [{ name: '/id', type: 'string', value: '9b1e0d2a' }, { name: '/said', type: 'boolean', value: 'true' }],
  named: [{ role: 'session', name: '/id', value: '9b1e0d2a' }],
  asker: { process: null, pid: 77 }, expires: null,
};
const UNKNOWN: Confirmation = {
  source: 'pipe', id: 'c3', module: 'DAORIS.DRIVER', route: 'SET_MYSTERY', fields: [{ name: '/keep', type: 'number', value: '3' }],
  asker: { process: null, pid: 77 }, expires: null,
};
const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict",
  fields: [{ name: '/verdict', type: 'string', value: 'reviewed' }], asker: null, expires: '2026-10-20T09:04:00Z',
};

const SESSIONS: Record<string, { title: string; repository: string; workspace?: string }> = {
  '3f2a91c4': { title: 'Fix the export feed', repository: 'game' },
  '9b1e0d2a': { title: 'Fix the export feed', repository: 'game' },
  '7c7c7c7c': { title: 'Conversation', repository: 'engine' },
  '1d1d1d1d': { title: 'Fix the export feed', repository: 'tools', workspace: 'studio' },
};
const NAMES: PressNames = { session: (id) => SESSIONS[id] ?? null };

const draw = (waiting: Confirmation[], answer: (waiting: Confirmation, confirm: boolean) => Promise<ConfirmationAnswer> = vi.fn(async () => ({ answered: true as const }))) => {
  const notify = vi.fn();
  const view = render(<Confirmations waiting={waiting} onAnswer={answer} notify={notify} names={NAMES} />);
  return { ...view, answer, notify };
};

describe("the window's confirmation of a terminal's press", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws nothing when nothing waits', () => {
    expect(draw([]).container).toBeEmptyDOMElement();
  });

  it('says sessions say in plain words: the session with its repository and short id, the words quoted, each path whole', () => {
    draw([SAY]);

    const card = screen.getByRole('group');
    expect(card).toHaveTextContent('A terminal asks to say to Fix the export feed (game · 3f2a91c4), as your words, at its next step:');
    expect(card).toHaveTextContent('“Run the database tests before you land.”');
    expect(card).toHaveTextContent('and to attach these files, read from this machine:');
    expect(within(card).getByText('src/feed/export.ts')).toBeInTheDocument();
    expect(within(card).getByText('test/feed/export.ts')).toBeInTheDocument();
    // Every value no role names is listed under the sentence, never left out of the card's view.
    const also = within(card).getByLabelText('It also carries');
    expect(within(also).getByText('/key')).toBeInTheDocument();
    expect(within(also).getByText('"k-1"')).toBeInTheDocument();
    expect(within(also).queryByText('/id')).toBeNull();
    // The route's name is never the sentence, and is said only under Details.
    expect(card).not.toHaveTextContent('SESSION_INPUT');
    expect(card).toHaveTextContent('Sent by bash, process 4120. · asked 1m ago');
    expect(card).toHaveTextContent('Daoris cannot tell whether you ran this or an agent did. Confirm only what you ran.');
  });

  it('opens the route and every value raw under Details', async () => {
    draw([SAY]);

    await userEvent.click(screen.getByRole('button', { name: 'Details' }));
    const card = screen.getByRole('group');
    expect(card).toHaveTextContent('DAORIS.DRIVER.SESSION_INPUT');
    const raw = within(card).getByLabelText('What it carries');
    expect(within(raw).getByText('/id')).toBeInTheDocument();
    expect(within(raw).getByText('true')).toBeInTheDocument();
  });

  it('tells apart two sessions sharing a title, a repository in two workspaces, and a conversation by its id', () => {
    draw([
      { ...STOP, id: 'a', named: [{ role: 'session', name: '/id', value: '3f2a91c4' }] },
      { ...STOP, id: 'b', named: [{ role: 'session', name: '/id', value: '9b1e0d2a' }] },
      { ...STOP, id: 'c', named: [{ role: 'session', name: '/id', value: '1d1d1d1d' }] },
      { ...STOP, id: 'd', named: [{ role: 'session', name: '/id', value: '7c7c7c7c' }] },
      { ...STOP, id: 'e', named: [{ role: 'session', name: '/id', value: 'ffffffff' }] },
    ]);

    const [first, second, other, conversation, none] = screen.getAllByRole('group');
    expect(first).toHaveTextContent('A terminal asks to stop a session:');
    expect(first).toHaveTextContent('Fix the export feed (game · 3f2a91c4)');
    expect(second).toHaveTextContent('Fix the export feed (game · 9b1e0d2a)');
    expect(other).toHaveTextContent('Fix the export feed (studio · tools · 1d1d1d1d)');
    expect(conversation).toHaveTextContent('Conversation (engine · 7c7c7c7c)');
    expect(none).toHaveTextContent('ffffffff: Daoris has no session by this id');
  });

  it("names any other press by its act, a service's by its door's own act, and one no catalogue words by its route", () => {
    draw([UNKNOWN, SERVICE]);

    const [unknown, service] = screen.getAllByRole('group');
    expect(unknown).toHaveTextContent('A terminal asks for SET_MYSTERY.');
    expect(within(unknown).getByText('3')).toBeInTheDocument();
    expect(service).toHaveTextContent("A terminal asks for a review's verdict.");
    expect(service).toHaveTextContent('Daoris could not name what sent it.');
  });

  it('keys a route by its module: the driver by its route, another module by both', () => {
    expect(routeKey('DAORIS.DRIVER', 'SESSION_INPUT')).toBe('person.route.SESSION_INPUT');
    expect(routeKey('DAORIS.REMOTES', 'SET')).toBe('person.route.REMOTES.SET');
    expect(i18n.t(routeKey('DAORIS.REMOTES', 'SET'))).toBe('wire a workspace to a remote');
  });

  it('bounds the region whatever the press carries: long words and 256 values each scroll inside a box of eight lines', () => {
    const many: Confirmation = {
      ...UNKNOWN, id: 'many',
      fields: Array.from({ length: 256 }, (_, index) => ({ name: `/v/${index}`, type: 'number', value: String(index) })),
    };
    const long: Confirmation = {
      ...SAY, id: 'long', named: [{ role: 'session', name: '/id', value: '3f2a91c4' }, { role: 'words', name: '/text', value: 'word '.repeat(4000) }],
    };
    draw([many, long]);

    const [values, words] = screen.getAllByRole('group');
    const box = within(values).getByLabelText('It also carries');
    expect(box.className).toContain('max-h-[8lh]');
    expect(box.className).toContain('overflow-y-auto');
    expect(words.querySelector('blockquote')!.className).toContain('max-h-[8lh]');
  });

  it('answers Confirm and Refuse by the ask, and offers neither while one is under way', async () => {
    let settle: (value: { answered: true }) => void = () => {};
    const answer = vi.fn(() => new Promise<{ answered: true }>((resolve) => { settle = resolve; }));
    draw([STOP], answer);

    await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(answer).toHaveBeenCalledWith(STOP, true);
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Refuse' })).toBeDisabled();
    settle({ answered: true });
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Refuse' })).toBeEnabled());
    await userEvent.click(screen.getByRole('button', { name: 'Refuse' }));
    expect(answer).toHaveBeenLastCalledWith(STOP, false);
  });

  it('says where it was pressed that one no longer waits, and a refusal in its own sentence', async () => {
    const answer = vi.fn()
      .mockResolvedValueOnce({ answered: false, why: 'gone', message: 'the driver said so.' })
      .mockResolvedValueOnce({ answered: false, why: null, message: 'This confirmation no longer waits: it expired.' });
    const { notify } = draw([STOP, SERVICE], answer);
    const [stop, service] = screen.getAllByRole('group');

    await userEvent.click(within(stop).getByRole('button', { name: 'Confirm' }));
    expect(await within(stop).findByRole('alert')).toHaveTextContent(
      'This no longer waits: it was answered in another window, or its terminal took it back.');
    await userEvent.click(within(service).getByRole('button', { name: 'Refuse' }));
    expect(await within(service).findByRole('alert')).toHaveTextContent('This confirmation no longer waits: it expired.');
    expect(notify).not.toHaveBeenCalled();
  });

  // NOTIFY2a (D196 point 4, the notification design §3.4): a Confirm past its time finds its card gone, so its refusal is toasted.
  it.each([
    ['en', 'Confirm', 'This no longer waits: it was answered in another window, or its terminal took it back.'],
    ['zh', '确认', '此请求已不再等待：它已在另一个窗口中得到回答，或其终端已将它撤回。'],
  ])('toasts a refusal whose card has gone with its press (%s)', async (language, confirm, words) => {
    await i18n.changeLanguage(language);
    let settle: (value: ConfirmationAnswer) => void = () => {};
    const answer = vi.fn(() => new Promise<ConfirmationAnswer>((resolve) => { settle = resolve; }));
    const notify = vi.fn();
    const { rerender } = render(<Confirmations waiting={[STOP]} onAnswer={answer} notify={notify} />);

    await userEvent.click(screen.getByRole('button', { name: confirm }));
    rerender(<Confirmations waiting={[]} onAnswer={answer} notify={notify} />);
    expect(screen.queryByRole('group')).toBeNull();
    settle({ answered: false, why: 'gone', message: 'the driver said so.' });

    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(words, 'error'));
    expect(notify).toHaveBeenCalledTimes(1);
  });

  // CONFIRMWAIT1 (the confirm-wait design §15.3 item 11): the shell takes a changed press's card as its answer settles, so the removal can
  // come before PERSON_CONFIRM answers; the reason is then toasted, in the reader's words, never lost.
  it.each([
    ['en', 'Confirm', 'Not done: the branch quest/x gained or lost commits while this waited.'],
    ['zh', '确认', '未执行：等待期间分支 quest/x 的提交已有增减。'],
  ])('toasts a changed reason whose card was taken before its answer came (%s)', async (language, confirm, words) => {
    await i18n.changeLanguage(language);
    let settle: (value: ConfirmationAnswer) => void = () => {};
    const answer = vi.fn(() => new Promise<ConfirmationAnswer>((resolve) => { settle = resolve; }));
    const notify = vi.fn();
    const { rerender } = render(<Confirmations waiting={[STOP]} onAnswer={answer} notify={notify} />);

    await userEvent.click(screen.getByRole('button', { name: confirm }));
    rerender(<Confirmations waiting={[]} onAnswer={answer} notify={notify} />);
    settle({ answered: false, why: 'changed', message: 'Not done.', reason: 'branch', values: { branch: 'quest/x' } });

    await vi.waitFor(() => expect(notify).toHaveBeenCalledWith(words, 'error'));
    expect(notify).toHaveBeenCalledTimes(1);
  });

  it("says a service's time left, stepping, and a terminal's press that it waits until the person answers", () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    try {
      vi.setSystemTime(new Date('2026-10-21T09:00:00Z'));
      draw([{ ...SERVICE, expires: '2026-10-21T09:01:40Z' }, { ...STOP, id: 'u', shown: true, waitedOn: true }, { ...STOP, id: 'w', waitedOn: false }]);
      const [service, waited, left] = screen.getAllByRole('group');
      expect(service).toHaveTextContent('refused by itself in 1m 40s');
      act(() => { vi.advanceTimersByTime(10_000); });
      expect(service).toHaveTextContent('refused by itself in 1m 30s');
      // CONFIRMWAIT1a-i: no time is drawn for a terminal's press, shown or not; whether a terminal waits on it is.
      expect(waited).toHaveTextContent('It waits until you answer. A terminal waits on this.');
      expect(waited).not.toHaveTextContent('refused by itself');
      expect(left).toHaveTextContent('It waits until you answer. No terminal waits on this now; it can read how it ended later.');
    } finally {
      vi.useRealTimers();
    }
  });

  it('says the age of a press that has waited over an hour first, in weight', () => {
    draw([{ ...STOP, id: 'old', asked: new Date(Date.now() - 3 * 3_600_000).toISOString() }, { ...STOP, id: 'new', asked: new Date().toISOString() }]);

    const [old, fresh] = screen.getAllByRole('group');
    expect(within(old).getByText(/asked 3h ago/).className).toContain('font-semibold');
    expect(within(fresh).getByText(/asked/).className).not.toContain('font-semibold');
  });

  it('replaces its buttons while its Confirm is judged, and gives them back', () => {
    const { rerender, answer, notify } = draw([{ ...STOP, checking: true }]);

    expect(screen.getByRole('status')).toHaveTextContent('Checking that nothing changed…');
    expect(screen.queryByRole('button', { name: 'Confirm' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Refuse' })).toBeNull();
    rerender(<Confirmations waiting={[STOP]} onAnswer={answer} notify={notify} names={NAMES} />);
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeEnabled();
  });

  // The design's stale row (§10): the reason a moved ground gives, said on its card in the reader's language, its states worded.
  it.each([
    ['en', 'Confirm', { reason: 'session', values: { then: 'awaiting-person', now: 'completed' } },
      'Not done: the session moved on while this waited (it was waiting on you, it is completed).'],
    ['en', 'Confirm', { reason: 'quest', values: { then: 'Open', now: 'Taken' } }, 'Not done: the quest moved on while this waited (it was open, it is taken).'],
    ['en', 'Confirm', { reason: 'setting', values: {} }, 'Not done: what this would set changed while this waited (stop a session).'],
    ['en', 'Confirm', { reason: 'branch', values: { branch: 'quest/x' } }, 'Not done: the branch quest/x gained or lost commits while this waited.'],
    ['zh', '确认', { reason: 'repository', values: { repository: 'Lyntai' } }, '未执行：仓库 Lyntai 在本机的注册已与当时不同。'],
    ['en', 'Confirm', { reason: 'repository', values: { repository: 'Lyntai' } },
      'Not done: the repository Lyntai is no longer registered on this machine as it was.'],
    ['zh', '确认', { reason: 'ask', values: {} }, '未执行：等待期间该需求已关闭。'],
  ])('says why a press whose ground moved was not done (%s, %j)', async (language, confirm, changed, words) => {
    await i18n.changeLanguage(language);
    const answer = vi.fn(async (): Promise<ConfirmationAnswer> => ({ answered: false, why: 'changed', message: 'the host said so', ...changed }));
    draw([STOP], answer);

    await userEvent.click(screen.getByRole('button', { name: confirm }));

    expect(await screen.findByRole('alert')).toHaveTextContent(words);
  });

  it.each([
    ['en', 'Confirm', "Daoris could not read what this rests on just now; it still waits."],
    ['zh', '确认', 'Daoris 暂时无法读取它所依据的内容；它仍在等待。'],
  ])('says a Confirm whose grounds could not be read leaves it waiting (%s)', async (language, confirm, words) => {
    await i18n.changeLanguage(language);
    const answer = vi.fn(async (): Promise<ConfirmationAnswer> => ({ answered: false, why: 'unread', message: '' }));
    draw([STOP], answer);

    await userEvent.click(screen.getByRole('button', { name: confirm }));

    expect(await screen.findByRole('alert')).toHaveTextContent(words);
    expect(screen.getByRole('button', { name: confirm })).toBeEnabled();
  });

  // CONFIRMWAIT1a-ii (the confirm-wait design §7.2, §8.1, §8.3): a press restored after a restart names its asker as its record's, in the
  // three forms the asker has; one carrying a key says it does not wait across a restart; a Confirm during an update's drain is refused on
  // its card, and it still waits.
  it.each([
    ['en', { process: 'bash', pid: 4120 }, 'The record says it was sent by bash, process 4120, before Daoris restarted.'],
    ['en', { process: null, pid: 77 }, 'The record says it was sent by process 77 before Daoris restarted.'],
    ['en', null, 'The record does not say what sent it before Daoris restarted.'],
    ['zh', { process: 'bash', pid: 4120 }, '记录显示它在 Daoris 重启前由 bash（进程 4120）发出。'],
    ['zh', { process: null, pid: 77 }, '记录显示它在 Daoris 重启前由进程 77 发出。'],
    ['zh', null, '记录未说明 Daoris 重启前是什么发出了它。'],
  ])('names a restored press\'s asker as its record\'s (%s, %j)', async (language, asker, words) => {
    await i18n.changeLanguage(language);
    draw([{ ...STOP, restored: true, asker, waitedOn: false }]);

    expect(screen.getByRole('group')).toHaveTextContent(words);
    expect(screen.getByRole('group')).not.toHaveTextContent(language === 'en' ? 'Sent by' : '由 bash 发出');
  });

  it.each([
    ['en', 'This carries a key, so it does not wait across a restart of Daoris.'],
    ['zh', '它带有密钥，因此不会在 Daoris 重启后继续等待。'],
  ])('says a press carrying a key does not wait across a restart (%s)', async (language, words) => {
    await i18n.changeLanguage(language);
    draw([{ ...STOP, keyed: true }, { ...STOP, id: 'plain' }]);

    const [keyed, plain] = screen.getAllByRole('group');
    expect(keyed).toHaveTextContent(words);
    expect(plain).not.toHaveTextContent(words);
  });

  it.each([
    ['en', 'Confirm', 'Daoris is about to restart; this still waits.'],
    ['zh', '确认', 'Daoris 即将重启；此请求仍在等待。'],
  ])('says a Confirm during an update\'s drain was refused and the press still waits (%s)', async (language, confirm, words) => {
    await i18n.changeLanguage(language);
    const answer = vi.fn(async (): Promise<ConfirmationAnswer> => ({ answered: false, why: 'draining', message: 'Daoris is about to restart; this still waits.' }));
    draw([STOP], answer);

    await userEvent.click(screen.getByRole('button', { name: confirm }));

    expect(await screen.findByRole('alert')).toHaveTextContent(words);
    expect(screen.getByRole('button', { name: confirm })).toBeEnabled();
  });

  // CONFIRMWAIT1 (the confirm-wait design §15.2 item 8): an answer Daoris could not keep is said on the card, which waits on, its buttons back.
  it.each([
    ['en', 'Refuse', 'Daoris could not keep your answer just now; nothing was done, and it still waits.'],
    ['zh', '拒绝', 'Daoris 暂时无法记下你的回答；未执行任何操作，它仍在等待。'],
  ])('says an answer it could not keep and the press still waits (%s)', async (language, refuse, words) => {
    await i18n.changeLanguage(language);
    const answer = vi.fn(async (): Promise<ConfirmationAnswer> => ({ answered: false, why: 'unrecorded', message: 'Daoris could not keep your answer just now; nothing was done, and it still waits.' }));
    draw([STOP], answer);

    await userEvent.click(screen.getByRole('button', { name: refuse }));

    expect(await screen.findByRole('alert')).toHaveTextContent(words);
    expect(screen.getByRole('button', { name: refuse })).toBeEnabled();
  });

  it('says all of it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw([SAY, UNKNOWN]);

    const [say, unknown] = screen.getAllByRole('group');
    expect(say).toHaveTextContent('一个终端请求以你的名义，在下一步时对 Fix the export feed（game · 3f2a91c4） 说：');
    expect(say).toHaveTextContent('“Run the database tests before you land.”');
    expect(say).toHaveTextContent('并附上这些文件，从本机读取：');
    expect(say).toHaveTextContent('它还携带');
    expect(say).toHaveTextContent('由 bash 发出，进程 4120。');
    expect(say).toHaveTextContent('Daoris 无法分辨这是你运行的还是智能体运行的。只确认你自己运行的。');
    expect(unknown).toHaveTextContent('一个终端请求 SET_MYSTERY。');
    expect(within(say).getByRole('button', { name: '确认' })).toBeInTheDocument();
    expect(within(say).getByRole('button', { name: '拒绝' })).toBeInTheDocument();
    expect(within(say).getByRole('button', { name: '详情' })).toBeInTheDocument();
  });
});

// CONFIRMWAIT1c (the confirm-wait design §8.1, §15.3 item 13): a terminal's press that rests on a plan is confirmed hours later from
// its card, so the card opens that plan's own page, scoped to the press's own session, repository, workspace, quest or ask.
describe("the door to a press's plan", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const press = (route: string, fields: Record<string, string>): Confirmation => ({
    source: 'pipe', id: `p-${route}`, module: 'DAORIS.DRIVER', route,
    fields: Object.entries(fields).map(([name, value]) => ({ name: `/${name}`, type: 'string', value })),
    asker: { process: 'bash', pid: 4120 }, expires: null,
  });

  it.each<[string, Confirmation, PlanDoor]>([
    ['a land, its session', press('LAND_SESSION_TREE', { id: '3f2a91c4', branch: 'main' }), { plan: 'land', session: '3f2a91c4' }],
    ['a set-up, its repository', press('SETUP_PRESS', { repository: 'game' }), { plan: 'setup', repository: 'game' }],
    ['a sweep, its workspace', press('SWEEP', { workspace: 'studio' }), { plan: 'sweep', workspace: 'studio' }],
    ['a sweep of no workspace named', press('SWEEP', {}), { plan: 'sweep', workspace: null }],
    ['a sync, its workspace', press('TREES_SYNC', { workspace: 'studio' }), { plan: 'sync', workspace: 'studio', repository: null }],
    ['a sync, its repository', press('TREES_SYNC', { repository: 'game' }), { plan: 'sync', workspace: null, repository: 'game' }],
    ['a clear, its workspace', press('HISTORY_CLEAR', { workspace: 'studio' }), { plan: 'clear', scope: 'workspace', id: 'studio' }],
    ['a clear, its quest', press('HISTORY_CLEAR', { quest: '7eb1d7ba71fe' }), { plan: 'clear', scope: 'quest', id: '7eb1d7ba71fe' }],
    ['a clear, its ask', press('HISTORY_CLEAR', { ask: '77ea1a000000' }), { plan: 'clear', scope: 'ask', id: '77ea1a000000' }],
  ])('opens the plan of %s', async (_, waiting, door) => {
    expect(planDoor(waiting)).toEqual(door);
    const onOpenPlan = vi.fn();
    render(<Confirmations waiting={[waiting]} onAnswer={vi.fn()} names={NAMES} onOpenPlan={onOpenPlan} />);

    await userEvent.click(screen.getByRole('button', { name: 'Look at it now' }));

    expect(onOpenPlan).toHaveBeenCalledExactlyOnceWith(door);
  });

  it('offers no door on a press that rests on no plan, a service\'s, or one whose plan names nothing', () => {
    const onOpenPlan = vi.fn();
    render(
      <Confirmations
        waiting={[STOP, SAY, UNKNOWN, SERVICE, press('LAND_SESSION_TREE', {}), press('HISTORY_CLEAR', {})]}
        onAnswer={vi.fn()}
        names={NAMES}
        onOpenPlan={onOpenPlan}
      />,
    );

    expect(screen.queryByRole('button', { name: 'Look at it now' })).not.toBeInTheDocument();
    expect(planDoor(SERVICE)).toBeNull();
  });

  it('offers none where the window hands it no door', () => {
    render(<Confirmations waiting={[press('SETUP_PRESS', { repository: 'game' })]} onAnswer={vi.fn()} names={NAMES} />);

    expect(screen.queryByRole('button', { name: 'Look at it now' })).not.toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    const onOpenPlan = vi.fn();
    render(<Confirmations waiting={[press('SWEEP', { workspace: 'studio' })]} onAnswer={vi.fn()} names={NAMES} onOpenPlan={onOpenPlan} />);

    await userEvent.click(screen.getByRole('button', { name: '查看当前情况' }));

    expect(onOpenPlan).toHaveBeenCalledExactlyOnceWith({ plan: 'sweep', workspace: 'studio' });
  });
});

describe('the adoption notice', () => {
  it('says once that the service in use was not started by this Daoris, until dismissed', async () => {
    const onDismiss = vi.fn();
    render(<AdoptionNotice onDismiss={onDismiss} />);

    expect(screen.getByRole('status')).toHaveTextContent(
      'Daoris is using a service it did not start, so your own acts (a review, a yes, an answer) are refused there. '
      + 'Close what started it and restart Daoris.');
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(onDismiss).toHaveBeenCalled();
  });
});

/**
 * *Shown* (NOTIFY2b, the notification design §3.4): the region in view, by one judgement over its intersection with its area, the
 * window's focus and size, and a drawer's scrim. jsdom lays nothing out, so each box is the test's, and an `IntersectionObserver`
 * stand-in (`watchScreen`) wakes the judgement as the window would.
 */
describe("a press's card shown", () => {
  type Box = { top: number; height: number };
  const layout: { area: Box; region: Box; buttons: Box } = {
    area: { top: 0, height: 400 }, region: { top: 20, height: 300 }, buttons: { top: 280, height: 30 },
  };
  const rect = ({ top, height }: Box) => ({ top, bottom: top + height, height, left: 0, right: 300, width: 300, x: 0, y: top, toJSON: () => ({}) });
  let screenStandIn: ReturnType<typeof watchScreen>;
  let focused: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    layout.area = { top: 0, height: 400 };
    layout.region = { top: 20, height: 300 };
    layout.buttons = { top: 280, height: 30 };
    screenStandIn = watchScreen();
    focused = vi.spyOn(document, 'hasFocus').mockReturnValue(true);
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function box(this: Element) {
      if (this.hasAttribute('data-area')) return rect(layout.area) as DOMRect;
      if (this.hasAttribute('data-press-region')) return rect(layout.region) as DOMRect;
      if (this.hasAttribute('data-press-buttons')) return rect(layout.buttons) as DOMRect;
      return rect({ top: 0, height: 0 }) as DOMRect;
    });
  });
  afterEach(() => {
    vi.restoreAllMocks();
    screenStandIn.restore();
  });

  function Area({ children }: { children: ReactNode }) {
    const area = useRef<HTMLDivElement>(null);
    return <div data-area="" ref={area}><PressArea.Provider value={area}>{children}</PressArea.Provider></div>;
  }
  const shownIn = (waiting: Confirmation[]) => {
    const onShown = vi.fn();
    const answer = vi.fn(async () => ({ answered: true as const }));
    const view = render(<Area><Confirmations waiting={waiting} onAnswer={answer} onShown={onShown} /></Area>);
    return { ...view, onShown, answer };
  };
  const wake = () => screenStandIn.show(true);

  it('is said once, with its region whole in view in a focused window, and never of one shown before', () => {
    const { onShown } = shownIn([STOP, { ...UNKNOWN, shown: true }]);

    expect(onShown).toHaveBeenCalledTimes(1);
    expect(onShown).toHaveBeenCalledWith(STOP);
    wake();
    act(() => { window.dispatchEvent(new Event('focus')); });
    expect(onShown).toHaveBeenCalledTimes(1);
  });

  it('is not said while its region is scrolled out, and is once it is scrolled in', () => {
    layout.region = { top: 500, height: 300 };
    layout.buttons = { top: 760, height: 30 };
    const { onShown } = shownIn([STOP]);
    expect(onShown).not.toHaveBeenCalled();

    // Half in view is not in view.
    layout.region = { top: 250, height: 300 };
    wake();
    expect(onShown).not.toHaveBeenCalled();

    layout.region = { top: 50, height: 300 };
    layout.buttons = { top: 310, height: 30 };
    wake();
    expect(onShown).toHaveBeenCalledTimes(1);
  });

  it('is not said while its window is not focused, and is when it comes in front', () => {
    focused.mockReturnValue(false);
    const { onShown } = shownIn([STOP]);
    wake();
    expect(onShown).not.toHaveBeenCalled();

    focused.mockReturnValue(true);
    act(() => { window.dispatchEvent(new Event('focus')); });
    expect(onShown).toHaveBeenCalledTimes(1);
  });

  it("is not said under a drawer's scrim, and is once the drawer closes", () => {
    const onShown = vi.fn();
    const answer = vi.fn(async () => ({ answered: true as const }));
    const drawn = (drawer: boolean) => (
      <Area>
        <Confirmations waiting={[STOP]} onAnswer={answer} onShown={onShown} />
        {drawer ? <Drawer title="About" onClose={() => {}}>about</Drawer> : null}
      </Area>
    );
    const { rerender } = render(drawn(true));
    wake();
    expect(onShown).not.toHaveBeenCalled();

    rerender(drawn(false));
    expect(onShown).toHaveBeenCalledTimes(1);
  });

  it('when taller than its area, is said once it fills it with its buttons in view, and not before, judged again on a resize', () => {
    layout.area = { top: 0, height: 200 };
    layout.region = { top: 0, height: 500 };
    layout.buttons = { top: 460, height: 30 };
    const { onShown } = shownIn([STOP]);
    // It fills its area, its buttons below the fold: two minutes must not start on an answer that cannot be seen.
    expect(onShown).not.toHaveBeenCalled();

    // A taller window: the area grows, still short of the region, its buttons now in view.
    layout.area = { top: 0, height: 480 };
    layout.region = { top: -20, height: 500 };
    layout.buttons = { top: 440, height: 30 };
    act(() => { window.dispatchEvent(new Event('resize')); });
    expect(onShown).toHaveBeenCalledTimes(1);
  });

  it("is said of a service's press too, which only this window judges", () => {
    const { onShown } = shownIn([SERVICE]);
    expect(onShown).toHaveBeenCalledWith(SERVICE);
  });

  it('judges the region by the smaller of its height and its area, its buttons where it fills', () => {
    const at = (top: number, height: number) => ({ top, bottom: top + height, height });
    expect(regionShown(at(10, 100), at(0, 400), at(80, 20))).toBe(true);
    expect(regionShown(at(350, 100), at(0, 400), at(420, 20))).toBe(false);
    expect(regionShown(at(-50, 900), at(0, 400), at(300, 20))).toBe(true);
    expect(regionShown(at(-50, 900), at(0, 400), at(820, 20))).toBe(false);
    expect(regionShown(at(0, 0), at(0, 400), null)).toBe(false);
  });
});
