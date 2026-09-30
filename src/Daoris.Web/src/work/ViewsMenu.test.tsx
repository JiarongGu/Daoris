import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { ViewsMenu, viewEntries } from './ViewsMenu';

// DOCK1b: the button at the end of a region's tab row. Chrome's tab list, so a tab drawn as its icon
// (TABS1) is never the only way to it, and VS Code's *Move to*, for the tab that is shown.

const t = (key: string) => ({
  'work.review.timelineTab': 'Timeline', 'work.review.tab': 'Review', 'help.title': 'Ask Daoris', 'work.views.console': 'Console',
}[key] ?? key);

async function open(props: Partial<Parameters<typeof ViewsMenu>[0]> = {}) {
  const onSelect = vi.fn();
  const onMove = vi.fn();
  render(
    <Tooltip.Provider>
      <ViewsMenu
        region="right"
        views={viewEntries(t, ['timeline', 'review', 'ask'])}
        selected="review"
        onSelect={onSelect}
        onMove={onMove}
        {...props}
      />
    </Tooltip.Provider>,
  );
  screen.getByRole('button', { name: /views in the/ }).focus();
  await userEvent.keyboard('{Enter}');
  return { onSelect, onMove };
}

afterEach(cleanup);

describe('the views menu', () => {
  it('names every view the region holds by its whole name, the shown one ticked', async () => {
    await open();
    const items = screen.getAllByRole('menuitem').map((item) => item.textContent);
    expect(items.slice(0, 3)).toEqual(['Timeline', 'Review', 'Ask Daoris']);
  });

  it('shows the view picked from it', async () => {
    const { onSelect } = await open();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Ask Daoris' }));
    expect(onSelect).toHaveBeenCalledWith('ask');
  });

  it('moves the shown view to the other region, and says which and where', async () => {
    const { onMove } = await open();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Move Review to the panel' }));
    expect(onMove).toHaveBeenCalledWith('review', 'panel');
  });

  it('moves out of the panel to the right side bar', async () => {
    const { onMove } = await open({ region: 'panel', views: viewEntries(t, ['console']), selected: 'console' });
    await userEvent.click(screen.getByRole('menuitem', { name: 'Move Console to the right side bar' }));
    expect(onMove).toHaveBeenCalledWith('console', 'right');
  });

  it('offers the reset only once something has moved', async () => {
    await open();
    expect(screen.queryByRole('menuitem', { name: 'Reset view locations' })).toBeNull();
    cleanup();

    const onReset = vi.fn();
    await open({ onReset });
    await userEvent.click(screen.getByRole('menuitem', { name: 'Reset view locations' }));
    expect(onReset).toHaveBeenCalled();
  });
});
