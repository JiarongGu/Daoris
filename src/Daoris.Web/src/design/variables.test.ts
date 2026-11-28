import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { rooted } from '../test/sources';

/**
 * 🔴 **A variable no rule declares draws nothing, and says nothing**. When the role set renamed every token at once, a page that
 * still asked for a name gone with it (`var(--ink-gone)`, `duration-(--pace)`) would take the property's initial value: a word in the
 * default black, a transition with no time. `tokens.test.ts` reads the classes; this reads every variable a source or a stylesheet
 * asks for, in `var(…)` or Tailwind's `-(…)`, against those `tokens.css` declares and the few a component sets at runtime.
 */
const VARIABLE_USE = /(?:var\(|-\()\s*(--[a-z][a-z0-9-]*[a-z0-9])(?![a-z0-9-]|\$\{)/g;

const SET_AT_RUNTIME: Record<string, string> = {
  '--canvas-k': "the canvas's scale, on its world (design/canvas/Canvas.tsx)",
  '--form-columns': "a setting form's field widths, on its grid (design/atoms/SettingForm.tsx)",
  '--frame-zoom': "the window zoom's inverse, on the root (design/zoom.ts)",
  '--session-head': "the session page head's height, on the main area (work/head/SessionPageHead.tsx)",
  '--wait-part': "a wait part's share of the panel (work/waits/WaitPanel.tsx)",
  '--radix-dropdown-menu-content-available-height': "Radix's menu, on its content",
  '--radix-select-content-available-height': "Radix's select, on its content",
  '--radix-select-trigger-width': "Radix's select, on its content",
};

/** The variables a stylesheet declares: each `--name:` in it. */
export function declaredVariables(css: string): Set<string> {
  return new Set([...css.matchAll(/(?<![\w-])(--[a-z][a-z0-9-]*[a-z0-9])\s*:/g)].map((match) => match[1]!));
}

/** Every variable a source asks for that `tokens.css` does not declare and no component sets at runtime. */
export function undeclaredVariables(files: [path: string, source: string][], css: string, runtime = SET_AT_RUNTIME): string[] {
  const known = declaredVariables(css);
  return files.flatMap(([path, source]) => [...source.matchAll(VARIABLE_USE)]
    .filter((match) => !known.has(match[1]!) && !(match[1]! in runtime))
    .map((match) => `${path} asks for ${match[1]}, which tokens.css does not declare`));
}

// From disk, not `?raw`: the CSS pipeline answers a raw import of a stylesheet with an empty string.
const tokensCss = readFileSync(join(process.cwd(), 'src', 'tokens.css'), 'utf8');

const sources = Object.entries(rooted(import.meta.glob(['/src/**/*.tsx', '/src/**/*.ts'], {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>)).filter(([path]) => !/\.test\.tsx?$/.test(path));
const stylesheets = Object.keys(rooted(import.meta.glob('/src/**/*.css')))
  .filter((path) => path !== './tokens.css')
  .map((path) => [path, readFileSync(join(process.cwd(), 'src', path), 'utf8')] as [string, string]);

describe('the variables', () => {
  it('catches a variable no rule declares, in a var(), a Tailwind shorthand and a stylesheet, and leaves the rest', () => {
    const asks = (source: string) => undeclaredVariables([['./work/frame/frame.tsx', source]], tokensCss);
    expect(asks('<span className="text-[color:var(--ink-gone)]" />'))
      .toEqual(['./work/frame/frame.tsx asks for --ink-gone, which tokens.css does not declare']);
    expect(asks("'transition-colors duration-(--pace)'")).toHaveLength(1);
    expect(asks('.hljs-string { color: var(--ink-old); }')).toHaveLength(1);
    expect(asks([
      "'duration-(--duration-quick) border-t-[length:var(--frame-rule)]' color: var(--accent-ink);",
      "top-[calc(var(--session-head,0px)-0.75rem)] var(--text-caption--line-height) `var(--leading-${step})`",
    ].join(' '))).toEqual([]);
  });

  it('is reading the sources and the stylesheets', () => {
    expect(sources.length).toBeGreaterThan(500);
    expect(stylesheets.map(([path]) => path)).toContain('./work/review/code.css');
  });

  it('holds: every variable the page asks for is one tokens.css declares or a component sets, and each runtime row is asked', () => {
    expect(undeclaredVariables([...sources, ...stylesheets], tokensCss)).toEqual([]);
    const asked = new Set(sources.flatMap(([, source]) => [...source.matchAll(VARIABLE_USE)].map((match) => match[1]!)));
    for (const name of Object.keys(SET_AT_RUNTIME)) expect(asked.has(name), `${name} is no longer asked for: its row goes`).toBe(true);
  });
});
