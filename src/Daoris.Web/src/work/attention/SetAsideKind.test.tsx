import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import { type Attention } from './AttentionRow';
import { AttentionList } from './AttentionList';
import { repeatedKinds } from './repeatedKinds';

const row = (kind: Attention['kind'], id: string): Attention => ({
  id, kind, title: `Row ${id}`, where: 'engine', since: '2026-09-21T09:00:00Z', detail: 'Why it waits.',
});

const drawn = (props: Parameters<typeof AttentionList>[0]) => <Tooltip.Provider><AttentionList {...props} /></Tooltip.Provider>;

const many =(kind: Attention['kind'], count: number) => Array.from({ length: count }, (_, at) => row(kind, `${kind}-${at}`));

describe('setting one kind of row aside', () => {
  it('names the kinds a group holds three or more of, and never a press’s own card', () => {
    const rows = [...many('parked', 3), ...many('review', 2), ...many('confirmation', 5)];

    expect(repeatedKinds(rows)).toEqual([{ kind: 'parked', count: 3 }]);
  });

  it('is one press under a confirmation that hands the kind and its count back', async () => {
    const onSetAsideKind = vi.fn();
    render(drawn({ items: [...many('parked', 4), ...many('review', 1)], onSetAsideKind }));

    expect(screen.getByText('4 rows are parked at a checkpoint.')).toBeInTheDocument();
    expect(onSetAsideKind).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Set all 4 aside…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Set 4 rows aside' }));

    expect(onSetAsideKind).toHaveBeenCalledWith('parked', 4, expect.objectContaining({ done: expect.any(Function) }));
  });

  it('is offered only where the band hands it, and not for a kind held fewer than three times', () => {
    const { rerender } = render(drawn({ items: many('parked', 4) }));
    expect(screen.queryByRole('button', { name: /aside/ })).toBeNull();

    rerender(drawn({ items: many('parked', 2), onSetAsideKind: () => {} }));
    expect(screen.queryByRole('button', { name: /aside/ })).toBeNull();
  });

  it('says the same in Chinese', async () => {
    await i18n.changeLanguage('zh');
    try {
      render(drawn({ items: many('parked', 3), onSetAsideKind: () => {} }));
      const group = screen.getAllByRole('group')[0]!;
      expect(within(group).getByRole('button', { name: '全部搁置 3 行…' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});
