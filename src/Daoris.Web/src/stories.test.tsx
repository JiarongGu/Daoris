import type { ComponentType } from 'react';
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { composeStories } from '@storybook/react-vite';
import './i18n';

/**
 * Every story, rendered — Storybook's states are the design review (D42 §5), and this makes each one
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
 * And each render remembers no language (STORY2): a story's language is the story's, so a story that wrote the page's
 * `daoris.language` would bring the next story, and every later load in that browser profile, up in its language. The
 * key is cleared before each render, as a fresh profile has it, so a write of any language shows. The import half is
 * `storyLanguage.test.tsx`'s, since these modules are imported before any test runs.
 */
type StoriesModule = Parameters<typeof composeStories>[0];
const modules = import.meta.glob('./**/*.stories.tsx', { eager: true }) as Record<string, StoriesModule>;
const LANGUAGE_KEY = 'daoris.language';

describe('the stories', () => {
  it('are found at all — a glob that matches nothing passes every assertion below it', () => {
    expect(Object.keys(modules).length).toBeGreaterThan(0);
  });

  for (const [path, module] of Object.entries(modules)) {
    const composed = composeStories(module) as unknown as Record<string, ComponentType>;
    for (const [name, Story] of Object.entries(composed)) {
      it(`${path.replace(/^\.\/|\.stories\.tsx$/g, '')} · ${name} renders`, () => {
        localStorage.removeItem(LANGUAGE_KEY);
        expect(() => render(<Story />)).not.toThrow();
        expect(localStorage.getItem(LANGUAGE_KEY), 'a story remembered a language').toBeNull();
      });
    }
  }
});
