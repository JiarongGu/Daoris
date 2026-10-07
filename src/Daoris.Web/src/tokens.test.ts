import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { TYPE_STEPS } from './lib/cn';

/**
 * **The type scale is a set of named steps, and a raw size is not one of them** (D41 §3 as amended
 * by D56).
 *
 * The rule is held by a test because the literal form had already drifted once, silently and one
 * component at a time: the code carried **fifteen** distinct `text-[…rem]` values where D41 named
 * eight, and four of them — 0.7, 0.78, 0.82, 0.85 — belonged to no scale at all. Nothing reported
 * it, because a hardcoded size is valid Tailwind and renders perfectly.
 *
 * A named step cannot drift without someone adding one to `tokens.css`, which is a diff a reviewer
 * sees. That is the whole difference.
 */
const RAW_SIZE = /\btext-\[[0-9.]+(?:rem|px|em)\]/g;

export function rawSizes(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(RAW_SIZE) ?? []).map((hit) => `${path} hardcodes ${hit}`));
}

/** The type steps `tokens.css` declares: each `--text-<step>`, its line height aside. */
export function declaredSteps(css: string): string[] {
  return [...css.matchAll(/^\s*--text-([a-z]+)\s*:/gm)].map((match) => match[1]!);
}

/** The colours it declares (`--color-<name>`), which share the `text-` prefix with the steps. */
export function declaredColours(css: string): string[] {
  return [...css.matchAll(/^\s*--color-([a-z][a-z-]*)\s*:/gm)].map((match) => match[1]!);
}

/** Tailwind's `text-` utilities that are no size and no colour: alignment, wrapping and overflow (UX5 U41). */
const LAYOUTS = ['left', 'center', 'right', 'justify', 'start', 'end', 'wrap', 'nowrap', 'balance', 'pretty', 'ellipsis', 'clip'];

/**
 * A `text-` class, read whole: `text-h3` is `h3`. A scan that read letters alone up to a word's end took
 * `text-h3` for nothing at all, so the monitor's title wore a step no token defines and was drawn at the
 * body's size, with every check green (audit MO11, FRAME1h).
 */
const TEXT_CLASS = /(?<![\w-])text-([a-z][a-z0-9]*(?:-[a-z0-9]+)*)/g;

/**
 * Tailwind's colour keywords that need no theme. `tokens.css` clears Tailwind's palette (`--color-*: initial`), so
 * `bg-white` or `border-red-500` draws nothing, while these three are values of their own and still draw (LOOK6).
 */
const BUILT_IN_COLOURS = ['transparent', 'current', 'inherit'];

/** Every `text-` class that names no step, colour or layout `tokens.css` and Tailwind declare. */
export function undeclaredSteps(files: [path: string, source: string][], css: string): string[] {
  const known = new Set([...declaredSteps(css), ...declaredColours(css), ...LAYOUTS, ...BUILT_IN_COLOURS]);
  return files.flatMap(([path, source]) => [...source.matchAll(TEXT_CLASS)]
    .filter((match) => !known.has(match[1]!))
    .map((match) => `${path} wears text-${match[1]}, which tokens.css does not declare`));
}

/**
 * 🔴 **A colour no token defines draws nothing, and says nothing** (LOOK6). Thirteen files asked for `bg-sunken` for
 * the box a destructive move asks in, and `tokens.css` declared no such colour: Tailwind emits no rule for a name
 * its theme lacks, so every one of those boxes had no background in either theme, with every check green. The
 * `text-` scan above already held the type steps; a `bg-` or `border-` colour had nothing.
 *
 * A `bg-` or `border-` class is read whole, its opacity aside (`bg-st-done/10` is `st-done`), and what is not a
 * colour is set aside by its shape: a background's attachment, clip, origin, repeat, size, position, image or
 * blend; a border's width, side, style or table layout. A side may carry a colour (`border-l-st-open`). A class
 * built from a variable (`bg-st-${state}`) and an arbitrary value (`bg-[…]`) are not names, so neither is read.
 */
