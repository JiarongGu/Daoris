import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { type MemoryRow, SessionMemory } from './SessionMemory';

// KNOWCENTRAL2e (the memory coordinator design §1, §4): a session's memory changes on its page, the person deciding its waits.

const row = (over: Partial<MemoryRow> & { id: string; kind: string }): MemoryRow => {
  const { kind, ...rest } = over;
  return {
    repository: 'Owner', state: 'staged', at: '2026-10-31T09:00:00Z', models: null, carried: null, answer: null,
    by: { session: 's1', adapter: 'claude-code' },
    change: { kind, text: 'The map is drawn in vectors now.', target: 'm1', revision: 1, relation: null },
    ...rest,
  };
};

afterEach(async () => { cleanup(); await i18n.changeLanguage('en'); });

describe("a session's memory", () => {
  it('shows a dispute and a proposed end of one entry as two decisions, only the end pressed by the person', async () => {
    const onDecide = vi.fn();
    render(<SessionMemory ended={false} onDecide={onDecide} onKeep={vi.fn()} rows={[
      row({ id: 'w1', kind: 'write', state: 'applied', change: { kind: 'write', text: 'The map is drawn in vectors.', target: 'm1', revision: null, relation: 'disputes' } }),
      row({ id: 'e1', kind: 'end', state: 'awaiting', models: 'opus' }),
    ]} />);

    const decisions = screen.getAllByRole('listitem');
    expect(decisions).toHaveLength(2);
    expect(within(decisions[0]!).getByText(/Disputes Owner:m1/)).toBeTruthy();
    expect(within(decisions[0]!).queryByRole('button')).toBeNull();
    expect(within(decisions[1]!).getByText(/End of Owner:m1 at revision 1/)).toBeTruthy();
    expect(within(decisions[1]!).getByText(/claude-code · opus/)).toBeTruthy();
    await userEvent.click(within(decisions[1]!).getByRole('button', { name: 'Approve the end' }));
    expect(onDecide).toHaveBeenCalledWith('e1', true);
  });

  it("offers Keep or Drop only on an ended session's staged changes", async () => {
    const onKeep = vi.fn();
    const staged = [row({ id: 'w2', kind: 'write' })];
    const { rerender } = render(<SessionMemory ended={false} rows={staged} onDecide={vi.fn()} onKeep={onKeep} />);
    expect(screen.queryByRole('button', { name: 'Drop' })).toBeNull();

    rerender(<SessionMemory ended rows={staged} onDecide={vi.fn()} onKeep={onKeep} />);
    expect(screen.getByText(/1 change waits on your Keep or Drop/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Drop' }));
    expect(onKeep).toHaveBeenCalledWith(false);
  });

  it("says a proposal's standing and the owner's answer", () => {
    render(<SessionMemory ended={false} onDecide={vi.fn()} onKeep={vi.fn()} rows={[
      row({ id: 'p1', kind: 'propose', state: 'declined', carried: 'q1', answer: 'The tiles stay.', change: { kind: 'propose', text: 'Tiles were retired.', target: 'm1', revision: 1, relation: 'end' } }),
    ]} />);
    expect(screen.getByText(/Proposed to Owner on quest #q1: end Owner:m1/)).toBeTruthy();
    expect(screen.getByText(/declined: The tiles stay\./)).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    render(<SessionMemory ended onDecide={vi.fn()} onKeep={vi.fn()} rows={[row({ id: 'e2', kind: 'end', state: 'awaiting' }), row({ id: 'w3', kind: 'write' })]} />);
    expect(screen.getByRole('region', { name: '记忆' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '批准这项结束' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '保留' })).toBeTruthy();
  });

  it('draws nothing for a session with no memory changes', () => {
    const { container } = render(<SessionMemory ended rows={[]} onDecide={vi.fn()} onKeep={vi.fn()} />);
    expect(container.innerHTML).toBe('');
  });
});
