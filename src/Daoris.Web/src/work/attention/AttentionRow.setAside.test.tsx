import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import { type Attention, AttentionRow } from './AttentionRow';

const row = (kind: Attention['kind'], id: string): Attention => ({
  id, kind, title: 'Expose a streaming budget on the chunk API', where: 'engine', since: '2026-09-21T09:00:00Z', detail: 'Why it waits.',
});

describe('setting a row aside', () => {
  it.each([
    ['a parked session', row('parked', 's1a2b3c4')],
    ['a go-ahead', row('go-ahead', '7c1e9a04b2d5#2')],
    ['a quest parked on its failed sessions', row('parked-quest', '7a82cc')],
    ['work to review', row('review', 'r3v13w00')],
  ])('is one press on %s, handing the row back', async (_name, item) => {
    const setAside = vi.fn();
    render(<AttentionRow item={item} acts={{ setAside }} />);

    await userEvent.click(screen.getByRole('button', { name: 'Set aside' }));
    expect(setAside).toHaveBeenCalledWith(item);
  });

  it('is offered where the band hands it and nowhere else, and not on a terminal’s press', () => {
    const { unmount } = render(<AttentionRow item={row('parked', 's1')} acts={{}} />);
    expect(screen.queryByRole('button', { name: 'Set aside' })).toBeNull();
    unmount();

    render(<AttentionRow item={row('confirmation', 'c1')} acts={{ setAside: vi.fn() }} />);
    expect(screen.queryByRole('button', { name: 'Set aside' })).toBeNull();
  });

  it('is said in 中文', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(<AttentionRow item={row('parked', 's1')} acts={{ setAside: vi.fn() }} />);
      expect(screen.getByRole('button', { name: '搁置' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
