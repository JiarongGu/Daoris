import { describe, expect, it, vi } from 'vitest';
import { type CommandState, commandTable } from './commands';
import { commandForKey, frameShortcut, isPress } from './shortcuts';

// The keys, read from the one table (UX7a, D152 §3): what the frame takes wherever focus is, and what it leaves to
// whatever has focus — a field, the shell included (CONSOLE4b).

const press = (key: string, mods: Partial<{ ctrl: boolean; shift: boolean; alt: boolean; meta: boolean }> = {}, code = '') => ({
  key, code, ctrlKey: Boolean(mods.ctrl), shiftKey: Boolean(mods.shift), altKey: Boolean(mods.alt), metaKey: Boolean(mods.meta),
});

describe('a key, read as its menu prints it', () => {
  it('matches by the key, or by its place where Shift or a layout changes what the key says', () => {
    expect(isPress(press('b', { ctrl: true }), 'Ctrl+B')).toBe(true);
    expect(isPress(press('B', { ctrl: true, alt: true }), 'Ctrl+Alt+B')).toBe(true);
    expect(isPress(press('¬', { ctrl: true, shift: true, alt: true }, 'KeyL'), 'Ctrl+Shift+Alt+L')).toBe(true);
    // Shift turns ` into ~ and 1 into !: the place is what was meant.
    expect(isPress(press('~', { ctrl: true, shift: true }, 'Backquote'), 'Ctrl+Shift+`')).toBe(true);
    expect(isPress(press('`', { ctrl: true }, 'Backquote'), 'Ctrl+`')).toBe(true);
    expect(isPress(press('1', { ctrl: true }, 'Digit1'), 'Ctrl+1')).toBe(true);
    expect(isPress(press(',', { ctrl: true }, 'Comma'), 'Ctrl+,')).toBe(true);
    expect(isPress(press('F6', { shift: true }), 'Shift+F6')).toBe(true);
  });

  it('needs every modifier it names and no other', () => {
    expect(isPress(press('b', { ctrl: true, shift: true }), 'Ctrl+B')).toBe(false);
    expect(isPress(press('F6', { ctrl: true }), 'F6')).toBe(false);
    expect(isPress(press('p', { ctrl: true }), 'Ctrl+Shift+P')).toBe(false);
    expect(isPress(press('`', { ctrl: true, shift: true }, 'Backquote'), 'Ctrl+`')).toBe(false);
  });
});

describe("the frame's keys, everywhere", () => {
  it('names each of the frame\'s own, which the terminal leaves to the frame', () => {
    expect(frameShortcut(press('L', { ctrl: true, shift: true, alt: true }, 'KeyL'))).toBe('help.quickAsk');
    expect(frameShortcut(press('F1'))).toBe('help.ask');
    expect(frameShortcut(press('i', { ctrl: true, alt: true }))).toBe('help.ask');
    expect(frameShortcut(press('b', { ctrl: true }))).toBe('view.list');
    expect(frameShortcut(press('j', { ctrl: true }))).toBe('view.panel');
    expect(frameShortcut(press('B', { ctrl: true, alt: true }))).toBe('view.side');
    expect(frameShortcut(press('F6'))).toBe('go.nextRegion');
    expect(frameShortcut(press('F6', { shift: true }))).toBe('go.previousRegion');
    // UX7a's: the places, Settings, the terminal and the console are the frame's everywhere too.
    expect(frameShortcut(press('3', { ctrl: true }, 'Digit3'))).toBe('go.quests');
    expect(frameShortcut(press(',', { ctrl: true }, 'Comma'))).toBe('workspace.settings');
    expect(frameShortcut(press('`', { ctrl: true }, 'Backquote'))).toBe('terminal.show');
    expect(frameShortcut(press('U', { ctrl: true, shift: true }, 'KeyU'))).toBe('view.console');
  });

  it('leaves every other key to whatever has focus: a shell\'s interrupt, its line keys, the palette\'s key in a field', () => {
    expect(frameShortcut(press('c', { ctrl: true }))).toBeNull();
    expect(frameShortcut(press('k', { ctrl: true }))).toBeNull();
    // A shell's next line (D152 §3.4), and a field's find.
    expect(frameShortcut(press('n', { ctrl: true }, 'KeyN'))).toBeNull();
    expect(frameShortcut(press('f', { ctrl: true }, 'KeyF'))).toBeNull();
    expect(frameShortcut(press('j', { ctrl: true, alt: true }))).toBeNull();
    expect(frameShortcut(press('b', { ctrl: true, shift: true }))).toBeNull();
    expect(frameShortcut(press('b'))).toBeNull();
    expect(frameShortcut(press('F2'))).toBeNull();
    expect(frameShortcut(press('F6', { ctrl: true }))).toBeNull();
    expect(frameShortcut(press('Enter'))).toBeNull();
  });
});

