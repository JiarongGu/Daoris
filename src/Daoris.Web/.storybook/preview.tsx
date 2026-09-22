// The same tokens and the same catalogs the product loads — the stories run on the shipped theme.
// Dark mode follows the OS (`prefers-color-scheme`), exactly as the product does.
import type { Decorator } from '@storybook/react-vite';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../src/tokens.css';
import '../src/i18n';

/**
 * 🔴 The provider the application mounts once (`main.tsx`), mounted once here too.
 *
 * A `Tooltip` outside one **throws**, so a component that gains a `Tip` breaks every story that
 * renders it — not visually, but at render, which `stories.test.tsx` catches as a story that cannot
 * render at all. That happened twice in one session (the status bar's items, then the toast's
 * clamped text), and both times the fix was to wrap one more story. Wrapping them all, once, is the
 * fix that does not need repeating: a decorator here applies in Storybook and in
 * `composeStories`, which is what the test renders through.
 */
export const decorators: Decorator[] = [
  (Story) => <Tooltip.Provider><Story /></Tooltip.Provider>,
];

const preview = {
  parameters: {
    layout: 'padded',
  },
  decorators,
};

export default preview;
