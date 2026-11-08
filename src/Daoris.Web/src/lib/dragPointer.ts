/**
 * Follows the pointer on `window` until the press ends, and answers the call that stops it early.
 *
 * @remarks
 * A press ends by `pointerup` or by `pointercancel`: the browser cancels one it takes for a scroll, a pen leaving range or
 * a touch it hands to the system, and a drag that heard only `pointerup` would go on resizing on every move after it.
 */
export function dragPointer(move: (moved: PointerEvent) => void): () => void {
  const stop = () => {
    window.removeEventListener('pointermove', move);
    window.removeEventListener('pointerup', stop);
    window.removeEventListener('pointercancel', stop);
  };
  window.addEventListener('pointermove', move);
  window.addEventListener('pointerup', stop);
  window.addEventListener('pointercancel', stop);
  return stop;
}
