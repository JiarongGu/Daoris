import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import i18n from '../locales/i18n';
import { Confirmations, type Confirmation } from './Confirmations';

// PERSONDOOR1f and RPC1c (D156 point 4, the person-door design §4.2): what a terminal asked the window to confirm, shown on every
// main window under the strip by the press's own name and every value it carries, naming what sent it, saying Daoris cannot tell
// who ran it; Confirm and Refuse, each said where it was pressed (the UI/UX design's R3); and the adoption notice, said once.

const ARCHIVE: Confirmation = {
  source: 'pipe', id: 'c1', route: 'SESSION_ARCHIVE',
  fields: [{ name: '/ids/0', type: 'string', value: 's1a2b3c4' }, { name: '/archived', type: 'boolean', value: 'true' }],
  asker: { process: 'daoris-driver', pid: 4242 }, expires: '2026-10-20T09:02:00Z',
};
const UNKNOWN: Confirmation = {
  source: 'pipe', id: 'c2', route: 'SET_TREES', fields: [{ name: '/keep', type: 'number', value: '3' }],
  asker: { process: null, pid: 77 }, expires: '2026-10-20T09:03:00Z',
};
const SERVICE: Confirmation = {
  source: 'service', id: 'h1', route: 'POST /api/quests/{id}/review', act: "a review's verdict",
  fields: [{ name: '/verdict', type: 'string', value: 'reviewed' }], asker: null, expires: '2026-10-20T09:04:00Z',
};

const draw = (waiting: Confirmation[], answer = vi.fn(async () => ({ answered: true as const })), notice = false) => {
  const onDismissNotice = vi.fn();
  const view = render(<Confirmations waiting={waiting} notice={notice} onAnswer={answer} onDismissNotice={onDismissNotice} />);
  return { ...view, answer, onDismissNotice };
};

