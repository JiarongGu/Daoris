import type { ReactElement } from 'react';
import { afterEach, describe, expect, it } from 'vitest';
import { act, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../i18n';
import type { SessionState } from '../api';
import { layTabs } from '../test/tabRoom';
import { type DockPreview, dockOpensOn, followed, RightDock } from './RightDock';
import type { ViewId } from './placements';

/** The provider the application mounts once (`main.tsx`), with no delay so a tip is read at once. */
const render = (node: ReactElement) => {
  const result = rtlRender(<Tooltip.Provider delayDuration={0}>{node}</Tooltip.Provider>);
  return {
    ...result,
    rerender: (next: ReactElement) => result.rerender(<Tooltip.Provider delayDuration={0}>{next}</Tooltip.Provider>),
  };
};

const THREE: ViewId[] = ['timeline', 'review', 'ask'];
const PREVIEW: DockPreview = { name: 'chunk.rs', path: 'src/world/streaming/chunk.rs', onClose: () => {} };

const dock = (props: Partial<Parameters<typeof RightDock>[0]> = {}) => (
  <RightDock
    tab="timeline" views={THREE} mode="docked" width={430}
    onTab={() => {}} onClose={() => {}} onOpen={() => {}} onFull={() => {}}
    {...props}
  >
    <p>the surface</p>
  </RightDock>
);

/**
 * UX6b (design §2.5, D150 §8): off Sessions the side bar kept an ended session from the day before, its whole done note
 * running past the window's foot. A session's views follow the attended session off Sessions only while it runs or waits
 * on the person; with none followed the side bar opens on Ask Daoris. On Sessions nothing changes.
 */
describe('what the side bar follows', () => {
  const session = (state: SessionState) => ({ id: 's1a2b3c4', state });
  const ENDED: SessionState[] = ['completed', 'declined', 'stood-down', 'failed', 'stopped'];
  const LIVE: SessionState[] = ['queued', 'starting', 'working', 'awaiting-person'];

  it('follows the attended session on Sessions, whatever its state', () => {
    for (const state of [...LIVE, ...ENDED]) expect(followed(session(state), false)).toEqual(session(state));
    expect(followed(null, false)).toBeNull();
  });

  it('follows it off Sessions only while it runs or waits on the person', () => {
    for (const state of LIVE) expect(followed(session(state), true)).toEqual(session(state));
    for (const state of ENDED) expect(followed(session(state), true)).toBeNull();
    expect(followed(null, true)).toBeNull();
  });

  it('opens on Ask Daoris off Sessions with no session followed, and on the timeline otherwise', () => {
    expect(dockOpensOn(true, false)).toBe('ask');
    expect(dockOpensOn(true, true)).toBe('timeline');
    // On Sessions it opens on the timeline as it always has, attended or not: nothing changes there.
    expect(dockOpensOn(false, false)).toBe('timeline');
    expect(dockOpensOn(false, true)).toBe('timeline');
  });
});

/** Each tab as the eye reads it: the words it shows, which are none when it is drawn as its icon. */
const shown = () => screen.getAllByRole('tab').map((tab) => [tab.getAttribute('aria-label'), tab.textContent]);
const row = () => screen.getByRole('tablist').closest('[data-fit]');

/**
 * TABS1: a dock tab shows its whole name or its icon, never a name cut to one character. At a 430px
 * side bar four tabs needed 367 of 349px, each unselected one shrank by about 6px, and 时间线 read as
 * 时… — cutting a short name buys nothing.
 */
describe('the side bar\'s tabs', () => {
  let room: ReturnType<typeof layTabs> | undefined;
  afterEach(async () => {
    room?.restore();
    room = undefined;
    await i18n.changeLanguage('en');
  });

  it('keeps every name whole while the row holds them', () => {
    room = layTabs(900);
    render(dock());

    expect(shown()).toEqual([['Timeline', 'Timeline'], ['Review', 'Review'], ['Ask Daoris', 'Ask Daoris']]);
    expect(row()?.getAttribute('data-fit')).toBe('names');
  });

  it('draws the unselected tabs as their icons when the row cannot hold every name whole, each still named', async () => {
    room = layTabs(430);
    render(dock({ tab: 'review' }));

    // The selected keeps its whole name, as a browser's active tab keeps its width; the rest are icons.
    expect(shown()).toEqual([['Timeline', ''], ['Review', 'Review'], ['Ask Daoris', '']]);
    expect(row()?.getAttribute('data-fit')).toBe('icons');

    // Named still: by its label to a screen reader, and by its tip to the eye.
    const ask = screen.getByRole('tab', { name: 'Ask Daoris' });
    await userEvent.hover(ask);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Ask Daoris');
  });

  it('never cuts a view\'s name: no tab shrinks, and no name carries an ellipsis', () => {
    room = layTabs(430);
    render(dock({ tab: 'review' }));

    for (const tab of screen.getAllByRole('tab')) {
      expect(tab).toHaveClass('shrink-0');
      expect(tab.querySelector('.truncate')).toBeNull();
    }
  });

  it('is its icons at the side bar the window showed, in 中文 too', async () => {
    await i18n.changeLanguage('zh');
    room = layTabs(430);
    render(dock({ tab: 'preview', preview: PREVIEW }));

    // 时间线, 审阅 and 问道衍 with a preview: 368px of names in 350px of room.
    expect(shown()).toEqual([['时间线', ''], ['审阅', ''], ['问道衍', ''], ['预览：chunk.rs', 'chunk.rs']]);
  });

  it('shows every name again once the side bar is widened to hold them', () => {
    room = layTabs(430);
    render(dock());
    expect(row()?.getAttribute('data-fit')).toBe('icons');

    room.resize(900);

    expect(shown()).toEqual([['Timeline', 'Timeline'], ['Review', 'Review'], ['Ask Daoris', 'Ask Daoris']]);
  });

  it('gives way to its icons as the side bar is dragged narrower, and settles there', () => {
    room = layTabs(900);
    render(dock());
    expect(row()?.getAttribute('data-fit')).toBe('names');

    room.resize(430);
    expect(row()?.getAttribute('data-fit')).toBe('icons');

    // A resize that changes nothing changes nothing: the row does not flip back and forth.
    room.resize(430);
    expect(row()?.getAttribute('data-fit')).toBe('icons');
    // Not until the row holds what the names needed (140 + 116 + 164 = 420px, after 80 of buttons).
    room.resize(499);
    expect(row()?.getAttribute('data-fit')).toBe('icons');
    room.resize(500);
    expect(row()?.getAttribute('data-fit')).toBe('names');
  });

  it('measures again when the language changes', async () => {
    // Timeline, Review and Ask Daoris need 420px; 时间线, 审阅 and 问道衍 need 228.
    room = layTabs(430);
    render(dock());
    expect(row()?.getAttribute('data-fit')).toBe('icons');

    await act(async () => { await i18n.changeLanguage('zh'); });

    expect(shown()).toEqual([['时间线', '时间线'], ['审阅', '审阅'], ['问道衍', '问道衍']]);
  });

  it('measures again when a preview opens, and when it closes', () => {
    room = layTabs(430);
    const { rerender } = render(dock({ views: ['timeline', 'review'] }));
    expect(row()?.getAttribute('data-fit')).toBe('names');

    const long = { ...PREVIEW, name: 'streaming-budget-report.md' };
    rerender(dock({ views: ['timeline', 'review'], preview: long }));
    expect(row()?.getAttribute('data-fit')).toBe('icons');

    rerender(dock({ views: ['timeline', 'review'] }));
    expect(shown()).toEqual([['Timeline', 'Timeline'], ['Review', 'Review']]);
  });

  describe('a file\'s preview', () => {
    it('is its whole name, never cut, while another tab is shown and the row holds it', () => {
      room = layTabs(1200);
      render(dock({ preview: PREVIEW }));

      const name = screen.getByText('chunk.rs');
      expect(name).not.toHaveClass('truncate');
      expect(name.closest('.border-b-2')).not.toHaveClass('max-w-52');
    });

    it('is its icon and its close when the row cannot hold every name whole, named still', async () => {
      room = layTabs(430);
      render(dock({ preview: PREVIEW }));

      const tab = screen.getByRole('tab', { name: 'Preview: chunk.rs' });
      expect(tab.textContent).toBe('');
      expect(screen.getByRole('button', { name: 'Close the preview' })).toBeTruthy();
      await userEvent.hover(tab);
      expect(await screen.findByRole('tooltip')).toHaveTextContent('Preview: src/world/streaming/chunk.rs');
    });

    it('keeps its name while it is shown, cut at its cap when the name is longer', () => {
      room = layTabs(430);
      render(dock({ tab: 'preview', preview: PREVIEW }));

      // A file's name is long and its tip carries the path, so the shown preview alone may be cut.
      const name = within(screen.getByRole('tab', { name: 'Preview: chunk.rs' })).getByText('chunk.rs');
      expect(name).toHaveClass('truncate');
      expect(name.closest('.border-b-2')).toHaveClass('max-w-52');
    });
  });
});
