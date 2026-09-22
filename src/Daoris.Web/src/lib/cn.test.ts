import { describe, expect, it } from 'vitest';
import { cn } from './cn';

/**
 * 🔴 A named type step is a SIZE, and a stock tailwind-merge does not know that.
 *
 * It groups every `text-…` it does not recognise with the colours, so `cn('text-small',
 * 'text-ink-soft')` kept only the colour — and every control built that way rendered at the
 * inherited body size instead of its own. Valid Tailwind, a page that renders, one step off the
 * scale, nothing to notice. Found while building the frame menus, from a class list in a test's
 * failure output.
 */
describe('cn', () => {
  it('keeps a named type step beside a colour', () => {
    expect(cn('text-small', 'text-ink-soft')).toBe('text-small text-ink-soft');
    expect(cn('text-meta', 'text-accent')).toBe('text-meta text-accent');
  });

  it('still resolves a real conflict last-wins, which is what it is for', () => {
    expect(cn('text-small', 'text-body')).toBe('text-body');
    expect(cn('text-ink', 'text-ink-soft')).toBe('text-ink-soft');
  });

  it('leaves the built-in scale alone', () => {
    expect(cn('px-2', 'px-3')).toBe('px-3');
  });
});
