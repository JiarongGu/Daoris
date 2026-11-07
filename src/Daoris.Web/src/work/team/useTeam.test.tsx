import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, renderHook, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { BringInAgent } from './BringInAgent';
import { useTeam } from './useTeam';

// COWORKUX1f (the co-worker design §6.2): *Bring in another agent…*'s doors for a session that leads, each judged by its own rule, in
// both languages; a session that is no lead brings in nobody.

vi.mock('../../bridge/sessions/sessionHelpers', () => ({
  usePassHelper: () => ({ mutate: vi.fn(), isPending: false }),
  useAskHelper: () => ({ mutate: vi.fn(), isPending: false }),
  useSessionHelpers: () => ({ data: [] }),
}));
vi.mock('../../bridge/sessions/channelOutside', () => ({
  useOutsideAgents: () => ({ data: [] }),
  useRemoveOutsideAgent: () => ({ mutate: vi.fn(), isPending: false }),
}));

const session = (over: Partial<Session> = {}) => ({
  id: 's1', door: 'claude-acp', repository: 'driver', state: 'working', tree: 'trees/s1', ...over,
}) as Session;

const teamOf = (attended: Session, open = vi.fn()) => {
  const team = renderHook(() => useTeam({ attended, here: true, nameOf: (agent) => agent, attend: vi.fn(), open })).result.current;
  if (!team) throw new Error('a session here has a team');
  return team;
};

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe("Bring in another agent…'s doors", () => {
  it('opens a question, a piece, the review for a second opinion and a hand-over, and says a second request runs alongside by itself', async () => {
    const open = vi.fn();
    const choices = teamOf(session(), open).choices ?? [];

    expect(choices.map((choice) => choice.door)).toEqual(['question', 'piece', 'opinion', 'goOn', 'alongside']);
    for (const door of ['question', 'piece', 'opinion', 'goOn'] as const) choices.find((choice) => choice.door === door)?.onChoose?.();
    expect(open.mock.calls.map(([door]) => door)).toEqual(['askHelper', 'askPiece', 'review', 'handOver']);
    const alongside = choices.find((choice) => choice.door === 'alongside')!;
    expect(alongside.onChoose).toBeUndefined();
    expect(alongside.off).toBeUndefined();

    render(<BringInAgent choices={choices} onClose={vi.fn()} />);
    expect(['Answer a question', 'Write a piece', 'Second opinion', 'Go on with another agent']
      .every((name) => screen.getByRole('button', { name }))).toBe(true);
    expect(screen.queryByRole('button', { name: 'Work alongside' })).toBeNull();
    expect(screen.getByText('Work alongside').closest('li')?.textContent).toMatch(/needs no press: a second request on the same ask already runs alongside/);
  });

  it('turns off a piece and a second opinion for a session with no tree of its own, with the reason', () => {
    const choices = teamOf(session({ tree: undefined })).choices ?? [];

    for (const door of ['piece', 'opinion'] as const) {
      const choice = choices.find((each) => each.door === door)!;
      expect(choice.onChoose).toBeUndefined();
      expect(choice.off).toContain('works in no tree of its own');
    }

    expect(choices.find((choice) => choice.door === 'question')?.onChoose).toBeTypeOf('function');
  });

  it('is drawn in Chinese with the glossary words', async () => {
    await i18n.changeLanguage('zh');
    render(<BringInAgent choices={teamOf(session()).choices ?? []} onClose={vi.fn()} />);

    for (const name of ['回答一个问题', '写一个分项', '第二意见', '换一个智能体继续']) {
      expect(screen.getByRole('button', { name })).toBeTruthy();
    }

    expect(screen.queryByRole('button', { name: '并行工作' })).toBeNull();
    expect(screen.getByText('并行工作').closest('li')?.textContent).toMatch(/无需按键：同一请求上的第二个请求已会自行并行运行/);
  });

  it('brings in nobody for a helper, a reviewer or an intake', () => {
    expect(teamOf(session({ serves: 's0' })).choices).toEqual([]);
    expect(teamOf(session({ opinion: 'o1' })).choices).toEqual([]);
    expect(teamOf(session({ ask: 'a1' })).choices).toEqual([]);
  });
});
