import type { ReactElement } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render as rtlRender, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../i18n';
import { StripMark } from '../ui';
import { LIST_BOUNDS, LIST_STRIP, type ListLayout } from './layout';
import { LIST_DOOR, LIST_ROW } from './listKeys';
import { ListPane } from './ListPane';

// D118 §3a, §5: one list pane for every view that has a list, Sessions' rail first. A molecule: every
// state is reached by its props, and a press goes out.

const render = (node: ReactElement) => rtlRender(<Tooltip.Provider>{node}</Tooltip.Provider>);

const OPEN: ListLayout = { mode: 'open', width: 280, beside: 280, auto: false };
const CLOSED: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: false };
const DRAWN: ListLayout = { mode: 'strip', width: LIST_STRIP, beside: LIST_STRIP, auto: true };
const OVER: ListLayout = { mode: 'over', width: 280, beside: LIST_STRIP, auto: true };

const LABELS = { open: 'Show the session list', close: 'Hide the session list', resize: 'session list width' };

function pane(layout: ListLayout, { children, ...extra }: Partial<Parameters<typeof ListPane>[0]> = {}) {
  const calls = { onOpen: vi.fn(), onClose: vi.fn(), onDismiss: vi.fn(), onResize: vi.fn(), onMake: vi.fn() };
  const view = render(
    <div>
      <ListPane
        name="Sessions"
        labels={LABELS}
        layout={layout}
        bounds={LIST_BOUNDS.sessions}
        make={{ label: 'Start a session', onMake: calls.onMake }}
        strip={<ul><StripMark label="Chat · engine · working" initialOf="engine" tone="live" /></ul>}
        onOpen={calls.onOpen}
        onClose={calls.onClose}
        onDismiss={calls.onDismiss}
        onResize={calls.onResize}
        {...extra}
      >
        {children ?? (
          <ul>
            {['first', 'second'].map((name) => (
              <li key={name} {...{ [LIST_ROW]: '' }}><button type="button">{name}</button></li>
            ))}
          </ul>
        )}
      </ListPane>
      <p>the main area</p>
    </div>,
  );
  return { ...view, ...calls };
}

