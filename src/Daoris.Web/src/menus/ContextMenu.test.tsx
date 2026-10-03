import * as React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../i18n';
import { Markdown } from '../work/Markdown';
import { ExternalLink } from '../links';
import { rightClick } from '../test/contextMenu';
import { PathText } from '../ui';
import { type ContextDoors, type ContextOffer, contextOffer } from './press';
import { ContextMenus } from './ContextMenu';

// CTX1 (D138): the one right-click handler, as a window holds it — what it opens on a surface, on selected words, on a
// link and on a code span; where it leaves the engine's menu and where it suppresses it; the keys; the menu's role,
// focus and Esc; and both languages.

function doors() {
  return { copy: vi.fn(), search: vi.fn(), ask: vi.fn(), openInBrowser: vi.fn() };
}

const take = vi.fn();
const remove = vi.fn();
const OFFER: ContextOffer = {
  label: 'Expose a streaming budget',
  acts: [
    { id: 'take', label: 'Take', onSelect: take },
    { id: 'delete', label: 'Delete…', icon: 'remove', onSelect: remove },
    { id: 'copy', label: 'Copy quest ID', icon: 'copy', copy: 'abc123' },
  ],
};

/** A window: the handler, and a page the engine's press can focus, as a main area can be. */
function windowWith(children: React.ReactNode, given: ContextDoors = doors()) {
  render(
    <>
      <ContextMenus doors={given} />
      <div data-page="" tabIndex={-1}>{children}</div>
    </>,
  );
  return given as ReturnType<typeof doors>;
}

afterEach(async () => {
  cleanup();
  vi.clearAllMocks();
  window.getSelection()?.removeAllRanges();
  await i18n.changeLanguage('en');
});

