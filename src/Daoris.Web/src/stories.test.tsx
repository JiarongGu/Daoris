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
 */
type StoriesModule = Parameters<typeof composeStories>[0];
const modules = import.meta.glob('./**/*.stories.tsx', { eager: true }) as Record<string, StoriesModule>;

describe('the stories', () => {
  it('are found at all — a glob that matches nothing passes every assertion below it', () => {
    expect(Object.keys(modules).length).toBeGreaterThan(0);
  });

  for (const [path, module] of Object.entries(modules)) {
    const composed = composeStories(module) as unknown as Record<string, ComponentType>;
    for (const [name, Story] of Object.entries(composed)) {
      it(`${path.replace(/^\.\/|\.stories\.tsx$/g, '')} · ${name} renders`, () => {
        expect(() => render(<Story />)).not.toThrow();
      });
    }
  }
});
