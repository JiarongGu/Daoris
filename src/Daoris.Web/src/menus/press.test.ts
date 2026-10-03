import { afterEach, describe, expect, it } from 'vitest';
import {
  clipped, contextSections, insideMenu, isMenuKey, isTextField, keyPoint, offerContext, offeredContext, pressedOn, quoted,
  searchWords,
} from './press';

// CTX1 (D138, design §3): what a right-click is on, read from the element pressed and the selection, and the groups the
// menu draws for it, most specific first. Pure, so every case is an assertion without a window.

const t = (key: string, values?: Record<string, unknown>) => (values ? `${key} ${JSON.stringify(values)}` : key);

function html(markup: string): HTMLElement {
  document.body.innerHTML = markup;
  return document.body;
}

const at = (selector: string) => document.querySelector(selector)!;

afterEach(() => {
  document.body.innerHTML = '';
  window.getSelection()?.removeAllRanges();
});

describe('a text field keeps the engine’s menu', () => {
  it('is an input of a text kind, a textarea, an editable region or the terminal', () => {
    html(`
      <input id="text"><input id="search" type="search"><input id="box" type="checkbox"><button id="button">b</button>
      <textarea id="area"></textarea><div contenteditable="true"><span id="edit">e</span></div>
      <div class="xterm"><canvas id="term"></canvas></div><p id="prose">p</p>`);
    expect(isTextField(at('#text'))).toBe(true);
    expect(isTextField(at('#search'))).toBe(true);
    expect(isTextField(at('#area'))).toBe(true);
    expect(isTextField(at('#edit'))).toBe(true);
    expect(isTextField(at('#term'))).toBe(true);
    // A checkbox and a button type nothing, and prose is the page's.
    expect(isTextField(at('#box'))).toBe(false);
    expect(isTextField(at('#button'))).toBe(false);
    expect(isTextField(at('#prose'))).toBe(false);
    expect(isTextField(null)).toBe(false);
  });

  it('a read-only or disabled field still has the engine’s copy', () => {
    html('<input id="ro" readonly><input id="off" disabled>');
    expect(isTextField(at('#ro'))).toBe(true);
    expect(isTextField(at('#off'))).toBe(true);
  });
});

describe('the keys that ask for a menu', () => {
  const key = (init: KeyboardEventInit) => new KeyboardEvent('keydown', init);

  it('are the menu key alone and Shift+F10', () => {
    expect(isMenuKey(key({ key: 'ContextMenu' }))).toBe(true);
    expect(isMenuKey(key({ key: 'F10', shiftKey: true }))).toBe(true);
    // F10 alone is the menu bar's, and a modified menu key is not the menu's.
    expect(isMenuKey(key({ key: 'F10' }))).toBe(false);
    expect(isMenuKey(key({ key: 'F10', shiftKey: true, ctrlKey: true }))).toBe(false);
    expect(isMenuKey(key({ key: 'ContextMenu', ctrlKey: true }))).toBe(false);
    expect(isMenuKey(key({ key: 'Enter' }))).toBe(false);
  });

  it('place the menu at the focused element’s corner, inside the window', () => {
    const rect = (left: number, top: number, width: number, height: number) =>
      ({ left, top, width, height, right: left + width, bottom: top + height }) as DOMRect;
    expect(keyPoint(rect(100, 50, 200, 30), { width: 1200, height: 800 })).toEqual({ x: 108, y: 80 });
    // A tall element (a page) is not asked for its bottom, which may be below the window.
    expect(keyPoint(rect(300, 40, 600, 2000), { width: 1200, height: 800 })).toEqual({ x: 308, y: 72 });
    // Off the window's edge, it comes back inside.
    expect(keyPoint(rect(1300, 900, 10, 10), { width: 1200, height: 800 })).toEqual({ x: 1192, y: 792 });
  });
});