describe("the window's confirmation of a terminal's press", () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws nothing when nothing waits and there is nothing to say', () => {
    expect(draw([]).container).toBeEmptyDOMElement();
  });

  it('names the press, every value it carries and what sent it, and says Daoris cannot tell who ran it', () => {
    draw([ARCHIVE, UNKNOWN]);

    const [archive, unknown] = screen.getAllByRole('group');
    expect(archive).toHaveTextContent('A terminal asks to archive sessions on this machine, or bring them back.');
    expect(archive).toHaveTextContent('SESSION_ARCHIVE');
    expect(archive).toHaveTextContent('Sent by daoris-driver, process 4242.');
    expect(within(archive).getByText('/ids/0')).toBeInTheDocument();
    expect(within(archive).getByText('"s1a2b3c4"')).toBeInTheDocument();
    expect(within(archive).getByText('true')).toBeInTheDocument();
    expect(archive).toHaveTextContent('Daoris cannot tell whether you ran this or an agent did. Confirm only what you ran.');
    // A press the page has no words for is named by its route alone, and a value keeps its type: 3, not "3".
    expect(unknown).toHaveTextContent('A terminal asks for SET_TREES.');
    expect(unknown).toHaveTextContent('Sent by process 77.');
    expect(within(unknown).getByText('3')).toBeInTheDocument();
  });

  it("names the service's ask by its own door's act, and says it cannot name what sent it", () => {
    draw([SERVICE]);

    const card = screen.getByRole('group');
    expect(card).toHaveTextContent("A terminal asks for a review's verdict.");
    expect(card).toHaveTextContent('POST /api/quests/{id}/review');
    expect(card).toHaveTextContent('Daoris could not name what sent it.');
  });

  it('answers Confirm and Refuse by the ask, and offers neither while one is under way', async () => {
    let settle: (value: { answered: true }) => void = () => {};
    const answer = vi.fn(() => new Promise<{ answered: true }>((resolve) => { settle = resolve; }));
    draw([ARCHIVE], answer);

    await userEvent.click(screen.getByRole('button', { name: 'Confirm' }));
    expect(answer).toHaveBeenCalledWith(ARCHIVE, true);
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Refuse' })).toBeDisabled();
    settle({ answered: true });
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Refuse' })).toBeEnabled());
    await userEvent.click(screen.getByRole('button', { name: 'Refuse' }));
    expect(answer).toHaveBeenLastCalledWith(ARCHIVE, false);
  });

  it('says where it was pressed that one no longer waits, and a refusal in its own sentence', async () => {
    const answer = vi.fn()
      .mockResolvedValueOnce({ answered: false, why: 'gone', message: 'the driver said so.' })
      .mockResolvedValueOnce({ answered: false, why: null, message: 'This confirmation no longer waits: it expired.' });
    draw([ARCHIVE, SERVICE], answer);
    const [archive, service] = screen.getAllByRole('group');

    await userEvent.click(within(archive).getByRole('button', { name: 'Confirm' }));
    expect(await within(archive).findByRole('alert')).toHaveTextContent(
      'This no longer waits: it was answered in another window, its two minutes passed, or its terminal took it back.');
    await userEvent.click(within(service).getByRole('button', { name: 'Refuse' }));
    expect(await within(service).findByRole('alert')).toHaveTextContent('This confirmation no longer waits: it expired.');
  });

  // CONFIRMSEEN1: a terminal's press's two minutes count from when it is in front of the person, so a card in a window behind others
  // says nothing until that window comes in front, and then says it once.
  it('says a terminal press is shown once its window is in front, once, and never of one shown or a service ask', async () => {
    const focused = vi.spyOn(document, 'hasFocus').mockReturnValue(false);
    try {
      const onShown = vi.fn();
      const answer = vi.fn(async () => ({ answered: true as const }));
      const { rerender } = render(
        <Confirmations waiting={[ARCHIVE, { ...UNKNOWN, shown: true }, SERVICE]} onAnswer={answer} onShown={onShown} />);
      expect(onShown).not.toHaveBeenCalled();

      focused.mockReturnValue(true);
      window.dispatchEvent(new Event('focus'));
      expect(onShown).toHaveBeenCalledTimes(1);
      expect(onShown).toHaveBeenCalledWith(ARCHIVE);

      window.dispatchEvent(new Event('focus'));
      rerender(<Confirmations waiting={[ARCHIVE, { ...UNKNOWN, shown: true }, SERVICE]} onAnswer={answer} onShown={onShown} />);
      expect(onShown).toHaveBeenCalledTimes(1);
      // The row's door finds the card by its anchor.
      expect(document.getElementById('confirmation-c1')).toHaveAttribute('role', 'group');
    } finally {
      focused.mockRestore();
    }
  });

  it('says once that the service in use was not started by this Daoris, until dismissed', async () => {
    const { onDismissNotice } = draw([], undefined, true);

    expect(screen.getByRole('status')).toHaveTextContent(
      'Daoris is using a service it did not start, so your own acts (a review, a yes, an answer) are refused there. '
      + 'Close what started it and restart Daoris.');
    await userEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(onDismissNotice).toHaveBeenCalled();
  });

  it('says all of it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw([ARCHIVE, UNKNOWN], undefined, true);

    const [archive, unknown] = screen.getAllByRole('group');
    expect(archive).toHaveTextContent('一个终端请求在本机归档会话，或将其恢复。');
    expect(archive).toHaveTextContent('由 daoris-driver 发出，进程 4242。');
    expect(archive).toHaveTextContent('Daoris 无法分辨这是你运行的还是智能体运行的。只确认你自己运行的。');
    expect(unknown).toHaveTextContent('一个终端请求 SET_TREES。');
    expect(unknown).toHaveTextContent('由进程 77 发出。');
    expect(within(archive).getByRole('button', { name: '确认' })).toBeInTheDocument();
    expect(within(archive).getByRole('button', { name: '拒绝' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Daoris 正在使用一个并非由它启动的服务');
  });
});
