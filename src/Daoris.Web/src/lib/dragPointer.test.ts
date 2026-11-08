import { describe, expect, it, vi } from 'vitest';
import { dragPointer } from './dragPointer';

describe('dragPointer', () => {
  it('follows moves until the pointer is released', () => {
    const move = vi.fn();
    dragPointer(move);
    window.dispatchEvent(new Event('pointermove'));
    window.dispatchEvent(new Event('pointerup'));
    window.dispatchEvent(new Event('pointermove'));
    expect(move).toHaveBeenCalledOnce();
  });

  it('stops where the browser cancels the press, which no pointerup follows', () => {
    const move = vi.fn();
    dragPointer(move);
    window.dispatchEvent(new Event('pointercancel'));
    window.dispatchEvent(new Event('pointermove'));
    expect(move).not.toHaveBeenCalled();
  });

  it('stops when told to, and takes away its other listeners too', () => {
    const move = vi.fn();
    const stop = dragPointer(move);
    stop();
    window.dispatchEvent(new Event('pointermove'));
    expect(move).not.toHaveBeenCalled();
  });
});