const COLOUR_CLASS = /(?<![\w-])(bg|border)-([a-z][a-z0-9]*(?:-[a-z0-9]+)*)(?![a-z0-9]|-[a-z0-9]|-?\$\{)/g;

const NOT_A_BACKGROUND_COLOUR = /^(?:fixed|local|scroll|none|auto|cover|contain|center|top|bottom|left|right|repeat|no-repeat)$|^(?:clip|origin|repeat|size|position|linear|radial|conic|gradient|blend|top|bottom|left|right)-/;
const NOT_A_BORDER_COLOUR = /^(?:\d+|solid|dashed|dotted|double|hidden|none|collapse|separate)$|^spacing(?:-|$)/;
const BORDER_SIDE = /^(?:x|y|t|r|b|l|s|e|bs|be)(?:-(.+))?$/;

/** The colour a `bg-` or `border-` class names, or null where it names none. */
function colourNamed(utility: string, value: string): string | null {
  if (utility === 'bg') return NOT_A_BACKGROUND_COLOUR.test(value) ? null : value;
  const side = BORDER_SIDE.exec(value);
  const rest = side ? side[1] : value;
  return rest === undefined || NOT_A_BORDER_COLOUR.test(rest) ? null : rest;
}

/** Every `bg-` and `border-` class whose colour is neither a token `tokens.css` declares nor a built-in that draws. */
export function undeclaredColours(files: [path: string, source: string][], css: string): string[] {
  const known = new Set([...declaredColours(css), ...BUILT_IN_COLOURS]);
  return files.flatMap(([path, source]) => [...source.matchAll(COLOUR_CLASS)]
    .filter((match) => {
      const colour = colourNamed(match[1]!, match[2]!);
      return colour !== null && !known.has(colour);
    })
    .map((match) => `${path} wears ${match[0]}, a colour tokens.css does not declare`));
}

/**
 * 🔴 **A scrim never covers the app strip**, where the window's three buttons are.
 *
 * The strip reserves three 44px slots that the **window** paints natively (SURF7/D56). A page-level
 * backdrop dims everything the page draws and cannot touch what the window draws — so opening the
 * palette dimmed the whole title bar and left the caption buttons as a **bright white block punched
 * through it**. Seen in a screenshot; invisible in every unit test, because there are no native
 * buttons in jsdom and no window in a browser.
 *
 * It is also the behaviour VS Code has: its title bar stays live while quick-open is up.
 *
 * The rule is the position, so the check is on the position: an overlay starts at `top-9` — the
 * strip's own height — not at `inset-0`.
 */
const FULL_BLEED_SCRIM = /fixed inset-0[^"'`]*bg-scrim|bg-scrim[^"'`]*fixed inset-0/g;

export function scrimsOverTheStrip(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(FULL_BLEED_SCRIM) ?? []).map((hit) => `${path} scrims the app strip: ${hit}`));
}

/**
 * 🔴 **And neither does a PANEL.** The scrim learned to start below the strip; the drawer beside it
 * did not, and stayed `inset-y-0` — so on the deployed application its header, close button
 * included, sat exactly where the window paints its caption buttons. The × was under the ✕: not
 * dimmed, not clickable, not visible. Seen in a screenshot of a quest drawer, on the second
 * deployment; in a browser there is no caption to hide behind.
 */
const FULL_HEIGHT_PANEL = /fixed inset-y-0[^"'`]*bg-overlay|bg-overlay[^"'`]*fixed inset-y-0/g;

export function panelsOverTheStrip(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(FULL_HEIGHT_PANEL) ?? []).map((hit) => `${path} raises a panel over the app strip: ${hit}`));
}

/**
 * 🔴 **Nor does either reach the status bar.** The frame is three bars, the strip, the activity bar
 * and the status bar, and an overlay belongs to the content between them. The scrim left the first
 * two alone and ran to `bottom-0`, so behind an open drawer the status bar's first 48px stayed
 * bright and the rest went grey, cut at the activity bar's edge. Seen on the installed window
 * (POLISH3). The rule is the position again: an overlay ends at `bottom-6`, the bar's own height.
 */
const OVER_THE_STATUS_BAR = /fixed[^"'`]*\bbottom-0\b[^"'`]*bg-(?:scrim|overlay)|bg-(?:scrim|overlay)[^"'`]*fixed[^"'`]*\bbottom-0\b/g;

export function overlaysOverTheStatusBar(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(OVER_THE_STATUS_BAR) ?? []).map((hit) => `${path} covers the status bar: ${hit}`));
}

