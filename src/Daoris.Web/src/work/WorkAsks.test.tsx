import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { code } from '../test/code';
import { outcomeOf, pauseAsk } from './pausing';
import { ABANDON_ANSWER, ABANDONED_ENTRY, MIXED_ASK, PAUSABLE_ASK, SPENT_ASK } from './pausingFixtures';
import type { Answered } from './InlineConfirm';
import { AbandonAsk, AbandonedWork, PauseAsk } from './WorkAsks';

// What a pause and an abandon ask under a header, and what an abandon leaves on a page (PAUSE1e, D132 §2.6, §3.1, §4.2), as
// molecules: every state is reached by passing it, and every press goes out.

const ASK = { scope: 'ask', id: 'a1b2c3' } as const;

describe('the pause’s ask (design §2.6)', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says what follows, then pauses on its second press, with never mind beside it', async () => {
    const pause = vi.fn();
    const cancel = vi.fn();
    render(<PauseAsk target={ASK} lines={pauseAsk(PAUSABLE_ASK, { wired: true })} meanIt="Pause ask" onPause={pause} onClose={cancel} />);

    const ask = screen.getByRole('group', { name: 'pause this work' });
    expect(ask).toHaveTextContent('Stops 1 running session now and starts nothing of ask #a1b2c3 on this machine until you resume it.');
    expect(ask).toHaveTextContent('Another machine may still take #5e4f3d: a pause holds this machine only.');
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Pause ask', 'Never mind']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(cancel).toHaveBeenCalledOnce();
    await userEvent.click(within(ask).getByRole('button', { name: 'Pause ask' }));
    expect(pause).toHaveBeenCalledOnce();
    expect(pause).toHaveBeenCalledWith(expect.objectContaining({ done: expect.any(Function), refused: expect.any(Function) }));
  });

  it('offers no pause while it reads what the pause would stop, and waits while one is on its way', () => {
    const { rerender } = render(<PauseAsk target={ASK} lines={null} meanIt="Pause ask" onPause={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('group')).toHaveTextContent('Reading what the pause would stop…');
    expect(screen.queryByRole('button', { name: 'Pause ask' })).toBeNull();

    rerender(<PauseAsk target={ASK} lines={pauseAsk(PAUSABLE_ASK, { wired: false })} meanIt="Pause ask" busy onPause={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('button', { name: 'Pause ask' })).toBeDisabled();
  });

  it('waits, says a refusal inside the ask, lets the move be pressed again, and closes when it lands (UXFIX2b2a)', async () => {
    const close = vi.fn();
    let answered: Answered | undefined;
    render(<PauseAsk target={ASK} lines={pauseAsk(PAUSABLE_ASK, { wired: false })} meanIt="Pause ask" onPause={(a) => { answered = a; }} onClose={close} />);
    const user = userEvent.setup();
    const ask = screen.getByRole('group', { name: 'pause this work' });
    expect(ask.querySelector('[tabindex="-1"]')).toBe(document.activeElement);

    await user.click(screen.getByRole('button', { name: 'Pause ask' }));
    expect(screen.getByRole('status')).toHaveTextContent(/\S/);
    expect(screen.getByRole('button', { name: 'Pause ask' })).toBeDisabled();
    act(() => answered!.refused('The driver could not stop it.'));
    expect(within(ask).getByRole('alert')).toHaveTextContent('The driver could not stop it.');
    expect(screen.getByRole('button', { name: 'Pause ask' })).toBeEnabled();
    expect(close).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Pause ask' }));
    act(() => answered!.done());
    expect(close).toHaveBeenCalledOnce();
  });

  it('says it in 中文, the ask by its Chinese name', async () => {
    await i18n.changeLanguage('zh');
    render(<PauseAsk target={ASK} lines={pauseAsk(PAUSABLE_ASK, { wired: false })} meanIt="确认暂缓需求" onPause={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('group')).toHaveTextContent('本机不会再启动需求 #a1b2c3 的任何工作');
  });
});

describe('the abandon’s list (design §3.1, §3.2)', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('lists what goes and what stays, each kept piece with why, and never a path on this machine', () => {
    render(<AbandonAsk target={ASK} plan={MIXED_ASK} meanIt="Abandon ask" placeholder="why — kept with each decline" onAbandon={() => {}} onClose={() => {}} />);

    const goes = screen.getByRole('list', { name: 'What goes' });
    expect(within(goes).getByText('#9a8b7c Cap chunk hydration per frame')).toBeInTheDocument();
    expect(within(goes).getByText('declined with your reason, only while it is open')).toBeInTheDocument();
    expect(within(goes).getByText('engine daoris/s-1a2b3c4d')).toBeInTheDocument();
    expect(goes).toHaveTextContent('3 commits only here');
    expect(goes).toHaveTextContent('7 uncommitted files: src/stream/hydrate.ts, src/stream/budget.ts, src/stream/frame.ts, docs/streaming.md, test/hydrate.test.ts, …');
    expect(within(goes).getByText('closed with your reason')).toBeInTheDocument();

    const stays = screen.getByRole('list', { name: 'What stays' });
    expect(stays).toHaveTextContent('taken on studio-pc: its work is theirs');
    expect(stays).toHaveTextContent('done: finished work keeps its record');
    expect(within(stays).getByText('main').tagName).toBe('CODE');
    expect(document.body.textContent).not.toMatch(/[A-Z]:[\\/]/);
  });

  it('asks for the reason, and abandons on its second press with the reason as typed', async () => {
    const abandon = vi.fn();
    const user = userEvent.setup();
    render(<AbandonAsk target={ASK} plan={PAUSABLE_ASK} meanIt="Abandon ask" placeholder="why — kept with each decline" onAbandon={abandon} onClose={() => {}} />);

    const move = screen.getByRole('button', { name: 'Abandon ask' });
    expect(move).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: 'why — kept with each decline' }), '  It went the wrong way.  ');
    await user.click(move);
    expect(abandon).toHaveBeenCalledWith('It went the wrong way.', expect.objectContaining({ refused: expect.any(Function) }));
  });

  it('says a refusal inside the ask, and keeps the reason typed (UXFIX2b2a)', async () => {
    let answered: Answered | undefined;
    const user = userEvent.setup();
    render(<AbandonAsk target={ASK} plan={PAUSABLE_ASK} meanIt="Abandon ask" placeholder="why" onAbandon={(_, a) => { answered = a; }} onClose={() => {}} />);
    await user.type(screen.getByRole('textbox', { name: 'why' }), 'Wrong way.');
    await user.click(screen.getByRole('button', { name: 'Abandon ask' }));
    act(() => answered!.refused('Nothing was abandoned.'));
    expect(screen.getByRole('alert')).toHaveTextContent('Nothing was abandoned.');
    expect(screen.getByRole('textbox', { name: 'why' })).toHaveValue('Wrong way.');
    expect(screen.getByRole('button', { name: 'Abandon ask' })).toBeEnabled();
  });

  it('says so where nothing is left to take, and offers only Close', async () => {
    const cancel = vi.fn();
    render(<AbandonAsk target={ASK} plan={SPENT_ASK} meanIt="Abandon ask" placeholder="why" onAbandon={() => {}} onClose={cancel} />);
    const ask = screen.getByRole('group', { name: 'abandon this work' });
    expect(ask).toHaveTextContent('Nothing of ask #a1b2c3 is left to abandon on this machine.');
    expect(within(ask).getAllByRole('button').map((button) => button.textContent)).toEqual(['Close']);
    await userEvent.click(within(ask).getByRole('button', { name: 'Close' }));
    expect(cancel).toHaveBeenCalledOnce();
  });

  it('lists in 中文', async () => {
    await i18n.changeLanguage('zh');
    render(<AbandonAsk target={ASK} plan={MIXED_ASK} meanIt="确认放弃需求" placeholder="原因" onAbandon={() => {}} onClose={() => {}} />);
    expect(screen.getByRole('list', { name: '要放弃的部分' })).toHaveTextContent('连同工作树一起丢弃');
    expect(screen.getByRole('list', { name: '会保留的部分' })).toHaveTextContent('已在 studio-pc 上接下');
  });
});

