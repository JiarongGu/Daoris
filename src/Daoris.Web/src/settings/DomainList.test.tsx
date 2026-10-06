import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LIST_ROW, useListKeys } from '../work/listKeys';
import { DomainList } from './DomainList';

// FRAME1g (D118 §2, §5): Settings' domains as its list pane holds them. A molecule: the domains, their names
// and the one chosen arrive as props, and a choice goes out.

const DOMAINS = [
  { id: 'start', label: 'Get started' },
  { id: 'appearance', label: 'Appearance' },
  { id: 'ai', label: 'AI features' },
  { id: 'driver', label: 'Driver' },
];

function Keyed(props: Parameters<typeof DomainList>[0]) {
  // The list pane binds the keys on the element holding the rows (`ListPane`'s body); this stands in for it.
  const keys = useListKeys();
  return <div onKeyDown={keys}><DomainList {...props} /></div>;
}

const draw = (extra: Partial<Parameters<typeof DomainList>[0]> = {}) => {
  const onChoose = vi.fn();
  render(<Keyed label="Settings domains" domains={DOMAINS} chosen="ai" onChoose={onChoose} {...extra} />);
  return onChoose;
};

describe("Settings' domain list", () => {
  it('names each domain once, in order, the chosen one marked as the page shown', () => {
    draw();

    const domains = screen.getByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getAllByRole('button').map((button) => button.textContent))
      .toEqual(['Get started', 'Appearance', 'AI features', 'Driver']);
    expect(within(domains).getByRole('button', { name: 'AI features' })).toHaveAttribute('aria-current', 'page');
    expect(within(domains).getByRole('button', { name: 'Get started' })).not.toHaveAttribute('aria-current');
  });

  it('is a list pane\'s rows: each row carries the mark the keys move between', () => {
    draw();

    const rows = document.querySelectorAll(`[${LIST_ROW}]`);
    expect(rows).toHaveLength(4);
    expect(within(rows[0] as HTMLElement).getByRole('button', { name: 'Get started' })).toBeInTheDocument();
  });

  it('asks for a domain by its id, by a press and by the keys', async () => {
    const onChoose = draw();
    const user = userEvent.setup();

    await user.click(screen.getByRole('button', { name: 'Driver' }));
    expect(onChoose).toHaveBeenLastCalledWith('driver');

    // ↑ from Driver reaches AI features, Home the first, and Enter opens the row it is on.
    await user.keyboard('{ArrowUp}');
    expect(screen.getByRole('button', { name: 'AI features' })).toHaveFocus();
    await user.keyboard('{Home}');
    expect(screen.getByRole('button', { name: 'Get started' })).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(onChoose).toHaveBeenLastCalledWith('start');
  });

  it('says beneath the domains what a window is not offered, where it is handed a note', () => {
    draw({ note: "A machine's own settings are on the desktop, where the machine is." });

    const domains = screen.getByRole('navigation', { name: 'Settings domains' });
    expect(within(domains).getByText("A machine's own settings are on the desktop, where the machine is.")).toBeInTheDocument();
  });
});
