import { afterEach, describe, expect, it, vi } from 'vitest';
import { fieldTracker, findTarget, runEdit } from './editing';

// Edit and Find from the menu bar (UX7a, D152 §3.2): on the field the person was in, and on the box in front.

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