const sources = import.meta.glob('./**/*.tsx', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>;

const components = Object.entries(sources).filter(([path]) => !/\.test\.tsx$/.test(path));

describe('the type scale', () => {
  it('catches a raw size — the check itself, sabotaged in the shapes a regression takes', () => {
    // The shape the sweep replaced.
    expect(rawSizes([['./ui.tsx', 'className="text-[0.85rem] text-ink"']]))
      .toEqual(['./ui.tsx hardcodes text-[0.85rem]']);
    // The shapes someone would reach for NEXT, which a rem-only check would wave through.
    expect(rawSizes([['./ui.tsx', 'className="text-[13px]"']])).toHaveLength(1);
    expect(rawSizes([['./ui.tsx', 'className="text-[1.2em]"']])).toHaveLength(1);
    // And what must keep passing.
    expect(rawSizes([['./ui.tsx', 'className="text-body text-ink-soft"']])).toEqual([]);
    expect(rawSizes([['./ui.tsx', 'className="w-[18rem] leading-[1.35]"']])).toEqual([]);
  });

  it('is looking at files at all — a vacuous check is a check that has stopped working', () => {
    expect(components.length).toBeGreaterThan(10);
  });

  it('holds: every size in the platform is a named step', () => {
    expect(rawSizes(components)).toEqual([]);
  });

  it('declares the seven steps the class merge knows, and reads its colours', () => {
    expect(declaredSteps(tokensCss).sort()).toEqual([...TYPE_STEPS].sort());
    expect(declaredColours(tokensCss)).toEqual(expect.arrayContaining(['ink', 'ink-faint', 'accent-ink', 'st-declined']));
  });

  it('catches a class that names no step — the check itself, in the shape the monitor\'s title took', () => {
    const wears = (source: string) => undeclaredSteps([['./work/MonitorWindow.tsx', source]], tokensCss);
    expect(wears('<h1 className="m-0 text-h3">')).toEqual(['./work/MonitorWindow.tsx wears text-h3, which tokens.css does not declare']);
    // Tailwind's own scale renders and is not the platform's (D56).
    expect(wears('className="text-sm"')).toHaveLength(1);
    expect(wears('className="hover:text-xl"')).toHaveLength(1);
    // And what must keep passing: a step, a colour with and without its opacity, a layout.
    expect(wears('className="text-view text-ink-soft hover:text-st-declined text-ink/70 text-pretty text-left"')).toEqual([]);
    // Tailwind's colour keywords draw though its palette is cleared (LOOK6).
    expect(wears('className="text-current text-inherit text-transparent"')).toEqual([]);
    // A raw size is the other check's, and a word that merely contains the prefix is no class.
    expect(wears('className="text-[0.85rem] context-text-h3"')).toEqual([]);
  });

  it('holds: every text- class names a step, a colour or a layout tokens.css or Tailwind declares', () => {
    expect(undeclaredSteps([...components, ...modules], tokensCss)).toEqual([]);
  });
});

describe('the colours', () => {
  const wears = (source: string, css = tokensCss) => undeclaredColours([['./asks/AskPage.tsx', source]], css);

  it('catches a colour no token defines — the check itself, in the shape bg-sunken took', () => {
    // tokens.css as it stood before LOOK6: the box asked for a colour the theme did not have.
    const before = tokensCss.replace(/^\s*--color-sunken\s*:.*$/m, '');
    expect(before).not.toBe(tokensCss);
    expect(wears('className="mb-4 flex rounded-control border border-line bg-sunken px-2.5 py-2"', before))
      .toEqual(['./asks/AskPage.tsx wears bg-sunken, a colour tokens.css does not declare']);
    // Tailwind's own palette, which tokens.css clears, behind a variant, with an opacity, and on one side.
    expect(wears('className="hover:bg-white"')).toHaveLength(1);
    expect(wears('className="border-red-500/40"')).toHaveLength(1);
    expect(wears('className="border-l-muted"')).toHaveLength(1);
    expect(wears("cn('text-ink', busy && 'bg-muted')")).toHaveLength(1);
  });

  it('leaves what is a token, a built-in or no colour at all', () => {
    expect(wears([
      'className="bg-raised bg-page/60 hover:bg-accent-soft data-[state=checked]:bg-accent bg-st-done/10 bg-scrim',
      'bg-transparent bg-inherit border-current border-transparent bg-ident-moss/15',
      'border border-0 border-2 border-x border-t border-b-0 border-l-[3px] border-l-st-open border-t-line',
      'border-dashed border-none border-collapse border-spacing-2 border-line-strong hover:enabled:border-accent',
      'bg-cover bg-center bg-left-top bg-no-repeat bg-clip-text bg-linear-to-r bg-[url(x)] border-[#cfcabe]"',
      // A class built from a variable is not a name, and a word that merely holds the prefix is no class.
      'className={`bg-st-${state} border-st-${state}`} data-bg-sunken="x" box-border',
    ].join(' '))).toEqual([]);
  });

  it('holds: every bg- and border- colour a web source names is one tokens.css declares', () => {
    expect(undeclaredColours([...components, ...modules], tokensCss)).toEqual([]);
  });
});

describe('the scrim', () => {
  it('catches a full-bleed scrim — the check itself, in the shape the regression took', () => {
    expect(scrimsOverTheStrip([['./ui.tsx', '<Dialog.Overlay className="fixed inset-0 z-10 bg-scrim" />']]))
      .toHaveLength(1);
    // And what must keep passing: a scrim that starts below the strip.
    expect(scrimsOverTheStrip([['./ui.tsx', 'className="fixed inset-x-0 bottom-0 top-9 z-10 bg-scrim"']]))
      .toEqual([]);
  });

  it('holds: no overlay covers the strip the window paints its buttons into', () => {
    expect(scrimsOverTheStrip(components)).toEqual([]);
  });

  it('catches a full-height panel — the shape the drawer had', () => {
    expect(panelsOverTheStrip([['./ui.tsx',
      '<Dialog.Content className="fixed inset-y-0 right-0 z-10 flex w-[min(32rem,100%)] flex-col bg-overlay">']]))
      .toHaveLength(1);
    // And what must keep passing: a panel that starts below the strip.
    expect(panelsOverTheStrip([['./ui.tsx', 'className="fixed bottom-0 right-0 top-9 z-10 flex bg-overlay"']]))
      .toEqual([]);
  });

  it('holds: no panel puts its own header under the caption buttons', () => {
    expect(panelsOverTheStrip(components)).toEqual([]);
  });

  it('catches a scrim or a panel that runs over the status bar, in the shape both had', () => {
    expect(overlaysOverTheStatusBar([['./ui.tsx',
      '<Dialog.Overlay className="fixed inset-y-0 bottom-0 left-12 right-0 top-9 z-10 bg-scrim" />']]))
      .toHaveLength(1);
    expect(overlaysOverTheStatusBar([['./ui.tsx',
      'className="fixed bottom-0 right-0 top-9 z-10 flex w-[min(32rem,100%)] flex-col bg-overlay"']]))
      .toHaveLength(1);
    // And what must keep passing: both ending above the bar.
    expect(overlaysOverTheStatusBar([['./ui.tsx', 'className="fixed bottom-6 left-12 right-0 top-9 z-10 bg-scrim"']]))
      .toEqual([]);
  });

  it('holds: no overlay covers the status bar', () => {
    expect(overlaysOverTheStatusBar(components)).toEqual([]);
  });
});

/**
 * 🔴 **A chosen theme is the same palette as the system's** (D66). `tokens.css` carries each theme
 * twice — once for the OS's media query, once for `data-theme` — because a chosen theme has to
 * outrank the query and CSS has no way to say "this block, again, under another selector". Two
 * copies are two palettes the first time one is edited alone, and the edit would look right in
 * whichever the person happens to be using. So every value is held equal here.
 */
export function palettes(css: string): Record<'system-light' | 'system-dark' | 'chosen-light' | 'chosen-dark', Record<string, string>> {
  const tokens = (body: string) =>
    Object.fromEntries([...body.matchAll(/(--[a-z-]+)\s*:\s*(#[0-9a-fA-F]{3,8})\s*;/g)].map((m) => [m[1], m[2].toLowerCase()]));
  const block = (pattern: RegExp) => tokens(pattern.exec(css)?.[1] ?? '');
  return {
    'system-light': block(/^:root\s*\{([^}]*)\}/m),
    'system-dark': block(/@media \(prefers-color-scheme: dark\)\s*\{\s*:root\s*\{([^}]*)\}/),
    'chosen-light': block(/:root\[data-theme="light"\]\s*\{([^}]*)\}/),
    'chosen-dark': block(/:root\[data-theme="dark"\]\s*\{([^}]*)\}/),
  };
}

// From disk, not `?raw`: the CSS pipeline answers a raw import of a stylesheet with an empty string,
// which is how the first version of this check came to hold nothing equal to nothing.
const tokensCss = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');

describe('the chosen themes', () => {
  it('reads every block — a parser that stopped reading would hold nothing equal to nothing', () => {
    const read = palettes(tokensCss);
    for (const [name, values] of Object.entries(read)) {
      expect(Object.keys(values).length, `${name} parsed ${Object.keys(values).length} tokens`).toBeGreaterThanOrEqual(15);
    }
    expect(read['system-light']['--page']).not.toBe(read['system-dark']['--page']);
  });

  it('catches a chosen block that drifted from its system twin', () => {
    const drifted = tokensCss.replace(/(:root\[data-theme="dark"\]\s*\{[^}]*--page:\s*)#16161a/, '$1#000000');
    expect(drifted).not.toBe(tokensCss);
    expect(palettes(drifted)['chosen-dark']).not.toEqual(palettes(drifted)['system-dark']);
  });

  it('holds: chosen light is the system light, chosen dark is the system dark', () => {
    const read = palettes(tokensCss);
    expect(read['chosen-light']).toEqual(read['system-light']);
    expect(read['chosen-dark']).toEqual(read['system-dark']);
  });
});

/**
 * **Every surface is readable in every ink, and the sunken one lies below the page** (D41 §3; LOOK6). The status and
 * identity hues were computed and the surfaces under the inks were not, until a fourth surface arrived for the box a
 * move asks once in. Body and secondary ink reach 4.5:1 on every surface; the faint ink, which carries meta and a
 * field's placeholder beside a label that says the same, reaches 3:1. Computed from `tokens.css`, in both themes.
 */
const SURFACES = ['--page', '--sunken', '--raised', '--overlay'];
const INK_FLOORS: [ink: string, floor: number][] = [['--ink', 4.5], ['--ink-soft', 4.5], ['--ink-faint', 3], ['--ink-danger', 4.5]];

/** Relative luminance of a `#rrggbb` colour (WCAG 2). */
export function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((at) => {
    const c = parseInt(hex.slice(at, at + 2), 16) / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  }) as [number, number, number];
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** The contrast ratio of two `#rrggbb` colours (WCAG 2), whichever is lighter. */
export function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [number, number];
  return (light + 0.05) / (dark + 0.05);
}

describe('the surfaces', () => {
  const themes = () => {
    const read = palettes(tokensCss);
    return { light: read['system-light'], dark: read['system-dark'] };
  };

  it('reads every surface and ink in both themes', () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      for (const token of [...SURFACES, ...INK_FLOORS.map(([ink]) => ink)]) {
        expect(tokens[token], `${theme} ${token}`).toMatch(/^#[0-9a-f]{6}$/);
      }
    }
  });

  it('catches an ink too faint for its surface, and a well above the page — the check itself', () => {
    const { light } = themes();
    // The faint ink on a field's strong line: the shape a "subtle" surface would take one step too far.
    expect(contrast(light['--ink-faint']!, light['--line-strong']!)).toBeLessThan(3);
    expect(contrast('#000000', '#ffffff')).toBeCloseTo(21, 5);
    expect(luminance(light['--raised']!)).toBeGreaterThan(luminance(light['--page']!));
  });

  it('holds: every ink reaches its floor on every surface, in both themes', () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      for (const surface of SURFACES) {
        for (const [ink, floor] of INK_FLOORS) {
          const ratio = contrast(tokens[ink]!, tokens[surface]!);
          expect(ratio, `${theme} ${ink} on ${surface}: ${ratio.toFixed(2)}`).toBeGreaterThanOrEqual(floor);
        }
      }
    }
  });

  it('holds: the sunken surface lies below the page in both themes, so the well reads as one', () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      expect(luminance(tokens['--sunken']!), `${theme} --sunken against --page`).toBeLessThan(luminance(tokens['--page']!));
    }
  });
});

