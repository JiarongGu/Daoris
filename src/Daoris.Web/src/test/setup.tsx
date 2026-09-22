import '@testing-library/jest-dom/vitest';
import { setProjectAnnotations } from '@storybook/react-vite';
import preview from '../../.storybook/preview';

// The three environment shims the bilingual sibling learned to need first; adopted with the layer.

// Node ≥ 22 ships its own global localStorage that shadows jsdom's inside the test environment —
// i18next's detector then reads the wrong (or an unusable) store. Point the global at jsdom's.
if (typeof window !== 'undefined') {
  Object.defineProperty(globalThis, 'localStorage', {
    value: window.localStorage,
    configurable: true,
    writable: true,
  });
}

// Radix positions popovers with ResizeObserver, which jsdom does not implement.
class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
(globalThis as unknown as { ResizeObserver?: unknown }).ResizeObserver ??= ResizeObserverStub;

// Radix Select opens on pointer events and asks the target about pointer capture, then scrolls the
// chosen item into view — three DOM methods jsdom does not implement. No test opened a select until
// the workspace switcher (WSP5); the shims are inert no-ops, exactly as the ResizeObserver above.
if (typeof Element !== 'undefined') {
  const proto = Element.prototype as unknown as Record<string, unknown>;
  proto.hasPointerCapture ??= () => false;
  proto.setPointerCapture ??= () => {};
  proto.releasePointerCapture ??= () => {};
  proto.scrollIntoView ??= () => {};
}

// jsdom has no matchMedia; anything reading `prefers-*` gets a quiet "no".
if (typeof window !== 'undefined' && !window.matchMedia) {
  window.matchMedia = (query: string) =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener() {},
      removeListener() {},
      addEventListener() {},
      removeEventListener() {},
      dispatchEvent: () => false,
    }) as MediaQueryList;
}

/**
 * 🔴 Storybook's project annotations, so `composeStories` renders a story the way Storybook does.
 *
 * Without this, `stories.test.tsx` applies each story's OWN decorators and none of the project's —
 * so the tooltip provider `.storybook/preview.tsx` mounts for every story is absent here, and any
 * component carrying a `Tip` throws. That was diagnosed twice in one session as "this story needs
 * wrapping", which is the wrong fix twice: the suite is meant to render what a reviewer sees, and
 * it was rendering something slightly different.
 */
setProjectAnnotations(preview);