describe('the list pane, open', () => {
  it('holds its name, its ＋ and its close in its header, and is resized by its right edge within its bounds', async () => {
    const { onMake, onClose, onResize } = pane(OPEN);

    expect(screen.getByText('Sessions')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Start a session' }));
    expect(onMake).toHaveBeenCalledWith(undefined);
    await userEvent.click(screen.getByRole('button', { name: 'Hide the session list' }));
    expect(onClose).toHaveBeenCalled();

    const edge = screen.getByRole('separator', { name: 'session list width' });
    expect(edge).toHaveAttribute('aria-valuenow', '280');
    expect(edge).toHaveAttribute('aria-valuemin', '264');
    expect(edge).toHaveAttribute('aria-valuemax', '420');
    fireEvent.doubleClick(edge);
    expect(onResize).toHaveBeenCalledWith(null);
    // Its rows are the list, and the strip's marks are not drawn beside them.
    expect(screen.getByRole('button', { name: 'first' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Chat · engine · working' })).toBeNull();
  });

  it('moves between its rows by the arrows', async () => {
    pane(OPEN);
    screen.getByRole('button', { name: 'first' }).focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('button', { name: 'second' })).toHaveFocus();
  });

  it('holds the view\'s own ⋯ in its header, before its close', () => {
    pane(OPEN, { more: <button type="button">filters</button> });
    const header = screen.getByText('Sessions').closest('header')!;
    const names = within(header).getAllByRole('button').map((button) => button.getAttribute('aria-label') ?? button.textContent);
    expect(names).toEqual(['Start a session', 'filters', 'Hide the session list']);
  });
});

describe('the list pane, closed to its strip', () => {
  it('keeps its open, its ＋ and its marks, and no edge to drag', async () => {
    const { onOpen, onMake } = pane(CLOSED);

    expect(screen.queryByText('Sessions')).toBeNull();
    expect(screen.queryByRole('separator')).toBeNull();
    expect(screen.getByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    expect(onOpen).toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Start a session' }));
    expect(onMake).toHaveBeenCalled();
  });

  /** D118 §3a, amending FRAME6: a strip the window drew has an open too, which lays the list over. */
  it('offers the open on a strip the window drew as well', async () => {
    const { onOpen } = pane(DRAWN);
    const open = screen.getByRole('button', { name: 'Show the session list' });
    expect(open).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(open);
    expect(onOpen).toHaveBeenCalled();
  });

  it('shows its controls and not its marks while the list first loads', () => {
    pane(CLOSED, { loading: true });
    expect(screen.getByRole('button', { name: 'Show the session list' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Chat · engine · working' })).toBeNull();
  });
});

describe('the list pane, laid over the main area', () => {
  it('is the list beside its strip, named, with the focus in it', () => {
    pane(OVER);
    const over = screen.getByRole('region', { name: 'Sessions' });
    expect(within(over).getByRole('button', { name: 'first' })).toBeInTheDocument();
    expect(over).toHaveFocus();
    expect(over).toHaveStyle({ width: '280px' });
    // The strip stays, so nothing hides behind a hamburger (platform language §2).
    expect(screen.getByRole('button', { name: 'Chat · engine · working' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Show the session list' })).toHaveAttribute('aria-expanded', 'true');
  });

  it('closes on Escape, handing the focus back to the strip\'s open', async () => {
    const { onDismiss } = pane(OVER);
    await userEvent.keyboard('{Escape}');
    expect(onDismiss).toHaveBeenCalledOnce();
    expect(screen.getByRole('button', { name: 'Show the session list' })).toHaveFocus();
  });

  it('leaves Escape to a search inside it that clears on it, and closes on the next', async () => {
    const { onDismiss } = pane(OVER, {
      children: (
        <input
          aria-label="search"
          defaultValue="engine"
          onKeyDown={(event) => {
            if (event.key !== 'Escape' || !event.currentTarget.value) return;
            event.preventDefault();
            event.currentTarget.value = '';
          }}
        />
      ),
    });
    const box = screen.getByRole('textbox', { name: 'search' });
    box.focus();
    await userEvent.keyboard('{Escape}');
    expect(onDismiss).not.toHaveBeenCalled();
    await userEvent.keyboard('{Escape}');
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it('closes on a press outside it, and not on one inside it or on its strip', async () => {
    const { onDismiss } = pane(OVER);
    fireEvent.pointerDown(screen.getByRole('button', { name: 'first' }));
    fireEvent.pointerDown(screen.getByRole('button', { name: 'Chat · engine · working' }));
    expect(onDismiss).not.toHaveBeenCalled();

    fireEvent.pointerDown(screen.getByText('the main area'));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it('leaves a press on one of its doors elsewhere to that door, which toggles it itself', () => {
    const { onDismiss } = pane(OVER);
    const door = document.createElement('button');
    door.setAttribute(LIST_DOOR, '');
    document.body.append(door);
    try {
      fireEvent.pointerDown(door);
      expect(onDismiss).not.toHaveBeenCalled();
    } finally {
      door.remove();
    }
  });

  it('closes from its header, and from the strip\'s open pressed again', async () => {
    const { onDismiss, onClose } = pane(OVER);
    await userEvent.click(within(screen.getByRole('region', { name: 'Sessions' })).getByRole('button', { name: 'Hide the session list' }));
    await userEvent.click(screen.getByRole('button', { name: 'Show the session list' }));
    expect(onDismiss).toHaveBeenCalledTimes(2);
    expect(onClose).not.toHaveBeenCalled();
  });
});

describe('the list pane\'s ＋', () => {
  it('offers both kinds where a view makes two, its primary first', async () => {
    const onMake = vi.fn();
    pane(OPEN, {
      make: { label: 'New', kinds: [{ id: 'ask', label: 'Ask' }, { id: 'quest', label: 'New quest' }], onMake },
    });

    screen.getByRole('button', { name: 'New' }).focus();
    await userEvent.keyboard('{Enter}');
    const items = await screen.findAllByRole('menuitem');
    expect(items.map((item) => item.textContent)).toEqual(['Ask', 'New quest']);
    await userEvent.click(items[1]!);
    expect(onMake).toHaveBeenCalledWith('quest');
  });
});

describe('the list pane\'s states', () => {
  it('draws skeleton rows while the list first loads, never the empty state', () => {
    const { container } = pane(OPEN, { loading: true, empty: { headline: 'Nothing is running', body: 'Sessions appear here.' } });
    expect(screen.queryByRole('button', { name: 'first' })).toBeNull();
    expect(screen.queryByText('Nothing is running')).toBeNull();
    expect(container.querySelector('[aria-hidden] .grid, .grid[aria-hidden]')).not.toBeNull();
  });

  it('says it is empty with the ＋\'s act', async () => {
    const { onMake } = pane(OPEN, { empty: { headline: 'Nothing is running', body: 'Sessions appear here.' } });
    expect(screen.getByText('Nothing is running')).toBeInTheDocument();
    const acts = screen.getAllByRole('button', { name: 'Start a session' });
    expect(acts).toHaveLength(2);
    await userEvent.click(acts[1]!);
    expect(onMake).toHaveBeenCalled();
  });
});
