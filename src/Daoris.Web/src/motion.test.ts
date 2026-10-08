import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { createElement } from 'react';
import { render } from '@testing-library/react';
import { compile } from 'tailwindcss';
import { describe, expect, it } from 'vitest';
import { DotMark } from './ui';

/**
 * **A mark at rest is still** (FREEZE1, D41's motion rule: motion on overlays and hovers, and no shimmer anywhere).
 *
 * The live dot pulsed for ever. Measured on the install (2026-10-08), one endless `motion-safe:animate-pulse` kept the
 * window drawing at the display's rate, 601 frames in 5 s, and its GPU process spent about a quarter of a core for as long
 * as any session was live. So the pulse says a turn began and then settles: a few cycles, never `infinite`.
 *
 * Read from the stylesheet the page is built with, compiled as the build compiles it, because the count is a token's
 * (`--animate-pulse` in `tokens.css`) and a class name says nothing of it. Tailwind stays out of the other tests (WEBFAST1);
 * this one compiles only the classes it names.
 */

/** The page's stylesheet with exactly these classes built, as `@tailwindcss/vite` builds it. */
async function built(classes: readonly string[]): Promise<string> {
  const web = process.cwd();
  const compiler = await compile(readFileSync(join(web, 'src', 'tokens.css'), 'utf8'), {
    base: join(web, 'src'),
    loadStylesheet: async (id, base) => {
      const path = id === 'tailwindcss' ? join(web, 'node_modules', 'tailwindcss', 'index.css') : resolve(base, id);
      return { path, base: dirname(path), content: readFileSync(path, 'utf8') };
    },
  });
  return compiler.build([...classes]);
}

/** A class as a selector names it: `motion-safe:animate-pulse` is `.motion-safe\:animate-pulse`. */
const selectorOf = (name: string) => `.${name.replace(/[^a-zA-Z0-9_-]/g, (mark) => `\\${mark}`)}`;

/**
 * What a class runs, as the stylesheet resolves it: its `animation` value, a variable read through to the value the
 * theme gives it. Null where the class sets no animation.
 */
export function animationOf(css: string, name: string): string | null {
  const at = css.indexOf(`${selectorOf(name)} {`);
  if (at < 0) return null;
  const rule = css.slice(at, css.indexOf('}', at));
  const value = /animation:\s*([^;]+);/.exec(rule)?.[1]?.trim();
  if (!value) return null;
  const variable = /^var\((--[\w-]+)\)$/.exec(value)?.[1];
  if (!variable) return value;
  return new RegExp(`${variable}:\\s*([^;]+);`).exec(css)?.[1]?.trim() ?? null;
}

/** How many times an animation runs: `infinite` for ever, a bare number that many times, and once where it says none. */
export function iterations(animation: string): number {
  // A timing function's arguments are numbers too, and say nothing of the count.
  const words = animation.replace(/[\w-]+\([^)]*\)/g, ' ').split(/[\s,]+/).filter(Boolean);
  if (words.includes('infinite')) return Number.POSITIVE_INFINITY;
  const count = words.find((word) => /^\d+(?:\.\d+)?$/.test(word));
  return count === undefined ? 1 : Number(count);
}

/** The animation classes an element wears, its variants kept: what the stylesheet is asked to build. */
const animationClasses = (element: Element) => [...element.classList].filter((name) => /(?:^|:)animate-/.test(name));

/** The most a mark at rest may pulse before it settles. */
const SETTLES_WITHIN = 3;

describe('motion settles', () => {
  it('counts an animation\'s runs — the check itself, in the shapes the pulse can take', () => {
    expect(iterations('pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite')).toBe(Number.POSITIVE_INFINITY);
    expect(iterations('pulse 2s cubic-bezier(0.4, 0, 0.6, 1) 3')).toBe(3);
    expect(iterations('drawer-in 140ms ease-out')).toBe(1);
    expect(animationOf('.a { animation: var(--x); } :root { --x: spin 1s linear infinite; }', 'a'))
      .toBe('spin 1s linear infinite');
    expect(animationOf('.motion-safe\\:a { animation: fade 1s 2; }', 'motion-safe:a')).toBe('fade 1s 2');
  });

  it('holds: the live mark pulses a few times and then stays still', async () => {
    const { container } = render(createElement(DotMark, { tone: 'live' }));
    const mark = container.firstElementChild!;
    const classes = animationClasses(mark);
    // A mark that stopped animating at all would pass the rest vacuously; the pulse is how a turn's start is seen.
    expect(classes).not.toEqual([]);

    const css = await built(classes);
    for (const name of classes) {
      const animation = animationOf(css, name);
      expect(animation, `${name} sets no animation in the built stylesheet`).not.toBeNull();
      expect(iterations(animation!), `${name} runs ${animation}`).toBeLessThanOrEqual(SETTLES_WITHIN);
    }
  });
});
