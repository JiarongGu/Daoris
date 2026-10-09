/**
 * A key press, as React hands one to a handler (its `nativeEvent` holds the flag) or as the engine hands one to a
 * listener (the flag is its own).
 */
export type KeyPress = { keyCode: number; isComposing?: boolean; nativeEvent?: { isComposing: boolean } };

/**
 * Whether an input method is composing with this press (IME1): the press is the input method's, and a handler that
 * acts on Enter, Escape, Tab or the arrows asks this first and does nothing when it answers yes.
 *
 * @remarks
 * Enter accepts a candidate and Escape drops the composition, so a handler that acted on them sent a half-written
 * message, picked a row, or closed what the person was typing in. For a person writing 中文, nearly every word is
 * composed.
 *
 * **229 is composing too.** The engine reports keyCode 229 for a press an input method took, and with some input
 * methods the press that ends a composition arrives after the composition has closed, its flag already down. Read
 * either way, so the last press of a composition is never the page's.
 *
 * `src/composingKeys.test.ts` holds that every handler which checks Enter or Escape asks this before it does.
 */
export function isComposing(event: KeyPress): boolean {
  return (event.nativeEvent ?? event).isComposing === true || event.keyCode === 229;
}
