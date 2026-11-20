import { type ComponentType, createElement } from 'react';
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { composeStories, setProjectAnnotations } from '@storybook/react-vite';
import preview from '../../.storybook/preview';
import '../locales/i18n';
import { rooted } from './sources';
import { glyphsInAccent } from './census/glyphs';

// 🔴 Storybook's project annotations, so `composeStories` renders a story the way Storybook does.
//
// Without them, a composed story applies its OWN decorators and none of the project's — so the tooltip provider
// `.storybook/preview.tsx` mounts for every story is absent, and any component carrying a `Tip` throws. That reads as
// "this story needs wrapping", which is the wrong fix: the suite renders what a reviewer sees.
//
// They are set here, by the files that compose stories, and not in the setup file every test file pays: loading
// Storybook's runtime costs a tenth of the unit run when each of 592 fresh vm contexts loads it.
setProjectAnnotations(preview);

export { composeStories };

/**
 * Every story, rendered — Storybook's states are the design review, and this makes each one
 * a smoke test for free. It is a multiplier, not a second suite: a story that throws is caught by
 * the inner loop rather than by a reviewer opening Storybook, and a state nobody wrote a story for
 * is still a state nobody checks.
 *
 * The roster is a glob, so a new `*.stories.tsx` is covered by the act of existing. `composeStories`
 * applies the story's own args and decorators, so what renders here is what a reviewer sees.
 *
 * The glob reaches DOWN, not just across: the working surface's components live in `src/work/`
 * (components plan §2), and a roster that stopped at the top level would have silently excluded
 * every one of them while still reporting a passing suite.
 *
 * The roster is shared out among the shards `design/stories.<n>.test.tsx`, each its own file so the run renders them side by
 * side (one file held every story, 250 s on one worker): the story files sorted by path, the shard that renders one
 * is its place modulo the shards present, so adding or removing a shard shares the roster again and no story falls between.
 *
 * And each render remembers no language: a story's language is the story's, so a story that wrote the page's
 * `daoris.language` would bring the next story, and every later load in that browser profile, up in its language. The
 * key is cleared before each render, as a fresh profile has it, so a write of any language shows. The import half is
 * `storyLanguage.test.tsx`'s, since these modules are imported before any test runs.
 */
type StoriesModule = Parameters<typeof composeStories>[0];
const LANGUAGE_KEY = 'daoris.language';

/** Declares the renders of the shard named `shard` among `shards` (both lazy globs of the shard files) over the lazy glob `stories`. */
export async function everyStoryOf(
  stories: Record<string, () => Promise<unknown>>,
  shards: Record<string, () => Promise<unknown>>,
  shard: string,
): Promise<void> {
  // A glob never holds the file that globs, so the shard adds itself.
  const names = [...new Set([...Object.keys(shards).map((path) => path.split('/').at(-1)!), shard])].sort();
  const at = names.indexOf(shard);
  const lazy = rooted(stories);
  const mine = Object.keys(lazy).sort().filter((_, index) => index % names.length === at);
  const modules = await Promise.all(mine.map(async (path) => [path, (await lazy[path]!()) as StoriesModule] as const));

  describe(`the stories (${shard})`, () => {
    it('are found at all — a glob that matches nothing passes every assertion below it', () => {
      expect(names.length, 'the shards are found by their glob').toBeGreaterThan(1);
      expect(modules.length).toBeGreaterThan(0);
    });

    for (const [path, module] of modules) {
      const composed = composeStories(module) as unknown as Record<string, ComponentType>;
      for (const [name, Story] of Object.entries(composed)) {
        it(`${path.replace(/^\.\/|\.stories\.tsx$/g, '')} · ${name} renders`, () => {
          localStorage.removeItem(LANGUAGE_KEY);
          let drawn: HTMLElement | undefined;
          expect(() => { drawn = render(createElement(Story)).baseElement; }).not.toThrow();
          expect(localStorage.getItem(LANGUAGE_KEY), 'a story remembered a language').toBeNull();
          // Its glyphs wear the inks, never the accent (`design/glyphInk.test.tsx`): read over the page, portals included.
          expect(glyphsInAccent(drawn!), 'a glyph wears the accent').toEqual([]);
        });
      }
    }
  });
}
