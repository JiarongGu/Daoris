/**
 * The terminal's colours (CONSOLE4b), read from the platform's tokens: a shell's output is drawn in
 * the same ink, on the same page, in either theme, and no colour here is one the tokens do not own.
 *
 * @remarks
 * **The sixteen ANSI colours by role.** The four status tokens were validated for contrast in both
 * themes (D41 §3), so red, green, yellow and blue are declined, done, open and taken. Magenta and cyan
 * have no token and are each halfway between the two that make them. The greys turn round with the
 * theme: in light, black is the ink and white the faint ink; in dark, black is the raised surface and
 * white the soft ink, so text a program writes in "white" stays readable on either page.
 *
 * Pure, so every theme is an argument: `read` is a token's value by name (`ink`, `st-done`).
 */
export type TerminalTheme = {
  background?: string; foreground?: string; cursor?: string; cursorAccent?: string;
  selectionBackground?: string; selectionInactiveBackground?: string;
  scrollbarSliderBackground?: string; scrollbarSliderHoverBackground?: string; scrollbarSliderActiveBackground?: string;
  black?: string; red?: string; green?: string; yellow?: string; blue?: string; magenta?: string; cyan?: string; white?: string;
  brightBlack?: string; brightRed?: string; brightGreen?: string; brightYellow?: string; brightBlue?: string;
  brightMagenta?: string; brightCyan?: string; brightWhite?: string;
};

export function terminalTheme(read: (token: string) => string, dark: boolean): TerminalTheme {
  const token = (name: string) => read(name).trim() || undefined;
  const between = (a: string, b: string) => {
    const first = token(a);
    const second = token(b);
    return first && second ? mix(first, second) : undefined;
  };

  const red = token('st-declined');
  const green = token('st-done');
  const yellow = token('st-open');
  const blue = token('st-taken');
  const magenta = between('st-declined', 'st-taken');
  const cyan = between('st-taken', 'st-done');

  return {
    background: token('page'),
    foreground: token('ink'),
    cursor: token('accent'),
    cursorAccent: token('page'),
    selectionBackground: token('accent-soft'),
    selectionInactiveBackground: token('line'),
    // The scrollbar is the page's own knob, as `tokens.css` draws every other one.
    scrollbarSliderBackground: token('line-strong'),
    scrollbarSliderHoverBackground: token('ink-faint'),
    scrollbarSliderActiveBackground: token('ink-faint'),
    black: dark ? token('raised') : token('ink'),
    red, green, yellow, blue, magenta, cyan,
    white: dark ? token('ink-soft') : token('ink-faint'),
    brightBlack: dark ? token('ink-faint') : token('ink-soft'),
    brightRed: red, brightGreen: green, brightYellow: yellow, brightBlue: blue, brightMagenta: magenta, brightCyan: cyan,
    brightWhite: dark ? token('ink') : token('line-strong'),
  };
}

/** Two six-digit hex colours, halfway between; undefined where either is written another way. */
export function mix(a: string, b: string): string | undefined {
  const channels = (colour: string) => {
    const hex = /^#([0-9a-f]{6})$/i.exec(colour.trim())?.[1];
    return hex ? [0, 2, 4].map((at) => parseInt(hex.slice(at, at + 2), 16)) : null;
  };
  const first = channels(a);
  const second = channels(b);
  if (!first || !second) return undefined;
  return `#${first.map((value, at) => Math.round((value + second[at]!) / 2).toString(16).padStart(2, '0')).join('')}`;
}

/** The tokens as the page has them now: the chosen theme's, or the system's. */
export function readToken(name: string): string {
  return typeof document === 'undefined' ? '' : getComputedStyle(document.documentElement).getPropertyValue(`--${name}`);
}
