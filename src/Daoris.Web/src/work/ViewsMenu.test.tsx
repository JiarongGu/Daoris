import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
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
  /**
   * UXFIX1b: the shown view is one choice among the region's, so its tick is said as well as drawn: a radio row each,
   * in one group named for the region, the shown one `aria-checked`. As a plain item with a drawn tick, a screen reader
   * heard no view as the shown one.
   */
  it('names every view the region holds by its whole name, the shown one ticked and said', async () => {
    await open();
    const group = screen.getByRole('group', { name: 'views in the right side bar' });
    const views = within(group).getAllByRole('menuitemradio');
    expect(views.map((view) => view.textContent)).toEqual(['Timeline', 'Review', 'Ask Daoris']);
    expect(views.map((view) => view.getAttribute('aria-checked'))).toEqual(['false', 'true', 'false']);
    // The moves are acts, not choices: they stay plain items.
    expect(screen.getByRole('menuitem', { name: 'Move Review to the panel' })).toBeInTheDocument();
  });

  it('ticks no view when none is shown', async () => {
    await open({ selected: undefined });
    const views = screen.getAllByRole('menuitemradio');
    expect(views).toHaveLength(3);
    expect(views.every((view) => view.getAttribute('aria-checked') === 'false')).toBe(true);
  });

  it('shows the view picked from it', async () => {
    const { onSelect } = await open();
    await userEvent.click(screen.getByRole('menuitemradio', { name: 'Ask Daoris' }));
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
