import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

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

/** The steps `tokens.css` declares. A class outside this set is either a typo or a new step. */
export const STEPS = ['meta', 'small', 'body', 'title', 'view', 'wordmark', 'value'] as const;

export function rawSizes(files: [path: string, source: string][]): string[] {
  return files.flatMap(([path, source]) =>
    (source.match(RAW_SIZE) ?? []).map((hit) => `${path} hardcodes ${hit}`));
}

/**
 * 🔴 **A scrim never covers the app strip** (owner, 2026-09-22: *"the backdrop should not cover the
 * topbar? because we do have the hole for the 3 buttons"*).
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

  it('every step the components use is one tokens.css declares', () => {
    const used = new Set(
      components.flatMap(([, source]) => [...source.matchAll(/\btext-([a-z]+)\b/g)].map((m) => m[1])),
    );
    // Colour utilities share the `text-` prefix; the scale is what is left after those.
    const colours = ['ink', 'accent', 'st', 'warn', 'center', 'left', 'right', 'transparent'];
    const sizes = [...used].filter((name) => !colours.includes(name));
    expect(sizes.sort()).toEqual([...STEPS].sort());
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