/**
 * 🔴 **Red drawn as words wears the danger ink, never the fill's colour** (UXFIX5, from the 2026-10-07 second opinion).
 * The status hues were computed to tell four states apart as fills, borders and marks. Declined's dark red drawn as a
 * danger button's label, a refusal's sentence or a declined pill's word measured 3.95:1 on the sunken box a move asks
 * once in, 3.18:1 on an overlay and 2.94:1 on its own soft field there: under the 4.5:1 text floor, in the theme
 * nobody had measured it in. `--ink-danger` is the red a word wears, held to the text floor on every surface and on
 * declined's soft field over each, in both themes; a fill, a border and a mark keep `--st-declined`.
 */
const DANGER_AS_TEXT = /(?<![\w-])text-st-declined(?![\w-])/g;
const DANGER_AS_CSS_TEXT = /(?<![\w-])color\s*:\s*var\(--st-declined\)/g;

/**
 * Files that still draw red words in the fill's colour, each with its reason. Empty since UXFIX5b swapped the three
 * Settings sites UXFIX5 left to their own lane, and held empty: a red word in the fill's colour is fixed, not allowed.
 */
const DANGER_TEXT_ELSEWHERE: Record<string, string> = {};

/** Every place a source draws danger as text in `--st-declined`: a `text-` class, or a stylesheet's `color`. */
export function dangerAsText(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !(path in DANGER_TEXT_ELSEWHERE))
    .flatMap(([path, source]) => [...(source.match(DANGER_AS_TEXT) ?? []), ...(source.match(DANGER_AS_CSS_TEXT) ?? [])]
      .map((hit) => `${path} draws danger as text in the fill's colour: ${hit}`));
}

