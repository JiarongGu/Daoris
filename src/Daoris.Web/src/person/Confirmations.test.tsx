import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { type ReactNode, useRef } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Drawer } from '../design/ui';
import i18n from '../locales/i18n';
import { watchScreen } from '../test/onScreen';
import {
  AdoptionNotice, type Confirmation, type ConfirmationAnswer, Confirmations, PressArea, type PressNames, regionShown, routeKey,
} from './Confirmations';

// The window's confirmation of a terminal's press (PERSONDOOR1f, RPC1c; D156 point 4, the person-door design §4.2), as NOTIFY2b
// (D196 point 4, the notification design §3) draws it in *Notifications*: in plain words that keep what tells one session and one
// file from another, every value in the card's view, the route's name under *Details*; its decision region bounded, and *shown*
// once that region is in view in a window in front of the person, which starts its two minutes.

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
  asker: { process: 'bash', pid: 4120 }, asked: new Date(Date.now() - 60_000).toISOString(), expires: '2026-10-21T09:10:00Z',
};
const STOP: Confirmation = {
  source: 'pipe', id: 'c2', module: 'DAORIS.DRIVER', route: 'STOP_SESSION',
  fields: [{ name: '/id', type: 'string', value: '9b1e0d2a' }, { name: '/said', type: 'boolean', value: 'true' }],
  named: [{ role: 'session', name: '/id', value: '9b1e0d2a' }],
  asker: { process: null, pid: 77 }, expires: '2026-10-21T09:12:00Z',
};
const UNKNOWN: Confirmation = {
  source: 'pipe', id: 'c3', module: 'DAORIS.DRIVER', route: 'SET_MYSTERY', fields: [{ name: '/keep', type: 'number', value: '3' }],
  asker: { process: null, pid: 77 }, expires: '2026-10-20T09:03:00Z',
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

const draw = (waiting: Confirmation[], answer = vi.fn(async () => ({ answered: true as const }))) => {
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
      'This no longer waits: it was answered in another window, its two minutes passed, or its terminal took it back.');
    await userEvent.click(within(service).getByRole('button', { name: 'Refuse' }));
    expect(await within(service).findByRole('alert')).toHaveTextContent('This confirmation no longer waits: it expired.');
    expect(notify).not.toHaveBeenCalled();
  });

  // NOTIFY2a (D196 point 4, the notification design §3.4): a Confirm past its time finds its card gone, so its refusal is toasted.
  it.each([
    ['en', 'Confirm', 'This no longer waits: it was answered in another window, its two minutes passed, or its terminal took it back.'],
    ['zh', '确认', '此请求已不再等待：它已在另一个窗口中得到回答、两分钟已过，或其终端已将它撤回。'],
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

  it('says its time left once shown, stepping, and until when it waits to be found before', () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    try {
      vi.setSystemTime(new Date('2026-10-21T09:00:00Z'));
      const shown = { ...STOP, id: 's', shown: true, expires: '2026-10-21T09:01:40Z' };
      draw([shown, { ...STOP, id: 'u' }]);
      const [counting, waiting] = screen.getAllByRole('group');
      expect(counting).toHaveTextContent('refused by itself in 1m 40s');
      act(() => { vi.advanceTimersByTime(10_000); });
      expect(counting).toHaveTextContent('refused by itself in 1m 30s');
      expect(waiting).toHaveTextContent('It waits until');
    } finally {
      vi.useRealTimers();
    }
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