describe('a surface’s acts', () => {
  it('opens a menu of them at the pointer, named for the surface, and suppresses the engine’s', async () => {
    windowWith(<section {...contextOffer(OFFER)}><p>The quest’s page</p></section>);

    const engine = rightClick(screen.getByText('The quest’s page'));
    expect(engine).toBe(false);

    const menu = await screen.findByRole('menu', { name: 'Actions for Expose a streaming budget' });
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Take', 'Delete…', 'Copy quest ID']);
  });

  it('carries out the act chosen, and closes', async () => {
    const given = windowWith(<section {...contextOffer(OFFER)}><p>The quest’s page</p></section>);
    rightClick(screen.getByText('The quest’s page'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Take' }));
    expect(take).toHaveBeenCalledOnce();
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());

    // A copy goes to the window's copy, which says it copied.
    rightClick(screen.getByText('The quest’s page'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy quest ID' }));
    expect(given.copy).toHaveBeenCalledWith('abc123');
  });

  it('the innermost surface offers: a row inside a page offers the row’s', async () => {
    const row: ContextOffer = { label: 'A row', acts: [{ id: 'open', label: 'Open', onSelect: () => {} }] };
    windowWith(
      <section {...contextOffer(OFFER)}>
        <ul><li {...contextOffer(row)}>the row</li></ul>
      </section>,
    );
    rightClick(screen.getByText('the row'));
    const menu = await screen.findByRole('menu', { name: 'Actions for A row' });
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Open']);
  });

  it('Esc closes it, and the focus goes back where it was', async () => {
    windowWith(<section {...contextOffer(OFFER)}><button type="button">A row’s door</button></section>);
    const door = screen.getByRole('button', { name: 'A row’s door' });
    door.focus();
    rightClick(door);
    await screen.findByRole('menu');
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(door));
  });

  it('an act that moves the focus keeps it there', async () => {
    function Page() {
      const [asking, setAsking] = React.useState(false);
      return (
        <section {...contextOffer({ label: 'q', acts: [{ id: 'decline', label: 'Decline…', onSelect: () => setAsking(true) }] })}>
          <button type="button">door</button>
          {asking && <input aria-label="Why" autoFocus />}
        </section>
      );
    }
    windowWith(<Page />);
    screen.getByRole('button', { name: 'door' }).focus();
    rightClick(screen.getByRole('button', { name: 'door' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Decline…' }));
    const why = await screen.findByRole('textbox', { name: 'Why' });
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    // Past the menu's own return of the focus, a beat later.
    await act(() => new Promise((resolve) => setTimeout(resolve, 10)));
    expect(document.activeElement).toBe(why);
  });

  it('a right-click elsewhere opens the next menu in its place', async () => {
    const other: ContextOffer = { label: 'Other', acts: [{ id: 'open', label: 'Open', onSelect: () => {} }] };
    windowWith(
      <>
        <section {...contextOffer(OFFER)}><p>one</p></section>
        <section {...contextOffer(other)}><p>two</p></section>
      </>,
    );
    rightClick(screen.getByText('one'));
    await screen.findByRole('menu', { name: 'Actions for Expose a streaming budget' });
    rightClick(screen.getByText('two'));
    await screen.findByRole('menu', { name: 'Actions for Other' });
    expect(screen.getAllByRole('menu')).toHaveLength(1);
  });
});

describe('where the menu is not the page’s', () => {
  it('a text field keeps the engine’s: cut, copy and paste', () => {
    windowWith(
      <section {...contextOffer(OFFER)}>
        <input aria-label="Reason" />
        <textarea aria-label="Message" />
      </section>,
    );
    expect(rightClick(screen.getByRole('textbox', { name: 'Reason' }))).toBe(true);
    expect(rightClick(screen.getByRole('textbox', { name: 'Message' }))).toBe(true);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('nothing offered: no menu, and the engine’s suppressed', () => {
    windowWith(<p>Plain words on the page</p>);
    expect(rightClick(screen.getByText('Plain words on the page'))).toBe(false);
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('a surface that answers the right-click itself (a tab’s views menu) is left to it', () => {
    const own = vi.fn((event: React.MouseEvent) => event.preventDefault());
    windowWith(<section {...contextOffer(OFFER)}><button type="button" onContextMenu={own}>A tab</button></section>);
    rightClick(screen.getByRole('button', { name: 'A tab' }));
    expect(own).toHaveBeenCalledOnce();
    expect(screen.queryByRole('menu')).toBeNull();
  });

  it('a browser keeps its own menu wherever no surface offers acts, a link and selected words included', async () => {
    render(
      <>
        <ContextMenus doors={{ copy: vi.fn() }} shell={false} />
        <div data-page="" tabIndex={-1}>
          <p>Plain words</p>
          <ExternalLink href="https://tickets.example/T-1">T-1</ExternalLink>
          <section {...contextOffer(OFFER)}><p>The quest’s page</p></section>
        </div>
      </>,
    );
    expect(rightClick(screen.getByText('Plain words'))).toBe(true);
    expect(rightClick(screen.getByRole('link', { name: 'T-1' }))).toBe(true);
    expect(screen.queryByRole('menu')).toBeNull();

    // A surface's acts are the page's to offer, in a browser too.
    expect(rightClick(screen.getByText('The quest’s page'))).toBe(false);
    expect(await screen.findByRole('menu', { name: 'Actions for Expose a streaming budget' })).toBeInTheDocument();
  });

  it('a right-click on the open menu asks for nothing', async () => {
    windowWith(<section {...contextOffer(OFFER)}><p>page</p></section>);
    rightClick(screen.getByText('page'));
    const item = await screen.findByRole('menuitem', { name: 'Take' });
    expect(rightClick(item)).toBe(false);
    expect(screen.getAllByRole('menu')).toHaveLength(1);
  });
});

describe('selected words', () => {
  function select(element: Element) {
    const range = document.createRange();
    range.selectNodeContents(element);
    // A selection holds one range, and a second is ignored: the menu's focus left a collapsed one behind.
    window.getSelection()!.removeAllRanges();
    window.getSelection()!.addRange(range);
  }

  it('Copy, Search Daoris for it, Ask Daoris about it, each with the words', async () => {
    const given = windowWith(<p>a per-frame budget</p>);
    const words = screen.getByText('a per-frame budget');
    select(words);
    rightClick(words);

    const menu = await screen.findByRole('menu', { name: 'Actions here' });
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent))
      .toEqual(['Copy', 'Search Daoris for it', 'Ask Daoris about it']);

    await userEvent.click(screen.getByRole('menuitem', { name: 'Search Daoris for it' }));
    expect(given.search).toHaveBeenCalledWith('a per-frame budget');

    select(words);
    rightClick(words);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Ask Daoris about it' }));
    expect(given.ask).toHaveBeenCalledWith('> a per-frame budget\n\n');

    select(words);
    rightClick(words);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy' }));
    expect(given.copy).toHaveBeenCalledWith('a per-frame budget');
  });

  it('a selection on a surface comes before the surface’s acts', async () => {
    windowWith(<section {...contextOffer(OFFER)}><p>the ask itself</p></section>);
    select(screen.getByText('the ask itself'));
    rightClick(screen.getByText('the ask itself'));
    const menu = await screen.findByRole('menu');
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent))
      .toEqual(['Copy', 'Search Daoris for it', 'Ask Daoris about it', 'Take', 'Delete…', 'Copy quest ID']);
    expect(within(menu).getAllByRole('separator')).toHaveLength(1);
  });
});

describe('a link', () => {
  it('Open (its own click), Open in Daoris’s browser, Copy link', async () => {
    const given = windowWith(<ExternalLink href="https://tickets.example/T-1">T-1</ExternalLink>);
    const link = screen.getByRole('link', { name: 'T-1' });
    const clicked = vi.fn((event: MouseEvent) => event.preventDefault());
    link.addEventListener('click', clicked);

    rightClick(link);
    const menu = await screen.findByRole('menu');
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent))
      .toEqual(['Open', "Open in Daoris's browser", 'Copy link']);

    await userEvent.click(screen.getByRole('menuitem', { name: 'Open' }));
    expect(clicked).toHaveBeenCalledOnce();

    rightClick(link);
    await userEvent.click(await screen.findByRole('menuitem', { name: "Open in Daoris's browser" }));
    expect(given.openInBrowser).toHaveBeenCalledWith('https://tickets.example/T-1');

    rightClick(link);
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy link' }));
    expect(given.copy).toHaveBeenCalledWith('https://tickets.example/T-1');
  });

  it('a sign-in’s link is never offered to Daoris’s browser, and a browser has none to offer', async () => {
    windowWith(<ExternalLink href="https://login.example/" system>sign in</ExternalLink>);
    rightClick(screen.getByRole('link', { name: 'sign in' }));
    expect(within(await screen.findByRole('menu')).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Open', 'Copy link']);
    cleanup();

    windowWith(<ExternalLink href="https://tickets.example/T-1">T-1</ExternalLink>, { copy: vi.fn() });
    rightClick(screen.getByRole('link', { name: 'T-1' }));
    expect(within(await screen.findByRole('menu')).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Open', 'Copy link']);
  });
});

describe('a code span or a path', () => {
  it('copies the code an agent wrote, and a path whole', async () => {
    const given = windowWith(
      <>
        <Markdown text={'Run `daoris sync` first.'} />
        <PathText path="C:/work/engine/src/a.ts" />
      </>,
    );
    rightClick(screen.getByText('daoris sync'));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy' }));
    expect(given.copy).toHaveBeenCalledWith('daoris sync');

    rightClick(screen.getByText('engine/', { exact: false }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Copy' }));
    expect(given.copy).toHaveBeenLastCalledWith('C:/work/engine/src/a.ts');
  });
});

describe('the keys', () => {
  it('the menu key opens the focused surface’s menu, its first act focused', async () => {
    windowWith(<section {...contextOffer(OFFER)}><button type="button">A row’s door</button></section>);
    const door = screen.getByRole('button', { name: 'A row’s door' });
    door.focus();

    const engine = fireEvent.keyDown(door, { key: 'ContextMenu' });
    expect(engine).toBe(false);
    const menu = await screen.findByRole('menu');
    await waitFor(() => expect(document.activeElement).toBe(within(menu).getByRole('menuitem', { name: 'Take' })));
    // The engine's own menu, which Windows asks for on the key's way up, is stopped too.
    expect(fireEvent.keyUp(document.activeElement!, { key: 'ContextMenu' })).toBe(false);

    await userEvent.keyboard('{ArrowDown}{Enter}');
    expect(remove).toHaveBeenCalledOnce();
  });

  it('Shift+F10 does the same', async () => {
    windowWith(<section {...contextOffer(OFFER)}><button type="button">door</button></section>);
    screen.getByRole('button', { name: 'door' }).focus();
    fireEvent.keyDown(document.activeElement!, { key: 'F10', shiftKey: true });
    expect(await screen.findByRole('menu', { name: 'Actions for Expose a streaming budget' })).toBeInTheDocument();
  });

  it('a field keeps the engine’s menu for the keys too', () => {
    windowWith(<section {...contextOffer(OFFER)}><input aria-label="Reason" /></section>);
    const field = screen.getByRole('textbox', { name: 'Reason' });
    field.focus();
    expect(fireEvent.keyDown(field, { key: 'ContextMenu' })).toBe(true);
    expect(fireEvent.keyUp(field, { key: 'ContextMenu' })).toBe(true);
    expect(screen.queryByRole('menu')).toBeNull();
  });
});

describe('中文', () => {
  it('says the menu and its acts in the reader’s language', async () => {
    await i18n.changeLanguage('zh');
    windowWith(<p>每帧预算</p>);
    const words = screen.getByText('每帧预算');
    const range = document.createRange();
    range.selectNodeContents(words);
    window.getSelection()!.addRange(range);
    rightClick(words);
    const menu = await screen.findByRole('menu', { name: '此处的操作' });
    expect(within(menu).getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['复制', '用 Daoris 搜索', '就此问道衍']);
  });
});