/** A colour laid over a surface at an opacity, as the browser composites `bg-st-declined/10`: per sRGB channel. */
export function over(colour: string, surface: string, alpha: number): string {
  const channel = (hex: string, at: number) => parseInt(hex.slice(at, at + 2), 16);
  return `#${[1, 3, 5].map((at) => Math.round(alpha * channel(colour, at) + (1 - alpha) * channel(surface, at))
    .toString(16).padStart(2, '0')).join('')}`;
}

/** The stylesheets beside the sources, read from disk: a raw import of a stylesheet answers an empty string. */
const stylesheets = Object.keys(import.meta.glob('./**/*.css'))
  .map((path) => [path, readFileSync(join(process.cwd(), 'src', path), 'utf8')] as [string, string]);

describe('the danger ink', () => {
  const themes = () => {
    const read = palettes(tokensCss);
    return { light: read['system-light'], dark: read['system-dark'] };
  };

  it("catches declined's fill drawn as words in dark — the check itself, in the measure the review took", () => {
    const { dark } = themes();
    const sunken = contrast(dark['--st-declined']!, dark['--sunken']!);
    expect(sunken).toBeGreaterThan(3.9);
    expect(sunken).toBeLessThan(4.5);
    expect(contrast(dark['--st-declined']!, over(dark['--st-declined']!, dark['--overlay']!, 0.1))).toBeLessThan(3);
    expect(over('#000000', '#ffffff', 0.1)).toBe('#e6e6e6');
  });

  it("holds: the danger ink reaches the text floor on declined's soft field over every surface, in both themes", () => {
    for (const [theme, tokens] of Object.entries(themes())) {
      for (const surface of SURFACES) {
        const field = over(tokens['--st-declined']!, tokens[surface]!, 0.1);
        const ratio = contrast(tokens['--ink-danger']!, field);
        expect(ratio, `${theme} --ink-danger on declined's soft field over ${surface}: ${ratio.toFixed(2)}`)
          .toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it("catches danger drawn as text in the fill's colour, behind a variant and in a stylesheet, and leaves a fill and a border", () => {
    expect(dangerAsText([['./work/Composer.tsx', '<p className="m-0 text-small text-st-declined">']]))
      .toEqual(["./work/Composer.tsx draws danger as text in the fill's colour: text-st-declined"]);
    expect(dangerAsText([['./ui.tsx', "'border-st-declined hover:text-st-declined text-st-declined/80'"]])).toHaveLength(2);
    expect(dangerAsText([['./work/code.css', '.hljs-deletion {\n  color: var(--st-declined);\n}']])).toHaveLength(1);
    // And what must keep passing: the ink itself, the fill, the border, a mark, and a word that holds the prefix.
    expect(dangerAsText([['./ui.tsx', [
      "'border border-st-declined text-ink-danger hover:enabled:bg-st-declined/10 border-l-st-declined'",
      "'bg-st-declined context-text-st-declined' background-color: var(--st-declined); border-color: var(--st-declined);",
    ].join(' ')]])).toEqual([]);
  });

  it('is reading the stylesheets too — a scan of none would hold nothing', () => {
    expect(stylesheets.map(([path]) => path)).toEqual(expect.arrayContaining(['./tokens.css', './work/code.css']));
  });

  it('holds: every red word in the platform wears the danger ink', () => {
    expect(dangerAsText([...components, ...modules, ...stylesheets])).toEqual([]);
  });

  it('holds: no file is allowed the fill as text any more, so a new red word cannot be waved through (UXFIX5b)', () => {
    expect(DANGER_TEXT_ELSEWHERE).toEqual({});
  });

  it('holds: each file still allowed the fill as text still draws it there, so a row goes when its swap lands', () => {
    for (const path of Object.keys(DANGER_TEXT_ELSEWHERE)) {
      const source = components.find(([each]) => each === path)?.[1] ?? '';
      expect(source.match(DANGER_AS_TEXT), `${path} no longer draws danger in the fill's colour: drop its row`).not.toBeNull();
    }
  });
});

/**
 * An ideograph has no italic. The Chinese system face carries none, so the browser slants it by
 * synthesis, and that is what the installed window showed under every italic hint in 中文. The
 * Latin face has a true italic and keeps it: only the synthesis is refused.
 */
describe('the page body', () => {
  it('never synthesises an oblique, so a Chinese sentence set italic stays upright', () => {
    const body = /@layer base\s*\{\s*body\s*\{([^}]*)\}/.exec(tokensCss)?.[1] ?? '';
    expect(body, 'the base body rule was not found').toContain('font-family');
    expect(body).toMatch(/font-synthesis-style:\s*none/);
  });
});

/**
 * 🔴 **A choice is the platform's own control, never the OS's** (UX5 U5). The start form's three
 * selects and checkbox, and a review file's *viewed*, were native controls beside `ui.tsx`'s own, so
 * they wore the OS's look and its accent in a palette that draws its own (platform language §4).
 * `SelectField`, `CheckField` and `Segmented` are the controls; only `ui.tsx` may build one.
 */
const NATIVE_CHOICE = /<select\b|type=["']checkbox["']|type=["']radio["']/g;

export function nativeChoices(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => path !== './ui.tsx')
    .flatMap(([path, source]) =>
      (source.match(NATIVE_CHOICE) ?? []).map((hit) => `${path} builds a native control: ${hit}`));
}

/**
 * 🔴 **A date and a count are in the reader's language** (UX5 U28). A quest's drawer read *发起
 * 23/09/2026, 1:58:49 pm* in 中文: `toLocaleString()` with no locale takes the machine's, not the
 * page's. `format.ts` holds the helpers that name `i18n.language`, and only it formats.
 */
const LANGUAGE_BLIND = /\.toLocale(?:String|DateString|TimeString)\(\s*\)/g;

export function languageBlind(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => path !== './format.ts')
    .flatMap(([path, source]) =>
      (source.match(LANGUAGE_BLIND) ?? []).map((hit) => `${path} leaves the locale to the machine: ${hit}`));
}

const modules = Object.entries(import.meta.glob('./**/*.ts', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>).filter(([path]) => !/\.test\.ts$/.test(path));

describe('dates and counts', () => {
  it("catches a locale left to the machine, and leaves format.ts its own", () => {
    expect(languageBlind([['./QuestsView.tsx', '{new Date(detail.filed).toLocaleString()}']])).toHaveLength(1);
    expect(languageBlind([['./App.tsx', 'entries: indexed.toLocaleString( ),']])).toHaveLength(1);
    expect(languageBlind([['./x.tsx', 'd.toLocaleDateString()']])).toHaveLength(1);
    expect(languageBlind([['./x.tsx', 'n.toLocaleString(i18n.language)']])).toEqual([]);
    expect(languageBlind([['./format.ts', 'value.toLocaleString()']])).toEqual([]);
  });

  it("holds: every date and count on the page is in the reader's language", () => {
    expect(languageBlind([...components, ...modules])).toEqual([]);
  });
});

/**
 * 🔴 **A page is one column, and the column is its pane** (D141, LAYOUT11). A quest's body wore `max-w-prose`, 65ch:
 * on the install at 1600 px its title ran the pane while its body stopped at 456 px, a plugin's detail wrapped at two
 * edges, and in 中文 the 65 Latin digits held about 32 glyphs. Every block of a page now takes the column's width, and
 * the column (`ViewMain`) is the one place a line's length could be set; it sets none.
 *
 * So no source names a line's measure: Tailwind's `max-w-prose`, or a width counted in `ch` or `em`. A cap, if one is
 * ever set, goes on the column once, in `em` (D141 §3), and `work/ViewMain.tsx` is then the one file this check lets
 * name it. A named width (`max-w-3xl`, 48rem) capped five blocks of a quest's and an ask's pages beside blocks that ran
 * the pane, so no product source names one either; a story's frame stands in for the column a part is drawn in, and
 * is not read. That the column itself sets none is `ViewMain.test.tsx`'s to hold, on what it renders.
 */
const LINE_MEASURE = /(?<![\w-])max-w-(?:prose|\[[\d.]+(?:ch|em)\])(?![\w-])/g;
const NAMED_WIDTH = /(?<![\w-])max-w-(?:xs|sm|md|lg|xl|[2-7]xl)(?![\w-])/g;

/** A named width that is no page's block, each with its reason. */
const NAMED_WIDTH_HOMES: Record<string, string> = {
  './work/CommandCenter.tsx': "the strip's command center, a control in the title bar, never a page's block",
};

export function lineMeasures(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(LINE_MEASURE) ?? []).map((hit) => `${path} sets a line's measure: ${hit}`));
}

export function namedWidths(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !/\.stories\.tsx$/.test(path) && !(path in NAMED_WIDTH_HOMES))
    .flatMap(([path, source]) =>
      (source.match(NAMED_WIDTH) ?? []).map((hit) => `${path} caps a block at a named width: ${hit}`));
}

describe("the page's measure", () => {
  it("catches a line's measure — the check itself, in the shapes the quest's body and the setup card's note took", () => {
    expect(lineMeasures([['./quests/QuestPage.tsx', '<p className="m-0 max-w-prose whitespace-pre-wrap">']]))
      .toEqual(["./quests/QuestPage.tsx sets a line's measure: max-w-prose"]);
    expect(lineMeasures([['./settings/GetStarted.tsx', '<Prose className="max-w-[28ch] text-small">']])).toHaveLength(1);
    // Behind a variant, and in the unit a future cap would take, which belongs on the column alone.
    expect(lineMeasures([['./x.tsx', 'className="md:max-w-prose"']])).toHaveLength(1);
    expect(lineMeasures([['./x.tsx', "cn('max-w-[72em]')"]])).toHaveLength(1);
    // And what must keep passing: a form's own size in rem, a width relative to the column, a word holding the prefix.
    expect(lineMeasures([['./x.tsx', 'className="max-w-[48rem] max-w-full max-w-none max-w-[min(36rem,100%)] data-max-w-prose"']]))
      .toEqual([]);
  });

  it('catches a block capped at a named width, and leaves a story its frame', () => {
    expect(namedWidths([['./quests/QuestPage.tsx', '<div className="mt-4 max-w-3xl">']]))
      .toEqual(['./quests/QuestPage.tsx caps a block at a named width: max-w-3xl']);
    expect(namedWidths([['./asks/GoAheadList.tsx', 'className="m-0 grid max-w-xl list-none"']])).toHaveLength(1);
    expect(namedWidths([['./work/SessionHead.stories.tsx', '<div className="max-w-3xl"><Story /></div>']])).toEqual([]);
    expect(namedWidths([['./work/CommandCenter.tsx', "'flex w-full min-w-0 max-w-md'"]])).toEqual([]);
    expect(namedWidths([['./x.tsx', 'className="max-w-[48rem] text-xl"']])).toEqual([]);
  });

  it("holds: no source names a line's measure, so every block of a page wraps at the column's edge", () => {
    expect(lineMeasures([...components, ...modules])).toEqual([]);
  });

  it('holds: no product source caps a block at a named width', () => {
    expect(namedWidths([...components, ...modules])).toEqual([]);
  });
});

describe('the form controls', () => {
  it('catches a native select, checkbox or radio, and leaves ui.tsx its own', () => {
    expect(nativeChoices([['./work/Start.tsx', '<select value={x}>']])).toHaveLength(1);
    expect(nativeChoices([['./work/Row.tsx', '<input type="checkbox" checked />']])).toHaveLength(1);
    expect(nativeChoices([['./work/Row.tsx', "<input type='radio' />"]])).toHaveLength(1);
    expect(nativeChoices([['./ui.tsx', '<select value={x}>']])).toEqual([]);
    expect(nativeChoices([['./work/Row.tsx', '<SelectField value={x} />']])).toEqual([]);
  });

  it('holds: every choice in the platform is the platform\'s own control', () => {
    expect(nativeChoices(components)).toEqual([]);
  });
});
