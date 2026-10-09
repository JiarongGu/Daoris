import { describe, expect, it } from 'vitest';
import { isComposing } from './composing';

/**
 * IME1: a key an input method is composing with is the input method's, never the page's. Enter that accepts a
 * candidate sent a half-written message from every conversation's composer, because nothing asked.
 */
describe('isComposing', () => {
  it('reads a native press: the flag the engine sets while a composition is open', () => {
    expect(isComposing(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true }))).toBe(true);
    expect(isComposing(new KeyboardEvent('keydown', { key: 'Enter' }))).toBe(false);
  });

  it("reads React's press through the native one it wraps, which is the only place the flag is kept", () => {
    const native = new KeyboardEvent('keydown', { key: 'Escape', isComposing: true });
    expect(isComposing({ keyCode: 0, nativeEvent: native })).toBe(true);
    expect(isComposing({ keyCode: 0, nativeEvent: new KeyboardEvent('keydown', { key: 'Escape' }) })).toBe(false);
  });

  it('takes 229 as composing too: the press that ends a composition can arrive with the flag already down', () => {
    expect(isComposing(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 229 }))).toBe(true);
    expect(isComposing({ keyCode: 229, nativeEvent: new KeyboardEvent('keydown', { key: 'Enter' }) })).toBe(true);
    expect(isComposing(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 13 }))).toBe(false);
  });
});
