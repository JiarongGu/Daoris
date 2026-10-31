import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { TogetherDoor, type TogetherView } from './TogetherDoor';

// LINESTOGETHER1d: Bring them together… on the head, the phases on the head and on a member, and the presses each phase allows.

const operation = (over: Partial<TogetherView> = {}): TogetherView => ({
  id: 'tg1', phase: 'asked', role: 'head', head: 'hhhh0000', why: null, into: null, line: false,
  members: [{ session: 'aaaa1111', branch: 'daoris/a', tip: 'a1' }, { session: 'bbbb2222', branch: 'daoris/b', tip: 'b1' }],
  ...over,
});

afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

describe('bringing sessions together', () => {
  it('ticks the sessions beside the head and brings only those', async () => {
    const onBring = vi.fn();
    render(<TogetherDoor candidates={['aaaa1111', 'bbbb2222']} operation={null} onBring={onBring} />);
    await userEvent.click(screen.getByRole('button', { name: 'Bring them together…' }));
    expect((screen.getByRole('button', { name: 'Bring them together' }) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(screen.getByRole('checkbox', { name: 'bbbb2222' }));
    await userEvent.click(screen.getByRole('button', { name: 'Bring them together' }));
    expect(onBring).toHaveBeenCalledWith(['bbbb2222']);
  });

  it('lets a head go before it lands, asks again after a failed check, and accepts once ready', async () => {
    const onLetGo = vi.fn();
    const onAskAgain = vi.fn();
    const onAccept = vi.fn();
    const { rerender } = render(<TogetherDoor candidates={[]} operation={operation({ phase: 'checking', why: 'missing' })} onLetGo={onLetGo} onAskAgain={onAskAgain} onAccept={onAccept} />);
    expect(screen.getByText(/Bringing 2 sessions into this one: checking/)).toBeTruthy();
    expect(screen.getByText('Its last check: missing')).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Ask again' }));
    expect(onAskAgain).toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Accept them together' })).toBeNull();

    rerender(<TogetherDoor candidates={[]} operation={operation({ phase: 'ready' })} onLetGo={onLetGo} onAskAgain={onAskAgain} onAccept={onAccept} />);
    await userEvent.click(screen.getByRole('button', { name: 'Accept them together' }));
    await userEvent.click(screen.getByRole('button', { name: 'Let them go' }));
    expect([onAccept.mock.calls.length, onLetGo.mock.calls.length]).toEqual([1, 1]);

    rerender(<TogetherDoor candidates={[]} operation={operation({ phase: 'landing' })} onLetGo={onLetGo} onAccept={onAccept} />);
    expect(screen.getByText(/: landing/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Let them go' })).toBeNull();
  });

  it('says on a member where it was brought into, never that it landed, in English and Chinese', async () => {
    render(<TogetherDoor candidates={[]} operation={operation({ role: 'member', phase: 'landed', into: 'main', line: true })} />);
    expect(screen.getByText('Brought into the line main by')).toBeTruthy();
    expect(screen.getByText('hhhh0000')).toBeTruthy();
    await i18n.changeLanguage('zh');
    cleanup();
    render(<TogetherDoor candidates={[]} operation={operation({ role: 'member', phase: 'replaying' })} />);
    expect(screen.getByText('正被并入另一个会话：正在重放它们的工作')).toBeTruthy();
  });
});