describe('an open menu', () => {
  it('is anything inside a menu', () => {
    html('<div role="menu"><div role="menuitem" id="item">Copy</div></div><p id="p">p</p>');
    expect(insideMenu(at('#item'))).toBe(true);
    expect(insideMenu(at('#p'))).toBe(false);
  });
});

describe('what a press is on', () => {
  it('a link: its address, whether it is a web page, and whether it must stay the system’s', () => {
    html(`
      <a id="web" href="https://tickets.example/T-1"><span id="inside">T-1</span></a>
      <a id="sign" href="https://login.example/" data-link="system">sign in</a>
      <a id="doc" href="docs/decisions/D138.md">D138</a>`);
    expect(pressedOn(at('#inside'), null).link).toMatchObject({ href: 'https://tickets.example/T-1', web: 'https://tickets.example/T-1', system: false });
    expect(pressedOn(at('#sign'), null).link).toMatchObject({ system: true });
    // A relative link is a link, and no web page for Daoris's browser.
    expect(pressedOn(at('#doc'), null).link).toMatchObject({ href: 'docs/decisions/D138.md', web: null });
  });

  it('a code span, a block of code or a path: what it copies', () => {
    html(`
      <p>Run <code id="code">daoris sync</code></p>
      <pre><code id="block">line one
line two</code></pre>
      <span id="path" data-copy="C:/work/engine/src/a.ts">C:/work/<wbr>engine/src/a.ts</span>
      <pre id="text">an entry, read as it is written</pre>
      <p id="prose">prose</p>`);
    expect(pressedOn(at('#code'), null).copy).toBe('daoris sync');
    expect(pressedOn(at('#block'), null).copy).toBe('line one\nline two');
    expect(pressedOn(at('#path'), null).copy).toBe('C:/work/engine/src/a.ts');
    // A page of text set in a `pre` is not a code span.
    expect(pressedOn(at('#text'), null).copy).toBeNull();
    expect(pressedOn(at('#prose'), null).copy).toBeNull();
  });

  it('selected text, only where the press is on it', () => {
    html('<p id="one">The first paragraph of words.</p><p id="two">Another one.</p>');
    const range = document.createRange();
    range.selectNodeContents(at('#one'));
    const selection = window.getSelection()!;
    selection.addRange(range);

    expect(pressedOn(at('#one'), selection).selection).toBe('The first paragraph of words.');
    // A selection left elsewhere is not what this press is on.
    expect(pressedOn(at('#two'), selection).selection).toBeNull();
    // Nothing selected is nothing.
    selection.removeAllRanges();
    expect(pressedOn(at('#one'), selection).selection).toBeNull();
  });

  it('whitespace alone is no selection', () => {
    html('<p id="one">   </p>');
    const range = document.createRange();
    range.selectNodeContents(at('#one'));
    window.getSelection()!.addRange(range);
    expect(pressedOn(at('#one'), window.getSelection()).selection).toBeNull();
  });
});

describe('what a surface offers', () => {
  it('is recorded on the event, the innermost surface winning', () => {
    const event = new MouseEvent('contextmenu');
    const inner = { label: 'row', acts: [{ id: 'open', label: 'Open', onSelect: () => {} }] };
    const outer = { label: 'page', acts: [{ id: 'take', label: 'Take', onSelect: () => {} }] };
    expect(offeredContext(event)).toBeNull();
    offerContext(event, inner);
    offerContext(event, outer);
    expect(offeredContext(event)).toBe(inner);
  });

  it('offers nothing with no acts, so an outer surface still can', () => {
    const event = new MouseEvent('contextmenu');
    offerContext(event, { label: 'empty', acts: [] });
    expect(offeredContext(event)).toBeNull();
    const page = { acts: [{ id: 'take', label: 'Take', onSelect: () => {} }] };
    offerContext(event, page);
    expect(offeredContext(event)).toBe(page);
  });
});