describe('what went and what stayed (design §4.2)', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says from the record what went, with each discarded branch’s way back as code, and what stayed with why', () => {
    render(<AbandonedWork target={ASK} outcome={outcomeOf(ABANDONED_ENTRY)} went="What went" stayed="What stayed" />);

    const went = screen.getByRole('region', { name: 'What went' });
    expect(went).toHaveTextContent('Declined #9a8b7c, #5e4f3d with your reason.');
    expect(went).toHaveTextContent('Closed ask #a1b2c3 with your reason.');
    expect(within(went).getByText(code('git branch daoris/s-1a2b3c4d 9f3e2a1'))).toBeInTheDocument();
    expect(went).toHaveTextContent('The remote confirmed the decline of #9a8b7c.');

    const stayed = screen.getByRole('region', { name: 'What stayed' });
    expect(stayed).toHaveTextContent('#0c1d2e');
    expect(stayed).toHaveTextContent('taken on studio-pc');
  });

  it('says from the answer what changed since the list and a decline the remote lost', () => {
    render(<AbandonedWork target={ASK} outcome={outcomeOf(ABANDON_ANSWER)} went="What went" stayed="What stayed" />);
    expect(screen.getByRole('region', { name: 'What went' })).toHaveTextContent('#5e4f3d was taken on another machine before your decline reached the remote');
    expect(screen.getByRole('region', { name: 'What stayed' })).toHaveTextContent('session p4rk3d00');
    expect(screen.getByRole('region', { name: 'What stayed' })).toHaveTextContent('it changed since the list: its tree keeps work to review');
  });

  it('says the scope stays paused after a step that could not finish', () => {
    render(<AbandonedWork target={ASK} outcome={outcomeOf({ ...ABANDON_ANSWER, stillPaused: true })} went="What went" stayed="What stayed" />);
    expect(screen.getByText(/It stays paused: a step could not finish/)).toBeInTheDocument();
  });
});
