import { describe, expect, it } from 'vitest';
import source from './terminalTheme.ts?raw';
import { mix, terminalTheme } from './terminalTheme';

// CONSOLE4b: the terminal wears the platform's tokens, never colours of its own (D41: the tokens are the
// only theme). Each theme below is read from a token table shaped like `tokens.css`'s two blocks.

const LIGHT: Record<string, string> = {
  ink: '#1a1a18', 'ink-soft': '#5b5a55', 'ink-faint': '#8a887f', page: '#faf9f6', raised: '#ffffff',
  line: '#e3e0d8', 'line-strong': '#cfcabe', accent: '#7a5c2e', 'accent-soft': '#f0e9dc',
  'st-open': '#b07818', 'st-taken': '#2f6db3', 'st-done': '#2c9a62', 'st-declined': '#9e2f24',
};
const DARK: Record<string, string> = {
  ink: '#e8e6df', 'ink-soft': '#a9a69c', 'ink-faint': '#7a7770', page: '#16161a', raised: '#1e1e23',
  line: '#2e2e34', 'line-strong': '#3d3d45', accent: '#d3ac6b', 'accent-soft': '#2a2419',
  'st-open': '#bd8226', 'st-taken': '#5c97dd', 'st-done': '#3da476', 'st-declined': '#c74534',
};

const read = (table: Record<string, string>) => (name: string) => table[name] ?? '';

describe('the terminal\'s theme (CONSOLE4b)', () => {
  it('draws on the page, in its ink, with the accent as the cursor', () => {
    const theme = terminalTheme(read(LIGHT), false);
    expect(theme.background).toBe(LIGHT.page);
    expect(theme.foreground).toBe(LIGHT.ink);
    expect(theme.cursor).toBe(LIGHT.accent);
    expect(theme.cursorAccent).toBe(LIGHT.page);
    expect(theme.selectionBackground).toBe(LIGHT['accent-soft']);
  });

  it('names its colours by the status palette, which was validated in both themes', () => {
    for (const [table, dark] of [[LIGHT, false], [DARK, true]] as const) {
      const theme = terminalTheme(read(table), dark);
      expect(theme.red).toBe(table['st-declined']);
      expect(theme.green).toBe(table['st-done']);
      expect(theme.yellow).toBe(table['st-open']);
      expect(theme.blue).toBe(table['st-taken']);
      // Two the palette has no token for are halfway between two it has: purple from red and blue, teal from blue and green.
      expect(theme.magenta).toBe(mix(table['st-declined']!, table['st-taken']!));
      expect(theme.cyan).toBe(mix(table['st-taken']!, table['st-done']!));
    }
  });

  it('turns the greys round with the theme, so white is readable on either page', () => {
    const light = terminalTheme(read(LIGHT), false);
    expect(light.black).toBe(LIGHT.ink);
    expect(light.white).toBe(LIGHT['ink-faint']);
    const dark = terminalTheme(read(DARK), true);
    expect(dark.black).toBe(DARK.raised);
    expect(dark.white).toBe(DARK['ink-soft']);
    expect(dark.brightWhite).toBe(DARK.ink);
  });

  it('leaves a colour it cannot read to the renderer\'s default, rather than inventing one', () => {
    const theme = terminalTheme(() => '', true);
    expect(theme.background).toBeUndefined();
    expect(theme.magenta).toBeUndefined();
  });

  it('mixes two colours halfway, and leaves what it cannot read alone', () => {
    expect(mix('#000000', '#ffffff')).toBe('#808080');
    expect(mix('#9e2f24', '#2f6db3')).toBe('#674e6c');
    expect(mix('rgb(1 2 3)', '#ffffff')).toBeUndefined();
  });

  /** The rule held where it breaks: a colour typed into this file is a colour no token owns. */
  it('carries no colour of its own', () => {
    expect(source.match(/#[0-9a-f]{3,8}\b/gi) ?? []).toEqual([]);
    expect(source).not.toMatch(/\brgba?\(/);
  });
});
