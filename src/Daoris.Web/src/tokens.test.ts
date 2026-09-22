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
});