describe('the groups the menu draws', () => {
  const doors = { copy: () => {}, search: () => {}, ask: () => {}, openInBrowser: () => {} };
  const none = { selection: null, link: null, copy: null };
  const ids = (sections: { id: string }[][]) => sections.map((section) => section.map((act) => act.id));

  it('selected text: Copy, Search Daoris for it, Ask Daoris about it', () => {
    const { sections, label } = contextSections({ pressed: { ...none, selection: 'a budget' }, offer: null, doors, t });
    expect(ids(sections)).toEqual([['copy', 'search', 'ask']]);
    expect(sections[0]![0]).toMatchObject({ label: 'contextMenu.act.copy', copy: 'a budget' });
    expect(label).toBe('contextMenu.label');
  });

  it('a window with no Search or Quick Ask offers its selection Copy alone', () => {
    const { sections } = contextSections({ pressed: { ...none, selection: 'a budget' }, offer: null, doors: { copy: () => {} }, t });
    expect(ids(sections)).toEqual([['copy']]);
  });

  it('a link: Open, Open in Daoris’s browser where it may, Copy link', () => {
    const anchor = document.createElement('a');
    const link = { href: 'https://tickets.example/T-1', web: 'https://tickets.example/T-1', system: false, element: anchor };
    expect(ids(contextSections({ pressed: { ...none, link }, offer: null, doors, t }).sections))
      .toEqual([['open', 'openInBrowser', 'copyLink']]);
    // A browser, a sign-in and a relative link are never sent to Daoris's browser.
    expect(ids(contextSections({ pressed: { ...none, link }, offer: null, doors: { copy: () => {} }, t }).sections))
      .toEqual([['open', 'copyLink']]);
    expect(ids(contextSections({ pressed: { ...none, link: { ...link, system: true } }, offer: null, doors, t }).sections))
      .toEqual([['open', 'copyLink']]);
    expect(ids(contextSections({ pressed: { ...none, link: { ...link, web: null } }, offer: null, doors, t }).sections))
      .toEqual([['open', 'copyLink']]);
  });

  it('a code span: Copy, and not twice beside a selection', () => {
    expect(ids(contextSections({ pressed: { ...none, copy: 'daoris sync' }, offer: null, doors, t }).sections)).toEqual([['copyCode']]);
    expect(ids(contextSections({ pressed: { ...none, copy: 'daoris sync', selection: 'sync' }, offer: null, doors, t }).sections))
      .toEqual([['copy', 'search', 'ask']]);
  });

  it('a surface’s acts come last, the menu named for it', () => {
    const offer = { label: 'Expose a budget', acts: [{ id: 'take', label: 'Take', onSelect: () => {} }] };
    const anchor = document.createElement('a');
    const link = { href: 'https://t.example/', web: 'https://t.example/', system: false, element: anchor };
    const made = contextSections({ pressed: { ...none, link }, offer, doors, t });
    expect(ids(made.sections)).toEqual([['open', 'openInBrowser', 'copyLink'], ['take']]);
    expect(made.label).toBe('contextMenu.for {"name":"Expose a budget"}');
  });

  it('nothing pressed and nothing offered is no menu', () => {
    expect(contextSections({ pressed: none, offer: null, doors, t }).sections).toEqual([]);
  });
});

describe('the words handed on', () => {
  it('quotes a selection for Quick Ask, each line its own', () => {
    expect(quoted('one\ntwo')).toBe('> one\n> two\n\n');
    expect(quoted('  padded  ')).toBe('> padded\n\n');
  });

  it('searches on the words, one line of them, not too many', () => {
    expect(searchWords('  a\n budget   cap ')).toBe('a budget cap');
    expect(searchWords('x'.repeat(500))).toHaveLength(200);
  });

  it('says what was copied in a line', () => {
    expect(clipped('short')).toBe('short');
    expect(clipped(`${'word '.repeat(30)}`)).toBe(`${'word '.repeat(12).trim().slice(0, 59)}…`);
    expect(clipped('a\nb')).toBe('a b');
  });
});