describe('which command a press runs', () => {
  const state = (over: Partial<CommandState> = {}): CommandState => ({
    attached: true, view: 'quests', list: { shown: true }, panelShown: true, sideShown: false, moved: false, workspaces: [],
    scope: null, circle: null, wired: false, theme: 'system', language: 'en', agents: [], domains: [], session: null, quest: null,
    record: false, find: true, field: false, ...over,
  });
  const doors = new Proxy({}, { get: () => vi.fn() }) as never;
  const entries = (over: Partial<CommandState> = {}) => commandTable(state(over), doors, (key) => key);
  const outside = { field: false, terminal: false };
  const inField = { field: true, terminal: false };
  const inTerminal = { field: true, terminal: true };

  it('runs the table\'s command for its key, outside a field', () => {
    expect(commandForKey(entries(), press('n', { ctrl: true }, 'KeyN'), outside)?.id).toBe('workspace.newAsk');
    expect(commandForKey(entries(), press('P', { ctrl: true, shift: true }, 'KeyP'), outside)?.id).toBe('view.commands');
    expect(commandForKey(entries(), press('k', { ctrl: true }, 'KeyK'), outside)?.id).toBe('view.commands');
    expect(commandForKey(entries(), press('f', { ctrl: true }, 'KeyF'), outside)?.id).toBe('edit.find');
  });

  it('leaves a field what a field means, and the terminal its next line too (D152 §3.4)', () => {
    expect(commandForKey(entries(), press('f', { ctrl: true }, 'KeyF'), inField)).toBeNull();
    expect(commandForKey(entries(), press('k', { ctrl: true }, 'KeyK'), inField)).toBeNull();
    expect(commandForKey(entries(), press('z', { ctrl: true }, 'KeyZ'), outside)).toBeNull();
    expect(commandForKey(entries(), press('n', { ctrl: true }, 'KeyN'), inField)?.id).toBe('workspace.newAsk');
    expect(commandForKey(entries(), press('n', { ctrl: true }, 'KeyN'), inTerminal)).toBeNull();
    // The frame's own keys stay the frame's in both.
    expect(commandForKey(entries(), press('2', { ctrl: true }, 'Digit2'), inTerminal)?.id).toBe('go.sessions');
  });

  it('takes no key for what does not apply, so the press goes where it would have', () => {
    expect(commandForKey(entries({ find: false }), press('f', { ctrl: true }, 'KeyF'), outside)).toBeNull();
    expect(commandForKey(entries({ list: null }), press('b', { ctrl: true }), outside)).toBeNull();
  });

  it('claims no key in a browser that a browser keeps for itself', () => {
    const browser = entries({ attached: false });
    expect(commandForKey(browser, press('n', { ctrl: true }, 'KeyN'), outside)).toBeNull();
    expect(commandForKey(browser, press('1', { ctrl: true }, 'Digit1'), outside)).toBeNull();
    expect(commandForKey(browser, press('f', { ctrl: true }, 'KeyF'), outside)).toBeNull();
    expect(commandForKey(browser, press('k', { ctrl: true }, 'KeyK'), outside)?.id).toBe('view.commands');
    expect(commandForKey(browser, press('b', { ctrl: true }), outside)?.id).toBe('view.list');
  });
});
