import { afterEach, describe, expect, it, vi } from 'vitest';
import { EDIT_EVENT, fieldTracker, findTarget, runEdit } from './editing';

// Edit and Find from the menu bar: on the field the person was in, and on the box in front.

afterEach(() => {
  document.body.innerHTML = '';
  vi.unstubAllGlobals();
});

describe('the field Edit acts on', () => {
  it('is the one last focused, kept while the menu bar and a menu hold the focus, and let go elsewhere', () => {
    document.body.innerHTML = `
      <textarea aria-label="box"></textarea>
      <nav data-menu-bar><button>Edit</button></nav>
      <div role="menu"><div role="menuitem" tabindex="-1">Undo</div></div>
      <button>elsewhere</button>
      <div class="xterm"><textarea aria-label="terminal"></textarea></div>`;
    const tracker = fieldTracker(document);
    const box = document.querySelector<HTMLTextAreaElement>('textarea[aria-label="box"]')!;

    box.focus();
    document.querySelector<HTMLButtonElement>('[data-menu-bar] button')!.focus();
    document.querySelector<HTMLElement>('[role="menuitem"]')!.focus();
    expect(tracker.field()).toBe(box);

    document.querySelector<HTMLButtonElement>('body > button')!.focus();
    expect(tracker.field()).toBeNull();
    // The terminal's keys are its own: it is never Edit's field.
    document.querySelector<HTMLTextAreaElement>('textarea[aria-label="terminal"]')!.focus();
    expect(tracker.field()).toBeNull();
    tracker.stop();
  });
});

describe("Edit's acts", () => {
  it('gives the field the focus back, then runs the engine\'s own command on it', async () => {
    document.body.innerHTML = '<input aria-label="name" value="hello" /><button>Edit</button>';
    const input = document.querySelector('input')!;
    const exec = vi.fn(() => true);
    Object.defineProperty(document, 'execCommand', { configurable: true, value: exec });

    document.querySelector('button')!.focus();
    expect(await runEdit('undo', input)).toBe(true);
    expect(document.activeElement).toBe(input);
    expect(exec).toHaveBeenCalledWith('undo');

    await runEdit('selectAll', input);
    expect(input.selectionStart).toBe(0);
    expect(input.selectionEnd).toBe(5);
  });

  it('pastes what the clipboard holds, and says so when the engine refuses the page the clipboard', async () => {
    document.body.innerHTML = '<textarea aria-label="box"></textarea>';
    const box = document.querySelector('textarea')!;
    const exec = vi.fn(() => true);
    Object.defineProperty(document, 'execCommand', { configurable: true, value: exec });

    vi.stubGlobal('navigator', { clipboard: { readText: vi.fn(async () => 'words') } });
    expect(await runEdit('paste', box)).toBe(true);
    expect(exec).toHaveBeenCalledWith('insertText', false, 'words');

    vi.stubGlobal('navigator', { clipboard: { readText: vi.fn(async () => { throw new Error('denied'); }) } });
    expect(await runEdit('paste', box)).toBe(false);
    expect(await runEdit('paste', null)).toBe(false);
  });
});

// A region with a history of its own, the workflow editor's draft, takes Edit's undo, redo, copy and paste where no field
// holds the focus; a field inside it keeps its own.
describe('a region with a history of its own', () => {
  it('is what Edit acts on outside a field, kept while the menu bar holds the focus, and its field is still the field', () => {
    document.body.innerHTML = `
      <section data-edit-history><div tabindex="0" aria-label="step">The work</div><input aria-label="name" /></section>
      <nav data-menu-bar><button>Edit</button></nav>
      <button>elsewhere</button>`;
    const tracker = fieldTracker(document);
    const region = document.querySelector<HTMLElement>('[data-edit-history]')!;

    document.querySelector<HTMLElement>('[aria-label="step"]')!.focus();
    document.querySelector<HTMLButtonElement>('[data-menu-bar] button')!.focus();
    expect(tracker.history()).toBe(region);
    expect(tracker.field()).toBeNull();

    document.querySelector<HTMLInputElement>('input')!.focus();
    expect(tracker.field()).toBe(document.querySelector('input'));
    expect(tracker.history()).toBeNull();
    document.querySelector<HTMLButtonElement>('body > button')!.focus();
    expect(tracker.history()).toBeNull();
    tracker.stop();
  });

  it("hears undo, redo, copy and paste as the region's own event, taken where it says so, and no other act", async () => {
    document.body.innerHTML = '<section data-edit-history></section>';
    const region = document.querySelector<HTMLElement>('section')!;
    const heard: string[] = [];
    region.addEventListener(EDIT_EVENT, (event) => {
      const { act } = (event as CustomEvent<{ act: string }>).detail;
      heard.push(act);
      if (act !== 'paste') event.preventDefault();
    });
    expect(await runEdit('undo', region)).toBe(true);
    expect(await runEdit('redo', region)).toBe(true);
    expect(await runEdit('paste', region)).toBe(false);
    expect(await runEdit('selectAll', region)).toBe(false);
    expect(heard).toEqual(['undo', 'redo', 'paste']);
  });
});

describe('the box Find goes to', () => {
  it('is the conversation\'s find on Sessions, else the list\'s box, else none', () => {
    document.body.innerHTML = `
      <aside data-region="list"><input type="search" aria-label="list box" /></aside>
      <main data-region="main"><input type="search" aria-label="find in this session" /></main>`;
    expect(findTarget(document, 'sessions')?.getAttribute('aria-label')).toBe('find in this session');
    expect(findTarget(document, 'projects')?.getAttribute('aria-label')).toBe('list box');

    document.body.innerHTML = '<main data-region="main"><p>a map</p></main>';
    expect(findTarget(document, 'map')).toBeNull();
  });
});
