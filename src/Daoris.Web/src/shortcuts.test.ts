import { describe, expect, it } from 'vitest';
import { frameShortcut } from './shortcuts';

// The frame's keys, one list for the application's handler and the terminal's renderer (CONSOLE4b):
// what the frame takes wherever focus is, and what it leaves to whatever has focus — the shell included.

const press = (key: string, mods: Partial<{ ctrl: boolean; shift: boolean; alt: boolean; meta: boolean }> = {}, code = '') => ({
  key, code, ctrlKey: Boolean(mods.ctrl), shiftKey: Boolean(mods.shift), altKey: Boolean(mods.alt), metaKey: Boolean(mods.meta),
});

describe('the frame\'s keys', () => {
  it('names each of the frame\'s own', () => {
    expect(frameShortcut(press('L', { ctrl: true, shift: true, alt: true }, 'KeyL'))).toBe('quickAsk');
    // By the key's place: under Shift and Alt a layout may call it something else.
    expect(frameShortcut(press('¬', { ctrl: true, shift: true, alt: true }, 'KeyL'))).toBe('quickAsk');
    expect(frameShortcut(press('F1'))).toBe('help');
    expect(frameShortcut(press('i', { ctrl: true, alt: true }))).toBe('help');
    expect(frameShortcut(press('b', { ctrl: true }))).toBe('list');
    expect(frameShortcut(press('j', { ctrl: true }))).toBe('panel');
    expect(frameShortcut(press('B', { ctrl: true, alt: true }))).toBe('right');
    // D118 §3e: VS Code's Focus Next Part and Focus Previous Part.
    expect(frameShortcut(press('F6'))).toBe('nextRegion');
    expect(frameShortcut(press('F6', { shift: true }))).toBe('previousRegion');
  });

  it('leaves every other key to whatever has focus: a shell\'s interrupt, its line keys, the palette\'s key in a field', () => {
    expect(frameShortcut(press('c', { ctrl: true }))).toBeNull();
    expect(frameShortcut(press('k', { ctrl: true }))).toBeNull();
    expect(frameShortcut(press('j', { ctrl: true, alt: true }))).toBeNull();
    expect(frameShortcut(press('b', { ctrl: true, shift: true }))).toBeNull();
    expect(frameShortcut(press('b'))).toBeNull();
    expect(frameShortcut(press('F2'))).toBeNull();
    expect(frameShortcut(press('F6', { ctrl: true }))).toBeNull();
    expect(frameShortcut(press('Enter'))).toBeNull();
  });
});
