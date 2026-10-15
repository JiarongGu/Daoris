import { act } from '@testing-library/react';

type Watched = {
  callback: IntersectionObserverCallback;
  observer: IntersectionObserver;
  options: IntersectionObserverInit;
  targets: Set<Element>;
};

/**
 * What is on screen in a scroll box, for jsdom, which lays nothing out and has no `IntersectionObserver` (WAITFOOT2a).
 *
 * @remarks
 * The frame asks whether the head's waits are on screen; with no observer it answers *not*, so the strip at the foot stands as
 * it always did, and every test that does not install this one reads the frame as before. Installed, the observers are ones the
 * test fires with `show`, as the window would when the person scrolls: `true` puts every observed element wholly on screen,
 * `false` takes every one off it, and a function says how much of each is on screen (0 to 1). `observers` is what each was
 * asked to watch, and how; `restore` puts jsdom's absence back.
 */
export function watchScreen() {
  const watched = new Set<Watched>();
  const had = Object.getOwnPropertyDescriptor(globalThis, 'IntersectionObserver');
  class Observer {
    readonly entry: Watched;
    readonly root: Element | Document | null;
    readonly rootMargin: string;
    readonly thresholds: readonly number[];
    constructor(callback: IntersectionObserverCallback, options: IntersectionObserverInit = {}) {
      this.root = options.root ?? null;
      this.rootMargin = options.rootMargin ?? '0px';
      this.thresholds = Array.isArray(options.threshold) ? options.threshold : [options.threshold ?? 0];
      this.entry = { callback, observer: this as unknown as IntersectionObserver, options, targets: new Set() };
      watched.add(this.entry);
    }
    observe(target: Element) { this.entry.targets.add(target); }
    unobserve(target: Element) { this.entry.targets.delete(target); }
    disconnect() {
      this.entry.targets.clear();
      watched.delete(this.entry);
    }
    takeRecords() { return []; }
  }
  Object.defineProperty(globalThis, 'IntersectionObserver', { configurable: true, writable: true, value: Observer });

  return {
    /** The observed elements are now this much on screen, and every observer is told, as the window would tell it. */
    show(on: boolean | ((target: Element) => number)) {
      const ratio = (target: Element) => (typeof on === 'function' ? on(target) : on ? 1 : 0);
      act(() => {
        for (const { callback, observer, targets } of [...watched]) {
          const entries = [...targets].map((target) => ({
            target,
            isIntersecting: ratio(target) > 0,
            intersectionRatio: ratio(target),
            time: 0,
            boundingClientRect: target.getBoundingClientRect(),
            intersectionRect: target.getBoundingClientRect(),
            rootBounds: null,
          }) as IntersectionObserverEntry);
          if (entries.length > 0) callback(entries, observer);
        }
      });
    },
    /** What each observer watches: its root, its margin and its elements. */
    observers() {
      return [...watched].map(({ options, targets }) => ({ ...options, targets: [...targets] }));
    },
    restore() {
      if (had) Object.defineProperty(globalThis, 'IntersectionObserver', had);
      else delete (globalThis as Record<string, unknown>).IntersectionObserver;
    },
  };
}
