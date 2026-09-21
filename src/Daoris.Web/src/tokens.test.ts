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
