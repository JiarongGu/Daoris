import { describe, expect, it } from 'vitest';

/**
 * **The atoms own the primitives** (MENU1). Radix supplies the behaviour and `ui.tsx` every pixel (D42), so a
 * screen reaches a headless primitive only through an atom. A screen that imports one styles it on its own, and
 * its copy keeps whatever that screen knew when it was written: eight dropdown menus did, and none capped its
 * height, so a long one ran off the window as SELECT1's select had, and was fixed in one place only.
 *
 * Held here for the reason `presentational.test.ts` holds what a molecule imports: it breaks one import at a
 * time and silently. What may import a primitive is a list by name, which someone must justify appending to.
 */

/** An import of a Radix primitive: `from '@radix-ui/…'`, or a bare or dynamic `import('@radix-ui/…')`. */
const PRIMITIVE = /(?:\bfrom\s+|\bimport\s*\(?\s*)['"](@radix-ui\/[^'"]+)['"]/g;

/** The atoms, which may import any primitive: they are where the pixels are. */
const ATOMS = new Set<string>(['./ui.tsx']);

/**
 * What else may import one, and which. The application's root mounts the one tooltip provider every `Tip`
 * needs (`main.tsx`); it renders no pixel of its own.
 */
const ROOTS: Record<string, readonly string[]> = {
  './main.tsx': ['@radix-ui/react-tooltip'],
};

/** Tests, stories and the test harness mount the same provider the root does, and ship nothing. */
const HARNESS = /\.(test|stories)\.tsx?$|^\.\/test\//;

export function reachers(files: [path: string, source: string][]): string[] {
  return files
    .filter(([path]) => !ATOMS.has(path))
    .flatMap(([path, source]) => [...source.matchAll(PRIMITIVE)]
      .map((match) => match[1]!)
      .filter((primitive) => !(ROOTS[path] ?? []).includes(primitive))
      .map((primitive) => `${path} imports ${primitive}`));
}

const sources = import.meta.glob('./**/*.{ts,tsx}', {
  eager: true, query: '?raw', import: 'default',
}) as Record<string, string>;

const shipped = Object.entries(sources).filter(([path]) => !HARNESS.test(path));

describe('the atoms own the primitives', () => {
  it('catches an import it is meant to catch — the check itself, sabotaged', () => {
    // The shape eight menus had.
    expect(reachers([['./work/AppMenu.tsx', "import * as Menu from '@radix-ui/react-dropdown-menu';\n"]]))
      .toEqual(['./work/AppMenu.tsx imports @radix-ui/react-dropdown-menu']);
    // And the shapes the next one might take: a named import, a type, a dynamic import.
    expect(reachers([['./help/QuickAsk.tsx', "import { Root } from \"@radix-ui/react-dialog\";\n"]])).toHaveLength(1);
    expect(reachers([['./map/Lines.tsx', "import type { DropdownMenuProps } from '@radix-ui/react-dropdown-menu';\n"]]))
      .toHaveLength(1);
    expect(reachers([['./map/Lines.tsx', "const menu = await import('@radix-ui/react-dropdown-menu');\n"]])).toHaveLength(1);
    // The root may mount the tooltip provider, and nothing else.
    expect(reachers([['./main.tsx', "import * as Tooltip from '@radix-ui/react-tooltip';\n"]])).toEqual([]);
    expect(reachers([['./main.tsx', "import * as Dialog from '@radix-ui/react-dialog';\n"]]))
      .toEqual(['./main.tsx imports @radix-ui/react-dialog']);
    // The atoms may import any.
    expect(reachers([['./ui.tsx', "import * as Menu from '@radix-ui/react-dropdown-menu';\n"]])).toEqual([]);
    // And the atoms' own import is no primitive.
    expect(reachers([['./work/AppMenu.tsx', "import { Menu } from '../ui';\n"]])).toEqual([]);
    // The harness is told apart by its name.
    expect(HARNESS.test('./work/AppMenu.test.tsx')).toBe(true);
    expect(HARNESS.test('./work/AppMenu.stories.tsx')).toBe(true);
    expect(HARNESS.test('./test/shellHarness.tsx')).toBe(true);
    expect(HARNESS.test('./work/AppMenu.tsx')).toBe(false);
  });

  it('is looking at files at all — a vacuous check is a check that has stopped working', () => {
    expect(shipped.length).toBeGreaterThan(50);
    expect(shipped.some(([path]) => path === './ui.tsx')).toBe(true);
    expect(shipped.some(([path]) => path === './main.tsx')).toBe(true);
  });

  it('holds: no screen imports a primitive, only the atoms do', () => {
    expect(reachers(shipped)).toEqual([]);
  });
});
