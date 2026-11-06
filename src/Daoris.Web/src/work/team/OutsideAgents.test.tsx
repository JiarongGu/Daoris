import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { outsideAgentsOf } from '../../bridge/sessions/channelMembers';
import { whoOf } from '../../asks/ChannelSection';
import { CoWorkers } from './CoWorkers';
import type { OutsideAgent } from './outsideAgents';

// CHANNELJOIN1c (the event-channel design §5.8): *At work* lists a line's outside agents beside its co-workers: the agent, the repository,
// when it joined and last read or posted, and *Remove*; shown as itself, never as the person or a session. In both languages.

const nameOf = (agent: string) => (agent === 'codex' ? 'Codex' : agent);
const AGENTS: OutsideAgent[] = [
  { id: 'o1a2b3c4', agent: 'codex', repository: 'checker', joined: '2026-11-06T09:00:00Z', seen: '2026-11-06T09:30:00Z' },
  { id: 'o5d6e7f8', agent: 'other', repository: null, joined: '2026-11-06T09:10:00Z', seen: null },
];

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const draw = (over: Partial<Parameters<typeof CoWorkers>[0]> = {}) => render(
  <CoWorkers session="s1" lead="Claude Code" helpers={{ asks: [], nameOf }} outside={AGENTS} {...over} />,
);
const line = (id: string) => within(document.querySelector(`[data-outside-agent="${id}"]`) as HTMLElement);

describe('the outside agents beside the co-workers', () => {
  it('lists each as itself, outside Daoris, with its repository, when it joined and when it last read or posted', () => {
    draw();

    expect(screen.getByText('Lead · Claude Code')).toBeTruthy();
    expect(line('o1a2b3c4').getByText(/^Codex for checker · outside Daoris · joined .+ · last read or posted /)).toBeTruthy();
    expect(line('o5d6e7f8').getByText(/^another agent · outside Daoris · joined .+ · has not read or posted yet$/)).toBeTruthy();
    // Never as the person, a session or a helper.
    expect(document.querySelector('[data-member]')).toBeNull();
    expect(screen.queryByText(/helper|You\b/)).toBeNull();
  });

  it('is drawn with no co-worker asked and no door to bring one in, and nothing where there is none', () => {
    const { container, rerender } = draw();
    expect(container.querySelector('[data-coworkers]')).not.toBeNull();
    rerender(<CoWorkers session="s1" lead="Claude Code" helpers={{ asks: [], nameOf }} outside={[]} />);
    expect(container.querySelector('[data-coworkers]')).toBeNull();
  });

  it('presses Remove on the one agent and says why where it was not removed', async () => {
    const onRemove = vi.fn((_member: string, answered: { refused: (sentence: string) => void }) => answered.refused('It was not removed: not-member'));
    draw({ onRemoveOutside: onRemove });

    await userEvent.click(line('o1a2b3c4').getByRole('button', { name: 'Remove' }));

    expect(onRemove).toHaveBeenCalledWith('o1a2b3c4', expect.anything());
    expect(line('o1a2b3c4').getByText('It was not removed: not-member')).toBeTruthy();
    expect(line('o5d6e7f8').queryByText(/not removed/)).toBeNull();
  });

  it('offers no Remove where the page may not end a membership', () => {
    draw();
    expect(screen.queryByRole('button', { name: 'Remove' })).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    draw({ onRemoveOutside: vi.fn() });

    expect(line('o1a2b3c4').getByText(/^Codex（checker） · Daoris 之外 · .+加入 · .+最后读过或发过言$/)).toBeTruthy();
    expect(line('o5d6e7f8').getByText(/^另一个智能体 · Daoris 之外 · .+加入 · 还没有读过或发过言$/)).toBeTruthy();
    expect(within(document.querySelector('[data-outside-agent="o1a2b3c4"]') as HTMLElement).getByRole('button', { name: '移除' })).toBeTruthy();
  });
});

describe('the members the shell answers', () => {
  it('reads each field by itself, leaving out one with no id, no moment it joined, or one that left', () => {
    expect(outsideAgentsOf({
      members: [
        { id: 'o1a2b3c4', agent: 'codex', repository: 'checker', joined: '2026-11-06T09:00:00Z', seen: '2026-11-06T09:30:00Z' },
        { id: 'o5d6e7f8', joined: '2026-11-06T09:10:00Z' },
        { agent: 'codex', joined: '2026-11-06T09:10:00Z' },
        { id: 'o9999999', agent: 'codex' },
        { id: 'o0000000', agent: 'codex', joined: '2026-11-06T09:10:00Z', left: '2026-11-06T10:00:00Z' },
      ],
    })).toEqual(AGENTS.slice(0, 1).concat({ id: 'o5d6e7f8', agent: 'other', repository: null, joined: '2026-11-06T09:10:00Z', seen: null }));
    expect(outsideAgentsOf(null)).toEqual([]);
    expect(outsideAgentsOf({ members: 'none' })).toEqual([]);
  });

  it('names a member’s message in the channel as outside Daoris, a session’s as a session', () => {
    const t = i18n.t.bind(i18n);
    const at = '2026-11-06T09:00:00Z';
    expect(whoOf(t, { kind: 'message', at, repository: 'checker', agent: 'codex', outside: true })).toBe('checker · codex (outside Daoris)');
    expect(whoOf(t, { kind: 'message', at, outside: true })).toBe('An agent outside Daoris');
    expect(whoOf(t, { kind: 'message', at, repository: 'billing', agent: 'claude-code' })).toBe('billing · claude-code');
  });
});
