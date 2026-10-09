import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import { AskHistory } from './AskHistory';
import type { HelpConversationRow } from './history';

// IME1: a name or a search typed through an input method is composed, and the Escape that drops a composition is the
// input method's. Apart from `AskHistory.test.tsx`, whose history is being redrawn beside this (ASKHIST1c).

const ROW: HelpConversationRow = {
  session: 'h3', title: 'how do I land on a branch?', name: null, opening: 'how do I land on a branch?', about: null,
  created: new Date().toISOString(), last: new Date().toISOString(), pinned: null, live: false, resumable: true,
  from: null, handed: null, found: null,
};

function draw(search = '') {
  const onSearch = vi.fn();
  render(
    <Tooltip.Provider>
      <AskHistory
        rows={[ROW]} search={search} onSearch={onSearch}
        onOpen={vi.fn()} onRename={vi.fn()} onPin={vi.fn()} onStartFrom={vi.fn()} onDelete={vi.fn()}
      />
    </Tooltip.Provider>,
  );
  return { onSearch };
}

describe('Ask Daoris’s history while an input method composes', () => {
  it('keeps the rename open on a composing Escape, and puts it down on a plain one', async () => {
    draw();
    screen.getByRole('button', { name: i18n.t('help.history.menu', { title: ROW.title }) }).focus();
    await userEvent.keyboard('{Enter}');
    await userEvent.click(await screen.findByRole('menuitem', { name: i18n.t('help.history.rename') }));
    const field = screen.getByRole('textbox', { name: i18n.t('help.history.name') });

    expect(fireEvent.keyDown(field, { key: 'Escape', isComposing: true })).toBe(true);
    expect(fireEvent.keyDown(field, { key: 'Escape', keyCode: 229 })).toBe(true);
    expect(screen.getByRole('form')).toBeInTheDocument();

    fireEvent.keyDown(field, { key: 'Escape' });
    expect(screen.queryByRole('form')).toBeNull();
  });

  it('keeps a search on a composing Escape, and clears it on a plain one', () => {
    const { onSearch } = draw('分支');
    const box = screen.getByRole('searchbox', { name: i18n.t('help.history.search.label') });

    expect(fireEvent.keyDown(box, { key: 'Escape', isComposing: true })).toBe(true);
    expect(onSearch).not.toHaveBeenCalled();

    fireEvent.keyDown(box, { key: 'Escape' });
    expect(onSearch).toHaveBeenCalledWith('');
  });
});
